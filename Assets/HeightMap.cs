using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HeightMap : ScriptableObject
{
	public int sizeX = 513, sizeY = 513;
	public bool tileable = false;
	public float magnitudeReduction = 0.5f;
	public bool island = true;
	public System.Random random;
	double[,] data;

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
		data = new double[sizeX, sizeY];
		if ( island )
		{
			data[0, 0] =
			data[sizeX - 1, 0] =
			data[0, sizeY - 1] =
			data[sizeX - 1, sizeY - 1] = 0;
		}
		else
		{
			data[0, 0] = random.NextDouble();
			data[sizeX - 1, 0] = random.NextDouble();
			data[0, sizeY - 1] = random.NextDouble();
			data[sizeX - 1, sizeY - 1] = random.NextDouble();
		}
		ProcessSquare( ( sizeX - 1 ) / 2, ( sizeY - 1 ) / 2, ( sizeX - 1 / 2 ), 1 );
	}

	void ProcessSquare( int x, int y, int s, float m )
	{
		double average  = 0;
		average += data[x - s, y - s];
		average += data[x + s, y - s];
		average += data[x - s, y + s];
		average += data[x + s, y + s];
		average /= 4;
		data[x, y] = average + random.NextDouble() * m;
		ProcessDiamond( x, y - s, s, m );
		ProcessDiamond( x - s, y, s, m );
		ProcessDiamond( x + s, y, s, m );
		ProcessDiamond( x, y + s, s, m );
	}

	void ProcessDiamond( int x, int y, int s, float m )
	{
		double average = 0;
		int count = 0;
		if ( y == 0 )
		{
			if ( tileable )
			{
				average += data[x, sizeY - l];
				count++;
			}
		}
		else
		{
			average += data[x, y - s];
			count++;
		}
			;
	}
}
		
