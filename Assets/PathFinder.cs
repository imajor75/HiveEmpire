using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

public class PathFinder
{
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

    public GroundNode target;
    public List<Reached> visited = new List<Reached>();
    public List<GroundNode> path = new List<GroundNode>();
	public List<Road> roadPath = new List<Road>();
    public bool ready = false;
    public int openNodes;
	public Mode mode;

	public enum Mode
	{
		avoidRoads,
		onRoad,
		total
	}

    public bool FindPathBetween( GroundNode start, GroundNode end, Mode mode )
    {
        ready = false;
        target = end;
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
		// If we cannot pass through the node, skip it
		if ( mode == Mode.avoidRoads )
		{
			if ( node.road || node.building )
				return;
		}

        // Check if the node was already visited
        var i = node.pathFindingIndex;
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
        node.pathFindingIndex = visited.Count;
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
			foreach ( var road in r.node.roadsStartingHere )
			{
				if ( road == null )
					continue;
				int index = road.NodeIndex( r.node );
				if ( index == 0 )
					VisitNode( road.nodes[road.nodes.Count - 1], r.costG + road.nodes.Count - 1, r, road );
				else
				{
					Assert.AreEqual( index, road.nodes.Count - 1 );
					VisitNode( road.nodes[0], r.costG + road.nodes.Count - 1, r, road );
				}
			}
		}
		else
		{
			foreach ( var node in r.node.neighbours )
				VisitNode( node, r.costG + 1, r ); // TODO cost should depend on steepness of the road
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
}
