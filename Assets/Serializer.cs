using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class Serializer
{
	public List<object> objects = new List<object>();
	public Dictionary<object, int> objectIndices = new Dictionary<object, int>();
	public int processedObjectCount = 0;
	JsonReader reader;
	JsonWriter writer;
	static MethodInfo scriptableObjectCreator;
	public string objectOwner;
	static bool logged;
	public string fileName;
	public bool allowUnityTypes = false;
	public int currentObjectIndex = -1;
	public Type currentObjectType;

	struct Reference
	{
		public object instance;
		public string name;
	}

	public Serializer( JsonReader reader, string fileName )
	{
		this.reader = reader;
		this.fileName = fileName;
	}

	public Serializer()
	{
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

	void ProcessField( ref object owner )
	{
		Assert.global.AreEqual( reader.TokenType, JsonToken.PropertyName );
		string name = (string)reader.Value;
		if ( name[0] == '$' )
		{
			if ( name != "$id" )
				Assert.global.IsNull( owner, $"Object {owner} already got an instance before ref attributes in file {fileName}" );
			reader.Read();
			int index = 0;
			switch ( name )
			{
				case "$id":
				{
					if ( reader.Value is long id )
						currentObjectIndex = (int)id;
					if ( reader.Value is string str )
						currentObjectIndex = int.Parse( str );
					Assert.global.IsTrue( currentObjectIndex >= 0, $"Invalid ID {currentObjectIndex} in file {fileName}" );
					break;
				}				
				case "$ref":
				{
					if ( reader.Value is long id )
						index = (int)id;
					if ( reader.Value is string str )
						index = int.Parse( str );
					Assert.global.IsTrue( index > 0 && index < objects.Count, $"Invalid ID {index} (max: {objects.Count}) referenced in file {fileName}" );
					owner = objects[index];
					break;
				}
				case "$create":
				{
					var newType = Type.GetType( reader.Value as string );
					Assert.global.IsNotNull( newType, $"Type {reader.Value} not found in {fileName}" );
					objects.Add( owner = CreateObject( newType ) );
					break;
				}
				case "$type":
				{
					currentObjectType = Type.GetType( reader.Value as string );
					break;
				}
				default:
				{
					Assert.global.Fail( $"Unknown command {name} in file {fileName}" );
					break;
				}
			}
			reader.Read();
			return;
		}

		if ( owner == null )
		{
			owner = CreateObject( currentObjectType );
			if ( currentObjectIndex != -1 && currentObjectIndex >= objects.Count )
			{
				while ( objects.Count < currentObjectIndex + 1 )
					objects.Add( null );
				Assert.global.AreEqual( objects[currentObjectIndex], null, $"Object {objects[currentObjectIndex]} is already indexed with {currentObjectIndex}, no room for {owner}" );
				objects[currentObjectIndex] = owner;
				processedObjectCount = currentObjectIndex+1;
				currentObjectIndex = -1;
				currentObjectType = null;
			}
		}
		var type = owner.GetType();
		FieldInfo i = type.GetField( name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance );	// What if there are multiple ones with the same name
		PropertyInfo p = type.GetProperty( name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance );
		Assert.global.IsTrue( i != null || p != null, $"No field with the name {name} found in {type.FullName}" );
		reader.Read();

		if ( p != null && ( i == null || p.DeclaringType.IsSubclassOf( i.DeclaringType ) ) )
			p.SetValue( owner, ProcessFieldValue( p.PropertyType, type.Name + '.' + name ) );
		else
			i.SetValue( owner, ProcessFieldValue( i.FieldType, type.Name + '.' + name ) );
	}

	object ProcessFieldValue( Type type, string owner )
	{
		switch ( reader.TokenType )
		{
			case JsonToken.Null:
			{
				reader.Read();
				return null;
			}
			case JsonToken.Integer:
			case JsonToken.Float:
			case JsonToken.String:
			case JsonToken.Boolean:
			{
				object result;
				if ( type.IsEnum )
				{
					if ( reader.TokenType == JsonToken.String )
						result = Enum.Parse( type, reader.Value as string );
					else
						result = Enum.ToObject( type, reader.Value );
				}
				else
					result = Convert.ChangeType( reader.Value, type );
				reader.Read();
				return result;
			}
			case JsonToken.StartObject:
			{
				currentObjectType = type;
				return FillObject( null );
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
				}
				reader.Read();
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
		Assert.global.Fail( $"Unknown token type {reader.TokenType} in file {fileName}" );
		return null;
	}

	void WriteObjectAsValue( JsonWriter writer, object value )
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
			foreach ( var element in array )
				WriteObjectAsValue( writer, element );
			writer.WriteEndArray();
			return;
		}
		if ( type.IsClass )
		{
			writer.WriteStartObject();
			int index;
			if ( objectIndices.TryGetValue( value, out index ) )
			{
				Assert.global.AreEqual( objects[index], value );
				writer.WritePropertyName( "$ref" );
				writer.WriteValue( index );
			}
			else
			{
				writer.WritePropertyName( "$create" );
				writer.WriteValue( value.GetType().FullName );
				objectIndices[value] = objects.Count;
				objects.Add( value );
			}
			writer.WriteEndObject();
			return;
		}
		if ( type.IsValueType && !type.IsPrimitive && !type.IsEnum )
			ProcessObject( value, -1 );
		else
			if ( type.IsEnum )
				writer.WriteValue( value.ToString() );
			else
				writer.WriteValue( value );
	}

	void ProcessObject( object source, int index )
	{
		writer.WriteStartObject();
		var type = source.GetType();
		if ( index != -1 )
		{
			writer.WritePropertyName( "$id" );
			writer.WriteValue( index );
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
				WriteObjectAsValue( writer, fi.GetValue( source ) );
			}
			else if ( member is PropertyInfo pi )
			{
				if ( pi.GetCustomAttribute<JsonPropertyAttribute>() == null )
					continue;
				writer.WritePropertyName( member.Name );
				WriteObjectAsValue( writer, pi.GetValue( source ) );
			}
		}
		writer.WriteEndObject();
	}

	object FillObject( object emptyObject )
	{
		Assert.global.AreEqual( reader.TokenType, JsonToken.StartObject, $"Unexpected token {reader.TokenType} at the beginning of an object in {fileName}" );
		reader.Read();
		while ( reader.TokenType == JsonToken.PropertyName )
			ProcessField( ref emptyObject );
		Assert.global.AreEqual( reader.TokenType, JsonToken.EndObject, $"Unexpected token {reader.TokenType} at the end of object in {fileName}" );
		reader.Read();
		return emptyObject;
	}

	object ReadFile( string fileName, Type rootType )
	{
		this.fileName = fileName;
		if ( !File.Exists( fileName ) )
			return null;
		var sw = new StreamReader( fileName );
		reader = new JsonTextReader( sw );
		reader.Read();
		objects.Add( CreateObject( rootType ) );
		if ( reader.TokenType == JsonToken.StartArray )
			reader.Read();
		while ( objects.Count > processedObjectCount )
			FillObject( objects[processedObjectCount++] );
		Assert.global.IsTrue( reader.TokenType == JsonToken.None || reader.TokenType == JsonToken.EndArray, $"Unexpected token {reader.TokenType} at the end of file {fileName}" );
		return objects.First();
	}

	static public rootType Read<rootType>( string fileName ) where rootType : class
	{
		var serializer = new Serializer();
		return (rootType)serializer.ReadFile( fileName, typeof( rootType ) );
	}

	public void WriteFile( string fileName, object source, bool intended, bool allowUnityTypes )
	{
		this.fileName = fileName;
		var sw = new StreamWriter( fileName );
		writer = new JsonTextWriter( sw );
		if ( intended )
			writer.Formatting = Formatting.Indented;
		objectIndices.Clear();
		objectIndices[source] = objects.Count;
		objects.Add( source );
		writer.WriteStartArray();
		while ( objects.Count != processedObjectCount )
		{
			ProcessObject( objects[processedObjectCount], processedObjectCount );
			processedObjectCount++;
		}
		writer.WriteEndArray();
		writer.Close();
		sw.Close();
	}

	static public void Write( string fileName, object source, bool intended = true, bool allowUnityTypes = false )
	{
		var serializer = new Serializer();
		serializer.WriteFile( fileName, source, intended, allowUnityTypes );
	}
}
