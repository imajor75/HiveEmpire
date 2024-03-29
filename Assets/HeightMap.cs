﻿using Newtonsoft.Json;
using System;
using UnityEngine;

public class HeightMap
{
	public int sizeX = ( 1 << Constants.HeightMap.defaultSize ) + 1, sizeY = ( 1 << Constants.HeightMap.defaultSize ) + 1, size = Constants.HeightMap.defaultSize;
	public System.Random random;
	[JsonIgnore]
	public float[,] data;
	[Range(0, 10000)]
	public int seed;
	public float averageValue;
	public CubicCurve randomCurve;

	[Serializable]
	public class Settings
	{
		public int mapSize = Constants.HeightMap.defaultSize;
		public bool tileable = Constants.HeightMap.defaultTileable;
		public bool island = Constants.HeightMap.defaultIsland;

		[Range(0.0f, 1.0f)]
		public float borderLevel = Constants.HeightMap.defaultBorderLevel;

		[Range(0.0f, 4.0f)]
		public float randomness = Constants.HeightMap.defaultRandomness;
		[Range(-1.0f, 1.0f)]
		public float noise = Constants.HeightMap.defaultNoise;
		[Range(-1.0f, 1.0f)]
		public float randomnessDistribution = Constants.HeightMap.defaultRandomnessDistribution;

		public bool normalize = Constants.HeightMap.defaultNormalize;
		[Range(-1.0f, 1.0f)]
		public float adjustment = Constants.HeightMap.defaultAdjustment;

		[Range(0.5f, 1.5f)]
		public float squareDiamondRatio = Constants.HeightMap.defaultSquareDiamondRatio;
	}

	Settings settings;

	public static HeightMap Create()
	{
		return new HeightMap();
	}

	public void Setup( Settings settings, int seed )
	{
		this.settings = settings;
		sizeX = sizeY = ( 1 << settings.mapSize ) + 1;
		this.seed = seed;
	}

	public void Fill()
	{
		random = new System.Random( seed );
		float derivative = (float)Math.Pow( 2, settings.randomnessDistribution );
		Assert.global.IsFalse( float.IsNaN( derivative ) );
		randomCurve = CubicCurve.Create().SetupAsQuadric( settings.randomness, settings.noise, -derivative );
		averageValue = 0;
		data = new float[sizeX, sizeY];
		Randomize( ref data[0, 0], 1 );
		if ( settings.tileable )
			data[sizeX - 1, 0] = data[0, sizeY - 1] = data[sizeX - 1, sizeY - 1] = data[0, 0];
		else
		{
			Randomize( ref data[sizeX - 1, 0], 1 );
			Randomize( ref data[0, sizeY - 1], 1 );
			Randomize( ref data[sizeX - 1, sizeY - 1], 1 );
		}
		int i = sizeX - 1;
		float step = 0;
		while ( i > 1 )
		{
			if ( settings.island )
			{
				Assert.global.AreEqual( sizeX, sizeY );
				for ( int j = 0; j < sizeX; j++ )
				{
					data[j, 0] = settings.borderLevel;
					data[j, sizeY - 1] = settings.borderLevel;
					data[0, j] = settings.borderLevel;
					data[sizeX - 1, j] = settings.borderLevel;
				}
			}
			float randomWeight = randomCurve.PositionAt( (float)( sizeX - 1 - i ) / ( sizeX - 1 ) );
			Assert.global.IsFalse( float.IsNaN( randomWeight ) );
			ProcessLevel( i, Math.Max( 0, Math.Min( randomWeight, 1 ) ) );
			if ( settings.tileable )
			{
				Assert.global.AreEqual( sizeX, sizeY );
				for ( int j = 0; j < sizeX; j++ )
				{
					data[j, 0] = data[j, sizeY - 1];
					data[0, j] = data[sizeX - 1, j];
				}
			}

			i /= 2;
			step += 1f/size;
		}

		PostProcess();
	}

	void PostProcess()
	{
		averageValue = 0;
		float power = (float)Math.Pow( 0.25f, settings.adjustment );
		float min = 1, max = 0;
		for ( int x = 0; x < sizeX; x++ )
		{
			for ( int y = 0; y < sizeY; y++ )
			{
				min = Math.Min( min, data[x, y] );
				max = Math.Max( max, data[x, y] );
			}
		}
		float normalizer = 1 / ( max - min );
		for ( int x = 0; x < sizeX; x++ )
		{
			for ( int y = 0; y < sizeY; y++ )
			{
				float value = data[x, y];
				if ( settings.normalize )
					value = ( value - min ) * normalizer;
				value = (float)Math.Pow( value, power );
				data[x, y] = value;
				averageValue += value;
			}
		}
		averageValue /= sizeX * sizeY;
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
		average /= 4;
		Randomize( ref average, m * settings.squareDiamondRatio );
		data[x, y] = average;
	}

	void ProcessDiamond( int x, int y, int s, float m )
	{
		float average = 0;
		int count = 0;
		if ( y == 0 )
		{
			if ( settings.tileable )
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
			if ( settings.tileable )
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
			if ( settings.tileable )
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
			if ( settings.tileable )
			{
				average += data[s, y];
				count++;
			}
		}
		else
		{
			average += data[x + s, y];
			count++;
		}
		Assert.global.IsTrue( count == 3 || count == 4 );
		average /= count;
		Randomize( ref average, m );
		data[x, y] = average;
	}

	void Randomize( ref float value, float weight )
	{
		float randomValue = (float)random.NextDouble();
		value = value * ( 1 - weight ) + randomValue * weight;
		Assert.global.IsFalse( float.IsNaN( value ) );
	}

	Texture2D AsTexture()
	{
		int w = 1 << size;
		var texture = new Texture2D( w, w );

		for ( int x = 0; x < w; x++ )
		{
			for ( int y = 0; y < w; y++ )
			{
				float h = data[x, y];
				texture.SetPixel( x, y, new Color( h, h, h ) );
			}
		}
		texture.Apply();
		return texture;
	}

	public void SavePNG( string file )
	{
		System.IO.File.WriteAllBytes( file, AsTexture().EncodeToPNG() );
	}
}

