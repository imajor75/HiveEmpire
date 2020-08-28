using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.VR;
using UnityEngine.XR;

public class Road : MonoBehaviour
{
    public static bool AddNodeToNew(Ground ground, GroundNode node)
    {
        // Starting a new road
        if ( newRoad == null || newRoad.nodes.Count == 0 )
        {
            if ( node.flag )
            {
                if ( newRoad == null )
                {
                    var roadObject = new GameObject();
                    roadObject.transform.SetParent( ground.transform, false );
                    roadObject.name = "Road";
                    newRoad = (Road)roadObject.AddComponent(typeof(Road));
                }
                newRoad.ground = ground;
                newRoad.nodes.Add( node );
                return true;
            }
            else
            {
                Debug.Log( "Road must start at a flag" );
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
            Debug.Log( "Roads cannot cross" );
            return false;
        }

        // Check if the current node has a building
        if ( node.building )
        {
            Debug.Log( "Cannot build a road on a building" );
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
                    newRoad.RebuildMesh();
					newRoad.worker = Worker.Create( ground, newRoad );
					newRoad = null;
                    return true;
                }
            }
            Debug.Log( "Node must be adjacent to previous one" );
            return false;
        }
        bool finished = newRoad.AddNode( node );
        newRoad.RebuildMesh();
		if ( finished )
		{
			newRoad = null;
			newRoad.worker = Worker.Create( ground, newRoad );
		}
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
            nodes[0].roadsStartingHere[nodes[0].DirectionTo( nodes[1] )] = this;
            node.roadsStartingHere[node.DirectionTo( nodes[nodes.Count - 2] )] = this;
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
    static Road newRoad;

    // Start is called before the first frame update
    void Start()
    {
        var renderer = gameObject.AddComponent<MeshRenderer>();
        renderer.material = material;
        var filter = gameObject.AddComponent<MeshFilter>();
        mesh = filter.mesh = new Mesh();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void RebuildMesh()
    {
        mesh.Clear();
        var l = nodes.Count-1;
        var vertices = new Vector3[l*6];
        for ( int j = 0; j < l * 6; j++ )
            vertices[j] = new Vector3();
        for ( int i = 0; i < l; i++ )
        {
            var a = nodes[i].Position();
            var b = nodes[i+1].Position();
            var ab = b-a;
            Vector3 o = new Vector3( ab.z, 0, -ab.x );
            o.Normalize();
            o *= GroundNode.size / 10;
            Vector3 h = new Vector3( 0, GroundNode.size / 10, 0 );
            vertices[i * 6 + 0] = a + o;
            vertices[i * 6 + 1] = a + h;
            vertices[i * 6 + 2] = a - o;
            vertices[i * 6 + 3] = b + o;
            vertices[i * 6 + 4] = b + h;
            vertices[i * 6 + 5] = b - o;
        }
        mesh.vertices = vertices;

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
    }

    public void Validate()
    {
        int length = nodes.Count;
        Assert.IsTrue( length > 1 );
        var first = nodes[0];
        var last = nodes[length-1];
        Assert.IsNotNull( first.flag );
        Assert.IsNotNull( last.flag );
        Assert.AreEqual( this, first.roadsStartingHere[first.DirectionTo( nodes[1] ) ] );
        Assert.AreEqual( this, last.roadsStartingHere[last.DirectionTo( nodes[length - 2] ) ] );
        for ( int i = 1; i < length - 1; i++ )
            Assert.AreEqual( this, nodes[i].road );
        for ( int i = 0; i < length - 1; i++ )
            Assert.IsTrue( nodes[i].DirectionTo( nodes[i + 1] ) >= 0 );
		if ( worker )
			worker.Validate();
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
		foreach ( var road in first.roadsStartingHere )
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

	public Worker worker;
    public bool ready = false;
    public Ground ground;
    public List<GroundNode> nodes = new List<GroundNode>();
    Mesh mesh;
    public static Material material;
}
