using System.Collections;
using System.Collections.Generic;
using System.IO;
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
	[Range(0.0f, 1.0f)]
	public float randomness = 0.5f;
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
		float w = island ? 0 : 1;
		Randomize( ref data[0, 0], magnitudeStart, w );
		Randomize( ref data[sizeX-1, 0], magnitudeStart, w );
		Randomize( ref data[0, sizeY-1], magnitudeStart, w );
		Randomize( ref data[sizeX-1, sizeY-1], magnitudeStart, w );
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
		average /= 4;
		Randomize( ref average, m );
		data[x, y] = average;
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
		average /= count;
		Randomize( ref average, m );
		data[x, y] = average;
	}

	void Randomize( ref float value, float magnitude, float weight = -1 )
	{
		if ( weight < 0 )
			weight = randomness;
		float randomValue = (float)Ground.rnd.NextDouble();
		randomValue = magnitudeOffset + ( randomValue - 0.5f ) * magnitude + 0.5f;
		value = value * ( 1 - weight ) + randomValue * weight;
	}

	public Texture2D mapTexture;
	void OnValidate()
	{
		Fill();
		if ( mapTexture == null )
			mapTexture = new Texture2D( 512, 512 );

		for ( int x = 0; x < 512; x++ )
		{
			for ( int y = 0; y < 512; y++ )
			{
				float h = data[x, y];
				mapTexture.SetPixel( x, y, new Color( h, h, h ) );
			}
		}

		var bytes = mapTexture.EncodeToPNG();
		FileStream file = File.Open("akarmi.png",FileMode.Create);
		BinaryWriter binary = new BinaryWriter(file);
		binary.Write( bytes );
		file.Close();

		mapTexture.Apply();
	}
}
		
