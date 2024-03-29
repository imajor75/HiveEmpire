﻿using System;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;

public class Assert
{
	readonly Component boss;
	public static Assert global = new Assert( null );
	public static bool problemSelected = false;

	public Assert() { }

	public Assert( Component boss = null )
	{
		this.boss = boss;
	}

	public static void Initialize()
	{
		Application.logMessageReceived += LogCallback;
	}

	[Conditional( "DEBUG" )]
	public void IsTrue( bool condition, string message = "" )
	{
		if ( !condition )
			Fail( message, 2 );
	}

	[Conditional( "DEBUG" )]
	public void IsFalse( bool condition, string message = "" )
	{
		if ( condition )
			Fail( message, 2 );
	}

	[Conditional( "DEBUG" )]
	public void IsNull<T>( T reference, string message = "" )
	{
		if ( reference != null && !reference.Equals( null ) )
			Fail( message, 2 );
	}

	[Conditional( "DEBUG" )]
	public void IsNotNull<T>( T reference, string message = "" )
	{
		if ( reference == null || reference.Equals( null ) )
			Fail( message, 2 );
	}

	[Conditional( "DEBUG" )]
	public void IsValid( UnityEngine.Object reference, string message = "" )
	{
		if ( reference == null )
			Fail( message, 2 );
	}

	[Conditional( "DEBUG" )]
	public void AreEqual( int a, int b, string message = "" )
	{
		if ( a == b )
			return;

		message += $" ({a}=={b})";
		Fail( message, 2 );
	}

	[Conditional( "DEBUG" )]
	public void AreEqual( float a, float b, string message = "" )
	{
		if ( a == b )
			return;

		message += $" ({a}=={b})";
		Fail( message, 2 );
	}

	[Conditional( "DEBUG" )]
	public void AreEqual<T>( T a, T b, string message = "" )
	{
		if ( a == null )
		{
			if ( b != null )
				Fail( message, 2 );
			return;
		}
		if ( a.Equals( b ) )
			return;

		Fail( message, 2 );
	}

	[Conditional( "DEBUG" )]
	public void AreNotEqual(int a, int b, string message = "")
	{
		if ( a != b )
			return;

		message += $" ({a}=={b})";
		Fail( message, 2 );
	}

	[Conditional( "DEBUG" )]
	public void AreNotEqual(float a, float b, string message = "")
	{
		if ( a != b )
			return;

		message += $" ({a}=={b})";
		Fail( message, 2 );
	}

	[Conditional( "DEBUG" )]
	public void AreNotEqual<T>(T a, T b, string message = "")
	{
		if ( b == null && a == null )
			Fail( message, 2 );
		if ( b == null || a == null )
			return;
		
		if ( a.Equals( b ) )
			Fail( message, 2 );
	}

	[Conditional( "DEBUG" )]
	public void AreApproximatelyEqual( float a, float b, string message = "", float tolerance = 0.00001f )
	{
		if ( Math.Abs( a - b ) < tolerance )
			return;

		Fail( message, 2 );
	}

	[Conditional( "DEBUG" )]
	public void IsNotSelected()
	{
#if DEBUG
		if ( Selection.Contains( boss.gameObject ) )
			UnityEngine.Debug.Log( "Code on selected from " + Caller( 2 ) );
#endif
	}

	public void Fail( string message = "Something went wrong", int depth = 1 )
	{
#if DEBUG
		message = Caller( depth + 1 ) + " : " + message;
		if ( message != "" )
		{
			HiveObject.Log( "! " + message );
			UnityEngine.Debug.LogAssertion( message );
		}

		if ( boss != null && !problemSelected )
		{
			Selection.activeGameObject = boss.gameObject;
			problemSelected = true;
		}
		//throw new Exception( message );
#endif
	}

	public static string Caller( int depth = 2 )
	{
		var stackTrace = new StackTrace();
		var stackFrame = stackTrace.GetFrame( depth );
		if ( stackFrame == null )
			return "none";
		var method = stackFrame.GetMethod();
		return method.DeclaringType.Name + "." + method.Name;
	}

	static void LogCallback( string condition, string stackTrace, LogType type )
	{
		if ( type == LogType.Exception || type == LogType.Assert || type == LogType.Error )
		{
			if ( !HiveCommon.root.errorOccured )
				HiveCommon.Log( $"Disabling exit save due to: {condition}" );
			HiveCommon.root.errorOccured = true;
		}
	}
}
