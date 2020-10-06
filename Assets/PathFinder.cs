using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;

public class PathFinder : ScriptableObject
{
	// TODO Detect when circumstances change, and invalidate the path
	public GroundNode target;
	public List<Reached> visited = new List<Reached>();
	public List<GroundNode> path = new List<GroundNode>();
	public List<Road> roadPath = new List<Road>();
	public bool ready = false;
	public int openNodes;
	public Mode mode;
	public bool ignoreFinalObstacle;

	public class Reached
    {
        public GroundNode node;
        public float costG;
        public float costH;
        public float costF;
        public Reached from;
        public bool processed = false;
		public Road road;
    }

	public enum Mode
	{
		avoidRoadsAndFlags,
		onRoad,
		avoidObjects,
		total
	}

    public bool FindPathBetween( GroundNode start, GroundNode end, Mode mode, bool ignoreFinalObstacle = false )
    {
		Assert.global.AreNotEqual( start, end );
        ready = false;
        target = end;
		this.ignoreFinalObstacle = ignoreFinalObstacle;
		if ( mode == PathFinder.Mode.onRoad )
		{
			Assert.global.IsNotNull( start.flag, "Trying to find a road path not starting at a flag" );
			Assert.global.IsNotNull( end.flag );
		}
		this.mode = mode;
        visited.Clear();
        openNodes = 0;
        AddNode( start, 0, null );
        while ( !ready && openNodes > 0 )
            ProcessBestNode();
        return ready;
    }

    void VisitNode( GroundNode node, float cost, Reached from, Road road = null )
    {
		if ( !ignoreFinalObstacle || node != target )
		{
			if ( node.IsBlocking( mode == Mode.avoidRoadsAndFlags ) )
				return;

			if ( mode == Mode.avoidRoadsAndFlags && ( node.owner != target.owner || node.road ) )
				return;

			if ( node.type == GroundNode.Type.underWater )
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
            }
            return;
        }
        AddNode( node, cost, from, road );
    }

    void AddNode( GroundNode node, float cost, Reached from, Road road = null )
    {
        var n = new Reached();
        n.node = node;
        n.costG = cost;
        n.costH = node.DistanceFrom( target );
        n.costF = n.costG + n.costH;
        n.from = from;
		n.road = road;
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
				if ( road == null )
					continue;
				int index = road.NodeIndex( r.node );
				if ( index == 0 )
					VisitNode( road.nodes[road.nodes.Count - 1], r.costG + road.Cost() + road.Jam(), r, road );
				else
				{
					Assert.global.AreEqual( index, road.nodes.Count - 1 );
					// TODO Calculate the additional cost better based on traffic jam
					VisitNode( road.nodes[0], r.costG + road.Cost() + road.Jam(), r, road );
				}
			}
		}
		else
		{
			for ( int i = 0; i < GroundNode.neighbourCount; i++ )
			{
				GroundNode t = r.node.Neighbour( i );
				VisitNode( t, r.costG + 0.01f/Worker.SpeedBetween( r.node, t ), r ); // TODO cost should depend on steepness of the road
			}
		}
    }

    void FoundPath( Reached goal )
    {
        path.Clear();
		roadPath.Clear();
        var r = goal;
        do
        {
			if ( mode == Mode.onRoad )
			{
				if ( r.road != null )
					roadPath.Insert( 0, r.road );
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
	virtual public void Validate()
	{
		if ( ready )
		{
			if ( mode == Mode.onRoad )
			{
				Assert.global.IsTrue( path.Count == 0 && roadPath.Count > 0 );
				Road last = roadPath[roadPath.Count - 1];
				Assert.global.IsTrue( last.GetEnd( 0 ).node == target || last.GetEnd( 1 ).node == target );
				foreach ( var road in roadPath )
					Assert.global.IsNotNull( road );
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

public class Path : PathFinder
{
	public int progress;

	public static Path Between( GroundNode start, GroundNode end, Mode mode, bool ignoreFinalObstacle = false )
	{
		var p = ScriptableObject.CreateInstance<Path>();
		if ( p.FindPathBetween( start, end, mode, ignoreFinalObstacle ) )
		{
			if ( mode != Mode.onRoad )
				p.progress = 1;
			return p;
		}
		return null;
	}

	public Road Road()
	{
		return roadPath[progress];
	}

	public Road NextRoad()
	{
		return roadPath[progress++];
	}

	public GroundNode Node()
	{
		return path[progress];
	}

	public GroundNode NextNode()
	{
		return path[progress++];
	}

	public bool IsFinished()
	{
		if ( mode == Mode.onRoad )
			return progress >= roadPath.Count;

		return progress >= path.Count;
	}

	public int StepsLeft()
	{
		if ( mode == Mode.onRoad )
			return roadPath.Count - progress;

		return path.Count - progress;
	}

	public override void Validate()
	{
		base.Validate();
		Assert.global.IsTrue( progress >= 0 );
		if ( mode == Mode.onRoad )
			Assert.global.IsTrue( progress <= roadPath.Count );
		else
			Assert.global.IsTrue( progress <= path.Count );
	}
}