using Newtonsoft.Json;
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
	JsonReader reader;

	public class SkipUnityContractResolver : DefaultContractResolver
	{
		public static readonly SkipUnityContractResolver Instance = new SkipUnityContractResolver();
		public static readonly Type[] gameClasses = {
			typeof( Player ),
			typeof( Flag ),
			typeof( BorderEdge ),
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
			typeof( Workshop.Buffer ),
			typeof( Worker.WalkToFlag ),
			typeof( Worker.WalkToNeighbour ),
			typeof( Worker.WalkToNode ),
			typeof( Worker.WalkToRoadPoint ),
			typeof( Worker.Task ),
			typeof( Worker.StartWorkingOnRoad ),
			typeof( Worker.PickupItem ),
			typeof( Worker.DeliverItem ),
			typeof( PathFinder ),
			typeof( Path ),
			typeof( Resource )
		};

		protected override JsonProperty CreateProperty( MemberInfo member, MemberSerialization memberSerialization )
		{
			JsonProperty property = base.CreateProperty(member, memberSerialization);

			if ( !gameClasses.Contains( member.DeclaringType ) )
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

	object Object()
	{
		if ( instance == null )
		{
			if ( type == typeof( Player ) )
				instance = ScriptableObject.CreateInstance<Player>();
			else if ( type == typeof( BorderEdge ) )
				instance = BorderEdge.Create();
			else if ( type == typeof( Flag ) )
				instance = Flag.Create();
			else if ( type == typeof( Stock ) )
				instance = Stock.Create();
			else if ( type == typeof( Workshop ) )
				instance = Workshop.Create();
			else if ( type == typeof( Ground ) )
				instance = Ground.Create();
			else if ( type == typeof( Worker ) )
				instance = Worker.Create();
			else if ( type == typeof( Worker.DeliverItem ) )
				instance = ScriptableObject.CreateInstance<Worker.DeliverItem>();
			else if ( type == typeof( Worker.PickupItem ) )
				instance = ScriptableObject.CreateInstance<Worker.PickupItem>();
			else if ( type == typeof( Worker.StartWorkingOnRoad ) )
				instance = ScriptableObject.CreateInstance<Worker.StartWorkingOnRoad>();
			else if ( type == typeof( Worker.WalkToFlag ) )
				instance = ScriptableObject.CreateInstance<Worker.WalkToFlag>();
			else if ( type == typeof( Worker.WalkToNeighbour ) )
				instance = ScriptableObject.CreateInstance<Worker.WalkToNeighbour>();
			else if ( type == typeof( Worker.WalkToNode ) )
				instance = ScriptableObject.CreateInstance<Worker.WalkToNode>();
			else if ( type == typeof( Worker.WalkToRoadPoint ) )
				instance = ScriptableObject.CreateInstance<Worker.WalkToRoadPoint>();
			else if ( type == typeof( WorkerMan ) )
				instance = WorkerMan.Create();
			else if ( type == typeof( WorkerWoman ) )
				instance = WorkerWoman.Create();
			else if ( type == typeof( WorkerBoy ) )
				instance = WorkerBoy.Create();
			else if ( type == typeof( Road ) )
				instance = Road.Create();
			else if ( type == typeof( Item ) )
				instance = Item.Create();
			else if ( type == typeof( Resource ) )
				instance = Resource.Create();
			else if ( type == typeof( Path ) )
				instance = ScriptableObject.CreateInstance<Path>();
			else if ( type == typeof( PathFinder ) )
				instance = ScriptableObject.CreateInstance<PathFinder>();
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
	void ProcessField()
	{
		Assert.AreEqual( reader.TokenType, JsonToken.PropertyName );
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
		reader.Read();
		i.SetValue( Object(), ProcessFieldValue( i.FieldType ) );
	}

	object ProcessFieldValue( Type type )
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
				return new Serializer( reader, boss ).Deserialize( type );
			}
			case JsonToken.StartArray:
			{
				reader.Read();
				Type elementType = null;
				if ( type.IsArray )
					elementType = type.GetElementType();
				if ( type.IsGenericType && type.GetGenericTypeDefinition() == typeof( List<> ) )
					elementType = type.GetGenericArguments()[0];
				Assert.IsNotNull( elementType );

				Type listType = typeof( List<> ).MakeGenericType( new [] { elementType } );
				IList list = (IList)Activator.CreateInstance( listType );

				while ( reader.TokenType != JsonToken.EndArray )
				{
					if ( reader.TokenType != JsonToken.Comment )
					{
						object value = ProcessFieldValue( elementType );
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
		this.type = staticType = type;
		Assert.AreEqual( reader.TokenType, JsonToken.StartObject );
		reader.Read();
		while ( reader.TokenType == JsonToken.PropertyName )
		{
			ProcessField();
			reader.Read();
		}
		Assert.AreEqual( reader.TokenType, JsonToken.EndObject );
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

