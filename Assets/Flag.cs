using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[SelectionBase]
public class Flag : HiveObject
{
	public Node node;
	public Item[] items = new Item[Constants.Flag.maxItems];
	public Unit user;
	public Unit candidate;				// This is null most of the time, but when a unit is trying to take exclusivity of a flag first it sets this member to itself in order to solve concurence problems
	public Game.Timer freedom = new ();	// This timer is used to prevent units allocating a flag on the same frame as it was released.
										// This is important because units should try to allocate the flag in the order how units lie in the world.hiveObjects array
	public bool crossing;
	public bool recentlyLeftCrossing;	// Only for validaion, debug purposes
	public Road[] roadsStartingHere = new Road[Constants.Node.neighbourCount];
	public Versioned itemsStored = new ();
	public Watch freeSlotsWatch = new ();
	public bool requestFlattening;
	public Building.Flattening flattening = new ();

	[JsonIgnore]
	public GameObject[] frames = new GameObject[Constants.Flag.maxItems];
	static GameObject template;
	static GameObject baseTemplate;
	GameObject tiles;
	GameObject pole;
	public SpriteRenderer onMap;

	[JsonIgnore]
	public bool wipe;

	public int roadsStartingHereCount
	{
		get
		{
			int roads = 0;
			foreach ( var road in roadsStartingHere )
				if ( road )
					roads++;
			return roads;
		}
	}

	override public UpdateStage updateMode => UpdateStage.turtle;

	override public int checksum
	{
		get
		{
			int checksum = base.checksum;
			foreach ( var item in items )
			{
				if ( item )
					checksum += item.checksum;
			}
			return checksum;
		}
	}

	[Obsolete( "Compatibility with old files", true )]
	public Player owner;
	[Obsolete( "Compatibility with old files", true )]
	Building building;

	static public void Initialize()
	{
		template = Resources.Load<GameObject>( "prefabs/others/pathPointers" );
		Assert.global.IsNotNull( template );
		baseTemplate = Resources.Load<GameObject>( "prefabs/others/flagBase" );
		Assert.global.IsNotNull( baseTemplate );
	}

	public static Flag Create()
	{
		return new GameObject().AddComponent<Flag>();
	}

	public Flag Setup( Node node, Team team, bool blueprintOnly = false, bool crossing = false, Resource.BlockHandling block = Resource.BlockHandling.block )
	{
		if ( IsNodeSuitable( node, team, ignoreBlockingResources:block != Resource.BlockHandling.block ) )
		{
			node.flag = this;
			this.node = node;
			this.team = team;
			this.blueprintOnly = blueprintOnly;
			this.crossing = crossing;
			base.Setup( node.world );
			if ( !blueprintOnly )
				team.flags.Add( this );
			freeSlotsWatch.Attach( itemsStored );
			if ( node.road && !blueprintOnly )
			{
				if ( node.road.ready )
					node.road.Split( this );
				else
				{
					assert.IsTrue( node == node.road.lastNode );
					node.road = null;
				}
			}
			if ( crossing )
				requestFlattening = true;
			if ( block == Resource.BlockHandling.remove )
				Resource.RemoveFromGround( node );
			ground.SetDirty( node );	// Remove grass
			return this;
		}
		base.Remove();
		noAssert = true;
		return null;
	}

	public override void Materialize()
	{
		team.flags.Add( this );
		base.Materialize();
		Resource.RemoveFromGround( node );
		if ( node.road )
		{
			if ( node.road.ready )
				node.road.Split( this );
			else
			{
				assert.IsTrue( node == node.road.lastNode );
				node.road = null;
			}
		}
	}

	public void SetTeam( Team team )
	{
		this.team.flags.Remove( this );
		this.team = team;
		foreach ( var item in items )
		{
			if ( item == null )
				continue;
			item.SetTeam( team );
		}
		if ( team )
			team.flags.Add( this );
	}

	public int PathDistanceFrom( Flag other )
	{
		var p = new PathFinder();
		p.FindPathBetween( node, other.node, Path.Mode.onRoad );
		return p.length;
	}

	new public void Start()
	{
		base.Start();
		if ( destroyed )
			return;
			
		name = $"Flag {node.x}:{node.y}";
		ground.Link( this );
		if ( crossing )
			pole = Instantiate( template, transform );

		tiles = Instantiate( baseTemplate );
		tiles.transform.SetParent( transform, false );

		UpdateBody();
		for ( int i = 0; i < Constants.Flag.maxItems; i++ )
		{
			frames[i] = new GameObject( "Item Frame " + i );
			var t = frames[i].transform;
			t.SetParent( transform, false );
			t.localScale = 0.15f * Vector3.one;
			Vector3 pos;
			float itemBottomHeight = items[i] == null ? 0 : items[i].bottomHeight;
			pos.x = Mathf.Sin( Mathf.PI * 2 / Constants.Flag.maxItems * i ) * Constants.Flag.itemSpread * Constants.Node.size;
			pos.z = Mathf.Cos( Mathf.PI * 2 / Constants.Flag.maxItems * i ) * Constants.Flag.itemSpread * Constants.Node.size;
			// Adjust the height of the frame so that the item in it should be just above the tiles of the flag
			pos.y = ground.GetHeightAt( node.position.x + pos.x, node.position.z + pos.z ) - t.localScale.y * itemBottomHeight - node.position.y + Constants.Flag.tilesHeight;	// TODO This is world pos, isn't it?
			assert.IsTrue( pos.y < 10000 && pos.y > -10000 );
			t.localPosition = pos;
			if ( items[i] != null )
				items[i].transform.SetParent( frames[i].transform, false );
		}

		onMap = new GameObject().AddComponent<SpriteRenderer>();
		onMap.name = "Flag on map";
		onMap.transform.SetParent( transform, false );
		onMap.transform.localPosition = Vector3.up * 3;
		onMap.transform.rotation = Quaternion.Euler( 90, 0, 0 );
		onMap.transform.localScale = Vector3.one * 0.3f;
		onMap.gameObject.layer = World.layerIndexMapOnly;
		onMap.material.renderQueue = 4003;
		onMap.sprite = Resources.Load<Sprite>( "icons/ring" );
	}

	public override void GameLogicUpdate( UpdateStage stage )
	{
		if ( requestFlattening && !flattening.builder && !blueprintOnly )
		{
			requestFlattening = false;
			if ( flattening == null )	// This should never be null, only after loading old files.
				flattening = new ();
			var area = new List<Node>();
			area.Add( node );
			foreach ( var o in Ground.areas[1] )
				if ( o )
					area.Add( node + o );
			flattening.Setup( area, false );
		}
		if ( !blueprintOnly )
			World.CRC( freeSlots + ( crossing ? Constants.Flag.maxItems : 0 ), OperationHandler.Event.CodeLocation.flagFreeSlots );
		if ( wipe )
		{
			foreach ( var item in items )
				item?.Remove();
			wipe = false;
		}
		flattening?.GameLogicUpdate( stage );
	}

	public void UpdateBody()
	{
		transform.localPosition = node.position;

		if ( tiles == null )
			return;

		var tileMesh = tiles.GetComponent<MeshFilter>().mesh;
		var gt = ground.transform;
		var vertices = tileMesh.vertices;
		var colors = new Color[vertices.Length];
		for ( int i = 0; i < vertices.Length; i++ )
		{
			var groundPosition = gt.InverseTransformPoint( tiles.transform.TransformPoint( vertices[i] ) );
			groundPosition.y = ground.GetHeightAt( groundPosition.x, groundPosition.z ) + Constants.Flag.tilesHeight;
			vertices[i] = tiles.transform.InverseTransformPoint( gt.TransformPoint( groundPosition ) );
			colors[i] = Color.black;
		}
		tileMesh.vertices = vertices;
		tileMesh.colors = colors;
	}

	public bool CaptureRoads( bool checkOnly = false )
	{
		for ( int i = 0; i < Constants.Node.neighbourCount; i++ )
		{
			Node A = node.Neighbour( i ), B = node.Neighbour( ( i + 1 ) % Constants.Node.neighbourCount );
			if ( A.road && A.road == B.road && A.road.team == team )
			{
				if ( A.road.ends[0] == this || A.road.ends[1] == this )
					continue;
				if ( !checkOnly )
					A.road.Split( this );
				return true;
			}
		}
		return false;
	}

	public void ConvertToCrossing()
	{
		assert.IsFalse( crossing );

		foreach ( var building in Buildings() )
			if ( building.type != Building.Type.headquarters )
				return;

		if ( user )
		{
			assert.AreEqual( user.exclusiveFlag, this );
			user.exclusiveFlag = null;
			user = null;
		}

		crossing = true;
		requestFlattening = true;
		pole = Instantiate( template, transform );
	}

	public void ConvertToNormal()
	{
		if ( !crossing )
			return;

		crossing = false;
		recentlyLeftCrossing = true;
		if ( pole )
		{
			Destroy( pole );
			pole = null;
		}
	}

	public bool ReleaseItem( Item item )
	{
		assert.AreEqual( item.flag, this );
		RemoveItem( item, item.buddy );

		item.flag = null;
		if ( item.buddy )
			item.buddy.buddy = null;
		return true;
	}

	public bool CancelItem( Item item )
	{
		assert.AreEqual( item.nextFlag, this );
		item.nextFlag = null;
		if ( item.buddy )
		{
			item.buddy = null;
			return true;
		}
		return RemoveItem( item );
	}

	public bool RemoveItem( Item item, Item replace = null )
	{
		for ( int i = 0; i < items.Length; i++ )
		{
			if ( items[i] == item )
			{
				items[i] = replace;
				UpdateBody();
				itemsStored.Trigger();
				return true;
			}
		}
		item.assert.Fail( "Item not found at flag" );
		return false;
	}

	public bool ReserveItem( Item item, Item replace = null )
	{
		assert.IsNull( item.nextFlag, "Item already has a flag" );
		if ( replace )
			assert.AreEqual( replace.flag, this );
		for ( int i = 0; i < items.Length; i++ )
		{
			if ( items[i] == replace )
			{
				if ( items[i] )
					items[i].buddy = item;
				else
					items[i] = item;
				item.nextFlag = this;
				UpdateBody();
				itemsStored.Trigger();
				return true;
			}
		}
		assert.Fail();
		return false;
	}

	public Item FinalizeItem( Item item )
	{
		assert.AreEqual( item.nextFlag, this );
		itemsStored.Trigger();

		item.flag = this;
		item.nextFlag = null;

		if ( item.buddy )
		{
			for ( int i = 0; i < Constants.Flag.maxItems; i++ )
			{
				if ( items[i] == item.buddy )
				{
					Item oldItem = items[i];
					items[i] = item;
					assert.IsNull( oldItem.buddy );
					oldItem.flag = null;
					item.buddy = null;
					LinkToFrame( item, i );
					return oldItem;
				}
			}
		}
		for ( int i = 0; i < Constants.Flag.maxItems; i++ )
		{
			if ( items[i] == item )
			{
				LinkToFrame( item, i );
				break;
			}
		}
		assert.IsTrue( items.Contains( item ) );
		return null;
	}

	void LinkToFrame( Item item, int frameIndex )
	{
		if ( frames[frameIndex] == null )
		{
			// This only happens rarely, when after load flag.Start is not called before this point is reached
			item.Link( transform );	
			return;
		}

		var t = frames[frameIndex].transform;
		item.Link( t );

		// Adjust the y coordinate of the frame so that the item would be just above the tiles of the flag
		Vector3 framePos = t.position;
		framePos.y = ground.GetHeightAt( framePos.x, framePos.z ) - t.localScale.y * item.bottomHeight + Constants.Flag.tilesHeight;
		assert.IsTrue( framePos.y < 10000 && framePos.y > -10000 );
		t.position = framePos;
		t.LookAt( transform );
		t.rotation *= Quaternion.Euler( Constants.Item.yawAtFlag[(int)item.type], 0, 0 );
		item.mapPosition = t.position;
	}

	public override void OnClicked( Interface.MouseButton button, bool show = false )
	{
		if ( button == Interface.MouseButton.right )
		{
			var controller = Interface.Controller.Create();
			controller.transform.SetParent( root.transform, false );
			controller.AddOption( Interface.Icon.newRoad, "Start new road from here", () => Road.StartInteractive( this ) );
			controller.AddOption( Interface.Icon.destroy, "Delete this junction", () => oh.ScheduleRemoveFlag( this ) );
			if ( CaptureRoads( true ) )
				controller.AddOption( Interface.Icon.magnet, "Capture roads running by", () => oh.ScheduleCaptureRoad( this ) );
			if ( Buildings().Count == 0 )
			{
				if ( crossing )
					controller.AddOption( Interface.Icon.crossing, "Convert to plain junction", () => oh.ScheduleChangeFlagType( this ) );
				else
					controller.AddOption( Interface.Icon.crossing, "Convert to crossing", () => oh.ScheduleChangeFlagType( this ) );
			}
			controller.Open();
		}
		if ( button == Interface.MouseButton.left )
			Interface.FlagPanel.Create().Open( this, show );
	}

	public override void Remove()
	{
		team.flags.Remove( this );
		foreach ( var building in Buildings() )
		{
			if ( building.destroyed )
				continue;
			building.Remove();
		}
		List<Road> roads = new ();
		foreach ( var road in roadsStartingHere )
			if ( road )	
				roads.Add( road );
		if ( roads.Count == 2 && roads[0] != roads[1] )
			roads[0].Merge( roads[1], this );
		else
			foreach ( var road in roads )
				if ( !road.destroyed )
					road?.Remove();	// If a road starts and ends at the same flag (making a circle) then it is listed twice in the roadsStartingHere array so it is also listed twice in the roads array, hence Remove is called twice

		foreach ( var item in items )
		{
			if ( item )		// Could be deleted (how? but it happened), hence ?. is not enought
				item.Remove();
		}

		if ( user )
		{
			assert.AreEqual( user.exclusiveFlag, this );
			user.exclusiveFlag = null;
		}
		node.flag = null;
		flattening?.Remove();
		ground.SetDirty( node );	// To allow grass
		base.Remove();
	}

	// Returns the number of available slots at the flag
	public int freeSlotsCached = -1;
	public int freeSlots
	{
		get
		{
			if ( freeSlotsWatch.status || freeSlotsCached == -1 )
			{
				freeSlotsCached = 0;
				for ( int i = 0; i < Constants.Flag.maxItems; i++ )
					if ( items[i] == null )
						freeSlotsCached++;
			}
			return freeSlotsCached;
		}
	}

	public override void Reset()
	{
		for ( int i = 0; i < Constants.Flag.maxItems; i++ )
		{
			if ( items[i] == null || items[i].flag != this )
				continue;
			items[i].Remove();
			items[i] = null;
		}
		foreach ( var road in roadsStartingHere )
			road?.Reset();
		assert.IsNull( user );
	}

	override public void Validate( bool chain )
    {
		if ( noAssert )
			return;
		foreach ( var building in Buildings() )
			assert.AreEqual( building.flag, this );
        assert.AreEqual( this, node.flag );
        for ( int i = 0; i < Constants.Node.neighbourCount; i++ )
            assert.IsNull( node.Neighbour( i ).flag );
		assert.IsTrue( freeSlots >= 0 );
		int usedSlots = 0;
		for ( int j = 0; j < Constants.Flag.maxItems; j++ )
		{
			Item i = items[j];
			if ( i )
			{
				assert.IsTrue( i.flag == this || i.nextFlag == this, "Item is here, but both flag and nextFlag pointers are referencing elsewhere (index: " + j + ")" );
				if ( chain )
					i.Validate( true );
				usedSlots++;
			}
		}
		assert.AreEqual( itemsStored, freeSlotsWatch.source, $"freeSlotsWatch not correctly connected in {this}" );
		assert.AreEqual( freeSlots, Constants.Flag.maxItems - usedSlots );
		for ( int j = 0; j < Constants.Node.neighbourCount; j++ )
			if ( roadsStartingHere[j] && roadsStartingHere[j].nodes[0] == node && chain )
				roadsStartingHere[j].Validate( true );
		if ( user )
		{
			assert.IsTrue( user.type == Unit.Type.hauler || user.type == Unit.Type.cart );
			if ( user.type == Unit.Type.hauler )
				assert.IsNotNull( user.road );
			assert.IsTrue( user.exclusiveMode );
			assert.AreEqual( user.exclusiveFlag, this );
		}
		if ( crossing )
			assert.IsNull( user );
		if ( team && !team.destroyed )
			assert.IsTrue( game.teams.Contains( team ) );
		if ( !blueprintOnly )
			assert.IsFalse( node.block.IsBlocking( Node.Block.Type.units ) );
		assert.IsTrue( blueprintOnly || team.flags.Contains( this ) );
		base.Validate( chain );
	}

	public override Node location
	{
		get
		{
			return node;
		}
	}

	/// <summary>
	/// Returns a list of buildings using this flag as their exit.
	/// </summary>
	/// <returns></returns>
	public List<Building> Buildings()
	{
		List<Building> list = new ();
		foreach ( var o in Ground.areas[1] )
		{
			if ( !o )
				continue;
			var b = node.Add( o ).building;
			if ( b && b.flag == this )
				list.Add( b );
		}
		return list;
	}

	public bool Move( int direction, bool checkOnly = false )
	{
		Node target = node.Neighbour( direction );
		if ( !IsNodeSuitable( target, team, this ) )
			return false;

		if ( Buildings().Count() > 0 )
			return false;

		List<Road> shorten = new (), change = new (), extend = new ();

		for ( int i = direction - 1; i < direction + Constants.Node.neighbourCount - 1; i++ )
		{
			int d = ( i + Constants.Node.neighbourCount ) % Constants.Node.neighbourCount;
			var road = roadsStartingHere[d];
			if ( road )
			{
				if ( i == direction )
					shorten.Add( road );
				else if ( i - direction == -1 || i - direction == 1 )
					change.Add( road );
				else
					extend.Add( road );
			}
		}
		if ( extend.Count() > 1 )
			return false;

		assert.IsTrue( shorten.Count <= 1 );
		assert.IsTrue( change.Count <= 2 );
		assert.IsTrue( extend.Count <= Constants.Node.neighbourCount - 3 );

		if ( checkOnly )
			return true;
		Resource.RemoveFromGround( target );

		void CloneRoad( Road road, Node remove, Node addition = null )
		{
			assert.IsTrue( road.ends[0] == this || road.ends[1] == this );

			var newNodes = new List<Node>( road.nodes );
			if ( remove )
				newNodes.Remove( remove );
			if ( addition )
			{
				if ( road.ends[1] == this )
					newNodes.Add( addition );
				else
					newNodes.Insert( 0, addition );
			}
			road.SplitNodeList( newNodes );
		}

		node.flag = null;
		var oldNode = node;
		node = target;
		node.flag = this;

		for ( int i = 0; i < Constants.Node.neighbourCount; i++ )
			roadsStartingHere[i] = null;

		foreach ( var road in shorten )
			CloneRoad( road, oldNode );
		foreach ( var road in change )
			CloneRoad( road, oldNode, node );
		foreach ( var road in extend )
			CloneRoad( road, null, node );

		ground.Link( this );
		UpdateBody();

		return true;
	}

	public new void OnDestroy()
	{
		if ( noAssert == false )
		{
			foreach ( var f in frames )
				if ( f )
					assert.AreEqual( f.transform.childCount, 0 );	// TODO Triggered when deleting a flag
		}
		base.OnDestroy();
	}

	static public SiteTestResult IsNodeSuitable( Node placeToBuildOn, Team team, Flag ignore = null, bool ignoreBlockingResources = true )
	{
		if ( placeToBuildOn.type == Node.Type.underWater )
			return new SiteTestResult( SiteTestResult.Result.wrongGroundType, Node.Type.aboveWater );

		if ( placeToBuildOn.block && placeToBuildOn.road == null )
		{
			bool resourceBlocking = false;
			foreach ( var resource in placeToBuildOn.resources )
			{
				if ( resource.type == Resource.Type.tree || resource.type == Resource.Type.rock || resource.type == Resource.Type.cornField || resource.type == Resource.Type.wheatField )
					resourceBlocking = true;
			}
			if ( !ignoreBlockingResources || !resourceBlocking )
				return new SiteTestResult( SiteTestResult.Result.blocked );
		}

		if ( placeToBuildOn.flag )
			return new SiteTestResult( SiteTestResult.Result.blocked );

		foreach ( var o in Ground.areas[1] )
		{
			if ( !o )
				continue;
			var otherFlag = placeToBuildOn.Add( o ).flag;
			if ( otherFlag && otherFlag != ignore )
				return new SiteTestResult( SiteTestResult.Result.flagTooClose );
		}

		if ( placeToBuildOn.team != team )
			return new SiteTestResult( SiteTestResult.Result.outsideBorder );

		return new SiteTestResult( SiteTestResult.Result.fit );
	}
}
 
