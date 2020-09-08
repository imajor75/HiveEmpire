using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Assertions;

[System.Serializable]
public class GroundNode
{
    public static float size = 1;
    public static int neighbourCount = 6;
    public int x, y;
    public Building building;
    public Flag flag;
    public Road road;
	public int roadIndex;
//	[JsonIgnore]
//	[HideInInspector]
//	public GroundNode[] neighbours = new GroundNode[neighbourCount];
    public float height = 0;
    public int pathFindingIndex = -1;
    public Ground ground;

    public Vector3 Position()
    {
        int rx = x-ground.width/2;
        int ry = y-ground.height/2;
        Vector3 position = new Vector3( rx*size+ry*size/2, height, ry*size );
        return position;
    }
    public static GroundNode FromPosition( Vector3 position, Ground ground )
    {
        int y = Mathf.FloorToInt( ( position.z + ( size / 2 ) ) / size );
        int x = Mathf.FloorToInt( ( position.x - y * size / 2 + ( size / 2 ) ) / size );
		return ground.GetNode( x+ground.width/2, y+ground.height/2 );
    }

    public int DirectionTo( GroundNode another )
    {
        int direction = -1;
        for ( int i = 0; i < 6; i++ )
            if ( Neighbour( i ) == another )
                direction = i;
        return direction;
    }

	public GroundNode Neighbour( int i )
	{
		Assert.IsTrue( i >= 0 && i < 6 );
		switch ( i )
		{
			case 0:
				return ground.GetNode( x + 0, y - 1 );
			case 1:
				return ground.GetNode( x + 1, y - 1 );
			case 2:
				return ground.GetNode( x + 1, y + 0 );
			case 3:
				return ground.GetNode( x + 0, y + 1 );
			case 4:
				return ground.GetNode( x - 1, y + 1 );
			case 5:
				return ground.GetNode( x - 1, y + 0 );
		}
		return null;
	}

    public void Initialize( Ground ground, int x, int y )
    {
        this.ground = ground;
        this.x = x;
        this.y = y;

        //neighbours[0] = ground.GetNode( x + 0, y - 1 );
        //neighbours[1] = ground.GetNode( x + 1, y - 1 );
        //neighbours[2] = ground.GetNode( x + 1, y + 0 );
        //neighbours[3] = ground.GetNode( x + 0, y + 1 );
        //neighbours[4] = ground.GetNode( x - 1, y + 1 );
        //neighbours[5] = ground.GetNode( x - 1, y + 0 );
    }

    public int DistanceFrom( GroundNode o )
    {
        int e = ground.height, w = ground.width;

        int h0 = Mathf.Abs(x-o.x);
        int h1 = Mathf.Abs(x-o.x-w);
        int h2 = Mathf.Abs(x-o.x+w);
        int h = Mathf.Min(Mathf.Min(h0,h1),h2);

        int v0 = Mathf.Abs(y-o.y);
        int v1 = Mathf.Abs(y-o.y-e);
        int v2 = Mathf.Abs(y-o.y+e);
        int v = Mathf.Min(Mathf.Min(v0,v1),v2);

        int d0 = Mathf.Abs(x-y-o.x+o.y+w*-1+h*-1);
        int d1 = Mathf.Abs(x-y-o.x+o.y+w*00+h*-1);
        int d2 = Mathf.Abs(x-y-o.x+o.y+w*01+h*-1);
        int d3 = Mathf.Abs(x-y-o.x+o.y+w*-1+h*00);
        int d4 = Mathf.Abs(x-y-o.x+o.y+w*00+h*00);
        int d5 = Mathf.Abs(x-y-o.x+o.y+w*01+h*00);
        int d6 = Mathf.Abs(x-y-o.x+o.y+w*-1+h*01);
        int d7 = Mathf.Abs(x-y-o.x+o.y+w*00+h*01);
        int d8 = Mathf.Abs(x-y-o.x+o.y+w*01+h*01);
        int d = Mathf.Min(Mathf.Min(Mathf.Min(Mathf.Min(Mathf.Min(Mathf.Min(Mathf.Min(Mathf.Min(d0,d1),d2),d3),d4),d5),d6),d7),d8);

        return Mathf.Max( Mathf.Max( h, v ), d );
    }

	public void Validate()
	{
		Assert.IsTrue( flag == null || road == null, "Both flag and road at the same node (" + x + ", " + y );
		Assert.IsTrue( flag == null || building == null );
		Assert.IsTrue( building == null || road == null );
		for ( int i = 0; i < 6; i++ )
			Assert.AreEqual( this, Neighbour( i ).Neighbour( ( i + 3 ) % 6 ) );
		if ( flag )
			flag.Validate();
		if ( building )
			building.Validate();
		if ( road )
			Assert.AreEqual( this, road.nodes[roadIndex] );
	}
}
