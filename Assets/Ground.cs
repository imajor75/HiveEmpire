using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;

public class Ground : HiveObject
{
	public World world;
	public int dimension;
	public GroundNode[] nodes;
	public static int maxArea = 10;
	public static List<Offset>[] areas = new List<Offset>[maxArea];
	[Range(0.0f, 1.0f)]
	public float sharpRendering = 0.5f;
	public List<Block> blocks = new List<Block>();
	public Material material;

	[Obsolete( "Compatibility with old files", true )]
	int width { set { if ( dimension == 0 ) dimension = value; assert.AreEqual( dimension, value ); } }
	[Obsolete( "Compatibility with old files", true )]
	int height { set { if ( dimension == 0 ) dimension = value; assert.AreEqual( dimension, value ); } }
	[Obsolete( "Compatibility with old files", true )]
	int overseas { set { world.overseas = value; } }
	[Obsolete( "Compatibility with old files", true )]
	int layoutVersion, meshVersion;

	public static Ground Create()
	{
		return new GameObject().AddComponent<Ground>();
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

	new public void Start()
	{
		gameObject.name = "Ground";
		transform.SetParent( World.instance.transform );
		material = Resources.Load<Material>( "GroundMaterial" );

		if ( blocks.Count == 0 )
			CreateBlocks();		// Compatibility with old files
		base.Start();
	}

	static public void Initialize()
	{
		CreateAreas();
	}

	void CreateBlocks()
	{
		foreach ( var block in blocks )
			Destroy( block );
		blocks.Clear();

		const int blockCount = 4;
		for ( int x = dimension / blockCount / 2; x < dimension; x += dimension / blockCount )
			for ( int y = dimension / blockCount / 2; y < dimension; y += dimension / blockCount )
				blocks.Add( Block.Create().Setup( this, GetNode( x, y ), dimension / blockCount ) );
	}

	public float n00x, n00y, n10x, n10y, n01x, n01y;

	public Ground Setup( World world, HeightMap heightMap, HeightMap forestMap, int dimension = 64 )
	{
		this.world = world;
		gameObject.name = "Ground";
		this.dimension = dimension;

		if ( nodes == null )
			nodes = new GroundNode[( dimension + 1 ) * ( dimension + 1 )];
		for ( int x = 0; x <= dimension; x++ )
			for ( int y = 0; y <= dimension; y++ )
				nodes[y * ( dimension + 1 ) + x] = GroundNode.Create().Setup( this, x, y );
		ScanHeights( heightMap, forestMap );

		n00x = GetNode( 0, 0 ).position.x;
		n00y = GetNode( 0, 0 ).position.z;
		n01x = GetNode( 0, 1 ).position.x;
		n01y = GetNode( 0, 1 ).position.z;
		n10x = GetNode( 1, 0 ).position.x;
		n10y = GetNode( 1, 0 ).position.z;

		CreateBlocks();

		return this;
    }

	public Block FindClosestBlock( GroundNode node )
	{
		foreach ( var block in blocks )
		{
			if ( node.x < block.center.x - block.dimension / 2 )
				continue;
			if ( node.x > block.center.x + block.dimension / 2 )
				continue;
			if ( node.y < block.center.y - block.dimension / 2 )
				continue;
			if ( node.y > block.center.y + block.dimension / 2 )
				continue;
			return block;
		}
		return null;
	}

	public void Link( HiveObject hiveObject, GroundNode location = null )
	{
		var bestBlock = FindClosestBlock( location ?? hiveObject.location );
		if ( bestBlock )
			hiveObject.transform.SetParent( bestBlock.transform, false );
		else
			hiveObject.transform.SetParent( transform, false );
	}

	public void LateUpdate()
	{
		var camera = Interface.root.viewport.visibleAreaCenter;
		foreach ( var block in blocks )
			block.UpdateOffset( camera );
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

	public void ScanHeights( HeightMap heightMap, HeightMap forestMap )
	{
		float xf = (float)( heightMap.sizeX - 1 ) / dimension;
		float yf = (float)( heightMap.sizeY - 1 ) / dimension;

		foreach ( var n in nodes )
		{
			float d = heightMap.data[(int)Math.Round( xf * n.x ), (int)Math.Round( yf * n.y )];
			n.height = d * World.instance.settings.maxHeight;
			n.type = GroundNode.Type.grass;
			float forestData = forestMap.data[(int)( xf * n.x ), (int)( yf * n.y )];
			forestData = forestData - forestMap.averageValue + 0.5f;
			if ( forestData < World.instance.settings.forestGroundChance )
				n.type = GroundNode.Type.forest;
			if ( d > World.instance.settings.hillLevel )
				n.type = GroundNode.Type.hill;
			if ( d > World.instance.settings.mountainLevel )
				n.type = GroundNode.Type.mountain;
			if ( d < World.instance.settings.waterLevel )
				n.type = GroundNode.Type.underWater;
			n.transform.localPosition = n.position;
		}
	}

	void Update()
	{
		var overseas = world.overseas;
		for ( int x = -overseas; x <= overseas; x++ )
		{
			for ( int y = -overseas; y <= overseas; y++ )
			{
				if ( x == 0 && y == 0 )
					continue;
				foreach ( var block in blocks )
					Graphics.DrawMesh( block.mesh, new Vector3( ( x + (float)y / 2 )* dimension * GroundNode.size, 0, y * dimension * GroundNode.size ) + block.transform.position, Quaternion.identity, material, 0 );
			}
		}
	}

	public GroundNode GetNode( int x, int y )
	{
		if ( x < 0 )
			x += dimension;
		if ( y < 0 )
			y += dimension;
		if ( x >= dimension )
			x -= dimension;
		if ( y >= dimension )
			y -= dimension;
		assert.IsTrue( x >= 0 && x <= dimension && y >= 0 && y <= dimension );
		return nodes[y * ( dimension + 1 ) + x];
	}

	public void SetNode( int x, int y, GroundNode node )
	{
		if ( nodes == null )
			nodes = new GroundNode[( dimension + 1 ) * ( dimension + 1 )];

		nodes[y * ( dimension + 1 ) + x] = node;
	}

	public float GetHeightAt( float x, float y )
	{
		float gridY = ( y - n00y ) / ( n01y - n00y );
		float offset = gridY * ( n01x - n00x );
		float gridX = ( x - offset - n00x ) / ( n10x - n00x );

		int gridXNode = (int)Math.Floor( gridX );
		int gridYNode = (int)Math.Floor( gridY );
		float gridXFrac = gridX - gridXNode;
		float gridYFrac = gridY - gridYNode;

		if ( gridXNode < 0 )
			gridXNode += dimension;
		if ( gridYNode < 0 )
			gridYNode += dimension;
		if ( gridXNode >= dimension )
			gridXNode -= dimension;
		if ( gridYNode >= dimension )
			gridYNode -= dimension;

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

	public GroundNode GetCenter()
	{
		return GetNode( dimension / 2, dimension / 2 );
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
						node.borders[j].DestroyThis();
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

	public override void Reset()
	{
		foreach ( var node in nodes )
			node.Reset();
	}

	public void SetDirty( GroundNode node )
	{
		foreach ( var block in blocks )
		{
			var xDif = Math.Abs( node.x - block.center.x );
			if ( xDif > dimension / 2 )
				xDif = dimension - xDif;
			if ( xDif > block.dimension / 2 + 2 )
				continue;

			var yDif = Math.Abs( node.y - block.center.y );
			if ( yDif > dimension / 2 )
				yDif = dimension - yDif;
			if ( yDif > block.dimension / 2 + 2 )
				continue;

			if ( yDif > block.dimension / 2 + 2 )
				continue;
			block.layoutVersion++;
		}
	}

	public override GroundNode location { get { return null; } }

	override public void Validate( bool chain )
 	{
        assert.IsTrue( dimension > 0 && dimension > 0, "Map size is not correct (" + dimension + ", " + dimension );
        assert.AreEqual( ( dimension + 1 ) * ( dimension + 1 ), nodes.Length, "Map layout size is incorrect" );

		if ( !chain )
			return;

        foreach ( var node in nodes )
            node.Validate( true );
    }

	[RequireComponent( typeof( MeshFilter ), typeof( MeshRenderer ), typeof( MeshCollider ) )]
	public class Block : HiveObject
	{
		public Ground boss;
		public GroundNode center;
		public int dimension;
		public Mesh mesh;
		public new MeshCollider collider;
		public int layoutVersion = 1, meshVersion = 0;

		public static Block Create()
		{
			return new GameObject().AddComponent<Block>();
		}

		public Block Setup( Ground boss, GroundNode center, int dimension )
		{
			this.boss = boss;
			this.center = center;
			this.dimension = dimension;
			return this;
		}

		new public void Start()
		{
			gameObject.layer = World.layerIndexPickable;
			MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();
			collider = gameObject.GetComponent<MeshCollider>();
			mesh = meshFilter.mesh = new Mesh();
			mesh.name = "GroundMesh";
			GetComponent<MeshRenderer>().material = boss.material;

			mesh = meshFilter.mesh = new Mesh();
			mesh.name = "GroundMesh";

			transform.SetParent( boss.transform );
			name = $"Ground block {center.x}:{center.y}";
			base.Start();
		}

		void Update()
		{
			if ( layoutVersion != meshVersion || mesh.vertexCount == 0 )
			{
				UpdateMesh();
				meshVersion = layoutVersion;
			}
		}

		public override GroundNode location
		{
			get
			{
				return center;
			}
		}

		void UpdateMesh()
		{
			var sharpRendering = boss.sharpRendering;

			if ( mesh == null )
				return;

			var positions = new List<Vector3>();
			var colors = new List<Color>();
			int dimension = this.dimension + 2;

			// Each ground triangle is rendered using 13 smaller triangles which all lie in the same plane (the plane of the original ground triangle between three nodes), to make it possible 
			// to control the smoothness of the rendering. The surface normal at the vertices will change, that makes the 13 triangles look different than a single triangle in the same area.
			// There is one triangle in the middle, which is totally flat (has the same surface normals at the three vertices) and 12 other triangles around it to make the transition to the 
			// adjacent ground triangles smooth.

			// This first block is simply creating a vertex for each node.

			for ( int y = -dimension / 2; y <= dimension / 2; y++ )
			{
				for ( int x = -dimension / 2; x <= dimension / 2; x++ )
				{
					var node = boss.GetNode( center.x + x, center.y + y );
					positions.Add( node.GetPosition( center.x + x, center.y + y ) );
					switch ( node.type )
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
			}

			// The following three blocks are adding two vertices for each edge between nodes. The distance of these vertices from the nodes depend on the sharpRendering property.
			// Note that the number of edges is different in all the three directions.
			int horizontalStart = positions.Count;
			for ( int y = 0; y <= dimension; y++ )
			{
				for ( int x = 0; x < dimension; x++ )
				{
					int a = y * ( dimension + 1 ) + x;
					AddVertex( a, a + 1, ( 1 + sharpRendering ) / 2 );
					AddVertex( a, a + 1, ( 1 - sharpRendering ) / 2 );
				}
			}

			int verticalStart = positions.Count;
			for ( int y = 0; y < dimension; y++ )
			{
				for ( int x = 0; x <= dimension; x++ )
				{
					int a = y * ( dimension + 1 ) + x;
					AddVertex( a, a + dimension + 1, ( 1 + sharpRendering ) / 2 );
					AddVertex( a, a + dimension + 1, ( 1 - sharpRendering ) / 2 );
				}
			}

			int diagonalStart = positions.Count;
			for ( int y = 0; y < dimension; y++ )
			{
				for ( int x = 0; x < dimension; x++ )
				{
					int a = y * ( dimension + 1 ) + x;
					AddVertex( a + dimension + 1, a + 1, ( 1 + sharpRendering ) / 2 );
					AddVertex( a + dimension + 1, a + 1, ( 1 - sharpRendering ) / 2 );
				}
			}

			// This block creates the actual triangles, one iteration is covering the area between four nodes, two ground triangles.
			var triangles = new List<int>();
			for ( int ring = 0; ring < dimension / 2; ring++ )
			{
				for ( int x = dimension / 2 - ring - 1; x <= dimension / 2 + ring; x++ )
				{
					for ( int y = dimension / 2 - ring - 1; y <= dimension / 2 + ring; y++ )
					{
						if ( ( x != dimension / 2 - ring - 1 && x != dimension / 2 + ring ) && ( y != dimension / 2 - ring - 1 && y != dimension / 2 + ring ) )
							continue;
						int hi = ( x + y * dimension ) * 2;
						int vi = ( x + y * ( dimension + 1 ) ) * 2;
						int di = ( x + y * dimension ) * 2;
						CoverTriangle(
							( y + 0 ) * ( dimension + 1 ) + ( x + 0 ),
							( y + 1 ) * ( dimension + 1 ) + ( x + 0 ),
							( y + 0 ) * ( dimension + 1 ) + ( x + 1 ),
							verticalStart + vi, verticalStart + vi + 1,
							diagonalStart + di, diagonalStart + di + 1,
							horizontalStart + hi + 1, horizontalStart + hi );
						CoverTriangle(
							( y + 0 ) * ( dimension + 1 ) + ( x + 1 ),
							( y + 1 ) * ( dimension + 1 ) + ( x + 0 ),
							( y + 1 ) * ( dimension + 1 ) + ( x + 1 ),
							diagonalStart + di + 1, diagonalStart + di,
							horizontalStart + dimension * 2 + hi, horizontalStart + dimension * 2 + hi + 1,
							verticalStart + 2 + vi + 1, verticalStart + 2 + vi );
					}
				}
			}

			assert.AreEqual( triangles.Count, 3 * 13 * dimension * dimension * 2 );

			mesh.vertices = positions.ToArray();
			mesh.colors = colors.ToArray();
			mesh.triangles = triangles.ToArray();

			mesh.RecalculateNormals();
			mesh.triangles = triangles.GetRange( 0, this.dimension * this.dimension * 3 * 13 * 2 ).ToArray();
			collider.sharedMesh = mesh;

			// This function adds two new vertices at an edge between two nodes. The weight is depending on the sharpRendering property.
			void AddVertex( int a, int b, float weight )
			{
				positions.Add( positions[a] * weight + positions[b] * ( 1 - weight ) );
				colors.Add( colors[a] * weight + colors[b] * ( 1 - weight ) );
			}

			// This function is covering the area of a ground triangle by creating 13 smaller triangles
			void CoverTriangle(
				int a, int b, int c,    // These three are the vertex indices of the original three corners of the trianle, they are coming from nodes
				int ar, int bl,         // These additional lines are the indices of vertices at the edges between original nodes.
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

		public void UpdateOffset( Vector3 camera )
		{
			var thisPosition = center.position;
			float limit = boss.dimension / 2 * GroundNode.size;
			int xo = 0, yo = 0;
			if ( camera.z - thisPosition.z > limit )
				yo = 1;
			if ( thisPosition.z - camera.z > limit )
				yo = -1;
			if ( camera.x - thisPosition.x - GroundNode.size * boss.dimension / 2 * yo > limit )
				xo = 1;
			if ( thisPosition.x - camera.x + GroundNode.size * boss.dimension / 2 * yo > limit )
				xo = -1;
			transform.localPosition = new Vector3( GroundNode.size * boss.dimension / 2 * ( xo * 2 + yo ), 0, GroundNode.size * boss.dimension * yo );
		}
	}
}