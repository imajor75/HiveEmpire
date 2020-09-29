using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[RequireComponent( typeof( MeshFilter ), typeof( MeshRenderer ), typeof( MeshCollider ) )]
public class Ground : MonoBehaviour
{
	public World world;
	public int width = 50, height = 50;
	public GroundNode[] nodes;
	public List<Building> influencers = new List<Building>();
	public int layoutVersion = 1;
	public int meshVersion = 0;
	[JsonIgnore]
	public Mesh mesh;
	[JsonIgnore]
	public new MeshCollider collider;
	public static int maxArea = 10;
	public static float maxHeight = 20;
	public static List<Offset>[] areas = new List<Offset>[maxArea];
	public static float waterLevel = 0.40f;
	public static float hillLevel = 0.55f;
	public static float mountainLevel = 0.6f;
	[JsonIgnore]
	public GameObject water;
	int reservedCount, reservationCount;
	public GameObject resources;
	public GameObject buoys;

	public static float forestChance = 0.004f;
	public static float rocksChance = 0.002f;
	public static float animalSpawnerChance = 0.001f;
	public static float ironChance = 0.04f;	
	public static float coalChance = 0.04f;
	public static float stoneChance = 0.02f;
	public static float saltChance = 0.02f;
	public static float goldChance = 0.02f;

	public HeightMap heightMap;

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
	}

	void Start()
	{
		if ( world.zero == null )
			world.zero = Worker.zero;
		else
			Worker.zero = world.zero;
		gameObject.name = "Ground";

		MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();
		collider = gameObject.GetComponent<MeshCollider>();
		mesh = meshFilter.mesh = new Mesh();
		mesh.name = "GroundMesh";
		GetComponent<MeshRenderer>().material = Resources.Load<Material>( "GroundMaterial" );

		mesh = meshFilter.mesh = new Mesh();
		mesh.name = "GroundMesh";

		water = GameObject.CreatePrimitive( PrimitiveType.Plane );
		water.transform.SetParent( transform );
		water.GetComponent<MeshRenderer>().material = Resources.Load<Material>( "Water" );
		water.name = "Water";
		water.transform.localPosition = Vector3.up * waterLevel * maxHeight ;
		water.transform.localScale = Vector3.one * Math.Max( width, height ) * GroundNode.size;
	}

	public GameObject ResourcesGameObject()
	{
		if ( resources == null )
		{
			resources = new GameObject();
			resources.transform.SetParent( transform );
			resources.name = "Resources";
		}
		return resources;
	}

	public GameObject BuoysGameObject()
	{
		if ( buoys == null )
		{
			buoys = new GameObject();
			buoys.transform.SetParent( transform );
			buoys.name = "Buoys";
		}
		return buoys;
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
		FinishLayout();
		SetHeights();
		CreateAreas();
		CreateMainBuilding();
		GenerateResources();
		return this;
    }

	void CreateMainBuilding()
	{
		Player mainPlayer = world.mainPlayer;

		GroundNode center = GetNode( width/2, height/2 ), best = null;
		float heightdDif = float.MaxValue;
		foreach ( var o in areas[8] )
		{
			GroundNode node = center.Add( o );
			if ( node.type != GroundNode.Type.grass )
				continue;
			float min, max;
			min = max = node.height;
			for ( int i = 0; i < GroundNode.neighbourCount; i++ )
			{
				if ( node.Neighbour( i ).type != GroundNode.Type.grass )
				{
					max = float.MaxValue;
					break;
				}
				float height = node.Neighbour( i ).height;
				if ( height < min )
					min = height;
				if ( height > max )
					max = height;
			}
			if ( max - min < heightdDif )
			{
				best = node;
				heightdDif = max - min;
			}
		}

		Assert.IsNull( world.mainBuilding );
		world.mainBuilding = Stock.Create();
		world.mainBuilding.SetupMain( this, best, mainPlayer );
		world.eye.FocusOn( world.mainBuilding.node );
	}

	void CreateAreas()
	{
		for ( int i = 0; i < areas.Length; i++ )
			areas[i] = new List<Offset>();

		GroundNode center = GetNode( width/2, height/2 );
		for ( int x = -maxArea; x < maxArea; x++ )
		{
			for ( int y = -maxArea; y < maxArea; y++ )
			{
				if ( x == 0 && y == 0 )
					continue;
				int distance = GetNode( width / 2 + x, height / 2 + y ).DistanceFrom( center );
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
			Assert.AreEqual( areas[i].Count, nodeCount );
			nodeCount += ( i + 1 ) * 6;
		}
	}

	public void GenerateResources()
	{
		foreach ( var node in nodes )
		{
			var r = new System.Random( World.rnd.Next() );
			if ( r.NextDouble() < forestChance )
				node.AddResourcePatch( Resource.Type.tree, 7, 0.5f );
			if ( r.NextDouble() < rocksChance )
				node.AddResourcePatch( Resource.Type.rock, 5, 0.5f );
			if ( r.NextDouble() < animalSpawnerChance )
				node.AddResource( Resource.Type.animalSpawner );
			if ( r.NextDouble() < ironChance )
				node.AddResourcePatch( Resource.Type.iron, 5, 10 );
			if ( r.NextDouble() < coalChance )
				node.AddResourcePatch( Resource.Type.coal, 5, 10 );
			if ( r.NextDouble() < stoneChance )
				node.AddResourcePatch( Resource.Type.stone, 3, 10 );
			if ( r.NextDouble() < saltChance )
				node.AddResourcePatch( Resource.Type.salt, 3, 10 );
			if ( r.NextDouble() < goldChance )
				node.AddResourcePatch( Resource.Type.gold, 3, 10 );
		}
	}

	public void FinishLayout()
	{
		for ( int x = 0; x <= width; x++ )
		{
			for ( int y = 0; y <= height; y++ )
			{
				if ( nodes[y * ( width + 1 ) + x] == null )
					nodes[y * ( width + 1 ) + x] = new GroundNode();
			}
		}
		for ( int x = 0; x <= width; x++ )
			for ( int y = 0; y <= height; y++ )
				GetNode( x, y ).Initialize( this, x, y );
	}

	public void SetHeights()
	{
		heightMap = ScriptableObject.CreateInstance<HeightMap>();
		heightMap.Setup( 6, World.rnd.Next() );
		heightMap.Fill();

		{
			var mapTexture = new Texture2D( 64, 64 );
			for ( int x = 0; x < 64; x++ )
			{
				for ( int y = 0; y < 64; y++ )
				{
					float h = heightMap.data[x, y];
					mapTexture.SetPixel( x, y, new Color( h, h, h ) );
				}
			}

			mapTexture.Apply();
		}

		foreach ( var n in nodes )
		{
			float d = (float)heightMap.data[n.x, n.y];
			n.height = d*maxHeight;
			if ( d > hillLevel )
				n.type = GroundNode.Type.hill;
			if ( d > mountainLevel )
				n.type = GroundNode.Type.mountain;
			if ( d < waterLevel )
				n.type = GroundNode.Type.underWater;
			n.gizmo.transform.localPosition = n.Position();
		}
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
						colors[i] = Color.red;
						break;
					}
					case GroundNode.Type.hill:
					{
						colors[i] = Color.green;
						break;
					}
					case GroundNode.Type.mountain:
					{
						colors[i] = Color.blue;
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

	public void RegisterInfluence( Building building )
	{
		influencers.Add( building );
		RecalculateOwnership();
	}

	public void UnregisterInfuence( Building building )
	{
		influencers.Remove( building );
		RecalculateOwnership();
	}

	void RecalculateOwnership()
	{
		foreach ( var n in nodes )
		{
			n.owner = null;
			n.influence = 0;
		}

		foreach ( var building in influencers )
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
					if ( node.owner != null )
						node.borders[j] = BorderEdge.Create().Setup( node, j );
				}
			}
		}
	}

	void OnGUI()
	{
		if ( heightMap?.mapTexture != null )
			GUI.DrawTexture( new Rect( 0, 0, 512, 512 ), heightMap.mapTexture );
	}

	public void Validate()
 	{
		reservationCount = reservedCount = 0;
        Assert.IsTrue( width > 0 && height > 0, "Map size is not correct (" + width + ", " + height );
        Assert.AreEqual( ( width + 1 ) * ( height + 1 ), nodes.Length, "Map layout size is incorrect" );
        foreach ( var node in nodes )
            node.Validate();
		Assert.AreEqual( reservedCount, reservationCount, "Reservation numbers are wrong" );
    }
}