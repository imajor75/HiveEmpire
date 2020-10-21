using Newtonsoft.Json;
using System;
using UnityEngine;

public class HeightMap : MonoBehaviour
{
	public int sizeX = 513, sizeY = 513, size = 9;
	public bool tileable = false;
	[Range(-2.0f, 2.0f)]    
	public float deepnessExp = 0;
	[Range(0.0f, 5.0f)]
	public float deepnessStart = 1;
	[Range(-1.0f, 1.0f)]
	public float deepnessOffset = 0;
	public bool island = false;
	[Range(0.5f, 1.5f)]
	public float squareDiamondRatio = 1;
	[Range(0.0f, 1.0f)]
	public float randomness = 0.5f;
	public System.Random random;
	[JsonIgnore]
	public float[,] data;
	[Range(0, 10000)]
	public int seed;
	public bool deepnessAffectsMagnitude = true;
	public bool deepnessAffectsRandomness = true;
	public bool normalize = true;
	[Range(-1.0f, 1.0f)]
	public float adjustLow = 0, adjustHigh = 0;
	public float averageValue;
	int count;
	float borderLevel = 0;

	public static HeightMap Create()
	{
		var map = new GameObject().AddComponent<HeightMap>();
		map.name = "Height map";
		return map;
	}

	public void Setup( int size, int seed, bool tileable = false, bool island = false, float deepnessExp = 0 )
	{
		this.size = size;
		sizeX = sizeY = ( 1 << size ) + 1;
		this.tileable = tileable;
		this.island = island;
		this.seed = seed;
		this.deepnessExp = deepnessExp;
	}

	public void Fill()
	{
		random = new System.Random( seed );
		count = 0;
		averageValue = 0;
		data = new float[sizeX, sizeY];
		Randomize( ref data[0, 0], deepnessStart, 1 );
		Randomize( ref data[sizeX - 1, 0], deepnessStart, 1 );
		Randomize( ref data[0, sizeY - 1], deepnessStart, 1 );
		Randomize( ref data[sizeX - 1, sizeY - 1], deepnessStart, 1 );
		int i = sizeX - 1;
		float step = 0;
		float c = deepnessStart, a = deepnessExp;
		float b = - a - c;
		while ( i > 1 )
		{
			if ( island )
			{
				Assert.global.AreEqual( sizeX, sizeY );
				for ( int j = 0; j < sizeX; j++ )
				{
					data[j, 0] = borderLevel;
					data[j, sizeY - 1] = borderLevel;
					data[0, j] = borderLevel;
					data[sizeX - 1, j] = borderLevel;
				}
			}
			ProcessLevel( i, a * step * step + b * step + c );

			i /= 2;
			step += 1f/size;
		}

		Assert.global.AreEqual( count, sizeX * sizeY );
		averageValue /= count;

		PostProcess();
	}

	void PostProcess()
	{
		averageValue = 0;
		float powerLow = (float)Math.Pow( 5, adjustLow );
		float powerHigh = (float)Math.Pow( 5, adjustHigh );
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
				if ( normalize )
					value = ( value - min ) * normalizer;
				if ( value < 0.5 )
					value = (float)( 0.5 * Math.Pow( value * 2, powerLow ) );
				else
					value = (float)( 1 - 0.5 * Math.Pow( ( 1 - value ) * 2, powerHigh ) );
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
		float r = randomness * squareDiamondRatio;
		Randomize( ref average, m, r );
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
		Assert.global.IsTrue( count == 3 || count == 4 );
		average /= count;
		Randomize( ref average, m );
		data[x, y] = average;
	}

	void Randomize( ref float value, float deepness, float weight = -1 )
	{
		if ( weight < 0 )
			weight = randomness;
		if ( deepnessAffectsMagnitude )
			weight *= deepness;
		float randomValue = (float)random.NextDouble();
		if ( deepnessAffectsRandomness )
			randomValue = deepnessOffset + ( randomValue - 0.5f ) * deepness + 0.5f;
		value = value * ( 1 - weight ) + randomValue * weight;
		count++;
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

	Texture2D mapTexture;
	void OnValidate()
	{
		sizeX = sizeY = ( 1 << size ) + 1;
		Fill();
		mapTexture = AsTexture();
	}

	void OnGUI()
	{
		if ( mapTexture != null )
			GUI.DrawTexture( new Rect( 140, 140, 512, 512 ), mapTexture );
	}
}

