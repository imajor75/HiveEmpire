using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#pragma warning disable UNT0001

public abstract class HiveObject : MonoBehaviour
{
	[JsonIgnore]
	public Assert assert;
	public bool blueprintOnly;
	static System.Random idSource = new System.Random();
	public int id = idSource.Next();		// Only to help debugging
	public bool noAssert;
	public bool inactive;

	public HiveObject()
	{
		assert = new Assert( this );
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

	public virtual bool Remove( bool takeYourTime = false )
	{
		assert.Fail();
		return false;
	}

	public abstract GroundNode location { get; }

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
}
