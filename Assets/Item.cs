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
	[JsonIgnore]
	public int age;
	static public Sprite[] sprites = new Sprite[(int)Type.total];
	static public Material[] materials = new Material[(int)Type.total];
	public Watch watchRoadDelete = new Watch();
	public Watch watchBuildingDelete = new Watch();
	public bool tripCancelled;
	public int born;
	public int flagTime;

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
		transform.LookAt( World.instance.eye.transform.position, -Vector3.up );
		if ( watchRoadDelete.Check() && path )
		{
			for ( int i = 0; i < path.roadPath.Count; i++ )
			{
				if ( path.roadPath[i] == null )
				{
					if ( i < path.progress )
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
		if ( flag == null && worker.type != Worker.Type.haluer )
			return;

		// If there is a hauler but no nextFlag, the item is on the last road of its path, and will be delivered into a buildig. Too late to offer it, the haluer will not be
		// able to skip entering the building, as it is scheduled already.
		if ( worker && !nextFlag )
			return;

		var priority = ItemDispatcher.Priority.stop;
		if ( destination == null )
			priority = ItemDispatcher.Priority.high;
		else if ( destination as Stock )
			priority = ItemDispatcher.Priority.stock;

		if ( destination && flag == destination.flag && worker == null )
		{
			flag.ReleaseItem( this );
			Arrived();
			return;
		}

		if ( priority == ItemDispatcher.Priority.stop )
			return;

		assert.IsNotSelected();
		owner.itemDispatcher.RegisterOffer( this, priority );
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
			destination = building;
			building.ItemOnTheWay( this );
			tripCancelled = false;
			if ( oldDestination && oldDestination != building )
				print( "Item reroute (" + type + " to " + destination.title + ")" );
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
		tripCancelled = true;
	}

	public void ArrivedAt( Flag flag )
	{
		assert.IsNull( this.flag );
		assert.AreEqual( flag, nextFlag );

		if ( destination )
		{
			// path.progess is zero if the item was rerouting while in the hands of the hauler
			assert.IsTrue( path.progress == 0 || flag == path.Road.GetEnd( 0 ) || flag == path.Road.GetEnd( 1 ), "Arrived at unknown flag (progress: " + path.progress + ", roads: " + path.roadPath.Count + ")" );
		}

		worker = null;
		assert.IsTrue( destination == null || !path.IsFinished || destination.flag == flag );

		if ( destination == null )
			CancelTrip();	// Why is this needed?

		flag.itemsStored.Trigger();
		flagTime = World.instance.time;
		this.flag = flag;
		nextFlag = null;
		assert.IsNotSelected();
	}

	public void Arrived()
	{
		if ( flag != null )
			assert.AreEqual( destination.flag, flag );
		destination.ItemArrived( this );
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
		Destroy( gameObject );
		return true;
	}

	public void Validate()
	{
		assert.IsTrue( flag != null || worker != null );
		if ( worker )
		{
			// If the item has a worker, but no nextFlag, that means that the item must be in the hand of the worker,
			// its previous destination was replaced with a new one, and the reservation at the previous nextFlag was 
			// cancelled. The worker has been not yet decided what to do with the item, so the list of tasks must be empty
			if ( ( path == null || path.StepsLeft > 1 ) && worker.taskQueue.Count > 0 ) 
				assert.IsNotNull( nextFlag, "No nextFlag for " + type + " but has a worker" );
			if ( worker.itemInHands )
				assert.AreEqual( this, worker.itemInHands );
		}
		if ( flag )
		{
			assert.IsTrue( flag.items.Contains( this ) );
			if ( destination )
				assert.IsNotNull( path );
			if ( destination && !path.IsFinished && !path.Road.invalid )
				assert.IsTrue( flag.roadsStartingHere.Contains( path.Road ) );
		}
		if ( nextFlag )
		{
			assert.IsNotNull( worker );
			assert.IsTrue( nextFlag.items.Contains( this ) );
		}
		if ( path != null )
			path.Validate();
		if ( destination )
		{
			assert.IsNotNull( path );
			assert.IsTrue( destination.itemsOnTheWay.Contains( this ) );
		}
	}
}
