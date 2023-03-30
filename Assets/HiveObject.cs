using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Diagnostics;
using UnityEngine;

#pragma warning disable UNT0001

public class HiveCommon : MonoBehaviour
{
	public static Game game { get { return Game.instance; } }
	public static Ground ground { get { return game.ground; } }
	public static int time { get { return game.time; } }
	public static OperationHandler oh { get { return game.operationHandler; } }
	public static Eye eye { get { return game.eye; } }
	public static Interface root { get { return Interface.root; } }
	public static Network network { get { return Network.instance; } }
	public static Settings settings { get { return root.globalSettings; } }

	public enum Severity
	{
		normal,
		important,
		warning,
		error,
		critical
	}

	public static void Log( string text, Severity severity = Severity.normal )
	{
		root.logFile?.Write( text + "\n" );
		root.logFile?.Flush();
		switch ( severity )
		{ 	
			case Severity.important: print( text ); return;
			case Severity.warning: UnityEngine.Debug.LogWarning( text ); return;
			case Severity.error: UnityEngine.Debug.LogError( text ); return;
			default: return;
		}
	}

	public static void LogStackTrace()
	{
		var stackTrace = new StackTrace();
		for ( var i = 1; i < stackTrace.FrameCount; i++ )
		{
			var stackFrame = stackTrace.GetFrame( i );
			var method = stackFrame.GetMethod();
			Log( method.DeclaringType.Name + "." + method.Name );
		}
	}

	public static void RemoveElements<Something>( List<Something> array ) where Something : HiveObject
	{
		var tmpArray = array.GetRange( 0, array.Count );
		foreach ( var element in tmpArray )
			element.Remove();
	}
	
	static public string Nice( string raw )
	{
		string nice = "";
		bool capitalize = true;
		foreach ( var c in raw )
		{
			char current = c;
			if ( Char.IsUpper( c ) )
				nice += " ";
			if ( capitalize )
			{
				current = Char.ToUpper( c );
				capitalize = false;
			}
			nice += current;
		}
		return nice;
	}

}

public abstract class HiveObject : HiveCommon
{
	public bool blueprintOnly;
	public bool inactive;
	public int id;
	public bool noAssert;
	public bool destroyed;
	public int[] updateIndices = new int[3];
	public World world;
	public Team team;
	public Simpleton.Data simpletonData;
	[JsonIgnore]
	public bool selectThis;

	[Obsolete( "Compatibility with old files", true )]
	public int worldIndex { set { Log( $"fixir {value} {id}" ); updateIndices[0] = value; } }
	[Obsolete( "Compatibility with old files", true )]
	public bool registered { set {} }

	public enum UpdateStage
	{
		none = 0,
		realtime = 1,
		lazy = 2,
		turtle = 4
	}

	[Serializable]
	public class Store
	{
		public List<HiveObject> objects = new ();
		public LinkedList<HiveObject> newObjects = new ();
		public LinkedList<int> freeSlots = new ();
		public int processIndex;
		public UpdateStage stage;
		public int stageIndex;
		public float updateSpeed = 1;
		public int objectCount => objects.Count + newObjects.Count - freeSlots.Count;

		public Store()
		{
		}

		public Store( UpdateStage stage, int stageIndex, float updateSpeed = 1 )
		{
			this.stage = stage;
			this.stageIndex = stageIndex;
			this.updateSpeed = updateSpeed;
		}

		public void Update()
		{
			Assert.global.IsTrue( updateSpeed > 0 );
			foreach ( var newObject in newObjects )
			{
				if ( newObject == null )
					continue;

				Assert.global.IsFalse( objects.Contains( newObject ) );
				if ( freeSlots.Count > 0 )
				{
					int i = freeSlots.Last.Value;
					freeSlots.RemoveLast();
					Assert.global.AreEqual( objects[i], null );
					objects[i] = newObject;
					newObject.updateIndices[stageIndex] = i;
				}
				else
				{
					newObject.updateIndices[stageIndex] = objects.Count;
					objects.Add( newObject );
				}

				if ( newObject.priority )
				{
					int i = newObject.updateIndices[stageIndex];
					while ( i > 0 && ( objects[i-1] == null || !objects[i-1].priority ) )
						i--;
					if ( i != newObject.updateIndices[stageIndex] )
					{
						var old = objects[i];
						objects[newObject.updateIndices[stageIndex]] = old;
						if ( old )
							old.updateIndices[stageIndex] = newObject.updateIndices[stageIndex];
						objects[i] = newObject;
						newObject.updateIndices[stageIndex] = i;
					}
				}
			}
			newObjects.Clear();

			game.updateStage = stage;
			int newIndex = processIndex + (int)( objects.Count * updateSpeed );	// nothing will be removed from the objects list during this loop right?
			if ( newIndex == processIndex )
				newIndex = processIndex + 1;
			if ( newIndex > objects.Count )
				newIndex = objects.Count;
			while ( processIndex < newIndex )
			{
				var hiveObject = objects[processIndex++];
				if ( hiveObject && !hiveObject.destroyed )
					hiveObject.GameLogicUpdate( stage );
			}
			if ( processIndex >= objects.Count )
				processIndex = 0;
			game.updateStage = UpdateStage.none;
		}

		public bool Contains( HiveObject ho )
		{
			return objects.Contains( ho ) || newObjects.Contains( ho );
		}
		
		public void Clear()
		{
			foreach ( var ho in objects )
				if ( ho )
					ho.updateIndices[stageIndex] = -1;

			objects.Clear();
			newObjects.Clear();
			freeSlots.Clear();
		}

		public void Validate()
		{
			int nullCount = 0;
			for ( int i = 0; i < objects.Count; i++ )
			{
				if ( objects[i] )
					objects[i].assert.AreEqual( objects[i].updateIndices[stageIndex], i, $"Store index for the object {objects[i]} is wrong, {objects[i].updateIndices[stageIndex]} is stored while the real value is {i}" );
				else
					nullCount++;
			}
			Assert.global.AreEqual( freeSlots.Count, nullCount, $"Corrupt free slots in the {stage} array" );
			foreach ( var freeSlot in freeSlots )
				Assert.global.IsNull( objects[freeSlot] );
		}

		public void Add( HiveObject newObject )
		{
			newObject.assert.AreEqual( newObject.updateIndices[stageIndex], -1, $"Already registered object {newObject} is added to the {stage} store" );
			newObjects.AddLast( newObject );
		}

		public void Remove( HiveObject objectToRemove )
		{
			if ( objectToRemove.updateIndices[stageIndex] >= 0 && objects.Count > objectToRemove.updateIndices[stageIndex] && objectToRemove == objects[objectToRemove.updateIndices[stageIndex]] )
			{
				objects[objectToRemove.updateIndices[stageIndex]] = null;
				freeSlots.AddLast( objectToRemove.updateIndices[stageIndex] );
				objectToRemove.updateIndices[stageIndex] = -1;
			}
			newObjects.Remove( objectToRemove );	// in pause mode the object might still sitting in this array
		}
	}

	virtual public UpdateStage updateMode => UpdateStage.realtime;

	public Simpleton.Data simpletonDataSafe
	{
		get
		{
			if ( simpletonData == null )
				simpletonData = new Simpleton.Data( this );

			return simpletonData;
		}
	}
	public virtual bool wantFoeClicks => false;

	public virtual int checksum => id;
	public virtual bool priority => false;
	
	[JsonIgnore]
	public Assert assert;

	public HiveObject()
	{
		assert = new Assert( this );
		updateIndices[0] = updateIndices[1] = updateIndices[2] = -1;
	}

	override public string ToString()
	{
		return name;
	}

	public void Register()
	{
		if ( !world ) return;

		Unregister();
		foreach ( var store in world.updateHiveObjects )
			if ( ( updateMode & store.stage ) != 0 )
				store.Add( this );
	}

	public void Unregister()
	{
		if ( !world ) return;

		foreach ( var store in world.updateHiveObjects )
			if ( updateIndices[store.stageIndex] != -1 )
				store.Remove( this );
	}

	public void Setup( World world )
	{
		this.world = world;
		if ( world )
		{
			foreach ( var store in world.updateHiveObjects )
				assert.IsFalse( store.Contains( this ) );
			Register();
			if ( !blueprintOnly )
				id = world.nextID++;
		}
	}

	public void OnDestroy()
	{
		Unregister();
		destroyed = true;
	}

	public void Update()
	{
		if ( selectThis )
		{
			selectThis = false;
			OnClicked( Interface.MouseButton.left, true );
		}
	}

	// This function is similar to FixedUpdate, but it contains code which is sensitive to execute order, sucs as when woodcutters decide which tree to cut. 
	// So when this function is called by World.FixedUpdate it is always called in the same order.
	public virtual void GameLogicUpdate( UpdateStage stage ) {}

	public virtual void Remove()
	{
		destroyed = true;
		Destroy( gameObject );
	}

	public virtual Node location { get { return null; } }

	public virtual void Reset()
	{ 
	}

	// The only reason for this function is to save the status
	public void SetActive( bool active )
	{
		inactive = !active;
		gameObject.SetActive( active );
	}

	public void Start()
	{
		gameObject.SetActive( !inactive );
		if ( destroyed )	// If this is true, and this function is called, we are right after load. We should let unity know that this object should be treated as nonexistent
		{
			Destroy( gameObject );
			Log( $"Dead object in file: {name} (type:{GetType()}, id:{id})" );
		}
	}

	public virtual void Materialize()
	{
		assert.IsTrue( blueprintOnly );
		blueprintOnly = false;
		id = world.nextID++;
	}

	public virtual void OnClicked( Interface.MouseButton button, bool show = false )
	{
	}

	public virtual void UnitCallback( Unit unit, float floatData, bool boolData ) 
	{
	}

	public static HiveObject GetByID( int id )
	{
		var list = Resources.FindObjectsOfTypeAll<HiveObject>();
		foreach ( var hiveObject in list )
			if ( hiveObject.id == id )
				return hiveObject;
		return null;
	}

	virtual public void OnDeadReference( MemberInfo member, HiveObject reference )
	{
		assert.Fail( $"Nondestroyed object {this} referencing the destroyed object {reference} through {member}" );
	}

	public virtual void Validate( bool chainCall )
	{
		if ( !world )
		{
			if ( !blueprintOnly )
				assert.AreEqual( id, 0, $"{this} has an ID ({id}) but has no world" );
			return;
		}

		foreach ( var store in world.updateHiveObjects )
		{
			if ( updateIndices[store.stageIndex] >= 0 )
			{
				assert.IsNotNull( world );
				assert.AreNotEqual( (int)( updateMode & store.stage ), 0 );
				if ( updateMode == UpdateStage.realtime )
				{
					assert.IsTrue( updateIndices[store.stageIndex] < store.objects.Count, "Hive object store is corrupt" );
					assert.AreEqual( this, store.objects[updateIndices[store.stageIndex]], "Hive object store is corrupt" );
				}
				assert.AreNotEqual( updateMode, UpdateStage.none );
			}
		}
	}

	public class SiteTestResult
	{
		public Result code;
		public Node.Type groundTypeMissing;

		public SiteTestResult( Result code, Node.Type groundTypeMissing = Node.Type.anything )
		{
			this.code = code;
			this.groundTypeMissing = groundTypeMissing;
		}

		public static implicit operator bool( SiteTestResult result )
		{
			return result.code == Result.fit;
		}

		public enum Result
		{
			fit,
			wrongGroundType,
			wrongGroundTypeAtEdge,
			blocked,
			flagTooClose,
			buildingTooClose,
			heightAlreadyFixed,
			outsideBorder,
			crossingInTheWay
		}
	}
}
