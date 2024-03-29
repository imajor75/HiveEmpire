using System.Collections.Generic;
using System;
using System.Linq;

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
	List<float> positions = new (), directions = new ();
	List<CubicCurve> curves = new ();

	public int length => positions.Count - 1;

	void UpdateCurves()
	{
		if ( curves.Count == positions.Count - 1 )
			return;

		Assert.global.AreEqual( positions.Count, directions.Count );
		Assert.global.AreNotEqual( directions.First(), float.MaxValue );
		Assert.global.AreNotEqual( directions.Last(), float.MaxValue );

		curves.Clear();
		for ( int i = 0; i < positions.Count - 1; i++ )
		{
			var curve = CubicCurve.Create();
			float direction0 = directions[i];
			if ( direction0 == float.MaxValue && i > 0 )
				direction0 = positions[i+1] - positions[i-1];
			float direction1 = directions[i+1];
			if ( direction1 == float.MaxValue && i != positions.Count - 2 )
				direction1 = positions[i+2] - positions[i];
			curve.Setup( positions[i], positions[i+1], direction0, direction1 );
			curves.Add( curve );
		}
	}

	public void AddPosition( float position, float direction = float.MaxValue )
	{
		positions.Add( position );
		directions.Add( direction );
	}

	public float PositionAt( float v )
	{
		UpdateCurves();
		if ( v >= curves.Count )
			return positions.Last();

		var floor = MathF.Floor( v );
		return curves[(int)floor].PositionAt( v - floor );
	}

	public float DirectionAt( float v )
	{
		UpdateCurves();
		if ( v >= curves.Count )
			return directions.Last();

		var floor = MathF.Floor( v );
		return curves[(int)floor].DirectionAt( v - floor );
	}
}