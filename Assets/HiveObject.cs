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
	public bool registered;
	public bool destroyed;
	public int worldIndex = -1;
	public World world;
	public Team team;
	public Simpleton.Data simpletonData;
	[JsonIgnore]
	public bool selectThis;

	public enum RunMode
	{
		realtime,
		lazy,
		sleep
	}

	virtual public RunMode runMode => RunMode.realtime;

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
	}

	override public string ToString()
	{
		return name;
	}

	public virtual void Register()
	{
		switch ( runMode )
		{
			case RunMode.realtime: world.realtimeHiveObjects.Add( this ); break;
			case RunMode.lazy: world.lazyHiveObjects.Add( this ); break;
		};
		registered = true;
	}

	public void Setup( World world )
	{
		this.world = world;
		if ( world )
		{
			assert.IsFalse( world.realtimeHiveObjects.objects.Contains( this ) );
			assert.IsFalse( world.realtimeHiveObjects.newObjects.Contains( this ) );
			assert.IsFalse( world.lazyHiveObjects.objects.Contains( this ) );
			assert.IsFalse( world.lazyHiveObjects.newObjects.Contains( this ) );
			Register();
			if ( !blueprintOnly )
				id = world.nextID++;
		}
	}

	public void OnDestroy()
	{
		switch ( runMode )
		{
			case RunMode.realtime: world?.realtimeHiveObjects?.Remove( this ); break;
			case RunMode.lazy: world?.lazyHiveObjects?.Remove( this ); break;
		}
		destroyed = true;
		registered = false;
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
	public virtual void GameLogicUpdate()
	{
	}

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
		if ( !blueprintOnly && world )
			assert.AreNotEqual( id, 0, $"{this} has an ID ({id}) but has no world" );
		if ( worldIndex >= 0 )
		{
			assert.IsTrue( registered );
			assert.IsNotNull( world );
			if ( runMode == RunMode.realtime )
			{
				assert.IsTrue( worldIndex < world.realtimeHiveObjects.objects.Count, "Hive object store is corrupt" );
				assert.AreEqual( this, world.realtimeHiveObjects.objects[worldIndex], "Hive object store is corrupt" );
			}
			if ( runMode == RunMode.lazy )
			{
				assert.IsTrue( worldIndex < world.lazyHiveObjects.objects.Count, "Hive object store is corrupt" );
				assert.AreEqual( this, world.lazyHiveObjects.objects[worldIndex], "Hive object store is corrupt" );
			}
			assert.AreNotEqual( runMode, RunMode.sleep );
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
