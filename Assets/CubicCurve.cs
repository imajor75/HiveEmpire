using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CubicCurve : ScriptableObject
{
	public float a, b, c, d;
	public static CubicCurve Create()
	{
		return ScriptableObject.CreateInstance<CubicCurve>();
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
