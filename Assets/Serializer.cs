using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Assertions;

public class Serializer : JsonSerializer
{
	public List<object> objects;
	object instance;
	Type type;
	Type staticType;
	Serializer boss;
	int index;

	public class SkipUnityContractResolver : DefaultContractResolver
	{
		public static readonly SkipUnityContractResolver Instance = new SkipUnityContractResolver();
		public static readonly Type[] gameClasses = {
			typeof( Flag ),
			typeof( Worker ),
			typeof( Road ),
			typeof( Building ),
			typeof( Building.Construction ),
			typeof( Stock ),
			typeof( Workshop ),
			typeof( PathFinder ),
			typeof( Ground ),
			typeof( GroundNode ),
			typeof( Item ),
			typeof( Workshop.Buffer )
		};

		protected override JsonProperty CreateProperty( MemberInfo member, MemberSerialization memberSerialization )
		{
			JsonProperty property = base.CreateProperty(member, memberSerialization);

			if ( !gameClasses.Contains( member.DeclaringType ) )
				property.ShouldSerialize = instance => false;

			return property;
		}
	}

	public Serializer( Serializer boss = null )
	{
		if ( boss == null )
		{
			boss = this;
			objects = new List<object>();
		}
		this.boss = boss;
	}

	object Object()
	{
		if ( instance == null )
		{
			if ( type == typeof( Flag ) )
				instance = Flag.Create();
			else if ( type == typeof( Stock ) )
				instance = Stock.Create();
			else if ( type == typeof( Workshop ) )
				instance = Workshop.Create();
			else if ( type == typeof( Ground ) )
				instance = Ground.Create();
			else if ( type == typeof( Worker ) )
				instance = Worker.Create();
			else if ( type == typeof( Road ) )
				instance = Road.Create();
			else if ( type == typeof( Item ) )
				instance = Item.Create();
			else
				instance = Activator.CreateInstance( type );
		}
		if ( index > 0 )
		{
			while ( boss.objects.Count <= index )
				boss.objects.Add( instance );
			boss.objects[index] = instance;
		}
		return instance;
	}
	void ProcessField( JsonReader r )
	{
		Assert.AreEqual( r.TokenType, JsonToken.PropertyName );
		string name = (string)r.Value;
		if ( name[0] == '$' )
		{
			r.Read();
			string value = (string)r.Value;
			switch ( name )
			{
				case "$id":
				{
					index = int.Parse( value );
					Assert.IsTrue( index > 0 );
					break;
				}
				case "$type":
				{
					type = Type.GetType( value );
					Assert.IsNotNull( type );
					break;
				}
				case "$ref":
				{
					index = int.Parse( value );
					Assert.IsTrue( index > 0 );
					instance = boss.objects[index];
					break;
				}
			}
			return;
		}
		FieldInfo i = type.GetField( name );
		Assert.IsNotNull( i );
		r.Read();
		i.SetValue( Object(), ProcessFieldValue( r, i.FieldType ) );
	}

	object ProcessFieldValue( JsonReader r, Type type )
	{
		switch ( r.TokenType )
		{
			case JsonToken.Integer:
			case JsonToken.Float:
			case JsonToken.String:
			case JsonToken.Boolean:
			{
				if ( type.IsEnum )
					return Enum.ToObject( type, r.Value );
				return Convert.ChangeType( r.Value, type );
			}
			case JsonToken.StartObject:
			{
				return new Serializer( boss ).Deserialize( type, r );
			}
			case JsonToken.StartArray:
			{
				r.Read();
				Type elementType = null;
				if ( type.IsArray )
					elementType = type.GetElementType();
				if ( type.IsGenericType && type.GetGenericTypeDefinition() == typeof( List<> ) )
					elementType = type.GetGenericArguments()[0];
				Assert.IsNotNull( elementType );

				Type listType = typeof( List<> ).MakeGenericType( new [] { elementType } );
				IList list = (IList)Activator.CreateInstance( listType );

				while ( r.TokenType != JsonToken.EndArray )
				{
					if ( r.TokenType != JsonToken.Comment )
					{
						object value = ProcessFieldValue( r, elementType );
						if ( value as IConvertible != null )
							list.Add( Convert.ChangeType( value, elementType ) );
						else
							list.Add( value );
					}
					r.Read();
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

	object Deserialize( Type type, JsonReader r )
	{
		this.type = staticType = type;
		Assert.AreEqual( r.TokenType, JsonToken.StartObject );
		r.Read();
		while ( r.TokenType == JsonToken.PropertyName )
		{
			ProcessField( r );
			r.Read();
		}
		Assert.AreEqual( r.TokenType, JsonToken.EndObject );
		return Object();
	}

	new public T Deserialize<T>( JsonReader r )
	{
		r.Read();
		T root = (T)Deserialize( typeof( T ), r );
		return root;
	}
}

