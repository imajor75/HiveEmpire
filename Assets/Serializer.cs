using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class Serializer : JsonSerializer
{
	public List<object> objects;
	object instance;
	Type type;
	readonly Serializer boss;
	int index;
	JsonReader reader;
	static MethodInfo scriptableObjectCreator;
	public string objectOwner;

	public class SkipUnityContractResolver : DefaultContractResolver
	{
		public static readonly SkipUnityContractResolver Instance = new SkipUnityContractResolver();

		protected override JsonProperty CreateProperty( MemberInfo member, MemberSerialization memberSerialization )
		{
			JsonProperty property = base.CreateProperty( member, memberSerialization );

			// If the type of the member is a unity type (for example a mesh) we ignore it
			if ( member is FieldInfo t && t.FieldType.Module == typeof( MonoBehaviour ).Module )
				property.ShouldSerialize = instance => false;

			// We ignore every property
			if ( member is PropertyInfo )
				property.ShouldSerialize = instance => false;

			// We ignore every member which is declared in a unity base class
			if ( member.DeclaringType.Module != GetType().Module )
				property.ShouldSerialize = instance => false;

			return property;
		}
	}

	public Serializer( JsonReader reader, Serializer boss = null )
	{
		this.reader = reader;
		if ( boss == null )
		{
			boss = this;
			objects = new List<object>();
		}
		this.boss = boss;
	}

	static object CreateObject( Type type )
	{
		if ( type == typeof( World ) )
			return World.instance;
		if ( typeof( MonoBehaviour ).IsAssignableFrom( type ) )
		{
			var m = type.GetMethod( "Create" );
			Assert.global.IsNotNull( m, "No Create method in " + type.FullName );
			Assert.global.AreEqual( m.IsStatic, true );
			object[] empty = new object[0];
			return m.Invoke( null, empty );
		}
		if ( typeof( ScriptableObject ).IsAssignableFrom( type ) )
		{
			if ( scriptableObjectCreator == null )
			{
				MethodInfo[] methods = typeof( ScriptableObject ).GetMethods();
				foreach ( var method in methods )
				{
					if ( method.Name == "CreateInstance" && method.IsGenericMethodDefinition )
						scriptableObjectCreator = method;
				}
				Assert.global.IsNotNull( scriptableObjectCreator );
			}
			Type[] types = { type };
			MethodInfo creator = scriptableObjectCreator.MakeGenericMethod( types );
			object[] parameters = null;
			return creator.Invoke( null, parameters );
		}
		return Activator.CreateInstance( type );
	}

	object Object()
	{
		if ( instance == null )
			instance = CreateObject( type );

		if ( index > 0 )
		{
			while ( boss.objects.Count <= index )
				boss.objects.Add( instance );
			boss.objects[index] = instance;
		}
		return instance;
	}

	void ProcessField()
	{
		Assert.global.AreEqual( reader.TokenType, JsonToken.PropertyName );
		string name = (string)reader.Value;
		if ( name[0] == '$' )
		{
			reader.Read();
			string value = (string)reader.Value;
			switch ( name )
			{
				case "$id":
				{
					index = int.Parse( value );
					Assert.global.IsTrue( index > 0 );
					break;
				}
				case "$type":
				{
					type = Type.GetType( value );
					Assert.global.IsNotNull( type );
					break;
				}
				case "$ref":
				{
					index = int.Parse( value );
					Assert.global.IsTrue( index > 0 );
					instance = boss.objects[index];
					break;
				}
			}
			return;
		}
		FieldInfo i = type.GetField( name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance );	// What if there are multiple ones with the same name
		PropertyInfo p = type.GetProperty( name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance );
		Assert.global.IsTrue( i != null || p != null, "No field with the name " + name + " found in " + type.FullName );
		reader.Read();

		try
		{
			if ( p != null && ( i == null || p.DeclaringType.IsSubclassOf( i.DeclaringType ) ) )
				p.SetValue( Object(), ProcessFieldValue( p.PropertyType, type.Name + '.' + name ) );
			else
				i.SetValue( Object(), ProcessFieldValue( i.FieldType, type.Name + '.' + name ) );
		}
		catch ( System.Exception exception )
		{
			Debug.Log( exception.Message );
			Assert.global.Fail( "Field type mismatch with " + type.Name + "." + i.Name );
		}
	}

	object ProcessFieldValue( Type type, string owner )
	{
		switch ( reader.TokenType )
		{
			case JsonToken.Integer:
			case JsonToken.Float:
			case JsonToken.String:
			case JsonToken.Boolean:
			{
				if ( type.IsEnum )
					return Enum.ToObject( type, reader.Value );
				return Convert.ChangeType( reader.Value, type );
			}
			case JsonToken.StartObject:
			{
				try
				{
					return new Serializer( reader, boss ).Deserialize( type );
				}
				catch ( SystemException exception )
				{
					Assert.global.Fail( "Error creating object of type " + type.FullName + " for " + owner );
					throw exception;
				}
			}
			case JsonToken.StartArray:
			{
				reader.Read();
				Type elementType = null;
				if ( type.IsArray )
					elementType = type.GetElementType();
				if ( type.IsGenericType && type.GetGenericTypeDefinition() == typeof( List<> ) )
					elementType = type.GetGenericArguments()[0];
				Assert.global.IsNotNull( elementType, "Unknown element type of " + type.ToString() );

				Type listType = typeof( List<> ).MakeGenericType( new [] { elementType } );
				IList list = (IList)Activator.CreateInstance( listType );

				while ( reader.TokenType != JsonToken.EndArray )
				{
					if ( reader.TokenType != JsonToken.Comment )
					{
						object value = ProcessFieldValue( elementType, owner );
						if ( value as IConvertible != null )
							list.Add( Convert.ChangeType( value, elementType ) );
						else
							list.Add( value );
					}
					reader.Read();
				}
				if ( type.IsArray )
				{
					Array array = Array.CreateInstance( elementType, list.Count );
					for ( int i = 0; i < list.Count; i++ )
						array.SetValue( list[i], i );
					return array;
				}
				return list;
			}
		}
		return null;
	}

	object Deserialize( Type type )
	{
		this.type = type;
		Assert.global.AreEqual( reader.TokenType, JsonToken.StartObject );
		reader.Read();
		while ( reader.TokenType == JsonToken.PropertyName )
		{
			ProcessField();
			reader.Read();
		}
		Assert.global.AreEqual( reader.TokenType, JsonToken.EndObject );
		return Object();
	}

	new public T Deserialize<T>( JsonReader r )
	{
		reader = r;
		r.Read();
		T root = (T)Deserialize( typeof( T ) );
		return root;
	}
}

