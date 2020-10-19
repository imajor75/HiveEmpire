﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class Assert
{
	Component boss;
	public static Assert global = new Assert( null );
	public static bool problemSelected = false;

	public Assert() { }

	public Assert( Component boss = null )
	{
		this.boss = boss;
	}

	[Conditional( "DEBUG" )]
	public void IsTrue( bool condition, string message = "" )
	{
		if ( !condition )
			Failed( message );
	}

	[Conditional( "DEBUG" )]
	public void IsFalse( bool condition, string message = "" )
	{
		if ( condition )
			Failed( message );
	}

	[Conditional( "DEBUG" )]
	public void IsNull<T>( T reference, string message = "" )
	{
		if ( reference != null )
			Failed( message );
	}

	[Conditional( "DEBUG" )]
	public void IsNotNull<T>( T reference, string message = "" )
	{
		if ( reference == null )
			Failed( message );
	}

	[Conditional( "DEBUG" )]
	public void IsValid( UnityEngine.Object reference, string message = "" )
	{
		if ( reference == null )
			Failed( message );
	}

	[Conditional( "DEBUG" )]
	public void AreEqual( int a, int b, string message = "" )
	{
		if ( a == b )
			return;

		message += "(" + a + " == " + b + ")";
		Failed( message );
	}

	[Conditional( "DEBUG" )]
	public void AreEqual( float a, float b, string message = "" )
	{
		if ( a == b )
			return;

		message += "(" + a + " == " + b + ")";
		Failed( message );
	}

	[Conditional( "DEBUG" )]
	public void AreEqual<T>( T a, T b, string message = "" )
	{
		if ( a == null )
		{
			if ( b != null )
				Failed( message );
			return;
		}
		if ( a.Equals( b ) )
			return;

		Failed( message );
	}

	[Conditional( "DEBUG" )]
	public void AreNotEqual(int a, int b, string message = "")
	{
		if ( a != b )
			return;

		message += "(" + a + " == " + b + ")";
		Failed( message );
	}

	[Conditional( "DEBUG" )]
	public void AreNotEqual(float a, float b, string message = "")
	{
		if ( a != b )
			return;

		message += "(" + a + " == " + b + ")";
		Failed( message );
	}

	[Conditional( "DEBUG" )]
	public void AreNotEqual<T>(T a, T b, string message = "")
	{
		if ( b == null && a == null )
			Failed( message );
		if ( b == null || a == null )
			return;
		
		if ( a.Equals( b ) )
			Failed( message );
	}

	[Conditional( "DEBUG" )]
	public void AreApproximatelyEqual( float a, float b, string message = "", float tolerance = 0.00001f )
	{
		if ( Math.Abs( a - b ) < tolerance )
			return;

		Failed( message );
	}

	[Conditional( "DEBUG" )]
	public void IsNotSelected()
	{
#if DEBUG
		if ( Selection.Contains( boss.gameObject ) )
			UnityEngine.Debug.Break();
#endif
	}

	void Failed( string message )
	{
		var stackTrace = new StackTrace();
		var stackFrame = stackTrace.GetFrame( 2 );
		var method = stackFrame.GetMethod();
		message = Caller( 3 ) + " : " + message;
		if ( message != "" )
			UnityEngine.Debug.LogAssertion( message );

#if DEBUG
		if ( boss != null && !problemSelected )
		{
			Selection.activeGameObject = boss.gameObject;
			problemSelected = true;
		}

		EditorApplication.isPaused = true;
#endif
		throw new System.Exception();
	}

	string Caller( int depth )
	{
		var stackTrace = new StackTrace();
		var stackFrame = stackTrace.GetFrame( depth );
		var method = stackFrame.GetMethod();
		return method.DeclaringType.Name + "." + method.Name;
	}

	public class Base : MonoBehaviour
	{
		[JsonIgnore]
		public Assert assert;

		public Base()
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
	}
}
