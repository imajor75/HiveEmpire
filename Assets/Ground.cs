using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[RequireComponent( typeof( MeshFilter ), typeof( MeshRenderer ), typeof( MeshCollider ) )]
public class Ground : Assert.Base
{
	public World world;
	public int width = 50, height = 50;
	public GroundNode[] nodes;
	public int layoutVersion = 1;
	public int meshVersion = 0;
	[JsonIgnore]
	public Mesh mesh;
	[JsonIgnore]
	public new MeshCollider collider;
	public static int maxArea = 10;
	public static List<Offset>[] areas = new List<Offset>[maxArea];

	[JsonIgnore]
	public Material material;

	public static Ground Create()
	{
		var groundObject = new GameObject();
		return groundObject.AddComponent<Ground>();
	}

	public class Offset
	{
		public Offset( int x, int y, int d )
		{
			this.x = x;
			this.y = y;
			this.d = d;
		}
		public int x;
		public int y;
		public int d;
		public static Offset operator -( Offset o )
		{
			return new Offset( -o.x, -o.y, o.d );
		}
	}

	void Start()
	{
		gameObject.name = "Ground";
		transform.SetParent( World.instance.transform );

		MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();
		collider = gameObject.GetComponent<MeshCollider>();
		mesh = meshFilter.mesh = new Mesh();
		mesh.name = "GroundMesh";
		material = GetComponent<MeshRenderer>().material = Resources.Load<Material>( "GroundMaterial" );

		mesh = meshFilter.mesh = new Mesh();
		mesh.name = "GroundMesh";
	}

	static public void Initialize()
	{
		CreateAreas();
	}

	public Ground Setup( World world, int seed )
	{
		this.world = world;
		World.rnd = new System.Random( seed );
		gameObject.name = "Ground";
		width = 50;
		height = 50;

		if ( nodes == null )
			nodes = new GroundNode[( width + 1 ) * ( height + 1 )];
		for ( int x = 0; x <= width; x++ )
			for ( int y = 0; y <= height; y++ )
				nodes[y * ( width + 1 ) + x] = GroundNode.Create().Setup( this, x, y );
		SetHeights();
		return this;
    }

	static void CreateAreas()
	{
		for ( int i = 0; i < areas.Length; i++ )
			areas[i] = new List<Offset>();

		for ( int x = -maxArea; x < maxArea; x++ )
		{
			for ( int y = -maxArea; y < maxArea; y++ )
			{
				if ( x == 0 && y == 0 )
					continue;
				int h = Mathf.Abs( x );
				int v = Mathf.Abs( y );
				int d = Mathf.Abs( x + y );

				int distance = Mathf.Max( h, Mathf.Max( v, d ) );
				for ( int j = 1; j < maxArea; j++ )
				{
					if ( distance > j )
						continue;
					int i = 0;
					while ( i < areas[j].Count && areas[j][i].d < distance )
						i++;
					areas[j].Insert( i, new Offset( x, y, distance ) );
				}
			}
		}
		int nodeCount = 0;
		for ( int i = 0; i < areas.Length; i++ )
		{
			Assert.global.AreEqual( areas[i].Count, nodeCount );
			nodeCount += ( i + 1 ) * 6;
		}
	}

	public void SetHeights()
	{
		bool reuse = true;
		var heightMapObject = GameObject.Find( "heightmap" );
		var heightMap = heightMapObject?.GetComponent<HeightMap>();

		if ( heightMap == null )
		{
			heightMap = HeightMap.Create();
			heightMap.Setup( 6, World.rnd.Next(), false, true );
			heightMap.deepnessExp = 1.1f;
			heightMap.randomness = 0.3f;
			heightMap.adjustLow = 0.3f;
			heightMap.adjustHigh = 0.6f;
			heightMap.Fill();
			reuse = false;
		};

		var forestMap = HeightMap.Create();
		forestMap.Setup( heightMap.size, World.rnd.Next() );
		forestMap.adjustLow = forestMap.adjustHigh = 0;
		forestMap.deepnessExp = 2;
		forestMap.deepnessStart = 2;
		forestMap.Fill();

		float xf = (float)( heightMap.sizeX - 1 ) / width;
		float yf = (float)( heightMap.sizeY - 1 ) / height;

		foreach ( var n in nodes )
		{
			float d = heightMap.data[(int)( xf * n.x ), (int)( yf * n.y )];
			n.height = d * World.instance.maxHeight;
			n.type = GroundNode.Type.grass;
			float forestData = forestMap.data[(int)( xf * n.x ), (int)( yf * n.y )];
			forestData = forestData - forestMap.averageValue + 0.5f;
			if ( forestData < World.instance.forestGroundChance )
				n.type = GroundNode.Type.forest;
			if ( d > World.instance.hillLevel )
				n.type = GroundNode.Type.hill;
			if ( d > World.instance.mountainLevel )
				n.type = GroundNode.Type.mountain;
			if ( d < World.instance.waterLevel )
				n.type = GroundNode.Type.underWater;
			n.transform.localPosition = n.Position();
		}

#if DEBUG
		forestMap.SavePNG( "forest.png" );
#endif

		if ( !reuse )
			Destroy( heightMap.gameObject );
		Destroy( forestMap.gameObject );
	}

	void Update()
	{
		if ( layoutVersion != meshVersion || mesh.vertexCount == 0 )
		{
			UpdateMesh();
			meshVersion = layoutVersion;
		}
	}

	public GroundNode GetNode( int x, int y )
	{
		if ( x < 0 )
			x += width + 1;
		if ( y < 0 )
			y += height + 1;
		if ( x > width )
			x -= width + 1;
		if ( y > height )
			y -= height + 1;
		return nodes[y * ( width + 1 ) + x];
	}

	public void SetNode( int x, int y, GroundNode node )
	{
		if ( nodes == null )
			nodes = new GroundNode[( width + 1 ) * ( height + 1 )];

		nodes[y * ( width + 1 ) + x] = node;
	}

	void UpdateMesh()
	{
		if ( mesh == null )
			return;

		if ( mesh.vertices == null || mesh.vertices.Length == 0 )
		{
			var vertices = new Vector3[(width+1)*(height+1)];
			var uvs = new Vector2[(width+1)*(height+1)];
			var colors = new Color[(width+1)*(height+1)];

			for ( int i = 0; i < ( width + 1 ) * ( height + 1 ); i++ )
			{
				var p = nodes[i].Position();
				vertices[i] = p;
				uvs[i] = new Vector2( p.x, p.z );
				switch ( nodes[i].type )
				{
					case GroundNode.Type.grass:
					case GroundNode.Type.underWater:
					{
						colors[i] = new Color( 1, 0, 0, 0 );
						break;
					}
					case GroundNode.Type.hill:
					{
						colors[i] = new Color( 0, 1, 0, 0 );
						break;
					}
					case GroundNode.Type.mountain:
					{
						colors[i] = new Color( 0, 0, 1, 0 );
						break;
					}
					case GroundNode.Type.forest:
					{
						colors[i] = new Color( 0, 0, 0, 1 );
						break;
					}
				}
			}
			mesh.vertices = vertices;
			mesh.uv = uvs;
			mesh.colors = colors;

			var triangles = new int[width*height*2*3];
			for ( int x = 0; x < width; x++ )
			{
				for ( int y = 0; y < height; y++ )
				{
					var i = (y*width+x)*2*3;
					triangles[i + 0] = ( y + 0 ) * ( width + 1 ) + ( x + 0 );
					triangles[i + 1] = ( y + 1 ) * ( width + 1 ) + ( x + 0 );
					triangles[i + 2] = ( y + 0 ) * ( width + 1 ) + ( x + 1 );
					triangles[i + 3] = ( y + 0 ) * ( width + 1 ) + ( x + 1 );
					triangles[i + 4] = ( y + 1 ) * ( width + 1 ) + ( x + 0 );
					triangles[i + 5] = ( y + 1 ) * ( width + 1 ) + ( x + 1 );
				}
			}
			mesh.triangles = triangles;
			mesh.RecalculateNormals();
			collider.sharedMesh = mesh;
		}
		else
		{
			var vertices = mesh.vertices;
			for ( int i = 0; i < ( width + 1 ) * ( height + 1 ); i++ )
				vertices[i] = nodes[i].Position();
			mesh.vertices = vertices;
			collider.sharedMesh = mesh;
		}
	}

	class InfluenceChange
	{
		GroundNode node;
		int newValue;
	}

	public GroundNode GetCenter()
	{
		return GetNode( width / 2, height / 2 );
	}

	public void RecalculateOwnership()
	{
		foreach ( var n in nodes )
		{
			n.owner = null;
			n.influence = 0;
		}

		foreach ( var player in World.instance.players )
		{
			foreach ( var building in player.influencers )
			{
				List<GroundNode> touched = new List<GroundNode>();
				touched.Add( building.node );
				for ( int i = 0; i < touched.Count; i++ )
				{
					int influence = building.Influence( touched[i] );
					if ( influence <= 0 )
						continue;
					if ( touched[i].influence < influence )
					{
						touched[i].influence = influence;
						touched[i].owner = building.owner;
					}
					for ( int j = 0; j < GroundNode.neighbourCount; j++ )
					{
						GroundNode neighbour = touched[i].Neighbour( j );
						if ( neighbour.index >= 0 && neighbour.index < touched.Count && touched[neighbour.index] == neighbour )
							continue;
						neighbour.index = touched.Count;
						touched.Add( neighbour );
					}
				}
			}
		}

		foreach ( var node in nodes )
		{
			for ( int j = 0; j < GroundNode.neighbourCount; j++ )
			{
				GroundNode neighbour = node.Neighbour( j );
				if ( node.owner == neighbour.owner )
				{
					if ( node.borders[j] )
					{
						Destroy( node.borders[j].gameObject );
						node.borders[j] = null;
					}
				}
				else
				{
					if ( node.owner != null && node.borders[j] == null )
						node.borders[j] = BorderEdge.Create().Setup( node, j );
				}
			}
		}
	}

	public void Validate()
 	{
        assert.IsTrue( width > 0 && height > 0, "Map size is not correct (" + width + ", " + height );
        assert.AreEqual( ( width + 1 ) * ( height + 1 ), nodes.Length, "Map layout size is incorrect" );
        foreach ( var node in nodes )
            node.Validate();
    }
}