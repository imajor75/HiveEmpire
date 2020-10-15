using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;

[System.Serializable]
public class GroundNode : Assert.Base
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

	public enum Type
	{
		grass,
		hill,
		mountain,
		underWater
	}

	static public GroundNode Create()
	{
		return new GameObject().AddComponent<GroundNode>();
	}

	public GroundNode Setup( Ground ground, int x, int y )
	{
		this.ground = ground;
		this.x = x;
		this.y = y;

		return this;
	}

	void Start()
	{
		name = "GroundNode (" + x + ", " + y + ")";
		transform.SetParent( World.nodes.transform );
	}

	void OnDrawGizmos()
	{
#if DEBUG
		Vector3 position = Position();
		if ( ( position - SceneView.lastActiveSceneView.camera.transform.position ).magnitude > 10 )
			return;

		Gizmos.color = Color.blue;
		Gizmos.DrawCube( position, Vector3.one * 0.1f );
		Handles.Label( position, x.ToString() + ":" + y.ToString() );
#endif
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
		assert.IsTrue( i >= 0 && i < 6 );
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
				if ( chance * 100 > World.rnd.Next( 100 ) )
					n.AddResource( type );
			}
		}
	}

	public void AddResource( Resource.Type type )
	{
		if ( this.resource != null )
			return;

		if ( type == Resource.Type.coal || type == Resource.Type.iron || type == Resource.Type.stone || type == Resource.Type.gold || type == Resource.Type.salt )
		{
			if ( this.type != Type.hill && this.type != Type.mountain )
				return;
		}
		Resource resource = Resource.Create().Setup( this, type );
		if ( resource && type == Resource.Type.tree )
			resource.growth = Resource.treeGrowthMax;
	}

	public GroundNode Add( Ground.Offset o )
	{
		return ground.GetNode( x + o.x, y + o.y );
	}

	public bool IsBlocking( bool roadsBlocking = true )
	{
		if ( building )
			return true;
		if ( resource && !resource.underGround && resource.type != Resource.Type.cornfield )
			return true;
		if ( !roadsBlocking )
			return false;

		if ( resource && resource.type == Resource.Type.cornfield )
			return true;

		return flag || road;
	}

	public void SetHeight( float height )
	{
		this.height = height;
		ground.layoutVersion++;
		if ( flag )
		{
			flag.UpdateBody();
			foreach ( var road in flag.roadsStartingHere )
				road?.RebuildMesh( true );
			flag?.building?.exit.RebuildMesh( true );
		}
		if ( road )
			road.RebuildMesh( true );
		if ( resource )
			resource.UpdateBody();
		foreach ( var border in borders )
			border?.UpdateBody();

		transform.localPosition = Position();
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

	public void OnClicked()
	{
		Interface.NodePanel.Create().Open( this );
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
		if ( resource && !resource.underGround && resource.type != Resource.Type.pasturingAnimal )
			o++;
		assert.IsTrue( o == 0 || o == 1 );  // TODO Sometimes this is triggered
		if ( flag )
			assert.AreEqual( this, flag.node );
		for ( int i = 0; i < 6; i++ )
			assert.AreEqual( this, Neighbour( i ).Neighbour( ( i + 3 ) % 6 ) );
		if ( flag )
		{
			assert.AreEqual( this, flag.node );
			flag.Validate();
		}
		if ( building )
		{
			if ( !building.huge )
				assert.AreEqual( this, building.node );
			building.Validate();
		}
		if ( road )
			assert.AreEqual( this, road.nodes[roadIndex] );
		if ( resource )
		{
			assert.AreEqual( resource.node, this );
			resource.Validate();
		}
	}
}
