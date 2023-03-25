using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[System.Serializable]
public class Node : HiveObject
{
	public int x, y;
	public Building building;
	public Flag flag;
	public Road road;
	public List<Resource> resources = new ();
	public int roadIndex;
	public float height = 0;
	public int index = -1;
	public int influence;
	public BorderEdge[] borders = new BorderEdge[Constants.Node.neighbourCount];
	public float staticHeight = -1;
	public Type type;
	static MediaTable<GameObject, Type> decorations;
	public float decorationPosition;
	public int decorationDirection = -1;
	public int decorationType;
	public bool avoidGrass;
	public Game.Timer suspendPlanting = new ();

	override public World.UpdateStage updateMode => World.UpdateStage.none;
	public bool fixedHeight { get { return staticHeight >= 0; } set { if ( value ) staticHeight = height; else staticHeight = -1; } }
	public bool hasDecoration { get { return decorationDirection != -1; } }
	public bool real
	{
		get
		{
			return world && this == world.ground.GetNode( x, y );
		}
		[Obsolete( "Compatibility with old files", true )]
		set {}
	}

	[Obsolete( "Compatibility with old files", true )]
	bool valuable { set { simpletonDataSafe.price = value ? 1.5f : 1.0f; } }
	[Obsolete( "Compatibility with old files", true )]
	public Player owner;
	[Obsolete( "Compatibility with old files", true )]
	Resource resource { set { if ( value ) resources.Add( value ); } }

	public static Ground.Offset operator -( Node a, Node b )
	{
		var dif = new Ground.Offset( a.x - b.x, a.y - b.y, a.DistanceFrom( b ) );
		return dif.Normalize();
	}


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
		high = hill + mountain,
		aboveWater = land + high,
		anything = 0x7fffffff
	}

	public class Block
	{
		public Type type;
		public Block( Type type )
		{
			this.type = type;
		}
		public bool IsBlocking( Type type )
		{
			return ( this.type & type ) > 0;
		}
		public static implicit operator bool( Block block )
		{
			return block.type != Type.none;
		}
		public enum Type
		{
			none = 0,
			units = 1,
			buildings = 2,
			roads = 4,
			unitsAndBuildings = units+buildings,
			unitssAndRoads = units+roads,
			buildingsAndRoads = buildings+roads,
			all = units+buildings+roads
		}
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


	static public Node Create()
	{
		return new GameObject().AddComponent<Node>();
	}

	public Node Setup( Ground ground, int x, int y, System.Random rnd )
	{
		this.x = x;
		this.y = y;
		this.world = ground.world;

		if ( rnd.NextDouble() <= Constants.Node.decorationDensity )
		{
			decorationPosition = (float)( Constants.Node.decorationSpreadMin + rnd.NextDouble() * ( Constants.Node.decorationSpreadMax - Constants.Node.decorationSpreadMin ) );
			decorationDirection = rnd.Next( Constants.Node.neighbourCount );
			decorationType = rnd.Next( 1000000 );		// TODO not so nice
			// At this moment we don't really know the count for the different decorations, so a big random number is generated. MediaTable is doing a %, so it is ok
			// The reason why we use an upper limit here is to avoid the value -1, which is OK, but gives an assert fail
		}

		return this;
	}

	new public void Start()
	{
		name = $"Node {x}:{y}";
		transform.SetParent( world.nodes.transform, false );
		transform.localPosition = position;

		// Decoration
		if ( hasDecoration )
		{
			var decoration = decorations.GetMediaData( type, decorationType );
			if ( decoration )
			{
				var d = Instantiate( decoration ).transform;
				d.SetParent( world.ground.transform, false );
				var o = Neighbour( decorationDirection );
				d.localPosition = position * ( 1 - decorationPosition ) + o.GetPositionRelativeTo( this ) * decorationPosition;
				d.gameObject.layer = World.layerIndexDecorations;
			}
		}
		base.Start();
	}

	public override void Register()
	{
	}

	override public void Remove()
	{
		building?.Remove();
		flag?.Remove();
		RemoveElements( resources );
		base.Remove();
	}

	public void OnDrawGizmos()
	{
#if DEBUG
		var position = transform.position;
		if ( ( position - SceneView.lastActiveSceneView.camera.transform.position ).magnitude > 10 )
			return;

		Gizmos.color = Color.blue;
		if ( !fixedHeight )
			Gizmos.color = Color.blue.Light();
		if ( resources.Count > 0 )
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
		var position = this.position;	
		if ( world.repeating )
		{
			float limit = world.ground.dimension * Constants.Node.size / 2;
			var difference = position - reference;
			float dv0 = difference.z, dv1 = -dv0, dh0 = difference.x - difference.z / 2, dh1 = -dh0;
			if ( difference.x - difference.z / 2 > limit )
				position -= new Vector3( limit * 2, 0, 0 );
			if ( difference.z / 2 - difference.x > limit )
				position += new Vector3( limit * 2, 0, 0 );
			if ( difference.z > limit )
				position -= new Vector3( limit, 0, limit * 2 );
			if ( -difference.z > limit )
				position += new Vector3( limit, 0, limit * 2 );
		}
		return position;
	}

	/// <summary>
	/// This function returns the position of the node when another node is used as a reference. This might give different result than normal position close to the edge of the map.
	/// </summary>
	/// <param name="reference"></param>
	/// <returns></returns>
	public Vector3 GetPositionRelativeTo( Node reference )
	{
		if ( reference )
			return GetPositionRelativeTo( reference.position );
		return position;
	} 

	public Vector3 GetPosition( int x, int y )
	{
		int rx = x-world.ground.dimension/2;
		int ry = y-world.ground.dimension/2;
		Vector3 position = new Vector3( rx*Constants.Node.size+ry*Constants.Node.size/2, height, ry*Constants.Node.size );
		return position;
	}

	public Vector3 position { get { return GetPosition( x, y );	} }
	public Vector3 positionInViewport { get { return GetPositionRelativeTo( world.eye.position ); } }

	public static Node FromPosition( Vector3 position, Ground ground )
	{
		int y = Mathf.FloorToInt( ( position.z + ( Constants.Node.size / 2 ) ) / Constants.Node.size );
		int x = Mathf.FloorToInt( ( position.x - y * Constants.Node.size / 2 + ( Constants.Node.size / 2 ) ) / Constants.Node.size );
		return ground.GetNode( x + ground.dimension / 2, y + ground.dimension / 2 );
	}

	public int DirectionTo( Node another )
	{
		int direction = -1;
		for ( int i = 0; i < 6; i++ )
			if ( Neighbour( i ) == another )
				direction = i;
		return direction;
	}

	public Node Neighbour( int i )
	{
		assert.IsTrue( i >= 0 && i < 6 );
		return i switch
		{
			0 => world.ground.GetNode( x + 0, y - 1 ),
			1 => world.ground.GetNode( x + 1, y - 1 ),
			2 => world.ground.GetNode( x + 1, y + 0 ),
			3 => world.ground.GetNode( x + 0, y + 1 ),
			4 => world.ground.GetNode( x - 1, y + 1 ),
			5 => world.ground.GetNode( x - 1, y + 0 ),
			_ => null,
		};
	}

	public int DistanceFrom( Node o )
	{
		int a = world.ground.dimension / 2;
		int h = Mathf.Abs( x - o.x );
		if ( h >= a )
			h = a * 2 - h;

		int v = Mathf.Abs( y - o.y );
		if ( v >= a )
			v = a * 2 - v;

		int d = Mathf.Abs( ( x - o.x ) + ( y - o.y ) );
		if ( world.repeating && d >= world.ground.dimension )
			d -= world.ground.dimension;
		if ( world.repeating && d >= a )
			d = a * 2 - d;

		return Mathf.Max( h, Mathf.Max( v, d ) );
	}

	public float DistanceFrom( Vector3 position )
	{
		var dif = GetPositionRelativeTo( position ) - position;

		float xa = dif.x + dif.z / 2;
		float xb = dif.x - dif.z / 2;

		if ( dif.z < 0 )
			dif.z *= -1;
		if ( xa < 0 )
			xa *= -1;
		if ( xb < 0 )
			xb *= -1;

		return Math.Max( Math.Max( xa, xb ), dif.z );
	}

	public Vector3 Offset( Node another )
	{
		return another.GetPositionRelativeTo( this ) - position;
	}

	public int AddResourcePatch( Resource.Type type, int size, float density, System.Random rnd, int charges, bool overwrite = false, bool scaleCharges = true )
	{
		int count = 0;
		for ( int x = -size; x < size; x++ )
		{
			for ( int y = -size; y < size; y++ )
			{
				Node n = world.ground.GetNode( this.x + x, this.y + y );
				int distance = DistanceFrom( n );
				float chance = density * (size-distance) / size;
				if ( chance * 100 > rnd.Next( 100 ) )
				{
					var factor = (size*1.5-distance) / size;
					int localCharges = charges;
					if ( localCharges != int.MaxValue && factor < 1 && scaleCharges )
					{
						localCharges = (int)( factor * localCharges );
						if ( localCharges == 0 )
							localCharges = 1;
					}
					if ( n.AddResource( type, localCharges, overwrite ) )
						count += localCharges == int.MaxValue ? 1 : localCharges;
				}
			}
		}
		return count;
	}

	public bool AddResource( Resource.Type type, int charges, bool overwrite = false )
	{
		if ( resources.Count > 0  )
		{
			if ( !overwrite )
				return false;
			while ( resources.Count > 0 )
				resources[0].Remove();
		}
		assert.AreEqual( resources.Count, 0 );

		if ( Resource.IsUnderGround( type ) )
		{
			if ( this.type != Type.hill && this.type != Type.mountain )
				return false;
		}
		else
		{
			if ( building || flag || road )
				return false;
		}
		Resource resource = Resource.Create().Setup( this, type, charges );
		if ( resource && type == Resource.Type.tree )
			resource.life.Start( -2 * Constants.Resource.treeGrowthTime );
		return resource != null;
	}

	public Node Add( Ground.Offset o )
	{
		return world.ground.GetNode( x + o.x, y + o.y );
	}

	public Block block
	{
		get
		{
			if ( building || this.type == Type.underWater )
				return new Block( Block.Type.all );
			Block.Type type = ( road || flag ) ? Block.Type.buildingsAndRoads : Block.Type.none;
			foreach ( var resource in resources )
				type |= resource.block.type;
			return new Block( type );	// TODO Couldn't we just return some premade constant objects?
		}
	}

	public bool HasResource( Resource.Type resourceType, bool skipDepleted = false )
	{
		foreach ( var resource in resources )
			if ( resource.type == resourceType )
				if ( !skipDepleted || !resource.keepAway.inProgress )
					return true;
				
		return false;
	}

	public void SetHeight( float height )
	{
		this.height = height;

		AlignType();

		world.ground.SetDirty( this );
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
		foreach ( var resource in resources )
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
		for ( int i = 0; i < Constants.Node.neighbourCount; i++ )
			if ( Neighbour( i ).fixedHeight )
				return false;
		return true;
	}

	public void AlignType()
	{
		var settings = game.generatorSettings;
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

	public override void OnClicked( Interface.MouseButton button, bool show = false )
	{
		if ( button == Interface.MouseButton.right )
		{
			var controller = Interface.Controller.Create();
			controller.transform.SetParent( root.transform, false );
			controller.AddOption( Interface.Icon.exc, $"Type: {type}\nOwner: {team}", null );
			if ( Flag.IsNodeSuitable( this, team ) )
				controller.AddOption( Interface.Icon.junction, "Create a junction here", () => oh.ScheduleCreateFlag( this, team ) );
			if ( HasResource( Resource.Type.tree ) || HasResource( Resource.Type.rock ) || HasResource( Resource.Type.cornField ) || HasResource( Resource.Type.wheatField ) )
				controller.AddOption( Interface.Icon.destroy, "Remove rocks and vegetation", ClearVisibleResources );
			controller.Open();
			return;
		}

	#if DEBUG
		root.viewport.nodeInfoToShow = Interface.Viewport.OverlayInfoType.none;
		Interface.NodePanel.Create().Open( this, show );
	#endif
	}

	void ClearVisibleResources()
	{
		foreach ( var resource in resources )
		{
			if ( resource.type == Resource.Type.tree || resource.type == Resource.Type.cornField || resource.type == Resource.Type.wheatField || resource.type == Resource.Type.rock )
				oh.ScheduleRemoveResource( resource );
		}
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
		foreach ( var resource in resources )
			resource?.Reset();
		Validate( true );		// Be careful not to do circle validation to avoid infinite cycles
	}

	public override Node location { get { return this; } }

	public static Node operator +( Node node, Ground.Offset offset )
	{
		return node.Add( offset );
	}

	public override void Validate( bool chain )
	{
		assert.IsTrue( real );
		int o = 0;
		if ( validFlag )
			o++;
		if ( road )
			o++;
		if ( building && !building.blueprintOnly )
			o++;
		foreach ( var resource in resources )
			if ( resource.block.IsBlocking( Block.Type.roads ) )
				o++;
		assert.IsTrue( o == 0 || o == 1 );  // TODO Sometimes this is triggered
											// Triggered during mass stress test, o==2 
		// Triggered again, o == 2 there is a tree and an animalspawner has a valid flag, and a road, this is 7, 14
		// road crossing here 14,10, ... 6, 13 contains 7, 14, flag has no roads starting here, no items, but one of the items is a unity null item, flag name correct

		if ( x != world.ground.dimension && y != world.ground.dimension )
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
		foreach ( var resource in resources )
		{
			assert.AreEqual( resource.node, this );
			if ( chain )
				resource.Validate( true );
		}
	}
}
