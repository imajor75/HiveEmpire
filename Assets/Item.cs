﻿using Newtonsoft.Json;
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

[SelectionBase]
public class Item : Assert.Base
{
	public Player owner;
	public Flag flag;           // If this is a valid reference, the item is waiting at the flag for a worker to pick it up
	public Flag nextFlag;       // If this is a valid reference, the item is on the way to nextFlag
	public Worker worker;
	public Type type;
	public Ground ground;
	public Path path;
	public ItemDispatcher.Priority currentOrderPriority = ItemDispatcher.Priority.zero;
	public Building destination;
	public Building origin;
	public Watch watchRoadDelete = new Watch();
	public Watch watchBuildingDelete = new Watch();
	public bool tripCancelled;
	public World.Timer life;
	public World.Timer atFlag;
	const int timeoutAtFlag = 9000;
	public Item buddy;  // If this reference is not null, the target item is holding this item on it's back at nextFlag
	public int index = -1;
	[JsonIgnore]
	public GameObject body;

	[JsonIgnore]
	public bool debugCancelTrip;

	static public Sprite[] sprites = new Sprite[(int)Type.total];
	static MediaTable<GameObject, Type> looks;

	public enum Type
    {
        log,
        stone,
        plank,
		fish,
		grain,
		flour,
		salt,
		pretzel,
		hide,
		iron,
		coal,
		gold,
		bow,
		steel,
		weapon,
		water,
		beer,
		pork,
		coin,
        total,
		unknown = -1
    }

	public static void Initialize()
	{
		string[] filenames = {
			"log",
			"rock",
			"plank",
			"fish",
			"wheat",
			"flour",
			"salt",
			"pretzel",
			"hide",
			"iron",
			"coal",
			"gold",
			"crossbow",
			"steel",
			"weapon",
			"water",
			"beer",
			"pork",
			"coin"
		};
		for ( int i = 0; i < (int)Type.total; i++ )
		{
			Texture2D tex = Resources.Load<Texture2D>( filenames[i] );
			sprites[i] = Sprite.Create( tex, new Rect( 0.0f, 0.0f, tex.width, tex.height ), new Vector2( 0.5f, 0.5f ) );
			Assert.global.IsNotNull( sprites[i] );
		}

		object[] looksData = {
			"prefabs/items/common" };
		looks.Fill( looksData );
	}

	public static Item Create()
	{
		GameObject itemBody = new GameObject();
		return itemBody.AddComponent<Item>();
	}

	public Item Setup( Type type, Building origin, Building destination = null, ItemDispatcher.Priority priority = ItemDispatcher.Priority.zero )
	{
		this.origin = origin;
		life.Start();
		ground = origin.ground;
		owner = origin.owner;
		watchRoadDelete.Attach( owner.versionedRoadDelete );
		watchBuildingDelete.Attach( owner.versionedBuildingDelete );
		this.type = type;
		if ( destination )
		{
			if ( !SetTarget( destination, priority, origin ) )
			{
				Destroy( gameObject );
				return null;
			}
		}
		owner.RegisterItem( this );
		return this;
	}

	void Start()
	{
		body = Instantiate( looks.GetMediaData( type ), transform );
		assert.IsNotNull( body );
		name = type.ToString();
	}

	void Update()
	{
		if ( debugCancelTrip )
		{
			CancelTrip();
			debugCancelTrip = false;
			return;
		}

		if ( watchRoadDelete.Check() && path )
		{
			for ( int i = 0; i < path.roadPath.Count; i++ )
			{
				if ( path.roadPath[i] == null )
				{
					if ( i < path.progress - 1 )
						path.roadPath[i] = null;
					else
					{
						CancelTrip();
						break;
					}
				}
			}
		}

		if ( path && !path.IsFinished && path.Road == null )
			CancelTrip();

		if ( watchBuildingDelete.Check() )
		{
			if ( destination == null && path )
				CancelTrip();
			if ( origin == null )
				origin = null;	// Releasing the reference when the building was removed
		}

		// If the item is just being gathered, it should not be offered yet
		if ( flag == null && worker.type != Worker.Type.hauler )
			return;

		// If there is a hauler but no nextFlag, the item is on the last road of its path, and will be delivered into a buildig. Too late to offer it, the hauler will not be
		// able to skip entering the building, as it is scheduled already.
		if ( worker && !nextFlag )
			return;

		// Anti-jam action. This can happen if all is met:
		// 1. item is waiting too much at a flag
		// 2. flag is in front of a stock which is already built
		// 3. item is not yet routing to this building
		// 4. worker not yet started coming
		if ( flag && flag.building as Stock && flag.building.construction.done && destination != flag.building && worker == null && atFlag.Age > timeoutAtFlag )
		{
			SetTarget( flag.building, ItemDispatcher.Priority.high );
			return;
		}

		var offerPriority = ItemDispatcher.Priority.zero;
		if ( currentOrderPriority == ItemDispatcher.Priority.zero )
			offerPriority = ItemDispatcher.Priority.high;
		if ( currentOrderPriority == ItemDispatcher.Priority.stock )
			offerPriority = ItemDispatcher.Priority.stock;

		if ( offerPriority == ItemDispatcher.Priority.zero )
			return;

		var area = ( origin as Workshop )?.outputArea;
		if ( area == null )
			area = new Ground.Area( flag?.node ?? worker.node, 8 );
		owner.itemDispatcher.RegisterOffer( this, offerPriority, area );
	}

	public void SetRawTarget( Building building, ItemDispatcher.Priority priority = ItemDispatcher.Priority.low )
	{
		destination = building;
		building.ItemOnTheWay( this );
		currentOrderPriority = priority;
		tripCancelled = false;
	}

	public bool SetTarget( Building building, ItemDispatcher.Priority priority, Building origin = null )
	{
		assert.IsTrue( building != destination || priority != currentOrderPriority );

		CancelTrip();

		Flag start = origin?.flag;
		if ( nextFlag )
			start = nextFlag;
		if ( flag )
			start = flag;

		path = Path.Between( start.node, building.flag.node, PathFinder.Mode.onRoad, this );
		if ( path != null )
		{
			flag?.itemsStored.Trigger();
			SetRawTarget( building, priority );
			return true;
		}
		return false;
	}

	public void CancelTrip()
	{
		path = null;

		if ( destination == null )
			return;

		destination.ItemOnTheWay( this, true );
		destination = null;
		currentOrderPriority = ItemDispatcher.Priority.zero;
		flag?.itemsStored.Trigger();
		tripCancelled = true;
	}

	public Item ArrivedAt( Flag flag )
	{
		assert.IsNull( this.flag );
		assert.AreEqual( flag, nextFlag );

		if ( destination && path.progress != 0 )	// TODO Triggered.
		{
			// path.progess is zero if the item was rerouting while in the hands of the hauler
			assert.IsFalse( path.IsFinished );
			assert.IsTrue( flag == path.Road.GetEnd( 0 ) || flag == path.Road.GetEnd( 1 ), "Path is not continuing at this flag (progress: " + path.progress + ", roads: " + path.roadPath.Count + ")" ); // TODO Triggered multiple times
			// Maybe this is happening when the hauler is exchanging two items, and the second item changes its path before the hauler would pick it up. Since no PickupItem.ExecuteFrame is called when picking
			// up the item, the hauler does not check if the item still want to go that way, and anyway it cannot do anything else than picking up the item and carrying to the other flag. So in this case the trip
			// of the item needs to be cancelled. DeliverItem.ExecuteFrame is doing this now, so maybe the bug causing the assert trigger is already fixed.
		}

		worker = null;
		assert.IsTrue( destination == null || !path.IsFinished || destination.flag == flag );

		atFlag.Start();
		return flag.FinalizeItem( this );
	}

	public void Arrived()
	{
		if ( flag != null )
			assert.AreEqual( destination.flag, flag );

		destination.ItemArrived( this );

		owner.UnregisterItem( this );
		Destroy( gameObject );
	}

	[JsonIgnore]
	public Road Road
	{
		get
		{
			if ( path == null )
				return null;
			if ( path.IsFinished )
				return null;
			return path.Road;
		}
	}

	public bool Remove()
	{
		CancelTrip();
		owner.UnregisterItem( this );
		Destroy( gameObject );
		return true;
	}

	public void Validate()
	{
		assert.IsTrue( flag != null || worker != null );
		if ( worker )
		{
			if ( worker.itemInHands )
				assert.IsTrue( worker.itemInHands == this || worker.itemInHands.buddy == this );
		}
		if ( flag )
		{
			assert.IsTrue( flag.items.Contains( this ) );
			if ( destination )
				assert.IsNotNull( path );
			if ( destination && !path.IsFinished && !path.Road.invalid )
				assert.IsTrue( flag.roadsStartingHere.Contains( path.Road ) );
		}
		else
			assert.IsNotNull( worker );
		if ( nextFlag )
		{
			assert.IsNotNull( worker );
			assert.IsTrue( nextFlag.items.Contains( this ) || nextFlag.items.Contains( buddy ) );
		}
		if ( path != null )
			path.Validate();
		if ( destination )
		{
			// During gathering the destination is the same as the origin building, otherwise there should be a path
			if ( origin != destination )
				assert.IsNotNull( path );	
			assert.IsTrue( destination.itemsOnTheWay.Contains( this ) );
			assert.IsTrue( currentOrderPriority > ItemDispatcher.Priority.zero );
		}
		else
			assert.IsTrue( currentOrderPriority == ItemDispatcher.Priority.zero );
		if ( buddy )
		{
			if ( buddy.buddy )
				assert.AreEqual( buddy.buddy, this );
			assert.IsNotNull( worker );
		}
		assert.AreNotEqual( index, -1 );
		assert.AreEqual( owner.items[index], this );
	}
}
