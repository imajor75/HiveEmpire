using Newtonsoft.Json;
using System;
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
	public BorderEdge[] borders = new BorderEdge[neighbourCount];
	public bool fixedHeight { get { return staticHeight >= 0; } set { if ( value ) staticHeight = height; else staticHeight = -1; } }
	public float staticHeight = -1;
	public Type type;
	static MediaTable<GameObject, Type> decorations;
	public bool hasDecoration { get { return decorationDirection != -1; } }
	public float decorationPosition;
	public int decorationDirection = -1;
	public int decorationType;
	const float decorationSpreadMin = 0.3f;
	const float decorationSpreadMax = 0.6f;
	const float decorationDensity = 0.4f;

	public Flag validFlag
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

		if ( World.rnd.NextDouble() <= decorationDensity )
		{
			decorationPosition = decorationSpreadMin + (float)World.rnd.NextDouble() * ( decorationSpreadMax - decorationSpreadMin );
			decorationDirection = World.rnd.Next( GroundNode.neighbourCount );
			decorationType = World.rnd.Next( 1000000 );		// TODO not so nice
			// At this moment we don't really know the count for the different decorations, so a big random number is generated. MediaTable is doing a %, so it is ok
			// The reason why we use an upper limit here is to avoid the value -1, which is OK, but gives an assert fail
		}

		return this;
	}

	new public void Start()
	{
		name = "GroundNode (" + x + ", " + y + ")";
		transform.SetParent( World.nodes.transform );
		transform.localPosition = position;

		// Decoration
		if ( hasDecoration )
		{
			var decoration = decorations.GetMediaData( type, decorationType );
			if ( decoration )
			{
				var d = Instantiate( decoration ).transform;
				d.SetParent( ground.FindClosestBlock( this ).transform );
				var o = Neighbour( decorationDirection );
				d.position = position * ( 1 - decorationPosition ) + o.GetPositionRelativeTo( this ) * decorationPosition;
			}
		}
		base.Start();
	}

	public void OnDrawGizmos()
	{
#if DEBUG
		if ( ( position - SceneView.lastActiveSceneView.camera.transform.position ).magnitude > 10 )
			return;

		Gizmos.color = Color.blue;
		if ( !fixedHeight )
			Gizmos.color = Color.Lerp( Color.blue, Color.white, 0.5f );
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

	public Vector3 GetPositionRelativeTo( Vector3 reference )
	{
		float limit = ground.dimension * size / 2;
		var position = this.position;	// cache
		var difference = position - reference;
		float dv0 = difference.z, dv1 = -dv0, dh0 = difference.x - difference.z / 2, dh1 = -dh0;
		if ( dh0 > dh1 && dh0 > dv0 && dh0 > dv1 )
		{
			if ( dh0 > limit )
				return position - new Vector3( limit * 2, 0, 0 );
		}
		else if ( dh1 > dv0 && dh1 > dv1 )
		{
			if ( dh1 > limit )
				return position + new Vector3( limit * 2, 0, 0 );
		}
		else if ( dv0 > dv1 )
		{
			if ( dv0 > limit )
				return position - new Vector3( limit, 0, limit * 2 );
		}
		else
		{
			if ( dv1 > limit )
				return position + new Vector3( limit, 0, limit * 2 );
		}
		return position;
	}

	/// <summary>
	/// This function returns the position of the node when another node is used as a reference. This might give different result than normal position close to the edge of the map.
	/// </summary>
	/// <param name="reference"></param>
	/// <returns></returns>
	public Vector3 GetPositionRelativeTo( GroundNode reference )
	{
		var position = this.position;
		if ( reference )
		{
			if ( reference.x - x > ground.dimension / 2 )
				position.x += ground.dimension * size;
			if ( x - reference.x > ground.dimension / 2 )
				position.x -= ground.dimension * size;
			if ( reference.y - y > ground.dimension / 2 )
			{
				position.z += ground.dimension * size;
				position.x += ground.dimension * size / 2;
			}
			if ( y - reference.y > ground.dimension / 2 )
			{
				position.z -= ground.dimension * size;
				position.x -= ground.dimension * size / 2;
			}
		}
		return position;
	}

	public Vector3 GetPosition( int x, int y )
	{
		int rx = x-ground.dimension/2;
		int ry = y-ground.dimension/2;
		Vector3 position = new Vector3( rx*size+ry*size/2, height, ry*size );
		return position;
	}

	public Vector3 position { get { 	return GetPosition( x, y );	} }

	public static GroundNode FromPosition( Vector3 position, Ground ground )
	{
		int y = Mathf.FloorToInt( ( position.z + ( size / 2 ) ) / size );
		int x = Mathf.FloorToInt( ( position.x - y * size / 2 + ( size / 2 ) ) / size );
		return ground.GetNode( x + ground.dimension / 2, y + ground.dimension / 2 );
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
		int a = ground.dimension / 2;
		int h = Mathf.Abs( x - o.x );
		if ( h >= a )
			h = a * 2 - h;

		int v = Mathf.Abs( y - o.y );
		if ( v >= a )
			v = a * 2 - v;

		int d = Mathf.Abs( ( x - o.x ) + ( y - o.y ) );
		if ( d >= a )
			d = a * 2 - d;

		return Mathf.Max( h, Mathf.Max( v, d ) );
	}

	public void AddResourcePatch( Resource.Type type, int size, float density, bool overwrite = false )
	{
		for ( int x = -size; x < size; x++ )
		{
			for ( int y = -size; y < size; y++ )
			{
				GroundNode n = ground.GetNode( this.x + x, this.y + y );
				int distance = DistanceFrom( n );
				float chance = density * (size-distance) / size;
				if ( chance * 100 > World.rnd.Next( 100 ) )
					n.AddResource( type, overwrite );
			}
		}
	}

	public void AddResource( Resource.Type type, bool overwrite = false )
	{
		if ( this.resource != null )
		{
			if ( overwrite )
				this.resource.Remove( false );
			else
				return;
		}
		assert.IsNull( this.resource );

		if ( building || flag || road )
			return;

		if ( Resource.IsUnderGround( type ) )
		{
			if ( this.type != Type.hill && this.type != Type.mountain )
				return;
		}
		Resource resource = Resource.Create().Setup( this, type );
		if ( resource && type == Resource.Type.tree )
			resource.life.Start( -2 * Resource.treeGrowthMax );
	}

	public GroundNode Add( Ground.Offset o )
	{
		return ground.GetNode( x + o.x, y + o.y );
	}

	public bool IsBlocking( bool roadsBlocking = true )
	{
		if ( building )
			return true;
		if ( resource && !resource.underGround && resource.type != Resource.Type.cornfield && resource.type != Resource.Type.fish )		// TODO There should be a bool called blocking in the resource class. Or even an enum, cornfields should block sometimes.
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

		AlignType();

		ground.SetDirty( this );
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
			location.Add( n ).flag?.UpdateBody();
		road?.RebuildMesh( true );
		resource?.UpdateBody();
		foreach ( var border in borders )
			border?.UpdateBody();
		building?.UpdateBody();

		transform.localPosition = position;
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

	public void AlignType()
	{
		var settings = ground.world.settings;
		float relativeHeight = height / settings.maxHeight;
		if ( relativeHeight < settings.waterLevel )
			type = Type.underWater;
		else if ( type == Type.underWater )
			type = Type.grass;
		if ( relativeHeight > settings.mountainLevel )
			type = Type.mountain;
		else if ( type == Type.mountain )
			type = Type.hill;
		if ( relativeHeight >= settings.hillLevel && relativeHeight < settings.mountainLevel )

			type = Type.hill;
		if ( relativeHeight < settings.hillLevel && type == Type.hill )
			type = Type.grass;
	}

	public override void OnClicked()
	{
		Interface.root.viewport.nodeInfoToShow = Interface.Viewport.NodeInfoType.none;
		Interface.NodePanel.Create().Open( this );
	}

	public bool CheckType( Type type )
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
		Validate( true );		// Be careful not to do circle validation to avoid infinite cycles
	}

	public override GroundNode location { get { return this; } }

	public static GroundNode operator +( GroundNode node, Ground.Offset offset )
	{
		return node.Add( offset );
	}

	public override void Validate( bool chain )
	{
		int o = 0;
		if ( validFlag )
			o++;
		if ( road )
			o++;
		if ( building )
			o++;
		if ( resource && !resource.underGround && resource.type != Resource.Type.pasturingAnimal && resource.type != Resource.Type.fish )
			o++;
		assert.IsTrue( o == 0 || o == 1 );  // TODO Sometimes this is triggered
		if ( x != ground.dimension && y != ground.dimension )
			for ( int i = 0; i < 6; i++ )
				assert.AreEqual( this, Neighbour( i ).Neighbour( ( i + 3 ) % 6 ) );
		if ( flag )
		{
			assert.AreEqual( this, flag.node );
			if ( chain )
				flag.Validate( true );
		}
		if ( building )
		{
			if ( !building.configuration.huge )
				assert.AreEqual( this, building.node );
			if ( chain )
				building.Validate( true );
		}
		if ( road )
			assert.AreEqual( this, road.nodes[roadIndex] );
		if ( resource )
		{
			assert.AreEqual( resource.node, this );
			if ( chain )
				resource.Validate( true );
		}
	}
}
