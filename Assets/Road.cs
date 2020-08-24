using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using UnityEngine;
using UnityEngine.Assertions;

public class Road : MonoBehaviour
{
    public static bool AddNodeToNew(Ground ground, GroundNode node)
    {
        // Starting a new road
        if ( newRoad == null || newRoad.Length() == 0 )
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

        GroundNode last = newRoad.nodes[newRoad.Length() - 1];
        // Special case, last node is the same as the current, remove one
        if ( last == node )
        {
            if ( newRoad.Length() == 1 )
            {
                CancelNew();
                return true;
            }
            newRoad.nodes.RemoveAt( newRoad.Length() - 1 );
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

        // Check if the current node is adjacent to the previous one
        int direction = last.IsAdjacentTo( node );
        if ( direction < 0 )
        {
            Debug.Log( "Node must be adjacent to previous one" );
            return false;
        }

        newRoad.nodes.Add( node );
        newRoad.RebuildMesh();
        if ( newRoad.Length() == 2 )
            newRoad.name = "Road " + node.x + ", " + node.y;

        // Finishing a road
        if ( node.flag )
        {
            newRoad.nodes[0].startingHere[newRoad.nodes[0].IsAdjacentTo( newRoad.nodes[1] )] = newRoad;
            node.startingHere[node.IsAdjacentTo( last )] = newRoad;
            newRoad.ready = true;
            newRoad = null;
            return true;
        };

        node.road = newRoad;
        return true;
    }

    public static void CancelNew()
    {
        if ( newRoad )
        {
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
        var l = Length()-1;
        var vertices = new Vector3[l*6];
        for ( int j = 0; j < l * 6; j++ )
            vertices[j] = new Vector3();
        for ( int i = 0; i < l; i++ )
        {
            var a = nodes[i].Position();
            var b = nodes[i+1].Position();
            var ab = b-a;
            Vector3 o = new Vector3( ab.z/10, 0, -ab.x/10 );
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

    public int Length()
    {
        return nodes.Count;
    }

    public void Validate()
    {
        int length = Length();
        Assert.IsTrue( length > 1 );
        var first = nodes[0];
        var last = nodes[length-1];
        Assert.IsNotNull( first.flag );
        Assert.IsNotNull( last.flag );
        Assert.AreEqual( this, first.startingHere[first.IsAdjacentTo( nodes[1] ) ] );
        Assert.AreEqual( this, last.startingHere[last.IsAdjacentTo( nodes[length - 2] ) ] );
        for ( int i = 1; i < length - 1; i++ )
            Assert.AreEqual( this, nodes[i].road );
    }

    public bool ready = false;
    Ground ground;
    List<GroundNode> nodes = new List<GroundNode>();
    Mesh mesh;
    public static Material material;
}
