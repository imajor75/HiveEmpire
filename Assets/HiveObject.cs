using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Diagnostics;
using System.Linq;
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

	public static void Eradicate( UnityEngine.Object target )
	{
		if ( target is Transform transform )
			Destroy( transform.gameObject );
		else
			Destroy( target );
	}

	public static void LogStackTrace( string prefix = "" )
	{
		var stackTrace = new StackTrace();
		for ( var i = 1; i < stackTrace.FrameCount; i++ )
		{
			var stackFrame = stackTrace.GetFrame( i );
			var method = stackFrame.GetMethod();
			Log( prefix + method.DeclaringType.Name + "." + method.Name );
		}
	}

	public static void RemoveElements<Something>( List<Something> array ) where Something : HiveObject
	{
		var tmpArray = array.GetRange( 0, array.Count );
		foreach ( var element in tmpArray )
			element.Remove();
	}
}

public abstract class HiveObject : HiveCommon, Serializer.IReferenceUser
{
	public bool blueprintOnly;
	public bool inactive;
	public int id = -1;
	public bool noAssert;
	public bool destroyed;
	public int[] updateIndices = new int[3];
	public World world;
	public Team team;
	public Simpleton.Data simpletonData;
	[JsonIgnore]
	public bool selectThis;
	virtual public string textId => $"{GetType()}" + location == null ? "" : " at {location.x}:{location.y}";

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
		public List<HiveObject> newObjects = new ();
		public List<int> freeSlots = new ();
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

				newObject.assert.IsFalse( objects.Contains( newObject ), $"Object {newObject} is already in {stage} store" );
				if ( freeSlots.Count > 0 )
				{
					int i = freeSlots.Last();
					freeSlots.RemoveAt( freeSlots.Count - 1 );
					Assert.global.AreEqual( objects[i], null, $"Slot {i} in the {stage} store is not null ({objects[i]})" );
					objects[i] = newObject;
					newObject.updateIndices[stageIndex] = i;
				}
				else
				{
					newObject.updateIndices[stageIndex] = objects.Count;
					objects.Add( newObject );
				}
			}
			newObjects.Clear();

			while ( freeSlots.Count > objects.Count / 10 )
			{
				var last = objects.Last();
				if ( last )
				{
					objects[freeSlots[0]] = last;
					last.updateIndices[stageIndex] = freeSlots[0];
				}
				else
					Assert.global.AreEqual( freeSlots[0], objects.Count - 1 );
				objects.RemoveAt( objects.Count - 1 );
				freeSlots.RemoveAt( 0 );
			}

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
			processIndex = 0;
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
			foreach ( var freeIndex in freeSlots )
				Assert.global.IsNull( objects[freeIndex] );
			Assert.global.AreEqual( freeSlots.Count, nullCount, $"Corrupt free slots in the {stage} array" );
			foreach ( var freeSlot in freeSlots )
				Assert.global.IsNull( objects[freeSlot] );
		}

		public void Add( HiveObject newObject )
		{
			newObject.assert.AreEqual( newObject.updateIndices[stageIndex], -1, $"Already registered object {newObject} is added to the {stage} store" );
			newObject.assert.IsFalse( Contains( newObject ), $"Object {newObject} is already in {stage} store" );
			newObjects.Add( newObject );
		}

		public void Remove( HiveObject objectToRemove )
		{
			if ( objectToRemove.updateIndices[stageIndex] >= 0 && objects.Count > objectToRemove.updateIndices[stageIndex] && objectToRemove == objects[objectToRemove.updateIndices[stageIndex]] )
			{
				objects[objectToRemove.updateIndices[stageIndex]] = null;
				freeSlots.Add( objectToRemove.updateIndices[stageIndex] );
				freeSlots.Sort( ( a, b ) => b.CompareTo( a ) );
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

	public virtual int checksum { get { assert.AreNotEqual( id, -1 ); return id; } }
	
	[JsonIgnore]
	public Assert assert;

	public HiveObject()
	{
		assert = new Assert( this );
		updateIndices[0] = updateIndices[1] = updateIndices[2] = -1;
	}

	override public string ToString() => name;
	
	public void ScheduleUpdates()
	{
		if ( !world ) return;

		UnscheduleUpdates();
		foreach ( var store in world.updateHiveObjects )
			if ( ( updateMode & store.stage ) != 0 )
				store.Add( this );
	}

	public void UnscheduleUpdates()
	{
		if ( !world ) return;

		foreach ( var store in world.updateHiveObjects )
			store.Remove( this );
	}

	public void Setup( World world )
	{
		this.world = world;
		if ( world )
		{
			foreach ( var store in world.updateHiveObjects )
				assert.IsFalse( store.Contains( this ) );
			ScheduleUpdates();
			if ( !blueprintOnly )
				id = world.nextID++;
		}
	}

	public void OnDestroy()
	{
		UnscheduleUpdates();
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

	// This function is similar to FixedUpdate, but it contains code which is sensitive to execute order, such as when woodcutters decide which tree to cut. 
	// So when this function is called by World.FixedUpdate it is always called in the same order.
	public virtual void GameLogicUpdate( UpdateStage stage ) {}

	public virtual void Remove()
	{
		UnscheduleUpdates();
		destroyed = true;
		Eradicate( gameObject );
	}

	public virtual Node location => null;
	public virtual Vector3 position => location.position;

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
			Eradicate( gameObject );
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

	void Serializer.IReferenceUser.OnDeadReference( MemberInfo member, HiveObject reference )
	{
		OnDeadReference( member, reference );
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
				assert.AreEqual( id, -1, $"{this} has an ID ({id}) but has no world" );
			return;
		}

		foreach ( var store in world.updateHiveObjects )
		{
			if ( updateIndices[store.stageIndex] >= 0 )
			{
				assert.IsNotNull( world );
				assert.IsTrue( updateIndices[store.stageIndex] < store.objects.Count, "Hive object store is corrupt" );
				assert.AreEqual( this, store.objects[updateIndices[store.stageIndex]], "Hive object store is corrupt" );
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

public class VisibleHiveObject : HiveObject
{
	public Transform flat;

	public Vector3 flatPosition { set { if ( flat ) flat.position = value; } }
	public virtual int flatRenderingSortOffset => int.MaxValue;

	public enum VisualType
	{
		nice2D,
		functional
	}

	// Creating the flat object is moved to Awake from Start, because when a new item is created it is linked
	// to the ItemsJustCreated object before Start would be called. This causes the scale not to be set correctly
	// by Item.SetParent. But setting the position of the flat object remains in Start, because the position property
	// uses the foundation of the building in case of buildings, and that is not ready at the time of the Awake call
	void Awake()
	{
		flat = new GameObject( "Flat" ).transform;
		flat.localRotation = Quaternion.Euler( 90, 90, 0 );
		flat.SetParent( transform, false );
	}

	new public void Start()
	{
		flat.position = position;
		
		foreach ( var type in new []{ VisualType.nice2D, VisualType.functional } )
		{
			var visual = CreateVisual( type );
			if ( visual )
			{
				visual.transform.SetParent( flat, false );
				if ( visual.layer == 0 )
					visual.layer = type switch {
						VisualType.nice2D => Constants.World.layerIndexSprites,
						VisualType.functional or _ => Constants.World.layerIndexMap
				};
			}
		}

		base.Start();
	}

	virtual public Sprite GetVisualSprite( VisualType visualType ) => null;

	virtual public GameObject CreateVisual( VisualType visualType )
	{
		var sprite = GetVisualSprite( visualType );
		if ( !sprite )
			return null;

		var renderer = new GameObject( "Sprite" ).AddComponent<SpriteRenderer>();
		renderer.Prepare( sprite, location.position, visualType == VisualType.functional, flatRenderingSortOffset );
		return renderer.gameObject;
	}

	public class SpriteController : MonoBehaviour
	{
		public SpriteRenderer spriteRenderer;
		public int sortOffset;

		void Start()
		{
			spriteRenderer = gameObject.GetComponent<SpriteRenderer>();
		}

		void Update()
		{
			spriteRenderer.sortingOrder = (int)( -transform.position.x * 100 + sortOffset );
		}
	}
}
