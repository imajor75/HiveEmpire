using System.Diagnostics;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using System;
using Newtonsoft.Json;
using System.Linq;

[SelectionBase]
public class Road : HiveObject, Interface.IInputHandler
{
	public Player owner;
	public List<Worker> workers = new List<Worker>();
	public bool ready = false;
	public int tempNodes = 0;
	public Ground ground;
	public List<GroundNode> nodes = new List<GroundNode>();
	public List<Worker> workerAtNodes = new List<Worker>();
	public Flag[] ends = new Flag[2];
	[JsonIgnore]
	public Mesh mesh;
	public static Material material;
	public static int timeBetweenWorkersAdded = 3000;
	public World.Timer workerAdded;
	public bool decorationOnly;
	public static float height = 1.0f/20;
	[JsonIgnore]
	public float cost = 0;
	public float cachedCost = 0;
	public int targetWorkerCount;   // Zero means automaic
	public List<CubicCurve>[] curves = new List<CubicCurve>[3];
	public Watch watchStartFlag = new Watch(), watchEndFlag = new Watch();
	public GroundNode referenceLocation;
	[JsonIgnore]
	Material mapMaterial;
	[JsonIgnore]
	Mesh mapMesh;
	public bool invalid;

	[Obsolete( "Compatibility for old files", true )]
	int timeSinceWorkerAdded;

	public static void Initialize()
	{
		material = Resources.Load<Material>( "Road" );
	}

	public static Road Create()
	{
		return new GameObject().AddComponent<Road>();
	}

	public Road SetupAsBuildingExit( Building building, bool blueprintOnly )
	{
		owner = building.owner;
		this.blueprintOnly = blueprintOnly;
		assert.AreEqual( nodes.Count, 0 );
		decorationOnly = true;
		nodes.Add( building.node );
		nodes.Add( building.flag.node );
		ground = building.ground;
		return this;
	}

	public static bool IsNodeSuitable( Road road, GroundNode node, Player owner )
	{
		if ( road.owner != owner )
			return false;

		road.assert.IsFalse( road.ready );

		// Starting a new road
		if ( road == null || road.nodes.Count == 0 )
		{
			if ( !node.validFlag )
				return false;

			if ( node.flag.owner != road.owner )
				return false;
			return true;
		}

		if ( node.owner != road.owner )
			return false;

		// Check if the current node is adjacent to the previous one
		int direction = road.lastNode.DirectionTo( node );
		if ( direction < 0 )
			return false;

		// Check if the current node is blocking
		if ( node.IsBlocking() && node.flag == null )
			return false;

		return true;
	}

	public Road Setup( Flag flag )
	{
		this.ground = flag.node.ground;
		nodes.Add( flag.node );
		owner = flag.owner;
		return this;
	}

	public bool AddNode( GroundNode node, bool checkConditions = false )
	{
		assert.IsFalse( ready );
		if ( checkConditions && !IsNodeSuitable( this, node, owner ) )
			return false;
		nodes.Add( node );
		if ( nodes.Count == 2 )
			name = "Road " + node.x + ", " + node.y;

		if ( !node.IsBlocking( true ) )
		{
			node.road = this;
			node.roadIndex = nodes.Count - 1;
		}
		return true;
	}

	public bool RemoveLastNode()
	{
		var node = lastNode;
		if ( !node.validFlag )
		{
			if ( node.road == this )
				node.road = null;
		}
		assert.IsFalse( ready );
		if ( nodes.Count == 0 )
			return false;

		nodes.RemoveAt( nodes.Count - 1 );
		if ( nodes.Count == 0 )
		{
			DestroyThis();
			return false;
		}
		return true;
	}

	public bool Finish()
	{
		assert.IsFalse( ready );
		if ( !lastNode.validFlag || ( nodes.Count == 3 && nodes[0] == nodes[2] ) )
			return false;

#pragma warning disable IDE0059 // Unnecessary assignment of a value
		foreach ( var n in nodes )
#pragma warning restore IDE0059 // Unnecessary assignment of a value
			workerAtNodes.Add( null );
		CallNewWorker();
		transform.localPosition = CenterNode.position;
		ground.Link( this );
		referenceLocation = CenterNode;
		curves = new List<CubicCurve>[3];
		CreateCurves();
		RebuildMesh();
		AttachWatches();
		RegisterOnGround();
		ends[0] = nodes[0].flag;
		ends[1] = lastNode.flag;
		ready = true;
		return true;
	}

	GroundNode GetNodeFromEnd( int index )
	{
		return nodes[nodes.Count - 1 - index];
	}

	public Flag OtherEnd( Flag flag )
	{
		if ( ends[0] == flag )
			return ends[1];

		assert.AreEqual( ends[1], flag );
		return ends[0];
	}

	public void Start()
	{
		ground.Link( this );
		if ( nodes.Count > 0 )
		{
			transform.localPosition = CenterNode.position;
			referenceLocation = CenterNode;
		}
		if ( invalid )
			return;

		if ( nodes.Count > 1 )
			name = "Road " + nodes[1].x + ", " + nodes[1].y;
		else
			name = "Road";

		var renderer = gameObject.AddComponent<MeshRenderer>();
		renderer.material = material;
		renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
		var filter = gameObject.AddComponent<MeshFilter>();
		mesh = filter.mesh = new Mesh();

		GameObject mapObject = new GameObject( "Map" );
		mapMesh = mapObject.AddComponent<MeshFilter>().mesh = new Mesh();
		var r = mapObject.AddComponent<MeshRenderer>();
		mapMaterial = r.material = new Material( World.defaultMapShader );
		mapMaterial.renderQueue = 4000;
		r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
		mapObject.transform.SetParent( transform, false );
		World.SetLayerRecursive( mapObject, World.layerIndexMapOnly );
		World.SetRenderMode( mapMaterial, World.BlendMode.Transparent );

		RebuildMesh();
	}

	public void FixedUpdate()
	{
		if ( !ready )
			return;

		int jam = Jam;
		const int maxJam = 2 * Flag.maxItems;
		float weight = (float)( jam - 2 ) / ( maxJam - 6 );
		mapMaterial.color = Color.Lerp( Color.green, Color.red, weight );

		if ( decorationOnly )
			return;
		if ( workers.Count >= nodes.Count - 2 )
			return;

		if ( ( jam > 3 && targetWorkerCount == 0 && workerAdded.done ) || workers.Count < targetWorkerCount || workers.Count == 0 )
			CallNewWorker();
	}

	static readonly int blocksInSection = 8;
	public void RebuildMesh( bool force = false )
	{
		const float width = 0.3f;
		if ( mesh == null )
			return; // The road was already destroyed in the past, but a reference keeps the script alive

		if ( force )
			curves = new List<CubicCurve>[3];
		CreateCurves();

		int vertexRows = (nodes.Count - 1) * blocksInSection + 1;
		Vector3 h = Vector3.up*GroundNode.size*height;
		mesh.Clear();
		mapMesh.Clear();
		if ( nodes.Count == 1 )
			return;

		int v = 0;
		var vertices = new Vector3[vertexRows * 3];
		for ( int j = 0; j < vertices.Length; j++ )
			vertices[j] = new Vector3();
		var uvs = new Vector2[vertexRows * 3];

		var mapVertices = new Vector3[nodes.Count * 2];

		for (int i = 0; i < nodes.Count; i++)
		{
			for ( int b = 0; b < blocksInSection; b++ )
            {
				if (i == nodes.Count - 1 && b > 0)
					continue;

				float tv = 1.0f / blocksInSection * b * 2;
				if ( tv > 1 )
					tv = 2 - tv;
				var pos = PositionAt(i, 1.0f / blocksInSection * b);
				var dir = DirectionAt( i, 1.0f / blocksInSection * b);
				var side = new Vector3
				{
					x = dir.z / 2,
					z = -dir.x / 2
				};

				if ( b == 0 )
				{
					mapVertices[i * 2 + 0] = transform.InverseTransformPoint( pos - side * 0.2f + Vector3.up );
					mapVertices[i * 2 + 1] = transform.InverseTransformPoint( pos + side * 0.2f + Vector3.up );
				}

				uvs[v] = new Vector2(0.0f, tv );
				vertices[v++] = pos + h - side * width;
				uvs[v] = new Vector2(0.5f, tv );
				vertices[v++] = pos + h;
				uvs[v] = new Vector2(1.0f, tv );
				vertices[v++] = pos + h + side * width;
			}
		}

		var position = referenceLocation.position;
		for ( int i = 0; i < vertices.Length; i++ )
		{
			vertices[i].y = ground.GetHeightAt( vertices[i].x, vertices[i].z ) + 0.02f;	// Add 0.02f to avoid z fight.
			vertices[i] -= position;
		}

		assert.AreEqual( v, vertexRows * 3 );
		mesh.vertices = vertices;
		mesh.uv = uvs;
		mapMesh.vertices = mapVertices;

		int blockCount = (nodes.Count - 1) * blocksInSection;
		var triangles = new int[blockCount * 4 * 3];
		for ( int j = 0; j < blockCount	; j++ )
		{
			triangles[j * 4 * 3 + 00] = j * 3 + 3;
			triangles[j * 4 * 3 + 01] = j * 3 + 1;
			triangles[j * 4 * 3 + 02] = j * 3 + 0;
			
			triangles[j * 4 * 3 + 03] = j * 3 + 3;
			triangles[j * 4 * 3 + 04] = j * 3 + 4;
			triangles[j * 4 * 3 + 05] = j * 3 + 1;
			
			triangles[j * 4 * 3 + 06] = j * 3 + 4;
			triangles[j * 4 * 3 + 07] = j * 3 + 2;
			triangles[j * 4 * 3 + 08] = j * 3 + 1;

			triangles[j * 4 * 3 + 09] = j * 3 + 4;
			triangles[j * 4 * 3 + 10] = j * 3 + 5;
			triangles[j * 4 * 3 + 11] = j * 3 + 2;
		}

		var mapTriangles = new int[(nodes.Count - 1) * 2 * 3];
		for ( int i = 0; i < nodes.Count - 1; i++ )
		{
			mapTriangles[i * 6 + 0] = i * 2;
			mapTriangles[i * 6 + 1] = i * 2 + 2;
			mapTriangles[i * 6 + 2] = i * 2 + 1;
			mapTriangles[i * 6 + 3] = i * 2 + 1;
			mapTriangles[i * 6 + 4] = i * 2 + 2;
			mapTriangles[i * 6 + 5] = i * 2 + 3;
		}
		mesh.triangles = triangles;
		mapMesh.triangles = mapTriangles;

		mesh.RecalculateNormals();
		mapMesh.RecalculateNormals();
	}

	public Vector3 PositionAt( int block, float fraction )
	{
		if ( fraction == 0 && block == nodes.Count - 1 )
		{
			block -= 1;
			fraction = 1;
		}
		return new Vector3(
			curves[0][block].PositionAt( fraction ),
			curves[1][block].PositionAt( fraction ),
			curves[2][block].PositionAt( fraction ) );
	}

	public Vector3 DirectionAt( int block, float fraction )
	{
		if ( fraction == 0 && block == nodes.Count - 1 )
		{
			block -= 1;
			fraction = 1;
		}
		return new Vector3(
			curves[0][block].DirectionAt( fraction ),
			curves[1][block].DirectionAt( fraction ),
			curves[2][block].DirectionAt( fraction ) );
	}

	public int NodeIndex( GroundNode node )
	{
		if ( node.road )
		{
			assert.AreEqual( nodes[node.roadIndex], node );
			return node.roadIndex;
		}
		if ( node.validFlag )
		{
			if ( nodes[0] == node )
				return 0;
			if ( GetNodeFromEnd( 0 ) == node )
				return nodes.Count - 1;
		}
		return -1;
	}

	static public Road Between( GroundNode first, GroundNode second )
	{
		Assert.global.IsNotNull( first.validFlag );
		Assert.global.IsNotNull( second.validFlag );
		if ( first.validFlag == null || second.validFlag == null )
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

	public int cachedJam;
	[JsonIgnore]
	public int Jam
	{
		get
		{
			if ( !ready )
				return 0;

			if ( watchStartFlag.Check() || watchEndFlag.Check() )
			{
				cachedJam = 0;
				for ( int e = 0; e < 2; e++ )
				{
					Flag flag = ends[e];
					for ( int i = 0; i < Flag.maxItems; i++ )
					{
						Item t = flag.items[i];
						if ( t != null && t.flag == flag && t.road == this )
							cachedJam++;
					}
				}
			}
			return cachedJam;
		}
	}

	public void CallNewWorker()
	{
		var worker = Worker.Create().SetupForRoad( this );
		if ( worker != null )
		{
			workers.Add( worker );
			workerAdded.Start( timeBetweenWorkersAdded );
		}
	}

	void AttachWatches()
	{
		watchStartFlag.Attach( nodes[0].flag.itemsStored );
		watchEndFlag.Attach( lastNode.flag.itemsStored );
	}

	Vector3 NodePosition( int index )
	{
		return nodes[index].GetPositionRelativeTo( referenceLocation );
	}

	public void CreateCurves()
	{
		if ( curves[0] != null && curves[0].Count == nodes.Count - 1 )
			return;

		List<Vector3> directions = new List<Vector3>();
		for ( int j = 0; j < nodes.Count; j++ )
		{
			int p = Math.Max( j - 1, 0 );
			int n = Math.Min( j + 1, nodes.Count - 1 );
			directions.Add( ( NodePosition( n ) - NodePosition( p ) ).normalized );
		}
		for ( int i = 0; i < 3; i++ )
		{
			curves[i] = new List<CubicCurve>();
			for ( int j = 0; j < nodes.Count - 1; j++ )
			{
				if ( i == 1 )
				{
					curves[i].Add( CubicCurve.Create().SetupAsLinear(
						NodePosition( j )[i],
						NodePosition( j + 1 )[i] ) );
				}
				else
				{
					curves[i].Add( CubicCurve.Create().Setup(
						NodePosition( j )[i],
						NodePosition( j + 1 )[i],
						directions[j][i],
						directions[j + 1][i] ) );
				}
			}
		}
	}

	public void OnClicked( GroundNode node )
	{
		assert.AreEqual( node.road, this );
		Interface.RoadPanel.Create().Open( this, node );
	}

	public void Split( Flag flag )
	{
		bool external = false;
		int forget = 0;
		assert.IsNull( flag.user );
		int splitPoint;
		// Two cases, in first the flag is already on the road, in the second the flag is next to the road.
		if ( flag.node.road == this )
			splitPoint = NodeIndex( flag.node );
		else
		{
			int start = 0;
			while ( start < nodes.Count - 1 && nodes[start].DistanceFrom( flag.node ) > 1 )
				start++;
			int end = start + 1;
			while ( end < nodes.Count - 1 && nodes[end].DistanceFrom( flag.node ) == 1 )
				end++;
			if ( end - start == 1 )
				return;

			external = true;
			splitPoint = end - 2;
			forget = end - 2 - start;
		}

		for ( int i = splitPoint; i > splitPoint - forget; i-- )
		{
			if ( workerAtNodes[i] )
				workerAtNodes[i].onRoad = false;
		}

		Road first = Create(), second = Create();
		first.owner = second.owner = owner;	
		first.ready = second.ready = true;
		first.ground = second.ground = ground;
		first.nodes = nodes.GetRange( 0, splitPoint + 1 - forget );
		first.workerAtNodes = workerAtNodes.GetRange( 0, splitPoint + 1 - forget );
		second.nodes = nodes.GetRange( splitPoint, nodes.Count - splitPoint );
		second.workerAtNodes = workerAtNodes.GetRange( splitPoint, workerAtNodes.Count - splitPoint );
		second.workerAtNodes[0] = null;
		if ( external )
		{
			first.nodes.Add( flag.node );
			first.workerAtNodes.Add( null );
			second.nodes[0] = flag.node;
		}

		foreach ( var worker in workers )
        {
			// TODO What if worker is not yet onRoad?
			int workerPoint = worker.IndexOnRoad();
			if ( worker.onRoad )
			{
				if ( flag.node == worker.node )
				{
					assert.IsFalse( external );
					assert.AreEqual( workerPoint, splitPoint ); // TODO Triggered
				}
				if ( !external )
					assert.AreNotEqual( workerPoint, -1 );
				if ( splitPoint == workerPoint && !external )
				{
					flag.user = worker;
					worker.exclusiveFlag = flag;
				}
			}
			if ( workerPoint <= splitPoint )
			{
				first.workers.Add( worker );
				worker.road = first;
			}
			else
			{
				second.workers.Add( worker );
				worker.road = second;
			}
			worker.ResetTasks();
		}

		UnregisterOnGround();
		first.RegisterOnGround();
		second.RegisterOnGround();
		first.AttachWatches();
		second.AttachWatches();

		first.ends[0] = ends[0];
		first.ends[1] = second.ends[0] = flag;
		second.ends[1] = ends[1];

		if ( first.workers.Count == 0 )
			first.CallNewWorker();
		if ( second.workers.Count == 0 )
			second.CallNewWorker();

		invalid = true;
		DestroyThis();

		first.Validate( false );
		second.Validate( false );
		flag.Validate( false );
	}

	void RegisterOnGround()
	{
		var a0 = nodes[0].flag.roadsStartingHere; var i0 = nodes[0].DirectionTo( nodes[1] );
		var a1 = lastNode.flag.roadsStartingHere; var i1 = GetNodeFromEnd( 0 ).DirectionTo( GetNodeFromEnd( 1 ) );
		assert.IsTrue( a0[i0] == null || a0[i0] == this );
		assert.IsTrue( a1[i1] == null || a1[i1] == this );
		a0[i0] = this;
		a1[i1] = this;
		for ( int i = 1; i < nodes.Count - 1; i++ )
		{
			assert.IsTrue( nodes[i].road == null || nodes[i].road == this );
			nodes[i].road = this;
			nodes[i].roadIndex = i;
		}
	}

	void UnregisterOnGround()
	{
		if ( ready )
		{
			var a0 = nodes[0].flag.roadsStartingHere;
			var i0 = nodes[0].DirectionTo( nodes[1] );
			assert.AreEqual( a0[i0], this );
			a0[i0] = null;

			var a1 = lastNode.flag.roadsStartingHere;
			var i1 = GetNodeFromEnd( 0 ).DirectionTo( GetNodeFromEnd( 1 ) );
			assert.AreEqual( a1[i1], this );
			a1[i1] = null;
		}

		for ( int i = 1; i < nodes.Count; i++ )
		{
			if ( i == nodes.Count - 1 )
			{
				if ( ready || nodes[i].road != this )
					continue;
			}
			assert.AreEqual( nodes[i].road, this );	// TODO Fired on unready road, nodes had 4 elements, the one with index 2 was null.
													// And fired again when I pressed ESC while the road had 4 nodes already.
													// Fired again when pressing esc. (i=1, Count=4, tempNodes=0)
													// Fired when pressing ESC in map mode during road construction. Road had three nodes, 
													// the one in the middle had the problem, nodes[1].road was null. It seems like the second
													// node was already finalized, like when you spin down that location.
													// Triggered again I think while I wanted to find a place for a road, meanwhile the
													// peasant planted a cornfield in the way.
													// Again
													// Again when interruping road construction
			nodes[i].road = null;
		}
	}

	public void OnDestroy()
	{
		owner.versionedRoadDelete.Trigger();
	}

	public override bool Remove( bool takeYourTime )
	{
		var localWorkers = workers.GetRange( 0, workers.Count );
		foreach ( var worker in localWorkers )
			if ( !worker.Remove() )
				return false;
		if ( !decorationOnly )
			UnregisterOnGround();

		invalid = true;
		DestroyThis();
		return true;
	}

	[JsonIgnore]
	public GroundNode CenterNode
	{
		get
		{
			return nodes[nodes.Count / 2];
		}
	}

	[JsonIgnore]
	public float Cost
	{
		get
		{
			if ( cachedCost == 0 )
			{
				for ( int i = 0; i < nodes.Count - 1; i++ )
					cachedCost += 0.01f / Worker.SpeedBetween( nodes[i], nodes[i + 1] );
			}

			float jamMultiplier = 1 + Jam / 2f;
			return cachedCost * jamMultiplier;
		}
	}

	[JsonIgnore]
	public int ActiveWorkerCount
	{
		get
		{
			int activeWorkers = 0;
			foreach ( var worker in workers )
				if ( worker.onRoad )
					activeWorkers++;
			return activeWorkers;
		}
	}

	[JsonIgnore]
	public GroundNode lastNode { get { return nodes[nodes.Count - 1]; } }

	public bool OnMovingOverNode( GroundNode node )
 	{
		if ( node == null )
			return true;
		assert.IsTrue( tempNodes < nodes.Count );

		while ( tempNodes > 0 )
		{
			RemoveLastNode();
			tempNodes--;
		}
		assert.IsTrue( nodes.Count > 0 );

		PathFinder p = ScriptableObject.CreateInstance<PathFinder>();
		if ( p.FindPathBetween( lastNode, node, PathFinder.Mode.avoidRoadsAndFlags, true ) )
		{
			var j = nodes.Count;
			for ( int i = 1; i < p.path.Count; i++ )
				AddNode( p.path[i] );

			tempNodes = nodes.Count - j;
		}
		RebuildMesh( true );

  		if ( node == GetNodeFromEnd( tempNodes ) )
		{
			Interface.root.viewport.SetCursorType( Interface.Viewport.CursorType.remove );
			return true;
		}

		if ( node.flag )
		{
			Interface.root.viewport.SetCursorType( Interface.Viewport.CursorType.flag );
			return true;
		}

		if ( node.DistanceFrom( lastNode ) == 1 )
		{
			Interface.root.viewport.SetCursorType( Interface.Viewport.CursorType.road );
			return true;
		}

		if ( Flag.IsNodeSuitable( node, owner ) )
		{
			Interface.root.viewport.SetCursorType( Interface.Viewport.CursorType.flag );
			return true;
		}

		Interface.root.viewport.SetCursorType( Interface.Viewport.CursorType.nothing );
		return true;
	}

	public bool OnNodeClicked( GroundNode node )
	{
		if ( node != lastNode )
			return true;

		if ( node.road && node.road != this )
		{
			if ( Flag.Create().Setup( node, owner ) == null )
				return true;
		}
		if ( Interface.GetKey( KeyCode.LeftShift ) || Interface.GetKey( KeyCode.RightShift ) )
			Flag.Create().Setup( node, owner );

		if ( tempNodes == 0 )
		{
			if ( RemoveLastNode() )
			{
				RebuildMesh();
				return true;
			}
			else
				return false;
		}

		if ( node.IsBlocking() && node.flag == null && node.road != this )
			return true;

		tempNodes = 0;
		RebuildMesh();
		if ( node.validFlag )
		{
			if ( !Finish() )
				Remove( false );
			Interface.root.viewport.showGridAtMouse = false;
			return false;
		}
		else
			return true;
	}

	public bool OnObjectClicked( HiveObject target )
	{
		return false;
	}

	public override void Reset()
	{
		while ( workers.Count > 1 )
			workers[1].Remove( false );
		workers[0].Reset();
	}

	public override GroundNode Node
	{
		get
		{
			return CenterNode;
		}
	}

	public override void Validate( bool chain )
	{
		if ( !ready && !decorationOnly )
		{
			assert.IsTrue( Interface.root.viewport.InputHandler is Road );
			return;
		}
		int length = nodes.Count;
		assert.IsTrue( length > 1 );
		if ( decorationOnly )
		{
			assert.AreEqual( nodes.Count, 2 );
			assert.IsNotNull( nodes[0].building );
			assert.AreEqual( nodes[1].flag, nodes[0].building.flag );
			return;
		}
		assert.IsNotNull( ends[0] );
		assert.IsNotNull( ends[1] );
		assert.AreEqual( ends[0].node, nodes[0] );
		assert.AreEqual( ends[1].node, lastNode );
		assert.IsTrue( ends[0].roadsStartingHere.Contains( this ) );
		assert.IsTrue( ends[1].roadsStartingHere.Contains( this ) );

		var first = nodes[0];
		var last = lastNode;
		assert.IsNotNull( first.flag );
		assert.IsNotNull( last.flag );
		assert.AreEqual( this, first.flag.roadsStartingHere[first.DirectionTo( nodes[1] )] );	
		// TODO Triggered after restarting and loading back the game. 
		// The road was not registered at any of the flags, and wasn't blueprint. 
		// It was registered at the two inner nodes. This is a recently built road.
		// The Validate was called from World.Load. The road has no workers on it.
		assert.AreEqual( this, last.flag.roadsStartingHere[last.DirectionTo( nodes[length - 2] )] );
		for ( int i = 1; i < length - 1; i++ )
			assert.AreEqual( this, nodes[i].road ); // TODO This assert fired once
		for ( int i = 0; i < length - 1; i++ )
			assert.IsTrue( nodes[i].DirectionTo( nodes[i + 1] ) >= 0 );
		foreach ( var worker in workers )
		{
			assert.IsValid( worker );
			if ( !worker.onRoad )
				continue;
			int i = 0;
			foreach ( var w in workerAtNodes )
				if ( w == worker )
					i++;
			assert.AreEqual( i, 1 );
			if ( chain )
				worker.Validate( true );
		}
		if ( workerAtNodes[0] != null && !ends[0].crossing && !ends[0].recentlyLeftCrossing )
			assert.AreEqual( ends[0].user, workerAtNodes[0] );
		if ( workerAtNodes[nodes.Count - 1] != null && !ends[1].crossing && !ends[1].recentlyLeftCrossing )
			assert.AreEqual( ends[1].user, workerAtNodes[nodes.Count - 1] );
		int realJam = 0;
		for ( int e = 0; e < 2; e++ )
		{
			Flag flag = ends[e];
			for ( int i = 0; i < Flag.maxItems; i++ )
			{
				Item t = flag.items[i];
				if ( t != null && t.flag == flag && t.road == this )
					realJam++;
			}
		}
		assert.AreEqual( realJam, Jam );	// TODO Triggered (realJam=7, Jam=8), triggered again (realJam=10, Jam=11, max=12) and again (realJam==3, Jam==4). Potential fix was made. Triggered again (4, 5)(2, 3).
			for ( int i = 0; i < nodes.Count - 1; i++ )
			assert.AreEqual( nodes[i].DistanceFrom( nodes[i + 1] ), 1 );
		if ( !ready )
			assert.IsTrue( tempNodes < nodes.Count );
		else
		{
			assert.AreEqual( tempNodes, 0 );
			assert.IsTrue( nodes.Count > 1 );
		}
		if ( !ready )
			assert.AreEqual( Interface.root.viewport.InputHandler, this );
	}

	public void OnLostInput()
	{
		bool removed = Remove( false );
		Interface.root.viewport.showGridAtMouse = false;
		assert.IsTrue( removed );
	}
}
