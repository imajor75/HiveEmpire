﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;

public class Ground : HiveObject
{
	public int dimension;
	public List<Node> nodes;
	public static List<Offset>[] areas = new List<Offset>[Constants.Ground.maxArea];
	[Range(0.0f, 1.0f)]
	public float sharpRendering = Constants.Ground.defaultSharpRendering;
	public List<Block> blocks = new ();
	public Grass grass;
	public int gameTimeID;
	public Material material;
	public Texture2D mapGroundTexture;
	public MeshRenderer mapGround;
	public Node centerNode { get { return nodes[dimension*dimension/2+dimension/2]; } }
	override public UpdateStage updateMode => UpdateStage.turtle;


	[Obsolete( "Compatibility with old files", true )]
	List<GrassBlock> grassBlocks { set {} }
	[Obsolete( "Compatibility with old files", true )]
	List<Matrix4x4> grassMatrices { set {} }
	[Obsolete( "Compatibility with old files", true )]
	int width { set { if ( dimension == 0 ) dimension = value; assert.AreEqual( dimension, value ); } }
	[Obsolete( "Compatibility with old files", true )]
	int height { set { if ( dimension == 0 ) dimension = value; assert.AreEqual( dimension, value ); } }
	[Obsolete( "Compatibility with old files", true )]
	int overseas { set {} }
	[Obsolete( "Compatibility with old files", true )]
	int layoutVersion, meshVersion;
	public static Ground Create()
	{
		return new GameObject().AddComponent<Ground>();
	}

	public bool dirtyOwnership;

	public class GrassBlock
	{
		public Block block;
		public float depth;
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
		public static implicit operator bool( Offset offset )
		{
			return offset.x != 0 || offset.y != 0;
		}
		public Offset Normalize()
		{
			while ( x > HiveCommon.ground.dimension / 2 )
				x -= HiveCommon.ground.dimension;
			while ( x < -HiveCommon.ground.dimension / 2 )
				x += HiveCommon.ground.dimension;
			while ( y > HiveCommon.ground.dimension / 2 )
				y -= HiveCommon.ground.dimension;
			while ( y < -HiveCommon.ground.dimension / 2 )
				y += HiveCommon.ground.dimension;
			return this;
		}
	}

	new public void Start()
	{
		PrepareRendering();
		base.Start();
	}

	void PrepareRendering()
	{
		if ( transform.parent )
			return;

		gameObject.name = "Ground";
		transform.SetParent( world.transform, false );
		material = Resources.Load<Material>( "GroundMaterial" );

		grassLayerCount = Constants.Ground.grassLevels;

		mapGround = new GameObject( "Map Ground" ).AddComponent<MeshRenderer>();
		var meshFilter = mapGround.gameObject.AddComponent<MeshFilter>();
		var mesh = meshFilter.mesh = new Mesh();

		var verts = new Vector3[4];
		var node = GetNode( 0, 0 );
		verts[0] = node.GetPosition( 0, 0 ) * 3;
		verts[1] = node.GetPosition( dimension, 0 ) * 3;
		verts[2] = node.GetPosition( 0, dimension ) * 3;
		verts[3] = node.GetPosition( dimension, dimension ) * 3;
		verts[0].y = verts[1].y = verts[2].y = verts[3].y = 0;
		mesh.vertices = verts;

		var tris = new int[6];
		tris[0] = 0;
		tris[1] = 2;
		tris[2] = 1;
		tris[3] = 1;
		tris[4] = 2;
		tris[5] = 3;
		mesh.triangles = tris;

		var tcs = new Vector2[4];
		tcs[0].Set( 0, 0 );
		tcs[1].Set( 3, 0 );
		tcs[2].Set( 0, 3 );
		tcs[3].Set( 3, 3 );
		mesh.uv = tcs;

		mapGround.gameObject.layer = Constants.World.layerIndexMap;
		mapGround.transform.SetParent( transform, false );
		mapGround.material = new Material( World.defaultTextureShader );
		RecreateMapGroundTexture();

		RenderSettings.fog = true;
		RenderSettings.fogMode = FogMode.Linear;
		RenderSettings.fogColor = Constants.Eye.fogColor;
		RenderSettings.fogStartDistance = dimension * Constants.Node.size * Constants.Eye.fogDistance;
		RenderSettings.fogEndDistance = dimension * Constants.Node.size * Constants.Eye.clipDistance;
		RenderSettings.skybox = new Material( World.defaultTextureShader );
		RenderSettings.skybox.mainTexture = Resources.Load<Texture>( "textures/skybox" );

		if ( grass == null )
			grass = Grass.Create().Setup();
	}

	override public void Remove()
	{
		foreach ( var node in nodes )
			node.Remove();
		foreach ( var block in blocks )
			block.Remove();
		base.Remove();
	}
	
	[JsonIgnore]
	public int grassLayerCount;

	static public void Initialize()
	{
		CreateAreas();
	}

	void CreateBlocks()
	{
		foreach ( var block in blocks )
			Eradicate( block.gameObject );
		blocks.Clear();

		for ( int x = dimension / Constants.Ground.blockCount / 2; x < dimension; x += dimension / Constants.Ground.blockCount )
			for ( int y = dimension / Constants.Ground.blockCount / 2; y < dimension; y += dimension / Constants.Ground.blockCount )
				blocks.Add( Block.Create().Setup( this, GetNode( x, y ), dimension / Constants.Ground.blockCount ) );
	}

	public float n00x, n00y, n10x, n10y, n01x, n01y;

	public Ground Setup( World world, HeightMap heightMap, HeightMap forestMap, System.Random rnd, int dimension = 64 )
	{
		gameObject.name = "Ground";
		this.world = world;
		this.dimension = dimension;

		if ( nodes == null )
			nodes = new ();
		for ( int y = 0; y < dimension; y++ )
			for ( int x = 0; x < dimension; x++ )
				nodes.Add( Node.Create().Setup( this, x, y, rnd ) );
		ScanHeights( heightMap, forestMap );

		n00x = GetNode( 0, 0 ).position.x;
		n00y = GetNode( 0, 0 ).position.z;
		n01x = GetNode( 0, 1 ).position.x;
		n01y = GetNode( 0, 1 ).position.z;
		n10x = GetNode( 1, 0 ).position.x;
		n10y = GetNode( 1, 0 ).position.z;

		PrepareRendering();
		CreateBlocks();
		grass = Grass.Create().Setup();
		base.Setup( world );

		return this;
    }

	public void Link( HiveObject hiveObject, Node location = null )
	{
		hiveObject.transform.SetParent( transform, false );
	}

	public override void GameLogicUpdate( UpdateStage stage )
	{
		if ( dirtyOwnership )
			RecalculateOwnership();
	}

	static void CreateAreas()
	{
		for ( int i = 0; i < areas.Length; i++ )
			areas[i] = new ();

		for ( int x = -Constants.Ground.maxArea; x < Constants.Ground.maxArea; x++ )
		{
			for ( int y = -Constants.Ground.maxArea; y < Constants.Ground.maxArea; y++ )
			{
				int h = Mathf.Abs( x );
				int v = Mathf.Abs( y );
				int d = Mathf.Abs( x + y );

				int distance = Mathf.Max( h, Mathf.Max( v, d ) );
				for ( int j = 0; j < Constants.Ground.maxArea; j++ )
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
		int nodeCount = 1;
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
			n.height = d * game.generatorSettings.maxHeight;
			n.type = Node.Type.grass;
			float forestData = forestMap.data[(int)( xf * n.x ), (int)( yf * n.y )];
			forestData = forestData - forestMap.averageValue + 0.5f;
			if ( forestData < game.generatorSettings.forestGroundChance )
				n.type = Node.Type.forest;
			if ( d > game.generatorSettings.hillLevel )
				n.type = Node.Type.hill;
			if ( d > game.generatorSettings.mountainLevel )
				n.type = Node.Type.mountain;
			if ( d < game.generatorSettings.waterLevel )
				n.type = Node.Type.underWater;
			n.transform.localPosition = n.position;
		}
	}

	public Node GetNode( int x, int y )
	{
		if ( dimension == 0 )
			return null;
		while ( x < 0 )
			x += dimension;
		while ( y < 0 )
			y += dimension;
		while ( x >= dimension )
			x -= dimension;
		while ( y >= dimension )
			y -= dimension;
		assert.IsTrue( x >= 0 && x < dimension && y >= 0 && y < dimension/*, $"Trying to get node {x}:{y}"*/ );	// Using a string here makes sense, but really slows down the running
		assert.IsTrue( nodes.Count > y * dimension + x );
		return nodes[y * dimension + x];
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

	public void RecalculateOwnership()
	{
		assert.AreEqual( this, game.ground );

		int previousPlayerNodeCount = 0, newPlayerNodeCount = 0;
		foreach ( var n in nodes )
		{
			if ( n.team == root.mainTeam )
				previousPlayerNodeCount++;
			n.team = null;
			n.influence = 0;
		}

		foreach ( var team in game.teams )
		{
			foreach ( var building in team.influencers )
			{
				List<Node> touched = new List<Node>	{ building.node	};
				for ( int i = 0; i < touched.Count; i++ )
				{
					int influence = building.Influence( touched[i] );
					if ( influence <= 0 )
						continue;
					if ( touched[i].influence < influence )
					{
						touched[i].influence = influence;
						touched[i].team = building.team;
					}
					for ( int j = 0; j < Constants.Node.neighbourCount; j++ )
					{
						Node neighbour = touched[i].Neighbour( j );
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
			if ( node.team == root.mainTeam )
				newPlayerNodeCount++;
			for ( int j = 0; j < Constants.Node.neighbourCount; j++ )
			{
				Node neighbour = node.Neighbour( j );
				if ( node.team == neighbour.team || node.team == null )
				{
					if ( node.borders[j] )
					{
						node.borders[j].Remove();
						node.borders[j] = null;
					}
				}
				else
				{
					if ( node.team != null && node.borders[j] == null )
						node.borders[j] = BorderEdge.Create().Setup( node, j );
				}
			}

			if ( node.building && node.building.team != node.team && node.building.team )
				node.building.Remove();
			if ( node.flag && node.flag.team != node.team && node.flag.team )
				node.flag.Remove();
			if ( node.road && node.road.team != node.team )
				node.road.Remove();
		}

		if ( newPlayerNodeCount < previousPlayerNodeCount && !game.lastAreaInfluencer.changedSide )
			root.mainTeam?.SendMessage( "You lost area due to this enemy building", game.lastAreaInfluencer );

		RecreateMapGroundTexture();
		dirtyOwnership = false;
	}

	public void RecreateMapGroundTexture()
	{
		if ( mapGroundTexture )
			Eradicate( mapGroundTexture );

		int size = 512;
		float pixelPerNode = (float)size / dimension;

		mapGroundTexture = new Texture2D( size, size );
		for ( int x = 0; x < mapGroundTexture.width; x++ )
		{
			for ( int y = 0; y < mapGroundTexture.height; y++ )
			{
				var color = Color.black;
				int baseX = (int)((float)x/size*dimension);
				int baseY = (int)((float)y/size*dimension);
				float fractionX = ( x - baseX * pixelPerNode ) / pixelPerNode;
				float fractionY = ( y - baseY * pixelPerNode ) / pixelPerNode;
				if ( fractionY * 2 < ( 1 - fractionX ) && fractionX * 2 < ( 1 - fractionY ) )
				{}
				else if ( ( 1 - fractionX ) * 2 < fractionY && ( 1 - fractionY ) * 2 < fractionX )
				{
					baseX++;
					baseY++;
				}
				else if ( fractionX < fractionY )
					baseY++;
				else
					baseX++;
				var node = GetNode( baseX, baseY );
				if ( node && node.team )
					color = node.team.color;
				mapGroundTexture.SetPixel( x, y, color );
			}
		}
		mapGroundTexture.Apply();
		if ( mapGround )
			mapGround.material.mainTexture = mapGroundTexture;
	}

	public class Grass : HiveCommon
	{
		public List<Block> blocks = new ();
		public static int gameTimeID;
		public static List<Material> materials = new ();
		static List<Matrix4x4> matrices = new ();	
		public static int layerIndex;

		public class Block
		{
			public Ground.Block block;
			public float depth;
		}

		public static void Initialize()
		{
			layerIndex = LayerMask.NameToLayer( "Grass" );

			var r = new System.Random( 0 );

			var shader = Resources.Load<Shader>( "shaders/Grass" );
			var texture = Resources.Load<Texture>( "icons/grass" );
			var windTexture = new Texture2D( 8, 8 );
			for ( int x = 0; x < 8; x++ )
			{
				for ( int y = 0; y < 8; y++ )
					windTexture.SetPixel( x, y, new Color( (float)r.NextDouble(), (float)r.NextDouble(), (float)r.NextDouble() ) );
			}
			windTexture.wrapMode = TextureWrapMode.Mirror;
			windTexture.Apply();

			var maskTexture = new Texture2D( Constants.Ground.grassMaskDimension, Constants.Ground.grassMaskDimension );
			maskTexture.filterMode = FilterMode.Trilinear;
			var grassMaskNull = new Color( 0, 0, 0, 0 );
			for ( int x = 0; x < Constants.Ground.grassMaskDimension; x++ )
			{
				for ( int y = 0; y < Constants.Ground.grassMaskDimension; y++ )
					maskTexture.SetPixel( x, y, grassMaskNull );
			}
			int grassCount = (int)( Constants.Ground.grassMaskDimension * Constants.Ground.grassMaskDimension * Constants.Ground.grassDensity );
			for ( int i = 0; i < grassCount; i++ )
				maskTexture.SetPixel( r.Next( Constants.Ground.grassMaskDimension ), r.Next( Constants.Ground.grassMaskDimension ), Color.white );
			maskTexture.Apply();

			Shader.SetGlobalTexture( "_Wind", windTexture );
			int materialsNeeded = Math.Max( Constants.Ground.blockCount * Constants.Ground.blockCount, Constants.Ground.grassLevels );
			for ( int i = 0; i < materialsNeeded; i++ )
			{
				var material = new Material( shader );
				material.SetTexture( "_Mask", maskTexture );
				material.SetTexture( "_Color", texture );
				material.renderQueue = 2450 + i;
				material.enableInstancing = true;
				materials.Add( material );
			}

			gameTimeID = Shader.PropertyToID( "_GameTime" );

			matrices.Clear();
			for ( int i = 0; i < Constants.Ground.grassLevels; i++ )
			{
				float offset = ( (float)i ) / Constants.Ground.grassLevels;
				matrices.Add( Matrix4x4.Translate( new Vector3( 0, offset * 0.15f, 0 ) ) );
			}			
		}

		public static Grass Create()
		{
			return new GameObject( "Grass" ).AddComponent<Grass>();
		}

		public Grass Setup()
		{
			blocks.Clear();
			foreach ( var block in ground.blocks )
				blocks.Add( new Block { block = block } );
			return this;
		}

		public void Start()
		{
			Shader.SetGlobalFloat( "_WorldScale", ground.dimension );
			transform.SetParent( ground.transform, false );
		}

		public void LateUpdate()
		{
			Shader.SetGlobalInt( gameTimeID, time );
			
			if ( !settings.grass )
				return;
			
			foreach ( var grass in blocks )
			{
				var a = eye.cameraGrid.center.transform.position;
				var b = grass.block.center.position;
				var d = a - b;
				float yDif = Math.Abs( d.z );
				float xDif = Math.Abs( d.x - d.z / 2 );
				grass.depth = xDif + yDif;
			}
			blocks.Sort( ( a, b ) => b.depth.CompareTo( a.depth ) );

			for ( int i = 0; i < blocks.Count; i++ )
			{
				// It seems that the Graphics.DrawMeshInstanced is not working in the standalone version. This feels like a unity bug which is reported
				// (https://unity3d.atlassian.net/servicedesk/customer/portal/2/IN-7570)
				// As a workaround the editor uses the Graphics.DrawMeshInstanced call, while the standalone version uses multiple Graphics.DrawMesh calls to
				// achieve the same effect at the cost of performance. Once the issue is solved both versions should use the Graphics.DrawMeshInstanced call.
	#if UNITY_EDITOR
				Graphics.DrawMeshInstanced( blocks[i].block.mesh, 0, materials[i], matrices, null, UnityEngine.Rendering.ShadowCastingMode.Off, true, layerIndex );
	#else
				for ( int j = 0; j < matrices.Count; j++ )
					Graphics.DrawMesh( blocks[i].block.mesh, matrices[j], materials[j], layerIndex, null, 0, null, false, true );
	#endif
			}
		}
	}

	[System.Serializable]
	public class Area
	{
		public Node center;
		public int radius = 8;
		public static Area global = new ();

		public static Area empty = new ();

		public Area()
		{
		}

		public Area( Node center, int radius )
		{
			this.center = center;
			this.radius = radius;
		}

		public bool IsInside( Node node )
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

	public void SetDirty( Node node )
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

	public override Node location { get { return null; } }

	override public void Validate( bool chain )
 	{
        assert.IsTrue( dimension > 0 && dimension > 0, "Map size is not correct (" + dimension + ", " + dimension );
        assert.AreEqual( dimension * dimension, nodes.Count, "Map layout size is incorrect" );

		if ( !chain )
			return;

        foreach ( var node in nodes )
            node.Validate( true );
    }

	public override void UnitCallback( Unit unit, float floatData, bool boolData )
	{
		if ( boolData || unit.node.fixedHeight == false )
			unit.node.SetHeight( floatData );
	}

	[RequireComponent( typeof( MeshFilter ), typeof( MeshRenderer ), typeof( MeshCollider ) )]
	public class Block : HiveObject
	{
		public Ground boss;
		public Node center;
		public int dimension;
		public Mesh mesh;
		public new MeshCollider collider;
		public int layoutVersion = 1, meshVersion = 0;

		override public UpdateStage updateMode => UpdateStage.none;

		public static Block Create()
		{
			return new GameObject( "Ground Block" ).AddComponent<Block>();
		}

		public Block Setup( Ground boss, Node center, int dimension )
		{
			this.boss = boss;
			this.center = center;
			this.dimension = dimension;
			base.Setup( boss.world );
			BuildMesh();
			return this;
		}

		new public void Start()
		{
			BuildMesh();
			base.Start();
		}

		public void BuildMesh()
		{
			if ( gameObject.layer == Constants.World.layerIndexGround )
				return;

			gameObject.layer = Constants.World.layerIndexGround;
			MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();
			collider = gameObject.GetComponent<MeshCollider>();
			mesh = meshFilter.mesh = new ();
			mesh.name = "GroundMesh";
			var renderer = GetComponent<MeshRenderer>();
			renderer.material = boss.material;
			renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

			transform.SetParent( boss.transform, false );
			name = $"Ground block {center.x}:{center.y}";
			UpdateMesh();
		}

		new void Update()
		{
			if ( layoutVersion != meshVersion || mesh.vertexCount == 0 )
			{
				UpdateMesh();
				meshVersion = layoutVersion;
			}
			base.Update();
		}

		public override Node location
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
			var uv = new List<Vector2>();
			var nodes = new List<Node>();
			int dimension = this.dimension + 2;
			Vector2 noGrass = new Vector2( 0, 0 ), allowGrass = new Vector2( 1, 0 );

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
					uv.Add( node.road == null && node.flag == null ? allowGrass : noGrass );
					nodes.Add( node );
					switch ( node.type )
					{
						case Node.Type.grass:
						case Node.Type.underWater:
						{
							colors.Add( new Color( 1, 0, 0, 0 ) );
							break;
						}
						case Node.Type.hill:
						{
							colors.Add( new Color( 0, 1, 0, 0 ) );
							break;
						}
						case Node.Type.mountain:
						{
							colors.Add( new Color( 0, 0, 1, 0 ) );
							break;
						}
						case Node.Type.forest:
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
					AddVertex( a, a + 1, ( 1 + sharpRendering ) / 2, nodes[a] );
					AddVertex( a, a + 1, ( 1 - sharpRendering ) / 2, nodes[a+1] );
				}
			}

			int verticalStart = positions.Count;
			for ( int y = 0; y < dimension; y++ )
			{
				for ( int x = 0; x <= dimension; x++ )
				{
					int a = y * ( dimension + 1 ) + x;
					AddVertex( a, a + dimension + 1, ( 1 + sharpRendering ) / 2, nodes[a] );
					AddVertex( a, a + dimension + 1, ( 1 - sharpRendering ) / 2, nodes[a + dimension + 1] );
				}
			}

			int diagonalStart = positions.Count;
			for ( int y = 0; y < dimension; y++ )
			{
				for ( int x = 0; x < dimension; x++ )
				{
					int a = y * ( dimension + 1 ) + x;
					AddVertex( a + dimension + 1, a + 1, ( 1 + sharpRendering ) / 2, nodes[a + dimension + 1] );
					AddVertex( a + dimension + 1, a + 1, ( 1 - sharpRendering ) / 2, nodes[a + 1] );
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

			// Disable grass unter water and at cornfields
			for ( int i = 0; i < positions.Count; i++ )
				if ( positions[i].y < game.waterLevel || nodes[i].avoidGrass )
					uv[i] = noGrass;

			mesh.vertices = positions.ToArray();
			mesh.colors = colors.ToArray();
			mesh.uv = uv.ToArray();
			mesh.triangles = triangles.ToArray();

			mesh.RecalculateNormals();
			mesh.triangles = triangles.GetRange( 0, this.dimension * this.dimension * 3 * 13 * 2 ).ToArray();
			collider.sharedMesh = mesh;

			// This function adds two new vertices at an edge between two nodes. The weight is depending on the sharpRendering property.
			void AddVertex( int a, int b, float weight, Node node )
			{
				positions.Add( positions[a] * weight + positions[b] * ( 1 - weight ) );
				colors.Add( colors[a] * weight + colors[b] * ( 1 - weight ) );
				bool roadCrossing = false;
				if ( nodes[a].road && !nodes[a].road.blueprintOnly )
				{
					if ( ( nodes[b].road == nodes[a].road && ( nodes[a].roadIndex == nodes[b].roadIndex + 1 || nodes[a].roadIndex == nodes[b].roadIndex - 1 ) ) || nodes[a].road.ends[0].node == nodes[b] || nodes[a].road.ends[1].node == nodes[b] )
						roadCrossing = true;
				}
				else if ( nodes[b].road && !nodes[b].road.blueprintOnly )
				{
					if ( nodes[b].road.ends[0].node == nodes[a] || nodes[b].road.ends[1].node == nodes[a] )
						roadCrossing = true;
				}
				if ( node.flag )
					roadCrossing = true;
 
				uv.Add( roadCrossing ? noGrass : allowGrass );
				nodes.Add( node );
			}

			// This function is covering the area of a ground triangle by creating 13 smaller triangles
			void CoverTriangle(
				int a, int b, int c,    // These three are the vertex indices of the original three corners of the trianle, they are coming from nodes
				int ar, int bl,         // These additional lines are the indices of vertices at the edges between original nodes.
				int br, int cl,
				int cr, int al )
			{
				// First three new vertices are created inside the ground triangle. The surface normal of these new vertices will be the same as the surface normal of the big triangle
				float mainWeight = ( sharpRendering * 2 + 1 ) / 3;
				float otherWeight = ( 1 - sharpRendering ) / 3;

				var ai = positions.Count;
				positions.Add( positions[a] * mainWeight + ( positions[b] + positions[c] ) * otherWeight );
				colors.Add( colors[a] * mainWeight + ( colors[b] + colors[c] ) * otherWeight );
				uv.Add( nodes[a].flag ? noGrass : allowGrass );
				nodes.Add( nodes[a] );

				var bi = positions.Count;
				positions.Add( positions[b] * mainWeight + ( positions[a] + positions[c] ) * otherWeight );
				colors.Add( colors[b] * mainWeight + ( colors[a] + colors[c] ) * otherWeight );
				uv.Add( nodes[b].flag ? noGrass : allowGrass );
				nodes.Add( nodes[b] );

				var ci = positions.Count;
				positions.Add( positions[c] * mainWeight + ( positions[a] + positions[b] ) * otherWeight );
				colors.Add( colors[c] * mainWeight + ( colors[a] + colors[b] ) * otherWeight );
				uv.Add( nodes[c].flag ? noGrass : allowGrass );
				nodes.Add( nodes[c] );

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
	}
}