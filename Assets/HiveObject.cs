using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HiveObject : MonoBehaviour
{
	[JsonIgnore]
	public Assert assert;

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

	public static new void Destroy( UnityEngine.Object toDestroy )
	{
		if ( toDestroy == null )
			return;
		UnityEngine.Object.Destroy( toDestroy );
	}

	public virtual bool Remove( bool takeYourTime = false )
	{
		assert.IsTrue( false );
		return false;
	}

	public virtual void Reset()
	{ 
	}

	public virtual void Validate()
	{
	}
}
