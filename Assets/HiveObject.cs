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
	static System.Random idSource = new System.Random();
	public int id = idSource.Next();		// Only to help debugging
	public bool noAssert;

	public HiveObject()
	{
		assert = new Assert( this );
	}

	public void Setup()
	{
		assert.IsFalse( World.instance.insideCriticalSection );
		assert.IsFalse( World.instance.hiveObjects.Contains( this ) );
		World.instance.hiveObjects.AddFirst( this );
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

	void OnDestroy()
	{
		World.instance.hiveObjects.Remove( this );
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
	}

	public virtual void OnClicked( bool show = false )
	{
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
