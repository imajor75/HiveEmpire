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

	public virtual void DestroyThis()
	{
		Destroy( gameObject );
	}

	public virtual bool Remove( bool takeYourTime = false )
	{
		assert.Fail();
		return false;
	}

	[JsonIgnore]
	public abstract GroundNode Node { get; }

	public virtual void Reset()
	{ 
	}

	public virtual void Materialize()
	{
		assert.IsTrue( blueprintOnly );
		blueprintOnly = false;
	}

	public virtual void OnClicked()
	{
	}

	public virtual void Validate()
	{
	}
}
