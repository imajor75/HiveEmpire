using System.Diagnostics;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

public class Road : MonoBehaviour
{
	public List<Worker> workers = new List<Worker>();
	public bool ready = false;
	public Ground ground;
	public List<GroundNode> nodes = new List<GroundNode>();
	public Worker[] workerAtNodes;
	public Mesh mesh;
	public static Material material;
	public int timeSinceWorkerAdded;
	public static int secBetweenWorkersAdded = 10;
	public static Road newRoad;
	public bool decorationOnly;

	public static void Initialize()
	{
		material = Resources.Load<Material>( "Road" );
	}

	public static Road Create()
	{
		var roadObject = new GameObject();
		roadObject.name = "Road";
		return (Road)roadObject.AddComponent( typeof( Road ) );
	}
	public Road SetupAsBuildingExit( Building building )
	{
		Assert.AreEqual( nodes.Count, 0 );
		decorationOnly = true;
		nodes.Add( building.node );
		nodes.Add( building.flag.node );
		ground = building.ground;
		return this;
	}

	public static bool AddNodeToNew( Ground ground, GroundNode node )
	{
		// Starting a new road
		if ( newRoad == null || newRoad.nodes.Count == 0 )
		{
			if ( node.flag )
			{
				if ( newRoad == null )
					newRoad = Create();
				newRoad.ground = ground;
				newRoad.nodes.Add( node );
				return true;
			}
			else
			{
				UnityEngine.Debug.Log( "Road must start at a flag" );
				return false;
			}
		}

		GroundNode last = newRoad.nodes[newRoad.nodes.Count - 1];
		// Special case, last node is the same as the current, remove one
		if ( last == node )
		{
			if ( newRoad.nodes.Count == 1 )
			{
				CancelNew();
				return true;
			}
			newRoad.nodes.RemoveAt( newRoad.nodes.Count - 1 );
			node.road = null;
			newRoad.RebuildMesh();
			return true;
		}

		// Check if the current node is already on a road
		if ( node.road )
		{
			UnityEngine.Debug.Log( "Roads cannot cross" );
			return false;
		}

		// Check if the current node has a building
		if ( node.building )
		{
			UnityEngine.Debug.Log( "Cannot build a road on a building" );
			return false;
		}

		// Check if the current node is adjacent to the previous one
		int direction = last.DirectionTo( node );
		if ( direction < 0 )
		{
			if ( node.flag )
			{
				// Find a path to the flag, and finish the road based on it
				var p = new PathFinder();
				if ( p.FindPathBetween( last, node, PathFinder.Mode.avoidRoads ) )
				{
					for ( int i = 1; i < p.path.Count; i++ )
						newRoad.AddNode( p.path[i] );
					newRoad.OnCreated();
					newRoad = null;
					return true;
				}
			}
			UnityEngine.Debug.Log( "Node must be adjacent to previous one" );
			return false;
		}
		bool finished = newRoad.AddNode( node );
		if ( finished )
		{
			newRoad.OnCreated();
			newRoad = null;
		}
		else
			newRoad.RebuildMesh();

		return true;
	}

	bool AddNode( GroundNode node )
	{
		nodes.Add( node );
		if ( newRoad.nodes.Count == 2 )
			newRoad.name = "Road " + node.x + ", " + node.y;

		// Finishing a road
		if ( node.flag )
		{
			nodes[0].flag.roadsStartingHere[nodes[0].DirectionTo( nodes[1] )] = this;
			node.flag.roadsStartingHere[node.DirectionTo( nodes[nodes.Count - 2] )] = this;
			ready = true;
			return true;
		}

		node.road = this;
		node.roadIndex = nodes.Count - 1;
		return false;
	}

	public Flag GetEnd( int side )
	{
		if ( side == 0 )
			return nodes[0].flag;
		return nodes[nodes.Count - 1].flag;
	}

	public static void CancelNew()
	{
		if ( newRoad )
		{
			newRoad.name = "Deleted";
			Destroy( newRoad );
			newRoad = null;
		}
	}

	void Start()
	{
		transform.SetParent( ground.transform, false );
		var renderer = gameObject.AddComponent<MeshRenderer>();
		renderer.material = material;
		var filter = gameObject.AddComponent<MeshFilter>();
		mesh = filter.mesh = new Mesh();
		RebuildMesh();
	}

	void Update()
	{
		if ( decorationOnly )
			return;
		if ( timeSinceWorkerAdded < secBetweenWorkersAdded * 50 )
			return;
		if ( workers.Count > nodes.Count / 2 )
			return;

		// TODO Refine when a new worker should be added
		if ( Jam() > 4 )
			CreateNewWorker();
	}

	void FixedUpdate()
	{
		timeSinceWorkerAdded++;
	}

	void RebuildMesh()
	{
		mesh.Clear();
		var l = nodes.Count-1;
		var vertices = new Vector3[l*6];
		var uvs = new Vector2[l*6];
		for ( int j = 0; j < l * 6; j++ )
			vertices[j] = new Vector3();
		for ( int i = 0; i < l; i++ )
		{
			var a = nodes[i].Position();
			var b = nodes[i+1].Position();
			var ab = b-a;
			Vector3 o = new Vector3( ab.z, 0, -ab.x );
			o.Normalize();
			o *= GroundNode.size / 5;
			Vector3 h = new Vector3( 0, GroundNode.size / 10, 0 );

			vertices[i * 6 + 0] = a + o;
			vertices[i * 6 + 1] = a + h;
			vertices[i * 6 + 2] = a - o;
			vertices[i * 6 + 3] = b + o;
			vertices[i * 6 + 4] = b + h;
			vertices[i * 6 + 5] = b - o;

			uvs[i * 6 + 0] = new Vector2( 0.0f, 0.0f );
			uvs[i * 6 + 1] = new Vector2( 0.5f, 0.0f );
			uvs[i * 6 + 2] = new Vector2( 1.0f, 0.0f );
			uvs[i * 6 + 3] = new Vector2( 0.0f, 1.0f );
			uvs[i * 6 + 4] = new Vector2( 0.5f, 1.0f );
			uvs[i * 6 + 5] = new Vector2( 1.0f, 1.0f );
		}
		mesh.vertices = vertices;
		mesh.uv = uvs;

		var triangles = new int[l*4*3];
		for ( int j = 0; j < l; j++ )
		{
			triangles[j * 4 * 3 + 00] = j * 6 + 0;
			triangles[j * 4 * 3 + 01] = j * 6 + 1;
			triangles[j * 4 * 3 + 02] = j * 6 + 3;

			triangles[j * 4 * 3 + 03] = j * 6 + 1;
			triangles[j * 4 * 3 + 04] = j * 6 + 4;
			triangles[j * 4 * 3 + 05] = j * 6 + 3;

			triangles[j * 4 * 3 + 06] = j * 6 + 1;
			triangles[j * 4 * 3 + 07] = j * 6 + 2;
			triangles[j * 4 * 3 + 08] = j * 6 + 4;

			triangles[j * 4 * 3 + 09] = j * 6 + 2;
			triangles[j * 4 * 3 + 10] = j * 6 + 5;
			triangles[j * 4 * 3 + 11] = j * 6 + 4;
		}
		mesh.triangles = triangles;
		mesh.RecalculateNormals();
	}

	public int NodeIndex( GroundNode node )
	{
		if ( node.flag )
		{
			if ( nodes[0] == node )
				return 0;
			if ( nodes[nodes.Count - 1] == node )
				return nodes.Count - 1;
			return -1;
		}
		if ( node.road != this )
			return -1;
		Assert.AreEqual( nodes[node.roadIndex], node );
		return node.roadIndex;
	}

	static public Road Between( GroundNode first, GroundNode second )
	{
		Assert.IsNotNull( first.flag );
		Assert.IsNotNull( second.flag );
		if ( first.flag == null || second.flag == null )
			return null;
		foreach ( var road in first.flag.roadsStartingHere )
		{
			if ( road == null )
				continue;
			var a = road.nodes[0];
			var b = road.nodes[road.nodes.Count-1];
			if ( a == first && b == second )
				return road;
			if ( a == second && b == first )
				return road;
		}
		return null;
	}

	public int Jam()
	{
		int itemCount = 0;
		for ( int e = 0; e < 2; e++ )
		{
			Flag flag = GetEnd( e );
			for ( int i = 0; i < Flag.maxItems; i++ )
			{
				Item t = flag.items[i];
				if ( t != null && t.path.Road() == this )
					itemCount++;
			};
		}
		return itemCount;
	}

	public void CreateNewWorker()
	{
		var worker = Worker.Create().SetupForRoad( this );
		if ( worker != null )
		{
			workers.Add( worker );
			timeSinceWorkerAdded = 0;
		}
	}

	public void OnCreated()
	{
		workerAtNodes = new Worker[nodes.Count];
		CreateNewWorker();
		RebuildMesh();
	}

	public void OnClicked()
	{
		RoadPanel.Open( this );
	}

	public void Remove()
	{
		while ( workers.Count > 0 )
			workers[0].Remove();
		for ( int i = 0; i < 2; i++ )
		{
			Flag flag = GetEnd( i );
			if ( flag == null )
				continue;

			for ( int j = 0; j < 6; j++ )
				if ( flag.roadsStartingHere[j] == this )
					flag.roadsStartingHere[j] = null;
		}
		foreach ( var node in nodes )
		{
			if ( node.road == this )
				node.road = null;
		}
		Destroy( gameObject );
	}

	public GroundNode CenterNode()
	{
		return nodes[nodes.Count / 2];
	}

	public void Validate()
	{
		int length = nodes.Count;
		Assert.IsTrue( length > 1 );
		if ( decorationOnly )
		{
			Assert.AreEqual( nodes.Count, 2 );
			Assert.IsNotNull( nodes[0].building );
			Assert.AreEqual( nodes[1].flag, nodes[0].building.flag );
			return;
		}
		var first = nodes[0];
		var last = nodes[length-1];
		Assert.IsNotNull( first.flag );
		Assert.IsNotNull( last.flag );
		Assert.AreEqual( this, first.flag.roadsStartingHere[first.DirectionTo( nodes[1] )] );
		Assert.AreEqual( this, last.flag.roadsStartingHere[last.DirectionTo( nodes[length - 2] )] );
		for ( int i = 1; i < length - 1; i++ )
			Assert.AreEqual( this, nodes[i].road );	// TODO This assert fired once
		for ( int i = 0; i < length - 1; i++ )
			Assert.IsTrue( nodes[i].DirectionTo( nodes[i + 1] ) >= 0 );
		foreach ( var worker in workers )
		{
			if ( !worker.atRoad )
				continue;
			int i = 0;
			foreach ( var w in workerAtNodes )
				if ( w == worker )
					i++;
			Assert.AreEqual( i, 1 );
			worker.Validate();
		}
	}
}
