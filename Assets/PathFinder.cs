using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking.Types;

public class PathFinder
{
    public class Reached
    {
        public GroundNode node;
        public float cost;
        public Reached from;
        public bool processed = false;
    }

    public GroundNode target;
    public List<Reached> visited = new List<Reached>();
    public List<GroundNode> path = new List<GroundNode>();
    public bool ready = false;
    public int openNodes;

    public bool FindPathBetween( GroundNode start, GroundNode end )
    {
        ready = false;
        target = end;
        visited.Clear();
        openNodes = 0;
        AddNode( start, 0, null );
        while ( !ready && openNodes > 0 )
            ProcessBestNode();
        return ready;
    }

    void VisitNode( GroundNode node, float cost, Reached from )
    {
        // If we cannot pass through the node, skip it
        if ( node.road || node.building )
            return;

        // Check if the node was already visited
        var i = node.pathFindingIndex;
        if ( i >= 0 && i < visited.Count && visited[i].node == node )
        {
            if ( cost < visited[i].cost )
            {
                visited[i].cost = cost;
                visited[i].from = from;
            }
            return;
        }
        AddNode( node, cost, from );
    }

    void AddNode( GroundNode node, float cost, Reached from )
    {
        var n = new Reached();
        n.node = node;
        n.cost = cost;
        n.from = from;
        node.pathFindingIndex = visited.Count;
        visited.Add( n );
        openNodes++;
        if ( node == target )
            FoundPath( n );
    }

    void ProcessBestNode()
    {
        Reached best = null;
        foreach ( var r in visited )
        {
            if ( r.processed || ( best != null && r.cost >= best.cost ) )
                continue;

            best = r;
        }
        if ( best != null )
            Process( best );
    }
    void Process( Reached r )
    {
        r.processed = true;
        openNodes--;
        foreach ( var node in r.node.neighbours )
        {
            VisitNode( node, r.cost + 1, r );
        }
    }

    void FoundPath( Reached goal )
    {
        path.Clear();
        var r = goal;
        do
        {
            path.Insert( 0, r.node );
            r = r.from;
        }
        while ( r != null );
        visited.Clear();
        ready = true;
        return;
    }
}
