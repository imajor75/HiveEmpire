using Newtonsoft.Json;
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

[SelectionBase]
public class Item : Assert.Base
{
	public Player owner;
	public Flag flag;			// If this is a valid reference, the item is waiting at the flag for a worker to pick it up
	public Flag nextFlag;		// If this is a valid reference, the item is on the way to nextFlag
	public Worker worker;
	public Type type;
	public Ground ground;
	public Path path;
	public Building destination;
	public Building origin;
	static public Sprite[] sprites = new Sprite[(int)Type.total];
	static public Material[] materials = new Material[(int)Type.total];
	public Watch watchRoadDelete = new Watch();
	public Watch watchBuildingDelete = new Watch();
	public bool tripCancelled;
	public int born;
	public int flagTime;
	const int timeoutAtFlag = 9000;
	public Item buddy;  // If this reference is not null, the target item is holding this item on it's back at nextFlag
	public int index = -1;

	[JsonIgnore]
	public bool debugCancelTrip;

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
			materials[i] = new Material( World.defaultShader );
			materials[i].SetTexture( "_MainTex", tex );
			World.SetRenderMode( materials[i], World.BlendMode.Cutout );
		}
	}

	public static Item Create()
	{
		GameObject itemBody = new GameObject();
		return itemBody.AddComponent<Item>();
	}

	public Item Setup( Type type, Building origin, Building destination = null )
	{
		this.origin = origin;
		born = World.instance.time;
		ground = origin.ground;
		owner = origin.owner;
		watchRoadDelete.Attach( owner.versionedRoadDelete );
		watchBuildingDelete.Attach( owner.versionedBuildingDelete );
		this.type = type;
		if ( destination )
		{
			if ( !SetTarget( destination, origin ) )
			{
				Destroy( gameObject );
				return null;
			}
		}
		UpdateLook();
		owner.RegisterItem( this );
		return this;
	}

	void Start()
	{
		transform.SetParent( ground.transform );
		transform.localScale *= 0.05f;
		name = type.ToString();
		UpdateLook();
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

		// Anti-jam action. This can happen if :
		// 1. item is waiting too much at a flag
		// 2. flag is in front of a stock
		// 3. item is not yet routing to this building
		// 4. worker not yet started coming
		if ( flag && flag.building as Stock && flag.building.construction.done && destination != flag.building && worker == null && timeAtFlag > timeoutAtFlag )
		{
			SetTarget( flag.building );
			return;
		}

		var priority = ItemDispatcher.Priority.stop;
		if ( destination == null )
			priority = ItemDispatcher.Priority.high;
		else if ( destination as Stock && destination.construction.done )
			priority = ItemDispatcher.Priority.stock;

		if ( priority == ItemDispatcher.Priority.stop )
			return;

		assert.IsNotSelected();
		owner.itemDispatcher.RegisterOffer( this, priority );
	}

	[JsonIgnore]
	public int timeAtFlag
	{
		get
		{
			return World.instance.time - flagTime;
		}
	}

	public bool SetTarget( Building building, Building origin = null )
	{
		assert.IsNotSelected();
		assert.AreNotEqual( building, destination );

		var oldDestination = destination;
		CancelTrip();

		Flag start = origin?.flag;
		if ( nextFlag )
			start = nextFlag;
		if ( flag )
			start = flag;

		path = Path.Between( start.node, building.flag.node, PathFinder.Mode.onRoad );
		if ( path != null )
		{
			flag?.itemsStored.Trigger();
			destination = building;
			building.ItemOnTheWay( this );
			tripCancelled = false;
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
		flag?.itemsStored.Trigger();
		tripCancelled = true;
	}

	public Item ArrivedAt( Flag flag )
	{
		assert.IsNull( this.flag );
		assert.AreEqual( flag, nextFlag );

		if ( destination && path.progress != 0 )
		{
			// path.progess is zero if the item was rerouting while in the hands of the hauler
			assert.IsFalse( path.IsFinished );
			assert.IsTrue( flag == path.Road.GetEnd( 0 ) || flag == path.Road.GetEnd( 1 ), "Patn is not continuing at this flag (progress: " + path.progress + ", roads: " + path.roadPath.Count + ")" );
		}

		worker = null;
		assert.IsTrue( destination == null || !path.IsFinished || destination.flag == flag );

		if ( destination == null )
			CancelTrip();	// Why is this needed?

		flagTime = World.instance.time;
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

	public void UpdateLook()	
	{
		if ( flag )
		{
			for ( int i = 0; i < Flag.maxItems; i++ )
			{
				if ( flag.items[i] == this )
				{
					// TODO Arrange the items around the flag
					transform.localPosition = flag.node.Position() + Vector3.up * GroundNode.size / 2 + Vector3.right * i * GroundNode.size / 10;
					return;
				}
			}
			assert.IsTrue( false );
		}
		if ( worker )
		{
			// TODO Put the item in the hand of the worker
			transform.localPosition = worker.transform.localPosition + Vector3.up * GroundNode.size / 2.5f;			;
		}
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
			assert.IsNotNull( path );
			assert.IsTrue( destination.itemsOnTheWay.Contains( this ) );
		}
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
