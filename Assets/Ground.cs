using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent( typeof( MeshFilter ), typeof( MeshRenderer ), typeof( MeshCollider ) )]
public class Ground : HiveObject
{
	public World world;
	public int width = 64, height = 64;
	public GroundNode[] nodes;
	public int layoutVersion = 1;
	public int meshVersion = 0;
	[JsonIgnore]
	public Mesh mesh;
	[JsonIgnore]
	public new MeshCollider collider;
	public static int maxArea = 10;
	public static List<Offset>[] areas = new List<Offset>[maxArea];
	[Range(0.0f, 1.0f)]
	public float sharpRendering = 0.5f;

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
		public int d;	// -1 means unknown
		public static Offset operator -( Offset o )
		{
			return new Offset( -o.x, -o.y, o.d );
		}
		public static Offset operator +( Offset a, Offset b )
		{
			return new Offset( a.x + b.x, a.y + b.y, -1 );
		}
	}

	void Start()
	{
		gameObject.name = "Ground";
		gameObject.layer = World.layerIndexPickable;
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

	public float n00x, n00y, n10x, n10y, n01x, n01y;

	public Ground Setup( World world, int seed, int width = 64, int height = 64 )
	{
		this.world = world;
		gameObject.name = "Ground";
		this.width = width;
		this.height = height;

		if ( nodes == null )
			nodes = new GroundNode[( width + 1 ) * ( height + 1 )];
		for ( int x = 0; x <= width; x++ )
			for ( int y = 0; y <= height; y++ )
				nodes[y * ( width + 1 ) + x] = GroundNode.Create().Setup( this, x, y );
		GenerateHeights();

		n00x = GetNode( 0, 0 ).Position.x;
		n00y = GetNode( 0, 0 ).Position.z;
		n01x = GetNode( 0, 1 ).Position.x;
		n01y = GetNode( 0, 1 ).Position.z;
		n10x = GetNode( 1, 0 ).Position.x;
		n10y = GetNode( 1, 0 ).Position.z;

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

	public void GenerateHeights()
	{
		bool reuse = true;
		var heightMapObject = GameObject.Find( "heightmap" );
		var heightMap = heightMapObject?.GetComponent<HeightMap>();

		if ( heightMap == null )
		{
			heightMap = HeightMap.Create();
			heightMap.Setup( 6, World.rnd.Next(), false, true );
			heightMap.randomness = 2;
			heightMap.adjustment = -0.3f;
			heightMap.Fill();
			reuse = false;
		};

		var forestMap = HeightMap.Create();
		forestMap.Setup( heightMap.size, World.rnd.Next() );
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
			n.transform.localPosition = n.Position;
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

	public float GetHeightAt( float x, float y )
	{
		float gridY = ( y - n00y ) / ( n01y - n00y );
		float offset = gridY * ( n01x - n00x );
		float gridX = ( x - offset - n00x ) / ( n10x - n00x );

		int gridXNode = (int)Math.Floor( gridX );
		int gridYNode = (int)Math.Floor( gridY );
		if ( gridXNode < 0 || gridYNode < 0 || gridXNode >= width || gridYNode >= height )
			return -1;
		float gridXFrac = gridX - gridXNode;
		float gridYFrac = gridY - gridYNode;

		if ( gridXFrac + gridYFrac < 1 )
		{
			float wx = gridXFrac;
			float wy = gridYFrac;
			float wxy = 1 - gridXFrac - gridYFrac;
			return GetNode( gridXNode, gridYNode ).height * wxy + GetNode( gridXNode + 1, gridYNode ).height * wx + GetNode( gridXNode, gridYNode + 1 ).height * wy;
		}
		else
		{
			float wx = 1 - gridXFrac;
			float wy = 1 - gridYFrac;
			float wxy = gridXFrac + gridYFrac - 1;
			return GetNode( gridXNode + 1, gridYNode + 1 ).height * wxy + GetNode( gridXNode, gridYNode + 1 ).height * wx + GetNode( gridXNode + 1, gridYNode ).height * wy;
		}
	}

	void UpdateMesh()
	{
		if ( mesh == null )
			return;

		var positions = new List<Vector3>();
		var colors = new List<Color>();

		// Each ground triangle is rendered using 13 smaller triangles which all lie in the same plane (the plane of the original ground triangle between three nodes), to make it possible 
		// to control the smoothness of the rendering. The surface normal at the vertices will change, that makes the 13 triangles look different than a single triangle in the same area.
		// There is one triangle in the middle, which is totally flat (has the same surface normals at the three vertices) and 12 other triangles around it to make the transition to the 
		// adjacent ground triangles smooth.

		// This first block is simply creating a vertex for each node.
		for ( int i = 0; i < ( width + 1 ) * ( height + 1 ); i++ )
		{
			positions.Add( nodes[i].Position );
			switch ( nodes[i].type )
			{
				case GroundNode.Type.grass:
				case GroundNode.Type.underWater:
				{
					colors.Add( new Color( 1, 0, 0, 0 ) );
					break;
				}
				case GroundNode.Type.hill:
				{
					colors.Add( new Color( 0, 1, 0, 0 ) );
					break;
				}
				case GroundNode.Type.mountain:
				{
					colors.Add( new Color( 0, 0, 1, 0 ) );
					break;
				}
				case GroundNode.Type.forest:
				{
					colors.Add( new Color( 0, 0, 0, 1 ) );
					break;
				}
			}
		}

		// The following three blocks are adding two vertices for each edge between nodes. The distance of these vertices from the nodes depend on the sharpRendering property.
		// Note that the number of edges is different in all the three directions.
		int horizontalStart = positions.Count;
		for ( int y = 0; y <= height; y++ )
		{
			for ( int x = 0; x < width; x++ )
			{
				int a = y * ( width + 1 ) + x;
				AddVertex( a, a + 1, ( 1 + sharpRendering ) / 2 );
				AddVertex( a, a + 1, ( 1 - sharpRendering ) / 2 );
			}
		}

		int verticalStart = positions.Count;
		for ( int y = 0; y < height; y++ )
		{
			for ( int x = 0; x <= width; x++ )
			{
				int a = y * ( width + 1 ) + x;
				AddVertex( a, a + height + 1, ( 1 + sharpRendering ) / 2 );
				AddVertex( a, a + height + 1, ( 1 - sharpRendering ) / 2 );
			}
		}

		int diagonalStart = positions.Count;
		for ( int y = 0; y < height; y++ )
		{
			for ( int x = 0; x < width; x++ )
			{
				int a = y * ( width + 1 ) + x;
				AddVertex( a + height + 1, a + 1, ( 1 + sharpRendering ) / 2 );
				AddVertex( a + height + 1, a + 1, ( 1 - sharpRendering ) / 2 );
			}
		}

		// This block creates the actual triangles, one iteration is covering the area between four nodes, two ground triangles.
		var triangles = new List<int>();
		for ( int x = 0; x < width; x++ )
		{
			for ( int y = 0; y < height; y++ )
			{
				int hi = ( x + y * width ) * 2;
				int vi = ( x + y * ( width + 1 ) ) * 2;
				int di = ( x + y * width ) * 2;
				CoverTriangle(
					( y + 0 ) * ( width + 1 ) + ( x + 0 ),
					( y + 1 ) * ( width + 1 ) + ( x + 0 ),
					( y + 0 ) * ( width + 1 ) + ( x + 1 ),
					verticalStart + vi, verticalStart + vi + 1,
					diagonalStart + di, diagonalStart + di + 1,
					horizontalStart + hi + 1, horizontalStart + hi );
				CoverTriangle(
					( y + 0 ) * ( width + 1 ) + ( x + 1 ),
					( y + 1 ) * ( width + 1 ) + ( x + 0 ),
					( y + 1 ) * ( width + 1 ) + ( x + 1 ),
					diagonalStart + di + 1, diagonalStart + di,
					horizontalStart + width * 2 + hi, horizontalStart + width * 2 + hi + 1,
					verticalStart + 2 + vi + 1, verticalStart + 2 + vi );
			}
		}		

		mesh.vertices = positions.ToArray();
		mesh.colors = colors.ToArray();
		mesh.triangles = triangles.ToArray();

		mesh.RecalculateNormals();
		collider.sharedMesh = mesh;

		// This function adds two new vertices at an edge between two nodes. The weight is depending on the sharpRendering property.
		void AddVertex( int a, int b, float weight )
		{
			positions.Add( positions[a] * weight + positions[b] * ( 1 - weight ) );
			colors.Add( colors[a] * weight + colors[b] * ( 1 - weight ) );
		}

		// This function is covering the area of a ground triangle by creating 13 smaller triangles
		void CoverTriangle(
			int a, int b, int c,	// These three are the vertex indices of the original three corners of the trianle, they are coming from nodes
			int ar, int bl,			// These additional lines are the indices of vertices at the edges between original nodes.
			int br, int cl,
			int cr, int al )
		{
			// First three new vertices are created inside the gound triangle. The surface normal of these new vertices will be the same as the surface normal of the big triangle
			float mainWeight = ( sharpRendering * 2 + 1 ) / 3;
			float otherWeight = ( 1 - sharpRendering ) / 3;

			var ai = positions.Count;
			positions.Add( positions[a] * mainWeight + ( positions[b] + positions[c] ) * otherWeight );
			colors.Add( colors[a] * mainWeight + ( colors[b] + colors[c] ) * otherWeight );

			var bi = positions.Count;
			positions.Add( positions[b] * mainWeight + ( positions[a] + positions[c] ) * otherWeight );
			colors.Add( colors[b] * mainWeight + ( colors[a] + colors[c] ) * otherWeight );

			var ci = positions.Count;
			positions.Add( positions[c] * mainWeight + ( positions[a] + positions[b] ) * otherWeight );
			colors.Add( colors[c] * mainWeight + ( colors[a] + colors[b] ) * otherWeight );

			// First create the inner triangle, which is the flat part of the whole ground rendering, the normals at the three corners are the same
			AddTriangle( ai, bi, ci );

			// And now create the additional 12 triangles on the perimeter, these will be smooth and handling the transition to other ground triangles. 
			// They are added in three groups, 4 triangle for every side of the triangle inside
			AddTriangle( a, ar, ai );
			AddTriangle( ar, bi, ai );
			AddTriangle( ar, bl, bi );
			AddTriangle( bl, b, bi );

			AddTriangle( b, br, bi );
			AddTriangle( br, ci, bi );
			AddTriangle( br, cl, ci );
			AddTriangle( cl, c, ci );

			AddTriangle( c, cr, ci );
			AddTriangle( cr, ai, ci );
			AddTriangle( cr, al, ai );
			AddTriangle( al, a, ai );

			void AddTriangle( int a, int b, int c )
			{
				triangles.Add( a );
				triangles.Add( b );
				triangles.Add( c );
			}
		}
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
				List<GroundNode> touched = new List<GroundNode>
				{
					building.node
				};
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

	[System.Serializable]
	public class Area
	{
		public GroundNode center;
		public int radius = 8;
		public static Area global = new Area();

		public Area()
		{
		}

		public Area( GroundNode center, int radius )
		{
			this.center = center;
			this.radius = radius;
		}

		public bool IsInside( GroundNode node )
		{
			if ( center == null )
				return true;

			return center.DistanceFrom( node ) <= radius;
		}
	}

	private void OnValidate()
	{
		UpdateMesh();
	}

	public override void Reset()
	{
		foreach ( var node in nodes )
			node.Reset();
	}

	public override GroundNode Node { get { return null; } }

	override public void Validate()
 	{
        assert.IsTrue( width > 0 && height > 0, "Map size is not correct (" + width + ", " + height );
        assert.AreEqual( ( width + 1 ) * ( height + 1 ), nodes.Length, "Map layout size is incorrect" );
        foreach ( var node in nodes )
            node.Validate();
    }
}