using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class Serializer : JsonSerializer
{
	public List<object> objects = new List<object>();
	object instance;
	Type type;
	readonly Serializer boss;
	int index;
	JsonReader reader;
	static MethodInfo scriptableObjectCreator;
	public string objectOwner;
	static List<Reference> referenceChain = new List<Reference>();
	static int maxDepth;
	static bool logged;
	public string fileName;
	public bool allowUnityTypes = false;

	struct Reference
	{
		public object instance;
		public string name;
	}

	public Serializer( JsonReader reader, string fileName, Serializer boss = null )
	{
		this.reader = reader;
		this.fileName = fileName;
		if ( boss == null )
		{
			boss = this;
			objects = new List<object>();
		}
		this.boss = boss;
	}

	public Serializer( string file )
	{
		fileName = file;
	}

	static object CreateSceneObject( Type type )
	{
		var m = type.GetMethod( "Create" );
		Assert.global.IsNotNull( m, $"No Create method in {type.FullName}" );
		Assert.global.AreEqual( m.IsStatic, true );
		object[] empty = new object[0];
		return m.Invoke( null, empty );
	}

	static object CreateObject( Type type )
	{
		if ( type == typeof( World ) )
			return HiveCommon.world;
		if ( typeof( MonoBehaviour ).IsAssignableFrom( type ) )
			return CreateSceneObject( type ) as MonoBehaviour;
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
			switch ( name )
			{
				case "$id":
				{
					if ( reader.Value is long id )
						index = (int)id;
					if ( reader.Value is string str )
						index = int.Parse( str );
					Assert.global.IsTrue( index > 0, $"Invalid ID {index} in file {fileName}" );
					break;
				}
				case "$type":
				{
					type = Type.GetType( reader.Value as string );
					Assert.global.IsNotNull( type, $"Type {reader.Value} not found in {fileName}" );
					break;
				}
				case "$ref":
				{
					if ( reader.Value is long id )
						index = (int)id;
					if ( reader.Value is string str )
						index = int.Parse( str );
					Assert.global.IsTrue( index > 0, $"Invalid ID referenced in file {fileName}" );
					instance = boss.objects[index];
					break;
				}
			}
			return;
		}
		FieldInfo i = type.GetField( name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance );	// What if there are multiple ones with the same name
		PropertyInfo p = type.GetProperty( name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance );
		Assert.global.IsTrue( i != null || p != null, $"No field with the name {name} found in {type.FullName}" );
		reader.Read();

		var target = Object();
		if ( p != null && ( i == null || p.DeclaringType.IsSubclassOf( i.DeclaringType ) ) )
			p.SetValue( target, ProcessFieldValue( p.PropertyType, type.Name + '.' + name ) );
		else
			i.SetValue( target, ProcessFieldValue( i.FieldType, type.Name + '.' + name ) );
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
				{
					if ( reader.TokenType == JsonToken.String )
						return Enum.Parse( type, reader.Value as string );
					else
						return Enum.ToObject( type, reader.Value );
				}
				return Convert.ChangeType( reader.Value, type );
			}
			case JsonToken.StartObject:
			{
				referenceChain.Add( new Reference { instance = Object(), name = owner } );
				if ( referenceChain.Count > maxDepth )
				{
					maxDepth = referenceChain.Count;
					if ( maxDepth > 530 && !logged )
					{
						foreach ( var r in referenceChain )
						HiveObject.Log( $"{r.instance} : {r.name}" );
						logged = true;
					}
				}
				var child = new Serializer( reader, fileName, boss ).Deserialize( type );
				Assert.global.AreEqual( Object(), referenceChain.Last().instance );
				referenceChain.RemoveAt( referenceChain.Count-1 );
				return child;
			}
			case JsonToken.StartArray:
			{
				reader.Read();
				Type elementType = null;
				if ( type.IsArray )
					elementType = type.GetElementType();
				if ( type.IsGenericType && type.GetGenericTypeDefinition() == typeof( List<> ) )
					elementType = type.GetGenericArguments()[0];
				if ( type.IsGenericType && type.GetGenericTypeDefinition() == typeof( LinkedList<> ) )
					elementType = type.GetGenericArguments()[0];
				Assert.global.IsNotNull( elementType, $"Unknown element type of {type.ToString()} for {owner}" );

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
				if ( type.IsGenericType && type.GetGenericTypeDefinition() == typeof( LinkedList<> ) )
				{
					Type linkedListType = typeof( LinkedList<> ).MakeGenericType( new [] { elementType } );
					return Activator.CreateInstance( linkedListType, list );
				}
				return list;
			}
		}
		return null;
	}

	void WriteObjectAsValue( JsonWriter writer, object value, Type slotType )
	{
		if ( value == null )
		{
			writer.WriteValue( (bool?) null );
			return;
		}
		var type = value.GetType();
		if ( type == typeof( string ) )
		{
			writer.WriteValue( value );
			return;
		}
		if ( value is IEnumerable array )
		{
			writer.WriteStartArray();
			var arraySlotType = type.GetElementType();
			if ( type.GenericTypeArguments.Length > 0 )
				arraySlotType = type.GenericTypeArguments.First();
			foreach ( var element in array )
				WriteObjectAsValue( writer, element, arraySlotType );
			writer.WriteEndArray();
			return;
		}
		if ( type.IsClass || (type.IsValueType && !type.IsPrimitive && !type.IsEnum) )
			Serialize( writer, value, slotType );
		else
			if ( type.IsEnum )
				writer.WriteValue( value.ToString() );
			else
				writer.WriteValue( value );
	}

	new void Serialize( JsonWriter writer, object source, Type slotType )
	{
		writer.WriteStartObject();
		var type = source.GetType();
		if ( type.IsClass )
		{
			int index = objects.IndexOf( source );
			if ( index >= 0 )
			{
				writer.WritePropertyName( "$ref" );
				writer.WriteValue( index + 1 );
				writer.WriteEndObject();
				return;
			}
			writer.WritePropertyName( "$id" );
			objects.Add( source );
			writer.WriteValue( objects.Count );
		}
		if ( type != slotType )
		{
			writer.WritePropertyName( "$type" );
			writer.WriteValue( type.AssemblyQualifiedName );
		}
		foreach ( var member in type.GetMembers() )
		{
			if ( member.GetCustomAttribute<JsonIgnoreAttribute>() != null )
				continue;

			if ( !allowUnityTypes && member.DeclaringType.Module != GetType().Module && member.DeclaringType != typeof( Color ) && member.DeclaringType != typeof( Vector2 ) && member.DeclaringType != typeof( Vector3 ) )
				continue;

			if ( member is FieldInfo fi )
			{
				if ( fi.IsLiteral || fi.IsInitOnly || fi.IsStatic || !fi.IsPublic )
					continue;
				if ( !allowUnityTypes && fi.FieldType.Namespace == "UnityEngine" && fi.FieldType != typeof( Color ) && fi.FieldType != typeof( Vector2 ) && fi.FieldType != typeof( Vector3 ) )
					continue;
				writer.WritePropertyName( member.Name );
				WriteObjectAsValue( writer, fi.GetValue( source ), fi.FieldType );
			}
			else if ( member is PropertyInfo pi )
			{
				if ( pi.GetCustomAttribute<JsonPropertyAttribute>() == null )
					continue;
				writer.WritePropertyName( member.Name );
				WriteObjectAsValue( writer, pi.GetValue( source ), pi.PropertyType );
			}
		}
		writer.WriteEndObject();
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

	static public T Read<T>( string fileName ) where T : class
	{
		referenceChain.Clear();
		maxDepth = 0;
		if ( !File.Exists( fileName ) )
			return null;
		using ( var sw = new StreamReader( fileName ) )
		using ( var reader = new JsonTextReader( sw ) )
		{
			reader.Read();
			var serializer = new Serializer( reader, fileName );
			var result = (T)serializer.Deserialize( typeof( T ) );
			HiveObject.Log( $"{result} read, max depth {maxDepth}" );
			return result;
		}
	}

	static public void Write( string fileName, object source, bool intended = true, bool allowUnityTypes = false )
	{
		using var sw = new StreamWriter( fileName );
		using JsonTextWriter writer = new JsonTextWriter( sw );
		if ( intended )
			writer.Formatting = Formatting.Indented;
		var serializer = new Serializer( fileName );
		serializer.allowUnityTypes = allowUnityTypes;
		serializer.Serialize( writer, source, source.GetType() );
	}
}
