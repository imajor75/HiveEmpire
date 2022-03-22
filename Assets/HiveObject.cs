using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#pragma warning disable UNT0001

public class HiveCommon : MonoBehaviour
{
	public static World world { get { return World.instance; } }
	public static Ground ground { get { return world.ground; } }
	public static int time { get { return world.time; } }
	public static OperationHandler oh { get { return world.operationHandler; } }
	public static Eye eye { get { return world.eye; } }
	public static Interface root { get { return Interface.root; } }
	public static Network network { get { return world.network; } }

	public static void Log( string text, bool important = false )
	{
		root.logFile.Write( text + "\n" );
		root.logFile.Flush();
		if ( important )
			print( text );
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
	public Team team;
	public Simpleton.Data simpletonData;
	[JsonIgnore]
	public bool selectThis;

	public Simpleton.Data simpletonDataSafe
	{
		get
		{
			if ( simpletonData == null )
				simpletonData = new Simpleton.Data( this );

			return simpletonData;
		}
	}
	public virtual bool wantFoeClicks { get { return false; } }

	public virtual int checksum { get { return id; } }
	
	[JsonIgnore]
	public Assert assert;

	public HiveObject()
	{
		assert = new Assert( this );
	}

	public virtual void Register()
	{
		world.newHiveObjects.AddFirst( this );
		registered = true;
	}

	public void Setup()
	{
		assert.IsFalse( world.hiveObjects.Contains( this ) );
		assert.IsFalse( world.newHiveObjects.Contains( this ) );
		Register();
		if ( !blueprintOnly )
			id = world.nextID++;
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

	public void OnDestroy()
	{
		world.hiveObjects.Remove( this );
		destroyed = true;
		registered = false;
	}

	public void Update()
	{
		if ( selectThis )
		{
			selectThis = false;
			OnClicked( true );
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
			Destroy( gameObject );
	}

	public virtual void Materialize()
	{
		assert.IsTrue( blueprintOnly );
		blueprintOnly = false;
		id = world.nextID++;
	}

	public virtual void OnClicked( bool show = false )
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

	public virtual void Validate( bool chainCall )
	{
		if ( !blueprintOnly )
			assert.AreNotEqual( id, 0 );
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
