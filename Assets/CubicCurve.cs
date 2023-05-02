using System.Collections.Generic;
using System;

[System.Serializable]
public class CubicCurve
{
	public float a, b, c, d;
	public static CubicCurve Create()
	{
		return new CubicCurve();
	}

	public CubicCurve Setup( float position0, float position1, float direction0, float direction1 )
	{
		d = position0;
		c = direction0;
		float n = position1 - c - d;
		float m = direction1 - c;
		a = m - 2 * n;
		b = n - a;
		return this;
	}

	public CubicCurve SetupAsQuadric( float position0, float position1, float direction0 )
	{
		a = 0;
		d = position0;
		c = direction0;
		b = position1 - c - d;
		return this;
	}

	public CubicCurve SetupAsLinear( float position0, float position1 )
	{
		a = b = 0;
		d = position0;
		c = position1 - d;
		return this;
	}

	public float PositionAt( float v )
	{
		return a * v * v * v + b * v * v + c * v + d;
	}

	public float DirectionAt( float v )
	{
		return 3 * a * v * v + 2 * b * v + c;
	}
}

public class CubicArray
{
	public float startDirection, endDirection;
	public List<float> positions = new ();
	public List<CubicCurve> curves = new ();

	void UpdateCurves()
	{
		if ( curves.Count == positions.Count )
			return;

		curves.Clear();
		for ( int i = 0; i < positions.Count - 1; i++ )
		{
			var curve = CubicCurve.Create();
			float direction0 = i == 0 ? startDirection : positions[i+1] - positions[i-1];
			float direction1 = i == positions.Count - 2 ? endDirection : positions[i+2] - positions[i];
			curve.Setup( positions[i], positions[i+1], direction0, direction1 );
		}
	}

	public float PositionAt( float v )
	{
		UpdateCurves();
		var floor = MathF.Floor( v );
		return curves[(int)floor].PositionAt( v - floor );
	}

	public float DirectionAt( float v )
	{
		UpdateCurves();
		var floor = MathF.Floor( v );
		return curves[(int)floor].DirectionAt( v - floor );
	}
}