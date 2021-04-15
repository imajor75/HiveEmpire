using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;

[System.Serializable]
public class GroundNode : HiveObject
{
	public const float size = 1;
	public const int neighbourCount = 6;
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
	static MediaTable<GameObject, Type> decorations;
	const float decorationSpreadMin = 0.3f;
	const float decorationSpreadMax = 0.6f;
	const float decorationDensity = 0.08f;

	[JsonIgnore]
	public Flag ValidFlag
	{
		get
		{
			if ( flag && !flag.blueprintOnly )
				return flag;
			return null;
		}
	}

	public enum Type
	{
		grass = 1,
		hill = 2,
		mountain = 4,
		forest = 8,
		underWater = 16,
		land = grass + forest,
		high = hill + mountain
	}

	public static void Initialize()
	{
		object[] decorationData = {
			"prefabs/ground/bush00", Type.grass,
			"prefabs/ground/bush01", Type.grass,
			"prefabs/ground/bush02", Type.grass,
			"prefabs/ground/fern00", Type.forest,
			"prefabs/ground/fern01", Type.forest,
			"prefabs/ground/fern02", Type.forest,
			"prefabs/ground/flower01", Type.grass,
			"prefabs/ground/flower02", Type.grass,
			"prefabs/ground/flower03", Type.grass,
			"prefabs/ground/flower04", Type.grass,
			"prefabs/ground/flower00", Type.grass,
			"prefabs/ground/grassTuft00", Type.grass,
			"prefabs/ground/grassTuft01", Type.grass,
			"prefabs/ground/grassTuft02", Type.grass,
			"prefabs/ground/herb", Type.forest,
			"prefabs/ground/ivy", Type.forest,
			"prefabs/ground/leaves", Type.forest,
			"prefabs/ground/mushroom00", Type.forest,
			"prefabs/ground/mushroom01", Type.forest,
			"prefabs/ground/reeds", Type.forest,
			"prefabs/ground/treeBurnt", Type.forest,
			"prefabs/ground/treeDead", Type.forest,
			"prefabs/ground/woodPile", Type.forest,
			"prefabs/ground/rock00", Type.hill,
			"prefabs/ground/rock01", Type.hill,
			"prefabs/ground/rock02", Type.hill,
			"prefabs/ground/rock03", Type.hill,
			"prefabs/ground/flower05", Type.hill,
			"prefabs/ground/crystal", Type.hill,
			"prefabs/ground/oreIron", Type.hill
		};
		decorations.Fill( decorationData );
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

	public void Start()
	{
		name = "GroundNode (" + x + ", " + y + ")";
		transform.SetParent( World.nodes.transform );
		transform.localPosition = Position;

		// Decoration
		World.rnd = new System.Random( 1000 * x + y );
		for ( int i = 0; i < neighbourCount / 2; i++ )
		{
			if ( World.rnd.NextDouble() > decorationDensity )
				continue;

			var decoration = decorations.GetMediaData( type );
			if ( decoration )
			{
				var d = Instantiate( decoration ).transform;
				d.SetParent( transform, false );
				var o = Neighbour( i );
				var l = decorationSpreadMin + (float)World.rnd.NextDouble() * ( decorationSpreadMax - decorationSpreadMin );
				d.position = Position * ( 1 - l ) + o.Position * l;
			}
		}
	}

	public void OnDrawGizmos()
	{
#if DEBUG
		Vector3 position = Position;
		if ( ( position - SceneView.lastActiveSceneView.camera.transform.position ).magnitude > 10 )
			return;

		Gizmos.color = Color.blue;
		if ( resource )
			Gizmos.color = Color.magenta;
		if ( building )
			Gizmos.color = Color.green;
		if ( flag )
			Gizmos.color = Color.red;
		if ( road )
			Gizmos.color = Color.yellow;
		Gizmos.DrawCube( position, Vector3.one * 0.1f );
		Handles.Label( position + Vector3.one * 0.1f, x.ToString() + ":" + y.ToString() );
#endif
	}

	[JsonIgnore]
	public Vector3 Position
	{
		get
		{
			int rx = x-ground.width/2;
			int ry = y-ground.height/2;
			Vector3 position = new Vector3( rx*size+ry*size/2, height, ry*size );
			return position;
		}
	}

	public static GroundNode FromPosition( Vector3 position, Ground ground )
	{
		int y = Mathf.FloorToInt( ( position.z + ( size / 2 ) ) / size );
		int x = Mathf.FloorToInt( ( position.x - y * size / 2 + ( size / 2 ) ) / size );
		return ground.GetNode( x + ground.width / 2, y + ground.height / 2 );
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
		return i switch
		{
			0 => ground.GetNode( x + 0, y - 1 ),
			1 => ground.GetNode( x + 1, y - 1 ),
			2 => ground.GetNode( x + 1, y + 0 ),
			3 => ground.GetNode( x + 0, y + 1 ),
			4 => ground.GetNode( x - 1, y + 1 ),
			5 => ground.GetNode( x - 1, y + 0 ),
			_ => null,
		};
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

	public void AddResourcePatch( Resource.Type type, int size, float density, bool overwrite = false, bool expose = false )
	{
		for ( int x = -size; x < size; x++ )
		{
			for ( int y = -size; y < size; y++ )
			{
				GroundNode n = ground.GetNode( this.x + x, this.y + y );
				int distance = DistanceFrom( n );
				float chance = density * (size-distance) / size;
				if ( chance * 100 > World.rnd.Next( 100 ) )
					n.AddResource( type, overwrite, expose );
			}
		}
	}

	public void AddResource( Resource.Type type, bool overwrite = false, bool expose = false )
	{
		if ( this.resource != null )
		{
			if ( overwrite )
				this.resource.Remove( false );
			else
				return;
		}
		assert.IsNull( this.resource );

		if ( type == Resource.Type.coal || type == Resource.Type.iron || type == Resource.Type.stone || type == Resource.Type.gold || type == Resource.Type.salt )
		{
			if ( this.type != Type.hill && this.type != Type.mountain )
				return;
		}
		Resource resource = Resource.Create().Setup( this, type );
		if ( resource && type == Resource.Type.tree )
			resource.life.Start( -2 * Resource.treeGrowthMax );

		if ( resource && expose )
			resource.exposed.Start( Resource.exposeMax );
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
		// TODO Dont rebuild the whole mesh
		this.height = height;
		ground.layoutVersion++;
		if ( flag )
		{
			flag.UpdateBody();
			foreach ( var road in flag.roadsStartingHere )
				road?.RebuildMesh( true );
			if ( flag )
			{
				foreach ( var building in flag.Buildings() )
					building.exit?.RebuildMesh( true );
			}
		}
		foreach ( var n in Ground.areas[1] )
			Node.Add( n ).flag?.UpdateBody();
		road?.RebuildMesh( true );
		resource?.UpdateBody();
		foreach ( var border in borders )
			border?.UpdateBody();
		building?.UpdateBody();

		transform.localPosition = Position;
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

	public override void OnClicked()
	{
		Interface.root.viewport.showPossibleBuildings = false;
		Interface.NodePanel.Create().Open( this );
	}

	[JsonIgnore]
	public int Id
	{
		get
		{
			return x + y * ground.width;
		}
	}

	public bool CheckType( GroundNode.Type type )
	{
		if ( ( this.type & type ) > 0 )
			return true;

		return false;
	}

	public override void Reset()
	{
		building?.Reset();
		flag?.Reset();
		resource?.Reset();
		Validate();
	}

	public override GroundNode Node { get { return this; } }

	public static GroundNode operator +( GroundNode node, Ground.Offset offset )
	{
		return node.Add( offset );
	}

	public override void Validate()
	{
		int o = 0;
		if ( ValidFlag )
			o++;
		if ( road )
			o++;
		if ( building )
			o++;
		if ( resource && !resource.underGround && resource.type != Resource.Type.pasturingAnimal )
			o++;
		assert.IsTrue( o == 0 || o == 1 );  // TODO Sometimes this is triggered
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
