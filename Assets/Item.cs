using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Linq;
using UnityEditor;
using UnityEngine;

// The item has three stages while on the way to it's destination:
// 1. Waiting for a hauler.
//		In this stage the item is sitting at a flag, waiting for a hauler to transfer it to the next flag. So the flag member is not null, but the hauler and nextFlag members are both zero
// 2. Has a hauler assigned, waiting for the hauler to pick it up.
//		In this stage the item is still at a flag, but it has an associated hauler, and the nextFlag member is also filled. The hauler is on its way to pick up the item. The item got a reserved spot at nextFlag
// 3. The item is in the hand of the hauler, who is carrying it to the next flag. In this state the flag member is null, as the item is linked to the hand of the hauler.

// The items are registered in three possible places:
// 1, In Player.items (every item is always registered there)
// 2. In Flag.items (if the item is sitting at a flag waiting for a hauler)
// 3. In Building.itemsOnTheWay (if the item has a destination)
[SelectionBase]
public class Item : HiveObject
{
	// The item is either at a flag (the flag member is not null) or in the hands of a hauler (the hauler member is not null and hauler.itemInHands references this object)
	// or special case: the item is a resource just created, and the unit (as a tinkerer) is about to pick it up
	public bool justCreated;    // True if the item did not yet enter the world (for example if the item is a log and in the hand of the woodcutter after chopping a tree, on the way back to the building)
	public Flag flag;           // If this is a valid reference, the item is waiting at the flag for a unit to pick it up
	public Flag nextFlag;       // If this is a valid reference, the item is on the way to nextFlag (Still could be waiting at a flag, but has an associated unit). It is also possible that this is null, but the item is in the hand of a unit. That happens when the item is on the last road of its path, and the unit will deliver it into a building, and not a flag.
	public Unit hauler;		// If this is a valid reference, the item has a unit who promised to come and pick it up. Other units will not care about the item, when it already has an associated unit.
	public Type type;
	public Path path;			// This is the current path of the item leading to the destination. The next road in this path is starting at the current flag, and the last road on this path is using the final flag. This member might be null at any point, if the previous path was destroyed for some reason (for example because the player did remove a road)
	public ItemDispatcher.Category currentOrderPriority = ItemDispatcher.Category.zero;	// This member shows the priority of the current order the item is fulfilling, so the destination member should not be zero. If this priority is low, the item keeps offering itself at higher priorities, so it is possible that the item changes its destination while on the way to the previous one. (for example a log is carried to a stock, but meanwhile a sawmill requests a new one)
	public Building destination;	// The current destination of the item. This could be null at any point, if the item is not needed anywhere. The game is trying to avoid it, so buildings are only spitting out items if they know that the item is needed somewhere, but the player might remove the destination building at any time. The current destination can change also if the game finds a better use for the item, except on the last road, where the item is not offered, even if it is delivered to a stock.
	public Building origin;			// This is where the item comes from. Only used for statistical and validation purposes. The game is constantly checking this reference to make sure it is valid, and if it is not, it sets it to null. Otherwise this reference might keep old buildings alive in the memory while the player already removed them from the game.
	public Watch watchRoadDelete = new ();
	public Watch watchBuildingDelete = new ();
	public bool tripCancelled;		// If this boolean is true, the items destination was lost, so the current value of the destination member is zero. It will be tudned back to false once a new destination for the item is found.
	public Game.Timer life = new ();
	public Game.Timer atFlag = new ();
	const int timeoutAtFlag = 9000;
	public Item buddy;  // This reference is used when two items at the different end of a road are swapping space, one goes to one direction, while the other one is going the opposite. 
						// Otherwise this member is null. Without this feature a deadlock at a road can very easily occur. When a hauler notices that there is a possibility to swap two items, 
						// it can do that even if both flags are full without free space. In this case the two items get a reference in this member to each other. Picking up the first item is 
						// done as usual, but when the unit arrives at the next flag with the item, it will not search for a free entry at the flag (because it is possible that there is no) 
						// but swaps the item with its buddy. After this swap, delivering the second item to the first flag is happening in a normal way.
						// There are three phases of this:
						// 1. Hauler realizes that two items A and B could be swapped, so marks them as buddies of each other. Walks to pick up A. During this phase, the buddy refernce of both
						// 		items are valid, referring to each other.
						// 2. A is in hand of the hauler it is on its way to put down A and pick up B. During this phase A.buddy is still referencing B, indicating that it will not look for a
						// 		free slot at the flag, but replace another item. B.buddy is null, cleared in Flag.ReleaseItem
						// 3. A is already delivered, the hauler is now carrying B as normal. Both A.buddy and B.buddy is null. The latter got cleared in Flag.FinalizeItem
	public int index = -1;
	public Watch roadNetworkChangeListener = new ();
	public float bottomHeight	// y coordinate of the plane at the bottom of the item in the parent transformation
	{
		get
		{
			var b = Constants.Item.bottomHeights[(int)type];
			if ( b == float.MaxValue ) 
				return 0;
			return b;
		}
		[Obsolete( "Compatibility with old files", true )]
		set {}
	}

	public float tripProgress
	{
		get
		{
			if ( path == null || path.roadPath.Count == 0 )		
				return 0;

			float progress = (float)path.progress / path.roadPath.Count;
			if ( hauler && ( hauler.itemsInHands[0] == this || hauler.itemsInHands[1] == this ) && hauler.firstTask is Unit.WalkToRoadPoint )
				progress -= ( 1 - hauler.roadProgress ) / path.roadPath.Count;
			return progress;
		}
	}

	public GameObject body;
	public Transform flat;
	public Vector3 mapPosition { set { if ( flat ) flat.position = value + Vector3.up * 6; } }
	override public UpdateStage updateMode => UpdateStage.turtle;

	public Transform Link( Unit hauler, Unit.LinkType linkType )
	{
		var slot = hauler.links[(int)linkType];
		if ( slot )
		{
			transform.SetParent( slot.transform, false );
			slot.SetActive( true );
		}
		if ( flat )
		{
			flat.SetParent( hauler.transform, false );
			flat.localScale = Vector3.one * 0.15f / hauler.transform.lossyScale.x;
		}
		transform.localPosition = Vector3.zero;
		return slot?.transform;
	}

	public void Link( Transform parent )
	{
		transform.SetParent( parent, false );
		if ( flat )
		{
			flat.SetParent( transform, false );
			flat.localScale = Vector3.one * 0.15f / transform.lossyScale.x;
		}
	}

	[JsonIgnore]
	public bool debugCancelTrip;

	static public Sprite[] generatedSprites = new Sprite[(int)Type.total];
	static public MediaTable<Sprite, Type> sprites;
	public static MediaTable<GameObject, Type> looks;

	[Obsolete( "Compatibility with old files", true )]
	public Player owner;

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
		copper,
		coal,
		gold,
		silver,
		bow,
		steel,
		weapon,
		water,
		beer,
		pork,
		jewelry,
		soldier,
		apple,
		corn,
		cornFlour,
		dung,
		charcoal,
		pie,
		milk,
		egg,
		cheese,
		sling,
		friedFish,
		sterling,
		total,
		unknown = -1
	}

	public static void Initialize()
	{
		object[] looksData = {
			"prefabs/items/package",
			"prefabs/items/coal", Type.coal,
			"prefabs/items/jug", Type.water,
			"prefabs/items/barrel dispenser", Type.beer,
			"prefabs/items/pretzel", Type.pretzel,
			"prefabs/items/plank", Type.plank,
			"prefabs/items/billy", Type.milk,
			"prefabs/items/crate", Type.charcoal,
			"prefabs/items/fish", Type.fish,
			"prefabs/items/crystal", Type.iron,
			"prefabs/items/gun", Type.sling,
			"prefabs/items/spice", Type.copper,
			"prefabs/items/pot", Type.gold,
			"prefabs/items/bucket", Type.silver,
			"prefabs/items/sword", Type.weapon,
			"prefabs/items/bow", Type.bow,
			"prefabs/items/animal pelt", Type.hide,
			"prefabs/items/beam cap", Type.stone,
			"prefabs/items/roasted fish", Type.friedFish,
			"prefabs/items/chest", Type.jewelry,
			"prefabs/items/iron ignot", Type.steel,
			"prefabs/items/gold ignot", Type.sterling,
			"prefabs/items/basket", Type.egg,
			"prefabs/items/meat ribs", Type.pork,
			"prefabs/items/pouch", Type.flour,
			"prefabs/items/hay", Type.grain,
			"prefabs/items/pie", Type.pie,
			"prefabs/items/jar", Type.salt,
			"prefabs/items/soldier", Type.soldier,
			"prefabs/items/apple", Type.apple,
			"prefabs/items/sack", Type.corn,
			"prefabs/items/jar2", Type.cornFlour,
			"prefabs/items/sack open", Type.dung,
			"prefabs/items/cheese", Type.cheese,
			"prefabs/items/wood", Type.log
		};
		looks.Fill( looksData );

		sprites.Fill();
		sprites.fileNamePrefix = "sprites/items/";
		sprites.missingMediaHandler = ( type ) => generatedSprites[(int)type];

		var dl = new GameObject( "Temporary Directional Light" );
		var l = dl.AddComponent<Light>();
		l.type = LightType.Directional;
		l.color = new Color( .7f, .7f, .7f );
		dl.transform.rotation = Quaternion.LookRotation( RuntimePreviewGenerator.PreviewDirection );

		RuntimePreviewGenerator.BackgroundColor = new Color( 0.5f, 0.5f, 0.5f, 0 );
		for ( int i = 0; i < (int)Type.total; i++ )
		{
			Texture2D tex = RuntimePreviewGenerator.GenerateModelPreview( looks.GetMediaData( (Type)i ).transform, 256, 256 );
			generatedSprites[i] = Sprite.Create( tex, new Rect( 0.0f, 0.0f, tex.width, tex.height ), new Vector2( 0.5f, 0.5f ) );
			Assert.global.IsNotNull( generatedSprites[i] );
		}

		Eradicate( dl );
	}

	public static Item Create()
	{
		return new GameObject().AddComponent<Item>();
	}

	public Item Setup( Type type, Building origin, Building destination = null, ItemDispatcher.Category priority = ItemDispatcher.Category.zero )
	{
		assert.AreNotEqual( type, Item.Type.soldier );
		this.origin = origin;
		life.Start();
		team = origin.team;
		justCreated = true;
		transform.SetParent( origin.world.itemsJustCreated.transform, false );
		transform.localPosition = Vector3.down * 100;
		watchRoadDelete.Attach( team.versionedRoadDelete );
		watchBuildingDelete.Attach( team.versionedBuildingDelete );
		this.type = type;
		if ( destination )
		{
			if ( !SetTarget( destination, priority, origin ) )
			{
				base.Remove();
				return null;
			}
		}
		team.RegisterItem( this );
		base.Setup( origin.world );
		return this;
	}

	new public void Start()
	{
		if ( transform.parent == null )
			transform.SetParent( world.itemsJustCreated.transform, false );	// Temporary parent until something else is not reparrenting it
		body = Instantiate( looks.GetMediaData( type ) );
		body.layer = World.layerIndexItems;

		flat = new GameObject( "Item in flat mode").transform;
		var sr = flat.gameObject.AddComponent<SpriteRenderer>();
		flat.SetParent( transform, false );
		flat.localPosition = Vector3.up * 6;
		flat.rotation = Quaternion.Euler( 90, 0, 0 );
		flat.gameObject.layer = World.layerIndexMapOnly;
		sr.material.renderQueue = 4003;
		sr.sprite = sprites.GetMediaData( type );

		var s = new GameObject( "2d item" ).AddComponent<SpriteRenderer>();
		s.transform.SetParent( flat, false );
		s.material.renderQueue = 4003;
		s.sprite = sprites.GetMediaData( type );
		s.gameObject.layer = Constants.World.layerIndex2d;

		if ( Constants.Item.bottomHeights[(int)type] == float.MaxValue )
			Constants.Item.bottomHeights[(int)type] = body.GetComponent<MeshRenderer>().bounds.min.y;
		body.transform.SetParent( transform, false );
		assert.IsNotNull( body );
		name = type.ToString();
		base.Start();
	}

	public enum DistanceType
	{
		roadCount,
		stepCount,
		stepsAsCrowFly
	}

	public int DistanceFromDestination( DistanceType type, bool full = false )
	{
		if ( destination == null || path == null )
			return -1;

		if ( type == DistanceType.roadCount )
			return path.roadPath.Count - ( full ? 0 : path.progress );

		if ( type == DistanceType.stepsAsCrowFly )
		{
			if ( full && origin == null )
				return 0;
			var start = full ? origin.location : location;
			return start.DistanceFrom( destination.location );
		}

		int distance = 0, segment = 0;

		if ( !full )
		{
			if ( path.progress > 0 )
			{
				var currentRoad = path.roadPath[path.progress - 1];
				var currentLocation = location;
				for ( int i = 0; i < currentRoad.nodes.Count; i++ )
				{
					if ( currentRoad.nodes[i] == currentLocation )
						distance = path.roadPathReversed[path.progress - 1] ? i : currentRoad.length - i;
				}
			}
			segment = path.progress;
		}

		while ( segment < path.roadPath.Count )
			distance += path.roadPath[segment++].length;

		return distance;
	}

	new void Update()
	{
		if ( eye.isMapModeUsed )
			flat.rotation = Quaternion.Euler( 90, (float)( eye.direction / Math.PI * 180 ), 0 );
		base.Update();
	}

	public override void GameLogicUpdate( UpdateStage stage )
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

		if ( watchRoadDelete.status && path != null )
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

		if ( path != null && !path.isFinished && path.road == null )
			CancelTrip();

		if ( watchBuildingDelete.status )
		{
			if ( destination == null && path != null )
				CancelTrip();
		}

		// If everything goes well, the roadNetworkChangeListener watch is not attached to a version
		// but if the delivery of the item fails to some destination (probably due to no path) this
		// watch gets connected, and then the item should not be offered as long as the watch is not
		// triggered. It is also possible that the watch is not attached, but unity creates a new 
		// instance of the Versioned class and puts that reference to roadNetworkChangeListener.source
		// creating a fake assigment state of the watch. No nice way to check this as far as I know
		if ( roadNetworkChangeListener.isAttached && roadNetworkChangeListener.source.version != 0 )
		{
			if ( !roadNetworkChangeListener.status )
				return;
			roadNetworkChangeListener.Disconnect();
		}

		// If the item is just being gathered, it should not be offered yet
		if ( flag == null && hauler.type != Unit.Type.hauler )
			return;

		// If there is a hauler but no nextFlag, the item is on the last road of its path, and will be delivered straight into a buildig. Too late to offer it, the hauler will not be
		// able to skip entering the building, as it is scheduled already.
		// Another rarer case when there is a hauler but nextFlag is zero: a hauler is carrying the item between two flags, but something interrupted him (called ResetTasks) causing the 
		// items to cancel their trips, null destination and nextFlag. Even in this case the item should not be offered.
		if ( hauler && !nextFlag )
			return; // justCreated is true here?

		// Anti-jam action. This can happen if all the following is met:
		// 1. item is waiting for too long at a flag
		// 2. flag is in front of a stock which is already built
		// 3. item is not yet routing to this building
		// 4. hauler not yet started coming
		if ( flag )
		{
			foreach ( var building in flag.Buildings() )
			{
				if ( building is Stock && building.construction.done && destination != building && hauler == null && atFlag.age > timeoutAtFlag )
				{
					SetTarget( building, ItemDispatcher.Category.work );
					return;
				}
			}
		}

		var offerPriority = ItemDispatcher.Category.zero;
		if ( currentOrderPriority == ItemDispatcher.Category.zero )
			offerPriority = ItemDispatcher.Category.work;
		if ( currentOrderPriority == ItemDispatcher.Category.reserve )
			offerPriority = ItemDispatcher.Category.reserve;

		if ( offerPriority == ItemDispatcher.Category.zero )
			return;

		team.itemDispatcher.RegisterOffer( this, offerPriority, Ground.Area.empty );
	}

	public void SetTeam( Team team )
	{
		this.team.UnregisterItem( this );
		this.team = team;
		CancelTrip();
		if ( team )
			team.RegisterItem( this );
	}

	public void SetRawTarget( Building building, ItemDispatcher.Category priority = ItemDispatcher.Category.work )
	{
		assert.IsNull( destination );
		destination = building;
		building.ItemOnTheWay( this );
		currentOrderPriority = priority;
		tripCancelled = false;
	}

	public bool SetTarget( Building building, ItemDispatcher.Category priority, Building origin = null )
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
		// item was a beer, destination is null. Item is in the hand of a hauler, but it is only second in the hand
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
		currentOrderPriority = ItemDispatcher.Category.zero;
		flag?.itemsStored.Trigger();
		tripCancelled = true;
	}

	public Item ArrivedAt( Flag flag )
	{
		assert.IsNull( this.flag );
		assert.AreEqual( flag, nextFlag );

		// path.progess == 0 if the item was rerouting while in the hands of the hauler
		if ( destination && path != null && path.progress != 0 )	// TODO path was null here
		{
			if ( path.isFinished )
			{
				// The path can be finished in a quite special case. There is a hauler on a road, which discovers two items at the two end of the road, which would like to switch places.
				// The hauler walks to A to pick it up, but in the meanwhile B rerouts to a building whose flag is the same as where the hauler is picking up A. If there is no swap, the PickupItem
				// task realizes that the path of the item has been changed (it keeps the original path as a reference) but in case of switch, there is no PickupItem for B, DeliverItem handles the pick up.
				// DeliverItem has no reference to the original path, so it will pick up the item no matter, and carry it to the flag where A was originally. Coincidently this is exactly the flag B would like
				// to go, so DeliverItem thinks everything is good, and increases path.progress. Then the hauler drops the item in front of the destination building, path is already finished, just one step is
				// needed to deliver the item, but since this is such a rare case, we can simply cancel the trip, and find a new destination.
				CancelTrip();
			}
			else
				assert.IsTrue( flag == path.road.ends[0] || flag == path.road.ends[1], "Path is not continuing at this flag (progress: " + path.progress + ", roads: " + path.roadPath.Count + ")" ); // TODO Triggered multiple times
		}

		hauler = null;
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

		team.UnregisterItem( this );
		Eradicate( flat.gameObject );
		base.Remove();
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

	public override void Remove()
	{
		if ( destroyed )
			return;
		destroyed = true;
			
		transform.SetParent( null );
		if ( hauler )
		{
			for ( int i = 0; i < hauler.itemsInHands.Length; i++ )
				if ( hauler.itemsInHands[i] == this )
					hauler.itemsInHands[i] = null;
			hauler.ResetTasks();	// TODO What if the hauler has a second item in hand?
		};
		CancelTrip();
		Eradicate( flat );
		flag?.RemoveItem( this );
		team.UnregisterItem( this );
		if ( Constants.Item.creditOnRemove )
		{
			team.mainBuilding.itemData[(int)type].content++;
			team.mainBuilding.contentChange.Trigger();
		}
		base.Remove();
	}

	[Conditional( "Debug" )]
	public new void OnDestroy()
	{
		assert.IsTrue( destination == null || !destination.itemsOnTheWay.Contains( this ) || World.massDestroy || noAssert, "Item on the way is destroyed" );	// TODO Triggered randomly for a beer. 
		// It has a destination (butcher) has a valid path, no unit, nextFlag null. 
		// Current node is 10, 11, butcher is at 11, 9. 
		// No buddy. Origin: brewery at 8, 14
		// tripCancelled false, justCreated false
		// Item is still registered at the flag (last entry)
		// Flag is almost full of beer and grain

		// Triggered again for a pork, destination gold mine, origin butcher, flag nextFlag and unit are all valid
		// Flag 15, 11
		// nextFlag 19, 11
		// unit.node 15, 11
		// unit.itemInHands null
		// destination.flag 20, 15
		// origin 12, 9
		// Item is correctly registered in all 3 places, destination.itemsOnTheWay, flag.items, owner.items
		base.OnDestroy();
	}

	public override void Reset()
	{
		assert.Fail();
	}

	public override Node location { get { return flag ? flag.location : hauler.location; } }

	public override void Validate( bool chain )
	{
		if ( hauler )
		{
			switch ( hauler.type )
			{
				case Unit.Type.hauler:
				{
					// In some rare cases it is possible that the hauler is carrying items, but not on the road. This 
					// might happening after using the magnet on a flag which is cornered by a road. One or more nodes
					// around the flag will end up not being part of any of the new roads. If a hauler was goind to that
					// node at the moment of the magnet, he will not be exclusiveMode, but might still has items in hands. In this
					// case the destination of the items is null, because Road.Split was calling hauler.ResetTasks, which was
					// calling CancelTrip for the items in hand
					if ( destination )
						assert.IsTrue( hauler.exclusiveMode );
					assert.IsNotNull( hauler.road );
					break;
				}
				default:
				{
					break;
				}
			}

			if ( hauler.hasItems )
			{
				if ( hauler.itemsInHands.Contains( this ) )
					assert.IsNull( flag );
				else
				{
					bool buddyIsCarried = false;
					foreach ( var item in hauler.itemsInHands )
						if ( item?.buddy == this )
							buddyIsCarried = true;
					assert.IsTrue( buddyIsCarried );
				}
			}
		}
		if ( flag )
		{
			assert.IsTrue( flag.items.Contains( this ) );
			assert.AreEqual( flag.team, team );
			if ( hauler )
				assert.IsFalse( hauler.itemsInHands.Contains( this ) );
			if ( destination )
				assert.IsNotNull( path );	// TODO Triggered when switching a building to overclock mode, and the items had nowhere to go
			if ( destination && !path.isFinished && path.road && !path.road.destroyed )
				assert.IsTrue( flag.roadsStartingHere.Contains( path.road ) );
		}
		else
		{
			if ( !justCreated )
			{
				assert.IsNotNull( hauler );
				assert.IsTrue( hauler.itemsInHands.Contains( this ) ); // TODO Triggered, triggered again
				// Right after loading a saved game which was working previously. The item is a hide whose has flag=null but nextFlag=23:7 that is where validate come from. 
				// 23,7 is the flag right in front of the bowmaker. The hauler has no items in its hand at this moment, as it is just about to leave the building at 23:6 and get
				// back to the road at 23:7, and has an empty task queue. So it is expected that the hauler has no items, as it left them in the building. The question is, why the nextFlag 
				// pointer is pointing at 23:7, and why the flag was not destroyed.

			}
		};
		if ( nextFlag )
		{
			assert.IsNotNull( hauler );		// TODO Triggered after the second global reset, in the Validate call, which is happening in every frame. Hide, destination=headquarters
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
			assert.IsTrue( currentOrderPriority > ItemDispatcher.Category.zero );
		}
		else
			assert.IsTrue( currentOrderPriority == ItemDispatcher.Category.zero );
		if ( buddy )
		{
			if ( buddy.buddy )
				assert.AreEqual( buddy.buddy, this );
			assert.IsNotNull( hauler );
		}
		assert.AreNotEqual( index, -1 );
		assert.AreEqual( team.items[index], this );
		assert.IsTrue( team.destroyed || game.teams.Contains( team ), $"Owner team {team} not valid for {this}" );	// TODO Fires when capturing the main building of a team
		base.Validate( chain );
	}
}
