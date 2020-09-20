using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HeightMap : ScriptableObject
{
	int sizeX = 513, sizeY = 513;
	public bool tileable = true;
	[Range(0.0f, 1.0f)]    
	public float magnitudeReduction = 0.5f;
	[Range(0.0f, 2.0f)]
	public float magnitudeStart = 1;
	[Range(-1.0f, 1.0f)]
	public float magnitudeOffset = 0;
	public bool island = false;
	[Range(0.5f, 1.5f)]
	public float squareDiamondRatio = 1;
	[Range(0.0f, 50.0f)]
	public float randomness = 20;
	public System.Random random;
	public float[,] data;
	public int seed;

	public void Setup( int size, int seed, bool tileable = false, bool island = false, float magnitudeReduction = 0.5f )
	{
		sizeX = sizeY = ( 1 << size ) + 1;
		this.tileable = tileable;
		this.island = island;
		this.seed = seed;
		this.magnitudeReduction = magnitudeReduction;
	}

	public void Fill()
	{
		random = new System.Random( seed );
		data = new float[sizeX, sizeY];
		if ( island )
		{
			data[0, 0] =
			data[sizeX - 1, 0] =
			data[0, sizeY - 1] =
			data[sizeX - 1, sizeY - 1] = magnitudeOffset;
		}
		else
		{
			data[0, 0] = magnitudeOffset + (float)random.NextDouble() * magnitudeStart;
			data[sizeX - 1, 0] = magnitudeOffset + (float)random.NextDouble() * magnitudeStart;
			data[0, sizeY - 1] = magnitudeOffset + (float)random.NextDouble() * magnitudeStart;
			data[sizeX - 1, sizeY - 1] = magnitudeOffset + (float)random.NextDouble() * magnitudeStart;
		}
		int i = sizeX - 1;
		float m = magnitudeStart;
		while ( i > 1 )
		{
			ProcessLevel( i, m );
			i /= 2;
			m *= magnitudeReduction;
		}
	}

	void ProcessLevel( int i, float m )
	{
		for ( int x = i / 2; x < sizeX; x += i )
			for ( int y = i / 2; y < sizeY; y += i )
				ProcessSquare( x, y, i / 2, m );

		for ( int x = 0; x < sizeX; x += i )
			for ( int y = i / 2; y < sizeY; y += i )
				ProcessDiamond( x, y, i / 2, m );
		for ( int x = i / 2; x < sizeX; x += i )
			for ( int y = 0; y < sizeY; y += i )
				ProcessDiamond( x, y, i / 2, m );
	}

	void ProcessSquare( int x, int y, int s, float m )
	{
		float average  = 0;
		average += data[x - s, y - s];
		average += data[x + s, y - s];
		average += data[x - s, y + s];
		average += data[x + s, y + s];
		float r = randomness * squareDiamondRatio;
		average += magnitudeOffset + (float)random.NextDouble() * m * r;
		data[x, y] = average / ( 4 + m * r );		;
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
		data[x, y] = ( average + randomness * ( magnitudeOffset + (float)random.NextDouble() * m ) ) / ( randomness * m + count );
	}
}
		
