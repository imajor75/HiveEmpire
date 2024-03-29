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
	public List<object> objects = new ();
	public Dictionary<object, int> objectIndices = new ();
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
	public List<ReferenceLink> referenceLinks = new ();
	public TypeConverter typeConverter;
	Dictionary<Type, List<MemberInfo>> cachedMembers = new ();


	public interface IReferenceUser
	{
		void OnDeadReference( MemberInfo member, HiveObject reference );
	}

	public class TypeConverter
	{
		public virtual object ChangeType( object value, Type conversionType )
		{
			return Convert.ChangeType( value, conversionType );
		}
	}

	public struct ReferenceLink
	{
		public object referencer;
		public MemberInfo member;
		public object reference;
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
		if ( type == typeof( World ) || type == typeof( Game ) )
			return HiveCommon.game;
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
					Assert.global.IsTrue( index >= 0 && index < objects.Count, $"Invalid ID {index} (max: {objects.Count}) referenced in file {fileName}" );
					Assert.global.IsTrue( owner == null || owner == objects[index], $"New instance of object has unexpected reference ID {index} in {fileName}" );
					owner = objects[index];
					break;
				}
				case "$create":
				{
					var typeName = reader.Value as string;
					if ( typeName == "World+Challenge" )
						typeName = "Game+Challenge";
					if ( typeName == "World+Timer" )
						typeName = "Game+Timer";
					if ( typeName == "World+Store" )
						typeName = "HiveObject+Store";
					var newType = Type.GetType( typeName );
					Assert.global.IsNotNull( newType, $"Type {typeName} not found in {fileName}" );
					objects.Add( owner = CreateObject( newType ) );
					break;
				}
				case "$type":
				{
					currentObjectType = Type.GetType( reader.Value as string );
					Assert.global.IsNotNull( currentObjectType, $"Unknown type {reader.Value}" );
					break;
				}
				case "$comment":
					break;
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
		MemberInfo[] members = type.GetMember( name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance );
		List<MemberInfo> options = new ();
		foreach ( var maybe in members )
			if ( maybe.Module == type.Module )
				options.Add( maybe );
		Assert.global.IsTrue( options.Count != 0, $"No member found with the name {name} in {type}" );
		if ( options.Count > 1 )
		{
			string classes = "";
			foreach ( var member in options )
				classes += $"{member.DeclaringType.Name} ({member.Module}), ";
			HiveObject.Log( $"The member with the name \"{name}\" is used in multiple classes ({classes.Substring( 0, classes.Length - 2 )})" );
		}
		reader.Read();

		if ( options.Last() is FieldInfo i )
			i.SetValue( owner, ProcessFieldValue( i.FieldType, i, owner ) );
		if ( options.Last() is PropertyInfo p )
			p.SetValue( owner, ProcessFieldValue( p.PropertyType, p, owner ) );
	}

	object ProcessFieldValue( Type type, MemberInfo m, object referencer )
	{
		if ( typeof( ICustomJson ).IsAssignableFrom( type ) && reader.TokenType != JsonToken.StartObject )
		{
			var created = CreateObject( type );
			(created as ICustomJson).Deserialize( reader );
			return created;
		}
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
					{
						// TODO Clean up this by adding a custom function which can convert enum strings
						var value = reader.Value as string;
						if ( type == typeof( Workshop.Type ) )
						{
							if ( value == "smelter" )
								value = "steelSmelter";
							if ( value == "goldBarMaker" )
								value = "jeweler";
							if ( value == "farm" )
								value = "wheatFarm";
							if ( value == "_geologistObsolete" )
								value = "unknown";
						}
						if ( type == typeof( Resource.Type ) && value == "cornfield" )
							value = "cornField";
						if ( type == typeof( Item.Type ) && value == "goldBar" )
							value = "jewelry";
						if ( !Enum.TryParse( type, value, out result ) )
							Assert.global.Fail( $"Enum value {value} not found in type {type}" );
					}
					else
						result = Enum.ToObject( type, reader.Value );
				}
				else
					result = typeConverter.ChangeType( reader.Value, type );
				reader.Read();
				return result;
			}
			case JsonToken.StartObject:
			{
				currentObjectType = type;
				var o = FillObject( null );
				referenceLinks.Add( new ReferenceLink{ referencer = referencer, member = m, reference = o } );
				return o;
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
				Assert.global.IsNotNull( elementType, $"Unknown element type of {type} for {referencer.GetType().Name}.{m.Name}" );

				Type listType = typeof( List<> ).MakeGenericType( new [] { elementType } );
				IList list = (IList)Activator.CreateInstance( listType );

				while ( reader.TokenType != JsonToken.EndArray )
				{
					if ( reader.TokenType != JsonToken.Comment )
					{
						object value = ProcessFieldValue( elementType, m, referencer );
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
			if ( value is ICustomJson custom )
			{
				custom.Serialize( writer );
				return;
			}
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
				writer.WritePropertyName( "$ref" );	// Reference index is only written to the file for debug purposes, it is not needed
				writer.WriteValue( objects.Count );
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
		List<MemberInfo> members = null;
		cachedMembers.TryGetValue( type, out members );
		if ( members == null )
		{
			members = new ();
			foreach ( var member in type.GetMembers() )
			{
				if ( member.GetCustomAttribute<JsonIgnoreAttribute>() != null )
					continue;

				if ( !allowUnityTypes && 
					member.DeclaringType.Module != GetType().Module && 
					member.DeclaringType != typeof( Color ) && 
					member.DeclaringType != typeof( Vector2 ) && 
					member.DeclaringType != typeof( Vector3 ) && 
					member.DeclaringType.IsSubclassOf( typeof( System.Runtime.CompilerServices.ITuple ) ) )
					continue;

				if ( member is FieldInfo fi )
				{
					if ( fi.IsLiteral || fi.IsInitOnly || fi.IsStatic || !fi.IsPublic )
						continue;
					if ( !allowUnityTypes && fi.FieldType.Namespace == "UnityEngine" && fi.FieldType != typeof( Color ) && fi.FieldType != typeof( Vector2 ) && fi.FieldType != typeof( Vector3 ) )
						continue;
					members.Add( member );
				}
				else if ( member is PropertyInfo pi )
				{
					if ( pi.GetCustomAttribute<JsonPropertyAttribute>() == null )
						continue;
					members.Add( member );
				}
			}
			cachedMembers[type] = members;
		}
		foreach ( var member in members )
		{
			writer.WritePropertyName( member.Name );
			if ( member is FieldInfo fi )
				WriteObjectAsValue( writer, fi.GetValue( source ) );
			else if ( member is PropertyInfo pi )
				WriteObjectAsValue( writer, pi.GetValue( source ) );
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
		using var sw = new StreamReader( fileName );
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

	static public rootType Read<rootType>( string fileName, TypeConverter typeConverter = null ) where rootType : class
	{
		var serializer = new Serializer();
		serializer.typeConverter = typeConverter ?? new TypeConverter();
		var result = (rootType)serializer.ReadFile( fileName, typeof( rootType ) );
		foreach ( var link in serializer.referenceLinks )
		{
			if ( link.referencer is Stock stock && link.reference is Cart cart && stock.destroyed && !cart.destroyed )
				cart.destroyed = true;
			if ( link.referencer is OperationHandler oh && link.reference is Game.Challenge challenge && challenge.destroyed && !oh.destroyed )
				challenge.destroyed = false;
			if ( link.referencer is GuardHouse gh && link.reference is Unit soldier && !gh.destroyed && soldier.destroyed && gh.assassin == soldier )
			{
				gh.assassin = null;
				continue;
			}
			if ( 
				link.referencer is Cart ct &&
				link.member.Name == "road" &&
				link.reference is HiveObject hiveObjectReference &&
				hiveObjectReference.destroyed &&
				!ct.exclusiveMode )
			{
				ct.road = null;
				continue;
			}
			bool referencerDestroyed = link.referencer is HiveObject ho && ho.destroyed;
			bool referenceDestroyed = link.reference is HiveObject hor && hor.destroyed;
			if ( !referencerDestroyed && referenceDestroyed )
			{
				if ( link.referencer is IReferenceUser referencer )
					referencer.OnDeadReference( link.member, link.reference as HiveObject );
				else
					Assert.global.Fail( $"Nondestroyed object {link.referencer} is referencing the destroyed object {link.reference} through {link.member} (raw)" );

				if ( link.member is FieldInfo field )
					field.SetValue( link.referencer, null );
				if ( link.member is PropertyInfo property )
					property.SetValue( link.referencer, null );
			}
		}
		return result;
	}

	public void WriteFile( string fileName, object source, bool intended, bool allowUnityTypes, bool logTypeCount = false )
	{
		this.fileName = fileName;
		this.allowUnityTypes = allowUnityTypes;
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
	#if UNITY_EDITOR
		if ( logTypeCount )
		{
			Dictionary<Type, int> content = new ();
			foreach ( var processedObject in objects )
			{
				var type = processedObject.GetType();
				if ( content.ContainsKey( type ) )
					content[type]++;
				else
					content[type] = 1;
			}
			List<(Type, int)> types = new ();
			foreach ( var type in content )
				types.Add( (type.Key, type.Value) );
			types.Sort( ( a, b ) => b.Item2.CompareTo( a.Item2 ) );
			HiveCommon.Log( $"Types in file {fileName}\n================" );
			foreach ( var type in types )
				HiveCommon.Log( $"{type.Item1} : {type.Item2}" );

		}
	#endif
		writer.Close();
		sw.Close();
	}

	static public void Write( string fileName, object source, bool intended = true, bool allowUnityTypes = false, bool logTypeCount = false )
	{
		var serializer = new Serializer();
		serializer.WriteFile( fileName, source, intended, allowUnityTypes, logTypeCount );
	}

	public interface ICustomJson
	{
		void Serialize( JsonWriter writer );
		void Deserialize( JsonReader reader );
	}
}
