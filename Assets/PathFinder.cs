using System;
using System.Collections.Generic;
using System.Reflection;

[Serializable]
public class PathFinder
{
	// TODO Detect when circumstances change, and invalidate the path
	public Node target, avoidNode;
	public List<Reached> visited = new ();
	public List<Node> path = new ();
	public List<Road> roadPath = new ();
	public List<bool> roadPathReversed = new ();
	public HiveObject ignoreObject;
	public Func<Node, float> appraiser;
	public bool ready = false;
	public int openNodes;
	public Mode mode;
	public bool ignoreFinalObstacle;

	[Obsolete( "Compatibility with old files", true )]
	public bool tryToAvoidValuableNodes { set {} }
	public class Reached
    {
        public Node node;
        public float costG;
        public float costH;
        public float costF;
        public Reached from;
        public bool processed = false;
		public Road road;
		public bool reversed;
    }

	public enum Mode
	{
		forRoads,
		onRoad,
		forUnits,
		total
	}

	public static PathFinder Create()
	{
		return new PathFinder();
	}

	public bool IsValid
	{
		get
		{
			if ( !ready )
				return false;

			if ( mode != Mode.onRoad )
				return true;

			foreach ( var road in roadPath )
				if ( road == null )
					return false;

			return true;
		}
	}

    public bool FindPathBetween( Node start, Node end, Mode mode, bool ignoreFinalObstacle = false, HiveObject ignoreObject = null, Func<Node, float> appraiser = null )
    {
        target = end;
		this.ignoreFinalObstacle = ignoreFinalObstacle;
		this.ignoreObject = ignoreObject;
		this.appraiser = appraiser;
		this.mode = mode;
		if ( start == end )
		{
			path.Clear();
			roadPath.Clear();
			roadPathReversed.Clear();
			ready = true;
			return true;
		}
		ready = false;

		if ( mode == PathFinder.Mode.onRoad && (start.flag == null || end.flag == null || start.flag.team != end.flag.team) )
			return false;

        visited.Clear();
        openNodes = 0;
        AddNode( start, 0, null );
        while ( !ready && openNodes > 0 )
            ProcessBestNode();
        return ready;
    }

    void VisitNode( Node node, float cost, Reached from, Road road = null, bool reversed = false )
    {
		if ( !ignoreFinalObstacle || node != target )
		{
			if ( node == avoidNode )
				return;
			if ( mode == Mode.forRoads && node.block.IsBlocking( Node.Block.Type.roads ) )
				return;
			if ( mode == Mode.forUnits && node.block.IsBlocking( Node.Block.Type.units ) )
			{
				if ( ignoreObject == null || ignoreObject != node.building ) 
					return;
			}

			if ( mode == Mode.forRoads && ( node.team != target.team || node.road ) )
				return;

			if ( node.type == Node.Type.underWater )
				return;
		}

        var i = node.index;
        if ( i >= 0 && i < visited.Count && visited[i].node == node )
        {
            if ( cost < visited[i].costG )
            {
                visited[i].costG = cost;
                visited[i].from = from;
				visited[i].road = road;
				visited[i].reversed = reversed;
            }
            return;
        }
        AddNode( node, cost, from, road, reversed );
    }

	void AddNode( Node node, float cost, Reached from, Road road = null, bool reversed = false )
    {
		var n = new Reached
		{
			node = node,
			costG = cost,
			costH = node.DistanceFrom( target )
		};
		n.costF = n.costG + n.costH;
        n.from = from;
		n.road = road;
		n.reversed = reversed;
        node.index = visited.Count;
        visited.Add( n );
        openNodes++;
    }

    void ProcessBestNode()
    {
        Reached best = null;
        foreach ( var r in visited )
        {
            if ( r.processed || ( best != null && r.costF >= best.costF ) )
                continue;

            best = r;
        }
        if ( best != null )
            Process( best );
    }

    void Process( Reached r )
    {
		if ( r.node == target )
		{
			FoundPath( r );
			return;
		}

		r.processed = true;
        openNodes--;
		if ( mode == Mode.onRoad )
		{
			foreach ( var road in r.node.flag.roadsStartingHere )
			{
				if ( road == null || !road.ready )
					continue;
				int index = road.NodeIndex( r.node );
				if ( index == 0 )
					VisitNode( road.lastNode, r.costG + road.cost, r, road, false );
				else
				{
					road.assert.AreEqual( index, road.nodes.Count - 1 );	// TODO Triggered, index==-1
					VisitNode( road.nodes[0], r.costG + road.cost, r, road, true );
				}
			}
		}
		else
		{
			for ( int i = 0; i < Constants.Node.neighbourCount; i++ )
			{
				Node t = r.node.Neighbour( i );
				var newCost = 0.01f/Unit.SpeedBetween( r.node, t );
				if ( appraiser != null )
					newCost *= appraiser( t );
				VisitNode( t, r.costG + newCost, r );
			}
		}
    }

    void FoundPath( Reached goal )
    {
        path.Clear();
		roadPath.Clear();
		roadPathReversed.Clear();
        var r = goal;
        do
        {
			if ( mode == Mode.onRoad )
			{
				if ( r.road != null )
				{
					roadPath.Insert( 0, r.road );
					roadPathReversed.Insert( 0, r.reversed );
				}
			}
			else
				path.Insert( 0, r.node );
			r = r.from;
        }
        while ( r != null );
        visited.Clear();
        ready = true;
        return;
    }

	public int length
	{
		get
		{
			if ( !ready )
				return int.MaxValue;
			if ( mode == Mode.onRoad )
			{
				int length = 0;
				foreach ( var road in roadPath )
					length += road.length;
				return length;
			}
			return path.Count - 1;
		}
	}

	virtual public void Validate()
	{
		if ( ready )
		{
			Assert.global.AreEqual( roadPath.Count, roadPathReversed.Count );
			if ( mode == Mode.onRoad )
			{
				for ( int i = 0; i < roadPath.Count - 1; i++ )
				{
					if ( roadPath[i] == null || roadPath[i + 1] == null )
						continue;
					Assert.global.AreEqual( roadPath[i].ends[roadPathReversed[i] ? 0 : 1], roadPath[i + 1].ends[roadPathReversed[i + 1] ? 1 : 0] );
				}
				Assert.global.IsTrue( path.Count == 0 );
				if ( roadPath.Count > 0 )
				{
					Road last = roadPath[roadPath.Count - 1];
					if ( last )
						Assert.global.IsTrue( last.ends[roadPathReversed[roadPath.Count - 1] ? 0 : 1].node == target );
				}
			}
			else
			{
				Assert.global.IsTrue( path.Count > 0 && roadPath.Count == 0 );
				Assert.global.AreEqual( path[path.Count - 1], target );
			}
		}
		else
		{
			Assert.global.AreEqual( path.Count, 0 );
			Assert.global.AreEqual( roadPath.Count, 0 );
		}
	}
}

[Serializable]
public class Path : PathFinder, Serializer.IReferenceUser
{
	public int progress;
	public HiveObject owner;

	public new static Path Create()
	{
		return new Path();
	}

	public static Path Between( Node start, Node end, Mode mode, HiveObject owner = null, bool ignoreFinalObstacle = false, HiveObject ignoreObject = null, Node avoid = null )
	{
		var p = Path.Create();
		p.owner = owner;
		p.avoidNode = avoid;
		if ( p.FindPathBetween( start, end, mode, ignoreFinalObstacle, ignoreObject ) )
		{
			if ( mode != Mode.onRoad )
				p.progress = 1;
			return p;
		}
		return null;
	}

	public Road road => roadPath == null ? null : roadPath[progress];

	public Road NextRoad()
	{
		return roadPath[progress++];
	}

	public Node location => path[progress];

	public Node NextNode()
	{
		return path[progress++];
	}

	public bool isFinished
	{
		get
		{
			if ( mode == Mode.onRoad )
				return progress >= roadPath.Count;

			return progress >= path.Count;
		}
	}

	public int stepsLeft
	{
		get
		{
			if ( mode == Mode.onRoad )
				return roadPath.Count - progress;

			return path.Count - progress;
		}
	}

	public static int DistanceBetween( Flag a, Flag b )
	{
		var path = Between( a.node, b.node, Mode.onRoad );
		return path == null ? int.MaxValue : path.length;
	}

	public override void Validate()
	{
		base.Validate();
		Assert owner = this.owner?.assert ?? Assert.global;
		owner.IsTrue( progress >= 0 );
		if ( mode == Mode.onRoad )
			owner.IsTrue( progress <= roadPath.Count );
		else
			owner.IsTrue( progress <= path.Count );
		/*if ( ready && mode == Mode.onRoad )
		{
			for ( int i = progress; i < roadPath.Count; i++ )
			{	// TODO Triggered multiple times after deleting a road
				// the road was close to the end of a path, but after deleting the road,
				// the reference in the path become null
				owner.assert.IsNotNull( roadPath[i] );
			}
		}*/
	}

    public void OnDeadReference(MemberInfo member, HiveObject reference)
    {
		// TODO Is it really necessary for the Path class to imlement this interface?
		// It is doing that because it might have reference to deleted roads
    }
}