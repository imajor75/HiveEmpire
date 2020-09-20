using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HeightMap : ScriptableObject
{
	int sizeX = 513, sizeY = 513;
	public bool tileable = false;
	public float magnitudeReduction = 0.5f;
	public float magnitudeStart = 1;
	public bool island = true;
	public float squareDiamondRatio = 1;
	public float randomness = 0.2f;
	public System.Random random;
	public float[,] data;

	public void Setup( int size, bool tileable = false, bool island = true, float magnitudeReduction = 0.5f )
	{
		sizeX = sizeY = ( 1 << size ) + 1;
		this.tileable = tileable;
		this.island = island;
		this.magnitudeReduction = magnitudeReduction;
	}

	public void Fill( int seed )
	{
		random = new System.Random( seed );
		data = new float[sizeX, sizeY];
		if ( island )
		{
			data[0, 0] =
			data[sizeX - 1, 0] =
			data[0, sizeY - 1] =
			data[sizeX - 1, sizeY - 1] = 0;
		}
		else
		{
			data[0, 0] = (float)random.NextDouble();
			data[sizeX - 1, 0] = (float)random.NextDouble();
			data[0, sizeY - 1] = (float)random.NextDouble();
			data[sizeX - 1, sizeY - 1] = (float)random.NextDouble();
		}
		ProcessSquare( ( sizeX - 1 ) / 2, ( sizeY - 1 ) / 2, ( sizeX - 1 / 2 ), magnitudeStart );
	}

	void ProcessSquare( int x, int y, int s, float m )
	{
		float average  = 0;
		average += data[x - s, y - s];
		average += data[x + s, y - s];
		average += data[x - s, y + s];
		average += data[x + s, y + s];
		float r = randomness * squareDiamondRatio;
		average += (float)random.NextDouble() * m * r;
		data[x, y] = average / ( 4 + m * r );		;
		ProcessDiamond( x, y - s, s, m );
		ProcessDiamond( x - s, y, s, m );
		ProcessDiamond( x + s, y, s, m );
		ProcessDiamond( x, y + s, s, m );
		if ( s == 1 )
			return;
		int n = s / 2;
		ProcessSquare( x - n, y - n, n, m * magnitudeReduction );
		ProcessSquare( x + n, y - n, n, m * magnitudeReduction );
		ProcessSquare( x - n, y + n, n, m * magnitudeReduction );
		ProcessSquare( x + n, y + n, n, m * magnitudeReduction );
	}

	void ProcessDiamond( int x, int y, int s, float m )
	{
		float average = 0;
		int count = 0;
		if ( y == 0 )
		{
			if ( tileable )
			{
				average += data[x, sizeY - s - 1];
				count++;
			}
		}
		else
		{
			average += data[x, y - s];
			count++;
		}
		if ( x == 0 )
		{
			if ( tileable )
			{
				average += data[sizeX - s - 1, y];
				count++;
			}
		}
		else
		{
			average += data[x - s, y];
			count++;
		}
		if ( y == sizeY - 1 )
		{
			if ( tileable )
			{
				average += data[x, s];
				count++;
			}
		}
		else
		{
			average += data[x, y + s];
			count++;
		}
		if ( x == sizeY - 1 )
		{
			if ( tileable )
			{
				average += data[s, x];
				count++;
			}
		}
		else
		{
			average += data[x + s, y];
			count++;
		}
		Assert.IsTrue( count == 3 || count == 4 );
		data[x, y] = (average + (float)random.NextDouble() * m)/(m+count);
	}
}
		
