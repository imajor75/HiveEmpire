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
                    roadObject.transform.SetParent( ground.transform );
                    roadObject.name = "Road";
                    newRoad = (Road)roadObject.AddComponent(typeof(Road));
                }
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
            return true;
        }

        // Check if the current node is adjacent to the previous one
        int direction = last.IsAdjacentTo( node );
        if ( direction < 0 )
        {
            UnityEngine.Debug.Log( "Node must be adjacent to previous one" );
            return false;
        }

        newRoad.nodes.Add( node );

        // Finishing a road
        if ( node.flag )
        {
            newRoad.nodes[0].startingHere[newRoad.nodes[0].IsAdjacentTo( newRoad.nodes[1] )] = newRoad;
            node.startingHere[node.IsAdjacentTo( last )] = newRoad;
            newRoad.ready = true;
            newRoad = null;
            UnityEngine.Debug.Log( "Road finished" );
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
        
    }

    // Update is called once per frame
    void Update()
    {
        
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

    bool ready = false;
    Ground ground;
    List<GroundNode> nodes = new List<GroundNode>();
}
