using Newtonsoft.Json;
using UnityEditor;
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
	public Resource resource;
	public int roadIndex;
    public float height = 0;
    public int index = -1;
    public Ground ground;
	public Player owner;
	public int influence;
	public BorderEdge[] borders = new BorderEdge[GroundNode.neighbourCount];
	public bool fixedHeight;
	public Type type;
	[JsonIgnore]
	public NodeGizmo gizmo;

	public enum Type
	{
		grass,
		hill,
		mountain,
		underWater
	}

	public class NodeGizmo : MonoBehaviour
	{
		public GroundNode node;

		void OnDrawGizmos()
		{
			Vector3 position = node.Position();
			if ( ( position - SceneView.lastActiveSceneView.camera.transform.position ).magnitude > 10 )
				return;

			Gizmos.color = Color.blue;
			Gizmos.DrawCube( position, Vector3.one * 0.1f );
			Handles.Label( position, node.x.ToString()+":"+node.y.ToString() );
		}
	}

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
		gizmo = new GameObject().AddComponent<NodeGizmo>();
		gizmo.node = this;
    }

    public int DistanceFrom( GroundNode o )
    {
        //int e = ground.height, w = ground.width;

        int h = Mathf.Abs( x - o.x );
        //int h1 = Mathf.Abs(x-o.x-w);
        //int h2 = Mathf.Abs(x-o.x+w);
        //int h = Mathf.Min(Mathf.Min(h0,h1),h2);

        int v = Mathf.Abs( y - o.y );
		//int v1 = Mathf.Abs(y-o.y-e);
		//int v2 = Mathf.Abs(y-o.y+e);
		//int v = Mathf.Min(Mathf.Min(v0,v1),v2);

		//int d = Mathf.Max(h,v);
		int d = Mathf.Abs( ( x - o.x ) + ( y - o.y ) );

		return Mathf.Max( h, Mathf.Max( v, d ) );
    }

	public void AddResourcePatch( Resource.Type type, int size, float density )
	{
		for ( int x = -size; x < size; x++ )
		{
			for ( int y = -size; y < size; y++ )
			{
				GroundNode n = ground.GetNode( this.x + x, this.y + y );
				int distance = DistanceFrom( n );
				float chance = density * (size-distance) / size;
				if ( chance * 100 > Ground.rnd.Next( 100 ) )
					Resource.Create().Setup( n, type );
			}
		}
	}

	public GroundNode Add( Ground.Offset o )
	{
		return ground.GetNode( x + o.x, y + o.y );
	}

	public void SetHeight( float height )
	{
		this.height = height;
		ground.layoutVersion++;
		if ( flag )
		{
			flag.UpdateBody();
			foreach ( var road in flag.roadsStartingHere )
				road?.RebuildMesh();
			flag?.building?.exit.RebuildMesh();
		}
		if ( road )
			road.RebuildMesh();
		if ( resource )
			resource.UpdateBody();

		gizmo.transform.localPosition = Position();
	}

	public bool CanBeFlattened()
	{
		// TODO Check maximum height difference between neighbours
		// TODO Check if any border node is part of another flattening
		for ( int i = 0; i < neighbourCount; i++ )
			if ( Neighbour( i ).fixedHeight )
				return false;
		return true;
	}

	public void Validate()
	{
		int o = 0;
		if ( flag )
			o++;
		if ( road )
			o++;
		if ( building )
			o++;
		if ( resource )
			o++;
		Assert.IsTrue( o == 0 || o == 1 );
		if ( building )
			Assert.AreEqual( this, building.node );
		if ( flag )
			Assert.AreEqual( this, flag.node );
		for ( int i = 0; i < 6; i++ )
			Assert.AreEqual( this, Neighbour( i ).Neighbour( ( i + 3 ) % 6 ) );
		if ( flag )
		{
			Assert.AreEqual( this, flag.node );
			flag.Validate();
		}
		if ( building )
		{
			Assert.AreEqual( this, building.node );
			building.Validate();
		}
		if ( road )
			Assert.AreEqual( this, road.nodes[roadIndex] );
		if ( resource )
		{
			Assert.AreEqual( resource.node, this );
			resource.Validate();
		}
	}
}
