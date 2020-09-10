using System;
using System.Diagnostics;
using UnityEngine;

public class Assert
{
	public static void IsTrue( bool condition, string message = "" )
	{
		if ( !condition )
			Failed( message );
	}

	public static void IsFalse( bool condition, string message = "" )
	{
		if ( condition )
			Failed( message );
	}

	public static void IsNull<T>( T reference, string message = "" )
	{
		if ( reference != null )
			Failed( message );
	}

	public static void IsNotNull<T>( T reference, string message = "" )
	{
		if ( reference == null )
			Failed( message );
	}

	public static void AreEqual( int a, int b, string message = "" )
	{
		if ( a == b )
			return;

		message += "(" + a + " == " + b + ")";
		Failed( message );
	}

	public static void AreEqual( float a, float b, string message = "" )
	{
		if ( a == b )
			return;

		message += "(" + a + " == " + b + ")";
		Failed( message );
	}

	public static void AreEqual( object a, object b, string message = "" )
	{
		if ( a == b )
			return;

		Failed( message );
	}

	public static void AreEqual<T>( T a, T b, string message = "" ) where T : System.Enum
	{
		if ( a.CompareTo( b ) == 0 )
			return;

		Failed( message );
	}

	static void Failed( string message )
	{
		var stackTrace = new StackTrace();
		var stackFrame = stackTrace.GetFrame( 2 );
		var method = stackFrame.GetMethod();
		message = method.DeclaringType.Name + "." + method.Name + " " + ": " + message;
		if ( message != "" )
			UnityEngine.Debug.LogAssertion( message );

		throw new System.Exception( "The condition was false" );
	}
}
