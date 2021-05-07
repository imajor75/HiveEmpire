using System;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;

public class Assert
{
	readonly Component boss;
	public static Assert global = new Assert( null );
	public static bool problemSelected = false;
	public static bool error;

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
			Fail( message );
	}

	[Conditional( "DEBUG" )]
	public void IsFalse( bool condition, string message = "" )
	{
		if ( condition )
			Fail( message );
	}

	[Conditional( "DEBUG" )]
	public void IsNull<T>( T reference, string message = "" )
	{
		if ( reference != null && !reference.Equals( null ) )
			Fail( message );
	}

	[Conditional( "DEBUG" )]
	public void IsNotNull<T>( T reference, string message = "" )
	{
		if ( reference == null || reference.Equals( null ) )
			Fail( message );
	}

	[Conditional( "DEBUG" )]
	public void IsValid( UnityEngine.Object reference, string message = "" )
	{
		if ( reference == null )
			Fail( message );
	}

	[Conditional( "DEBUG" )]
	public void AreEqual( int a, int b, string message = "" )
	{
		if ( a == b )
			return;

		message += "(" + a + " == " + b + ")";
		Fail( message );
	}

	[Conditional( "DEBUG" )]
	public void AreEqual( float a, float b, string message = "" )
	{
		if ( a == b )
			return;

		message += "(" + a + " == " + b + ")";
		Fail( message );
	}

	[Conditional( "DEBUG" )]
	public void AreEqual<T>( T a, T b, string message = "" )
	{
		if ( a == null )
		{
			if ( b != null )
				Fail( message );
			return;
		}
		if ( a.Equals( b ) )
			return;

		Fail( message );
	}

	[Conditional( "DEBUG" )]
	public void AreNotEqual(int a, int b, string message = "")
	{
		if ( a != b )
			return;

		message += "(" + a + " == " + b + ")";
		Fail( message );
	}

	[Conditional( "DEBUG" )]
	public void AreNotEqual(float a, float b, string message = "")
	{
		if ( a != b )
			return;

		message += "(" + a + " == " + b + ")";
		Fail( message );
	}

	[Conditional( "DEBUG" )]
	public void AreNotEqual<T>(T a, T b, string message = "")
	{
		if ( b == null && a == null )
			Fail( message );
		if ( b == null || a == null )
			return;
		
		if ( a.Equals( b ) )
			Fail( message );
	}

	[Conditional( "DEBUG" )]
	public void AreApproximatelyEqual( float a, float b, string message = "", float tolerance = 0.00001f )
	{
		if ( Math.Abs( a - b ) < tolerance )
			return;

		Fail( message );
	}

	[Conditional( "DEBUG" )]
	public void IsNotSelected()
	{
#if DEBUG
		if ( Selection.Contains( boss.gameObject ) )
		{

			UnityEngine.Debug.Log( "Code on selected from " + Caller( 2 ) );
			int h = 3; h++;
		}
#endif
	}

	public void Fail( string message = "Something went wrong" )
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
		throw new Exception();
	}

	string Caller( int depth )
	{
		var stackTrace = new StackTrace();
		var stackFrame = stackTrace.GetFrame( depth );
		var method = stackFrame.GetMethod();
		return method.DeclaringType.Name + "." + method.Name;
	}

	static void LogCallback( string condition, string stackTrace, LogType type )
	{
		if ( type == LogType.Exception || type == LogType.Assert || type == LogType.Error )
			error = true;
	}
}
