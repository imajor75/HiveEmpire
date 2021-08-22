using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Linq;
using UnityEditor;
using UnityEngine;

// The item has three stages while on the way to it's destination:
// 1. Waiting for a worker.
//		In this stage the item is sitting at a flag, waiting for a worker to transfer it to the next flag. So the flag member is not null, but the worker and nextFlag members are both zero
// 2. Has a worker assigned, waiting for the worker to pick it up.
//		In this stage the item is still at a flag, but it has an associated worker, and the nextFlag member is also filled. The worker is on its way to pick up the item. The item got a reserved spot at nextFlag
// 3. The item is in the hand of the worker, who is carrying it to the next flag. In this state the flag member is null, as the item is linked to the hand of the worker.

// The items are registered in three possible places:
// 1, In Player.items (every item is always registered there)
// 2. In Flag.items (if the item is sitting at a flag waiting for a worker)
// 3. In Building.itemsOnTheWay (if the item has a destination)
[SelectionBase]
public class Item : HiveObject
{
	// The item is either at a flag (the flag member is not null) or in the hands of a worker (the worker member is not null and worker.itemInHands references this object)
	// or special case: the item is a resource just created, and the worker (as a tinkerer) is about to pick it up
	public bool justCreated;    // True if the item did not yet enter the world (for example if the item is a log and in the hand of the woodcutter after chopping a tree, on the way back to the building)
	public Player owner;
	public Flag flag;           // If this is a valid reference, the item is waiting at the flag for a worker to pick it up
	public Flag nextFlag;       // If this is a valid reference, the item is on the way to nextFlag (Still could be waiting at a flag, but has an associated worker). It is also possible that this is null, but the item is in the hand of a worker. That happens when the item is on the last road of its path, and the worker will deliver it into a building, and not a flag.
	public Worker worker;		// If this is a valid reference, the item has a worker who promised to come and pick it up. Other workers will not care about the item, when it already has an associated worker.
	public Type type;
	public Path path;			// This is the current path of the item leading to the destination. The next road in this path is starting at the current flag, and the last road on this path is using the final flag. This member might be null at any point, if the previous path was destroyed for some reason (for example because the player did remove a road)
	public ItemDispatcher.Priority currentOrderPriority = ItemDispatcher.Priority.zero;	// This member shows the priority of the current order the item is fulfilling, so the destination member should not be zero. If this priority is low, the item keeps offering itself at higher priorities, so it is possible that the item changes its destination while on the way to the previous one. (for example a log is carried to a stock, but meanwhile a sawmill requests a new one)
	public Building destination;	// The current destination of the item. This could be null at any point, if the item is not needed anywhere. The game is trying to avoid it, so buildings are only spitting out items if they know that the item is needed somewhere, but the player might remove the destination building at any time. The current destination can change also if the game finds a better use for the item, except on the last road, where the item is not offered, even if it is delivered to a stock.
	public Building origin;			// This is where the item comes from. Only used for statistical and validation purposes. The game is constantly checking this reference to make sure it is valid, and if it is not, it sets it to null. Otherwise this reference might keep old buildings alive in the memory while the player already removed them from the game.
	public Watch watchRoadDelete = new Watch();
	public Watch watchBuildingDelete = new Watch();
	public bool tripCancelled;		// If this boolean is true, the items destination was lost, so the current value of the destination member is zero. It will be tudned back to false once a new destination for the item is found.
	public World.Timer life;
	public World.Timer atFlag;
	const int timeoutAtFlag = 9000;
	public Item buddy;  // This reference is used when two items at the different end of a road are swapping space, one goes to one direction, while the other one is going the opposite. 
						// Otherwise this member is null. Without this feature a deadlock at a road can very easily occur. When a worker notices that there is a possibility to swap two items, 
						// it can do that even if both flags are full without free space. In this case the two items get a reference in this member to each other. Picking up the first item is 
						// done as usual, but when the worker arrives at the next flag with the item, it will not search for a free entry at the flag (because it is possible that there is no) 
						// but swaps the item with its buddy. After this swap, delivering the second item to the first flag is happening in a normal way.
						// There are three phases of this:
						// 1. Worker realizes that two items A and B could be swapped, so marks them as buddies of each other. Walks to pick up A. During this phase, the buddy refernce of both
						// 		items are valid, referring to each other.
						// 2. A is in hand of the worker it is on its way to put down A and pick up B. During this phase A.buddy is still referencing B, indicating that it will not look for a
						// 		free slot at the flag, but replace another item. B.buddy is null, cleared in Flag.ReleaseItem
						// 3. A is already delivered, the worker is now carrying B as normal. Both A.buddy and B.buddy is null. The latter got cleared in Flag.FinalizeItem
	public int index = -1;
	public Watch roadNetworkChangeListener = new Watch();
	public float bottomHeight	// y coordinate of the plane at the bottom of the item in the parent transformation
	{
		get
		{
			return Constants.Item.bottomHeights[(int)type];
		}
		[Obsolete( "Compatibility with old files", true )]
		set {}
	}

	public Ground ground
	{
		get { return World.instance.ground; }
		[Obsolete( "Compatibility for old files", true )]
		set {}
	}

	public GameObject body;

	[JsonIgnore]
	public bool debugCancelTrip;

	static public Sprite[] sprites = new Sprite[(int)Type.total];
	public static MediaTable<GameObject, Type> looks;

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
		goldBar,
		soldier,
		total,
		unknown = -1
	}

	public static void Initialize()
	{
		object[] looksData = {
			"prefabs/items/common",
			"prefabs/items/barrel dispenser", Type.beer,
			"prefabs/items/bucket", Type.water,
			"prefabs/items/pretzel", Type.pretzel,
			"prefabs/items/plank", Type.plank,
			"prefabs/items/fish", Type.fish,
			"prefabs/items/crystal", Type.iron,
			"prefabs/items/pot", Type.gold,
			"prefabs/items/sword", Type.weapon,
			"prefabs/items/bow", Type.bow,
			"prefabs/items/animal pelt", Type.hide,
			"prefabs/items/beam cap", Type.stone,
			"prefabs/items/gold ignot", Type.goldBar,
			"prefabs/items/iron ignot", Type.steel,
			"prefabs/items/meat ribs", Type.pork,
			"prefabs/items/pouch", Type.flour,
			"prefabs/items/hay", Type.grain,
			"prefabs/items/jar", Type.salt,
			"prefabs/items/soldier", Type.soldier,
			"prefabs/items/wood", Type.log
		};
		looks.Fill( looksData );

		var dl = new GameObject( "Temporary directional light" );
		var l = dl.AddComponent<Light>();
		l.type = LightType.Directional;
		l.color = new Color( .7f, .7f, .7f );
		dl.transform.rotation = Quaternion.LookRotation( RuntimePreviewGenerator.PreviewDirection );

		RuntimePreviewGenerator.BackgroundColor = new Color( 0.5f, 0.5f, 0.5f, 0 );
		for ( int i = 0; i < (int)Type.total; i++ )
		{
			Texture2D tex = RuntimePreviewGenerator.GenerateModelPreview( looks.GetMediaData( (Type)i ).transform, 256, 256 );
			sprites[i] = Sprite.Create( tex, new Rect( 0.0f, 0.0f, tex.width, tex.height ), new Vector2( 0.5f, 0.5f ) );
			Assert.global.IsNotNull( sprites[i] );
		}

		Destroy( dl );
	}

	public static Item Create()
	{
		return new GameObject().AddComponent<Item>();
	}

	public Item Setup( Type type, Building origin, Building destination = null, ItemDispatcher.Priority priority = ItemDispatcher.Priority.zero )
	{
		this.origin = origin;
		life.Start();
		owner = origin.owner;
		justCreated = true;
		transform.SetParent( World.itemsJustCreated.transform, false );
		watchRoadDelete.Attach( owner.versionedRoadDelete );
		watchBuildingDelete.Attach( owner.versionedBuildingDelete );
		this.type = type;
		if ( destination )
		{
			if ( !SetTarget( destination, priority, origin ) )
			{
				DestroyThis();
				return null;
			}
		}
		owner.RegisterItem( this );
		return this;
	}

	new public void Start()
	{
		if ( transform.parent == null )
			transform.SetParent( World.itemsJustCreated.transform, false );	// Temporary parent until something else is not reparrenting it
		body = Instantiate( looks.GetMediaData( type ) );
		if ( Constants.Item.bottomHeights[(int)type] == float.MaxValue )
			Constants.Item.bottomHeights[(int)type] = body.GetComponent<MeshRenderer>().bounds.min.y;
		body.transform.SetParent( transform, false );
		assert.IsNotNull( body );
		name = type.ToString();
		base.Start();
	}

	public void FixedUpdate()
	{
		// This is dirty. When the origin of an item is destroyed, unity will return true when comparing it to null, however the object is still there, because the 
		// reference keeps it alive. The problem occurs when the game is saved, the destroyed building is also serialized into the file, and when the file is loaded,
		// even the unity graphics will be restored, so unity will no longer saying that the reference is null.
		if ( origin == null )
			origin = null;

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

		if ( path && !path.isFinished && path.road == null )
			CancelTrip();

		if ( watchBuildingDelete.Check() )
		{
			if ( destination == null && path )
				CancelTrip();
		}

		// If the item appears to be separated from the HQ, should not be offeted yet
		if ( roadNetworkChangeListener.isAttached )
		{
			if ( !roadNetworkChangeListener.Check() )
				return;
			roadNetworkChangeListener.Disconnect();
		}

		// If the item is just being gathered, it should not be offered yet
		if ( flag == null && worker.type != Worker.Type.hauler )
			return;

		// If there is a hauler but no nextFlag, the item is on the last road of its path, and will be delivered straight into a buildig. Too late to offer it, the hauler will not be
		// able to skip entering the building, as it is scheduled already.
		// Another rarer case when there is a worker but nextFlag is zero: a hauler is carrying the item between two flags, but something interrupted him (called ResetTasks) causing the 
		// items to cancel their trips, null destination and nextFlag. Even in this case the item should not be offered.
		if ( worker && !nextFlag )
			return; // justCreated is true here?

		// Anti-jam action. This can happen if all the following is met:
		// 1. item is waiting for too long at a flag
		// 2. flag is in front of a stock which is already built
		// 3. item is not yet routing to this building
		// 4. worker not yet started coming
		if ( flag )
		{
			foreach ( var building in flag.Buildings() )
			{
				if ( building as Stock && building.construction.done && destination != building && worker == null && atFlag.age > timeoutAtFlag )
				{
					SetTarget( building, ItemDispatcher.Priority.high );
					return;
				}
			}
		}

		var offerPriority = ItemDispatcher.Priority.zero;
		if ( currentOrderPriority == ItemDispatcher.Priority.zero )
			offerPriority = ItemDispatcher.Priority.high;
		if ( currentOrderPriority == ItemDispatcher.Priority.stock )
			offerPriority = ItemDispatcher.Priority.stock;

		if ( offerPriority == ItemDispatcher.Priority.zero )
			return;

		owner.itemDispatcher.RegisterOffer( this, offerPriority, Ground.Area.empty );
	}

	public void SetRawTarget( Building building, ItemDispatcher.Priority priority = ItemDispatcher.Priority.low )
	{
		assert.IsNull( destination );
		destination = building;
		building.ItemOnTheWay( this );
		currentOrderPriority = priority;
		tripCancelled = false;
	}

	public bool SetTarget( Building building, ItemDispatcher.Priority priority, Building origin = null )
	{
		assert.IsTrue( building != destination || priority != currentOrderPriority );

		Flag start = origin?.flag;
		if ( nextFlag )
			start = nextFlag;
		if ( flag )
			start = flag;
		if ( start == null )
			return false;

		var newPath = Path.Between( start.node, building.flag.node, PathFinder.Mode.onRoad, this );
		if ( newPath == null )
			return false;

		CancelTrip();

		path = newPath;
		// TODO Exception happened here, start was null after I left the computer running for a long time
		// item was a beer, destination is null. Item is in the hand of a worker, but it is only second in the hand
		// Trip cancelled for the item (tripCancelled is true)
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

		// path.progess == 0 if the item was rerouting while in the hands of the hauler
		if ( destination && path && path.progress != 0 )	// TODO path was null here
		{
			if ( path.isFinished )
			{
				// The path can be finished in a quite special case. There is a worker on a road, which discovers two items at the two end of the road, which would like to switch places.
				// The worker walks to A to pick it up, but in the meanwhile B rerouts to a building whose flag is the same as where the worker is picking up A. If there is no swap, the PickupItem
				// task realizes that the path of the item has been changed (it keeps the original path as a reference) but in case of switch, there is no PickupItem for B, DeliverItem handles the pick up.
				// DeliverItem has no reference to the original path, so it will pick up the item no matter, and carry it to the flag where A was originally. Coincidently this is exactly the flag B would like
				// to go, so DeliverItem thinks everything is good, and increases path.progress. Then the worker drops the item in front of the destination building, path is already finished, just one step is
				// needed to deliver the item, but since this is such a rare case, we can simply cancel the trip, and find a new destination.
				CancelTrip();
			}
			else
				assert.IsTrue( flag == path.road.ends[0] || flag == path.road.ends[1], "Path is not continuing at this flag (progress: " + path.progress + ", roads: " + path.roadPath.Count + ")" ); // TODO Triggered multiple times
		}

		worker = null;
		assert.IsTrue( destination == null || path == null || !path.isFinished || destination.flag == flag );

		atFlag.Start();
		return flag.FinalizeItem( this );
	}

	/// <summary>
	///  It also destroys the item.
	/// </summary>
	public void Arrived()
	{
		if ( flag != null )
			assert.AreEqual( destination.flag, flag );

		destination.ItemArrived( this );

		owner.UnregisterItem( this );
		DestroyThis();
	}

	public Road road
	{
		get
		{
			if ( path == null )
				return null;
			if ( path.isFinished )
				return null;
			return path.road;
		}
	}

	public bool lastRoad { get { return path != null && path.stepsLeft == 1; } }

	public override bool Remove( bool takeYourTime )
	{
		transform.SetParent( null );
		if ( worker )
		{
			for ( int i = 0; i < worker.itemsInHands.Length; i++ )
				if ( worker.itemsInHands[i] == this )
					worker.itemsInHands[i] = null;
			worker.ResetTasks();	// TODO What if the worker has a second item in hand?
		};
		CancelTrip();
		owner.UnregisterItem( this );
		if ( Constants.Item.creditOnRemove )
			owner.mainBuilding.itemData[(int)type].content++;
		DestroyThis();
		return true;
	}

	[Conditional( "Debug" )]
	public void OnDestroy()
	{
		assert.IsTrue( destination == null || !destination.itemsOnTheWay.Contains( this ) || World.massDestroy || noAssert );	// TODO Triggered randomly for a beer. 
		// It has a destination (butcher) has a valid path, no worker, nextFlag null. 
		// Current node is 10, 11, butcher is at 11, 9. 
		// No buddy. Origin: brewery at 8, 14
		// tripCancelled false, justCreated false
		// Item is still registered at the flag (last entry)
		// Flag is almost full of beer and grain

		// Triggered again for a pork, destination gold mine, origin butcher, flag nextFlag and worker are all valid
		// Flag 15, 11
		// nextFlag 19, 11
		// worker.node 15, 11
		// worker.itemInHands null
		// destination.flag 20, 15
		// origin 12, 9
		// Item is correctly registered in all 3 places, destination.itemsOnTheWay, flag.items, owner.items
	}

	public override void Reset()
	{
		assert.Fail();
	}

	public override Node location { get { return flag ? flag.location : worker.location; } }

	public override void Validate( bool chain )
	{
		if ( worker )
		{
			switch ( worker.type )
			{
				case Worker.Type.hauler:
				{
					// In some rare cases it is possible that the hauler is carrying items, but not on the road. This 
					// might happening after using the magnet on a flag which is cornered by a road. One or more nodes
					// around the flag will end up not being part of any of the new roads. If a worker was goind to that
					// node at the moment of the magnet, he will not be exclusiveMode, but might still has items in hands. In this
					// case the destination of the items is null, because Road.Split was calling Worker.ResetTasks, which was
					// calling CancelTrip for the items in hand
					if ( destination )
						assert.IsTrue( worker.exclusiveMode );
					assert.IsNotNull( worker.road );
					break;
				}
				case Worker.Type.tinkerer:
				{
					if ( !justCreated )
						assert.AreEqual( worker.itemsInHands[0], this );
					break;
				}
				case Worker.Type.unemployed:
				{
					break;
				}
				default:
				{
					assert.Fail();
					break;
				}
			}

			if ( worker.hasItems )
			{
				if ( worker.itemsInHands.Contains( this ) )
					assert.IsNull( flag );
				else
				{
					bool buddyIsCarried = false;
					foreach ( var item in worker.itemsInHands )
						if ( item?.buddy == this )
							buddyIsCarried = true;
					assert.IsTrue( buddyIsCarried );
				}
			}
		}
		if ( flag )
		{
			assert.IsTrue( flag.items.Contains( this ) );
			if ( worker )
				assert.IsFalse( worker.itemsInHands.Contains( this ) );
			if ( destination )
				assert.IsNotNull( path );	// TODO Triggered when switching a building to overclock mode, and the items had nowhere to go
			if ( destination && !path.isFinished && !path.road.invalid )
				assert.IsTrue( flag.roadsStartingHere.Contains( path.road ) );
		}
		else
		{
			if ( !justCreated )
			{
				assert.IsNotNull( worker );
				assert.IsTrue( worker.itemsInHands.Contains( this ) ); // TODO Triggered, triggered again
				// Right after loading a saved game which was working previously. The item is a hide whose has flag=null but nextFlag=23:7 that is where validate come from. 
				// 23,7 is the flag right in front of the bowmaker. The worker has no items in its hand at this moment, as it is just about to leave the building at 23:6 and get
				// back to the road at 23:7, and has an empty task queue. So it is expected that the worker has no items, as it left them in the building. The question is, why the nextFlag 
				// pointer is pointing at 23:7, and why the flag was not destroyed.

			}
		};
		if ( nextFlag )
		{
			assert.IsNotNull( worker );		// TODO Triggered after the second global reset, in the Validate call, which is happening in every frame. Hide, destination=headquarters
			assert.IsTrue( nextFlag.items.Contains( this ) || nextFlag.items.Contains( buddy ) );
		}
		else

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
