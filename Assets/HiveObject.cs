using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#pragma warning disable UNT0001

public abstract class HiveObject : MonoBehaviour
{
	public bool blueprintOnly;
	public bool inactive;
	
	[JsonIgnore]
	public Assert assert;
	public int id;
	public bool noAssert;
	public bool registered;

	public HiveObject()
	{
		assert = new Assert( this );
	}

	public void Setup()
	{
		assert.IsFalse( World.instance.hiveObjects.Contains( this ) );
		World.instance.newHiveObjects.AddFirst( this );
		registered = true;
		if ( !blueprintOnly )
			id = World.instance.nextID++;
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

	public virtual void DestroyThis( bool noAssert = false )
	{
		this.noAssert = noAssert;
		Destroy( gameObject );
	}

	public static void Log( string text )
	{
		Interface.root.logFile.Write( text + "\n" );
	}

	public void OnDestroy()
	{
		World.instance.hiveObjects.Remove( this );
		registered = false;
	}

	// This function is similar to FixedUpdate, but it contains code which is sensitive to execute order, sucs as when woodcutters decide which tree to cut. 
	// So when this function is called by World.FixedUpdate it is always called in the same order.
	public virtual void CriticalUpdate()
	{

	}

	public virtual bool Remove( bool takeYourTime = false )
	{
		assert.Fail();
		return false;
	}

	public abstract Node location { get; }

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
	}

	public virtual void Materialize()
	{
		assert.IsTrue( blueprintOnly );
		blueprintOnly = false;
		id = World.instance.nextID++;
	}

	public virtual void OnClicked( bool show = false )
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
