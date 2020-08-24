using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

public class GroundNode
{
    public static float size = 1;
    public static int neighbourCount = 6;
    public int x, y;
    public Building building;
    public Flag flag;
    public Road road;
    public Road[] startingHere = new Road[neighbourCount];
    public GroundNode[] neighbours = new GroundNode[neighbourCount];
    public float height = 0;
    Ground ground;
    public Vector3 Position()
    {
        Vector3 position = new Vector3( x*size+y*size/2, height, y*size );
        return position;
    }
    public int IsAdjacentTo( GroundNode another )
    {
        int direction = -1;
        for ( int i = 0; i < 6; i++ )
            if ( neighbours[i] == another )
                direction = i;
        return direction;
    }
    public void Validate()
    {
        Assert.IsTrue( flag == null || road == null, "Both flag and road at the same node (" + x + ", " + y );
        Assert.IsTrue( flag == null || building == null );
        Assert.IsTrue( building == null || road == null );
        for ( int i = 0; i < 6; i++ )
            Assert.AreEqual( this, neighbours[i].neighbours[( i + 3 ) % 6] );
        for ( int j = 0; j < 6; j++ )
            if ( startingHere[j] )
                startingHere[j].Validate();
        if ( flag )
            flag.Validate();
        if ( building )
            building.Validate();
    }

    public void Initialize( Ground ground, int x, int y )
    {
        this.ground = ground;
        this.x = x;
        this.y = y;

        neighbours[0] = ground.GetNode( x + 0, y - 1 );
        neighbours[1] = ground.GetNode( x + 1, y - 1 );
        neighbours[2] = ground.GetNode( x + 1, y + 0 );
        neighbours[3] = ground.GetNode( x + 0, y + 1 );
        neighbours[4] = ground.GetNode( x - 1, y + 1 );
        neighbours[5] = ground.GetNode( x - 1, y + 0 );
    }
}

