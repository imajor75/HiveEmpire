﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[SelectionBase]
public class Flag : HiveObject
{
	public Node node;
	public Item[] items = new Item[Constants.Flag.maxItems];
	public Worker user;
	public bool crossing;
	public bool recentlyLeftCrossing;	// Only for validaion, debug purposes
	public Road[] roadsStartingHere = new Road[Constants.Node.neighbourCount];
	public Versioned itemsStored = new Versioned();
	public Watch freeSlotsWatch = new Watch();
	public bool requestFlattening;
	public Building.Flattening flattening = new Building.Flattening();

	[JsonIgnore]
	public GameObject[] frames = new GameObject[Constants.Flag.maxItems];
	static GameObject template;
	static GameObject baseTemplate;
	GameObject tiles;
	GameObject pole;

	[Obsolete( "Compatibility with old files", true )]
	public Player owner;
	[Obsolete( "Compatibility with old files", true )]
	Building building;

	static public void Initialize()
	{
		template = (GameObject)Resources.Load( "prefabs/others/pathPointers" );
		Assert.global.IsNotNull( template );
		baseTemplate = (GameObject)Resources.Load( "prefabs/others/flagBase" );
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
			base.Setup();
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
			freeSlotsWatch.Attach( itemsStored );
			ground.SetDirty( node );	// Remove grass
			return this;
		}
		DestroyThis();
		noAssert = true;
		return null;
	}

	public override void Materialize()
	{
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

	new public void Start()
	{
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
		base.Start();
	}

	public override void CriticalUpdate()
	{
		if ( requestFlattening && !flattening.worker && !blueprintOnly )
		{
			requestFlattening = false;
			if ( flattening == null )	// This should never be null, only after loading old files.
				flattening = new Building.Flattening();
			var area = new List<Node>();
			area.Add( node );
			foreach ( var o in Ground.areas[1] )
				area.Add( node + o );
			flattening.Setup( area, false );
		}
		if ( !blueprintOnly )
			World.CRC( freeSlots + ( crossing ? Constants.Flag.maxItems : 0 ), OperationHandler.Event.CodeLocation.flagFreeSlots );
		flattening?.CriticalUpdate();
	}

	public void UpdateBody()
	{
		transform.localPosition = node.position;

		if ( tiles == null )
			return;

		var tileMesh = tiles.GetComponent<MeshFilter>().mesh;
		var gt = ground.transform;
		var vertices = tileMesh.vertices;
		for ( int i = 0; i < vertices.Length; i++ )
		{
			var groundPosition = gt.InverseTransformPoint( tiles.transform.TransformPoint( vertices[i] ) );
			groundPosition.y = ground.GetHeightAt( groundPosition.x, groundPosition.z ) + Constants.Flag.tilesHeight;
			vertices[i] = tiles.transform.InverseTransformPoint( gt.TransformPoint( groundPosition ) );
		}
		tileMesh.vertices = vertices;
	}

	public void CaptureRoads()
	{
		for ( int i = 0; i < Constants.Node.neighbourCount; i++ )
		{
			Node A = node.Neighbour( i ), B = node.Neighbour( ( i + 1 ) % Constants.Node.neighbourCount );
			if ( A.road && A.road == B.road )
			{
				if ( A.road.ends[0] == this || A.road.ends[1] == this )
					continue;
				A.road.Split( this );
				return;
			}
		}
	}

	public bool ConvertToCrossing( bool checkConditions = true )
	{
		assert.IsFalse( crossing );

		if ( checkConditions )
		{
			foreach ( var building in Buildings() )
				if ( !(building is Stock) )
					return false;
		}

		if ( user )
		{
			assert.AreEqual( user.exclusiveFlag, this );
			user.exclusiveFlag = null;
			user = null;
		}

		crossing = true;
		requestFlattening = true;
		pole = Instantiate( template, transform );
		return true;
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

	bool RemoveItem( Item item, Item replace = null )
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
					return oldItem;
				}
			}
		}
		for ( int i = 0; i < Constants.Flag.maxItems; i++ )
		{
			if ( items[i] == item )
			{
				if ( frames[i] )
				{
					var t = frames[i].transform;
					item.transform.SetParent( t, false );

					// Adjust the y coordinate of the frame so that the item would be just above the tiles of the flag
					Vector3 framePos = frames[i].transform.position;
					framePos.y = ground.GetHeightAt( framePos.x, framePos.z ) - t.localScale.y * item.bottomHeight + Constants.Flag.tilesHeight;
					assert.IsTrue( framePos.y < 10000 && framePos.y > -10000 );
					t.position = framePos;
					
					frames[i].transform.LookAt( transform );
					frames[i].transform.rotation *= Quaternion.Euler( Constants.Item.yawAtFlag[(int)items[i].type], 0, 0 );
				}
				else	// This only happens rarely, when after load flag.Start is not called before this point is reached
					item.transform.SetParent( transform, false );	
				break;
			}
		}
		assert.IsTrue( items.Contains( item ) );
		return null;
	}

	public override void OnClicked( bool show = false )
	{
		Interface.FlagPanel.Create().Open( this, show );
	}

	public override bool Remove( bool takeYourTime = false )
	{
		foreach ( var building in Buildings() )
			if ( !building.Remove( takeYourTime ) )
				return false;
		List<Road> roads = new List<Road>();
		foreach ( var road in roadsStartingHere )
			if ( road )
				roads.Add( road );
		if ( roads.Count == 2 )
			roads[0].Merge( roads[1] );
		else
			foreach ( var road in roads )
				road?.Remove( takeYourTime );

		foreach ( var item in items )
		{
			if ( item )		// Could be deleted (how? but it happened), hence ?. is not enought
				item.Remove( false );
		}

		if ( user )
		{
			assert.AreEqual( user.exclusiveFlag, this );
			user.exclusiveFlag = null;
		}
		node.flag = null;
		ground.SetDirty( node );	// To allow grass
		DestroyThis();
		return true;
	}

	// Returns the number of available slots at the flag
	public int freeSlotsCached = -1;
	public int freeSlots
	{
		get
		{
			if ( freeSlotsWatch.Check() || freeSlotsCached == -1 )
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
			items[i].Remove( false );
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
		assert.AreEqual( freeSlots, Constants.Flag.maxItems - usedSlots );
		for ( int j = 0; j < Constants.Node.neighbourCount; j++ )
			if ( roadsStartingHere[j] && roadsStartingHere[j].nodes[0] == node && chain )
				roadsStartingHere[j].Validate( true );
		if ( user )
		{
			assert.IsTrue( user.type == Worker.Type.hauler || user.type == Worker.Type.cart );
			if ( user.type == Worker.Type.hauler )
				assert.IsNotNull( user.road );
			assert.IsTrue( user.exclusiveMode );
			assert.AreEqual( user.exclusiveFlag, this );
		}
		if ( crossing )
			assert.IsNull( user );
		if ( team )
			assert.IsTrue( world.teams.Contains( team ) );
		assert.IsTrue( registered );
		if ( !blueprintOnly )
			assert.IsFalse( node.block.IsBlocking( Node.Block.Type.workers ) );
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
		List<Building> list = new List<Building>();
		foreach ( var o in Ground.areas[1] )
		{
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

		List<Road> shorten = new List<Road>(), change = new List<Road>(), extend = new List<Road>();

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

		UpdateBody();

		return true;
	}

	public override void DestroyThis( bool noAssert = false )
	{
		if ( noAssert == false )
		{
			foreach ( var f in frames )
				if ( f )
					assert.AreEqual( f.transform.childCount, 0 );	// TODO Triggered when deleting a flag
		}
		base.DestroyThis( noAssert );
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
				if ( resource.type == Resource.Type.tree || resource.type == Resource.Type.rock || resource.type == Resource.Type.cornfield )
					resourceBlocking = true;
			}
			if ( !ignoreBlockingResources || !resourceBlocking )
				return new SiteTestResult( SiteTestResult.Result.blocked );
		}

		if ( placeToBuildOn.flag )
			return new SiteTestResult( SiteTestResult.Result.blocked );

		foreach ( var o in Ground.areas[1] )
		{
			var otherFlag = placeToBuildOn.Add( o ).flag;
			if ( otherFlag && otherFlag != ignore )
				return new SiteTestResult( SiteTestResult.Result.flagTooClose );
		}

		if ( placeToBuildOn.team != team )
			return new SiteTestResult( SiteTestResult.Result.outsideBorder );

		return new SiteTestResult( SiteTestResult.Result.fit );
	}
}
 
