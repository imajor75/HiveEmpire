using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

[SelectionBase]
abstract public class Building : Assert.Base
{
	public Player owner;
	public Worker worker;
	public Flag flag;
	public Ground ground;
	public GroundNode node;
	[JsonIgnore]
	public Road exit;
	[JsonIgnore]
	public List<MeshRenderer> renderers;
	public Construction construction = new Construction();
	static int flatteningTime = 300;
	public float height = 1.5f;
	public static Ground.Offset flagOffset = new Ground.Offset( 1, -1, 1 );
	public List<Item> itemsOnTheWay = new List<Item>();

	[System.Serializable]
	public class Construction
	{
		public Building boss;
		public bool done;
		public float progress;
		public int plankNeeded;
		public int plankOnTheWay;
		public int plankArrived;
		public int stoneNeeded;
		public int stoneOnTheWay;
		public int stoneArrived;
		public Worker worker;
		public static Shader shader;
		public static int sliceLevelID;
		public int timeSinceCreated;
		public bool flatteningNeeded;
		public int flatteningCorner;
		public int flatteningCounter;
		public GroundNode.Type groundTypeNeeded = GroundNode.Type.grass;

		static public void Initialize()
		{
			shader = (Shader)Resources.Load( "Construction" );
			Assert.global.IsNotNull( shader );
			sliceLevelID = Shader.PropertyToID( "_SliceLevel" );
		}

		public void Setup( Building boss )
		{
			this.boss = boss;
			if ( flatteningNeeded )
			{
				foreach ( var o in Ground.areas[1] )
					boss.node.Add( o ).fixedHeight = true;
			}
		}

		public void Update( Building building )
		{
			if ( done )
				return;

			int plankMissing = plankNeeded - plankOnTheWay - plankArrived;
			ItemDispatcher.lastInstance.RegisterRequest( building, Item.Type.plank, plankMissing, ItemDispatcher.Priority.high );
			int stoneMissing = stoneNeeded - stoneOnTheWay - stoneArrived;
			ItemDispatcher.lastInstance.RegisterRequest( building, Item.Type.stone, stoneMissing, ItemDispatcher.Priority.high );
		}

		public void FixedUpdate()
		{
			if ( done )
				return;

			// TODO Try to find a path only if the road network has been changed
			if ( worker == null && Path.Between( boss.ground.world.mainBuilding.flag.node, boss.flag.node, PathFinder.Mode.onRoad ) != null )
			{
				if ( timeSinceCreated > 50 )
				{
					Building main = boss.ground.world.mainBuilding;
					worker = Worker.Create();
					worker.SetupForConstruction( boss );
				}
				else
					timeSinceCreated++;
			}
			if ( worker == null || !worker.IsIdle( true ) )
				return;
			if ( flatteningNeeded )
			{
				flatteningCounter++;
				if ( flatteningCounter > flatteningTime / 6 )
				{
					flatteningCounter = 0;
					boss.node.Add( Ground.areas[1][flatteningCorner++] ).SetHeight( boss.node.height );
					if ( flatteningCorner == Ground.areas[1].Count )
						flatteningNeeded = false;
				}
				return;
			}
			progress += 0.001f*boss.ground.world.speedModifier;	// TODO This should be different for each building type
			float maxProgress = ((float)plankArrived+stoneArrived)/(plankNeeded+stoneNeeded);
			if ( progress > maxProgress )
				progress = maxProgress;

			if ( progress < 1 )
				return;

			done = true;
			worker.Remove();
			worker.ScheduleWalkToNeighbour( boss.flag.node );
		}
		public bool ItemOnTheWay( Item item, bool cancel = false )
		{
			if ( done )
				return false;

			if ( item.type == Item.Type.plank )
			{
				if ( cancel )
				{
					plankOnTheWay--;
					boss.assert.IsTrue( plankOnTheWay >= 0 );
				}
				else
				{
					plankOnTheWay++;
					boss.assert.IsTrue( plankArrived + plankOnTheWay <= plankNeeded );
				}
				return true;
			}
			if ( item.type == Item.Type.stone )
			{
				if ( cancel )
				{
					stoneOnTheWay--;
					boss.assert.IsTrue( stoneOnTheWay >= 0 );
				}
				else
				{
					stoneOnTheWay++;
					boss.assert.IsTrue( stoneArrived + stoneOnTheWay <= stoneNeeded );
				}
				return true;			}

			boss.assert.IsTrue( false );
			return false;
		}

		public virtual bool ItemArrived( Item item )
		{
			if ( done )
				return false;

			if ( item.type == Item.Type.plank )
			{
				boss.assert.IsTrue( plankOnTheWay > 0 );
				plankOnTheWay--;
				plankArrived++;
				boss.assert.IsTrue( plankArrived + plankOnTheWay <= plankNeeded );
				return true;
			}
			if ( item.type == Item.Type.stone )
			{
				boss.assert.IsTrue( stoneOnTheWay > 0 );
				stoneOnTheWay--;
				stoneArrived++;
				boss.assert.IsTrue( stoneArrived + stoneOnTheWay <= stoneNeeded );
				return true;
			}

			boss.assert.IsTrue( false );
			return false;
		}

		public void Validate()
		{
			worker?.Validate();
			if ( !done )
				boss.assert.AreEqual( plankOnTheWay + stoneOnTheWay, boss.itemsOnTheWay.Count );
		}
	}

	public static void Initialize()
	{
		Construction.Initialize();
	}

	public Building Setup( Ground ground, GroundNode node, Player owner )
	{
		if ( node.IsBlocking() )
		{
			Debug.Log( "Node is already occupied" );
			Destroy( gameObject );
			return null;
		}
		if ( node.owner != owner )
		{
			Debug.Log( "Node is outside of border" );
			Destroy( gameObject );
			return null;
		}
		if ( construction.flatteningNeeded )
		{
			foreach ( var o in Ground.areas[1] )
			{
				if ( node.Add( o ).owner != owner )
				{
					Debug.Log( "Node perimeter is outside of border" );
					Destroy( gameObject );
					return null;
				}
			}
		}
		if ( node.type != construction.groundTypeNeeded )
		{
			Debug.Log( "Node has different type" );
			Destroy( gameObject );
			return null;
		}
		var flagNode = node.Add( flagOffset );
		Flag flag = Flag.Create().Setup( ground, flagNode, owner );
		if ( flag == null )
		{
			Debug.Log( "Flag couldn't be created" );
			Destroy( gameObject );
			return null;
		}

		this.ground = ground;
		this.flag = flag;
		this.owner = owner;
		flag.building = this;

		this.node = node;
		construction.Setup( this );
		node.building = this;

		return this;
	}

	public void Start()
	{
		name = "Building " + node.x + ", " + node.y;
		transform.SetParent( ground.transform );
		transform.localPosition = node.Position();
		renderers = new List<MeshRenderer>();
		ScanChildObject( transform );
		foreach( var renderer in renderers )
			foreach ( var m in renderer.materials )
				m.shader = Construction.shader;

		assert.IsNull( exit, "Building already has an exit road" );
		exit = Road.Create();
		exit.SetupAsBuildingExit( this );
	}

	void ScanChildObject( Transform transform )
	{
		var renderer = transform.GetComponent<MeshRenderer>();
		if ( renderer != null )
			renderers.Add( renderer );
		for ( int i = 0; i < transform.childCount; i++ )
			ScanChildObject( transform.GetChild( i ) );
	}

	public void FixedUpdate()
	{
		construction.FixedUpdate();
	}

	public void Update()
	{
		construction.Update( this );
		UpdateLook();
	}

	public virtual Item SendItem( Item.Type itemType, Building destination )
	{
		if ( worker == null || !worker.IsIdle( true ) || flag.FreeSpace() == 0 )
			return null;

		// TODO Don't create the item, if there is no path between this and destination
		Item item = Item.Create().Setup( itemType, this, destination );
		if ( item != null )
		{
			assert.IsNull( worker.reservation );
			worker.reservation = flag;
			flag.reserved++;
			worker.SchedulePickupItem( item );
			worker.ScheduleWalkToNeighbour( flag.node );
			worker.ScheduleDeliverItem( item );
			worker.ScheduleWalkToNeighbour( node );
		}
		return item;
	}

	public virtual void ItemOnTheWay( Item item, bool cancel = false )
	{
		if ( cancel )
		{
			assert.IsTrue( itemsOnTheWay.Contains( item ) );
			itemsOnTheWay.Remove( item );
		}
		else
			itemsOnTheWay.Add( item );
		construction.ItemOnTheWay( item, cancel );
	}

	public virtual void ItemArrived( Item item )
	{
		assert.IsTrue( itemsOnTheWay.Contains( item ) );
		itemsOnTheWay.Remove( item );
		construction.ItemArrived( item );
	}

	virtual public void OnClicked()
	{
	}

	public void UpdateLook()
	{
		float lowerLimit = transform.position.y;
		float upperLimit = lowerLimit + height;
		float level = upperLimit;
		if ( !construction.done )
			level = lowerLimit+(upperLimit-lowerLimit)*construction.progress;

		foreach ( var r in renderers )
			foreach ( var m in r.materials )
				m.SetFloat( Construction.sliceLevelID, level );
	}

	public virtual bool Remove()
	{
		var list = itemsOnTheWay.GetRange( 0, itemsOnTheWay.Count );
		foreach ( var item in list )
			item.CancelTrip();
		if ( !exit.Remove() )
			return false;
		if ( worker != null && !worker.Remove() )
			return false;
		node.building = null;
		flag.building = null;
		Destroy( gameObject );
		return true;
	}

	public virtual int Influence( GroundNode node )
	{
		return 0;
	}	

	virtual public void Validate()
	{
		assert.AreEqual( this, flag.building );
		assert.AreEqual( this, node.building );
		assert.AreEqual( flag, node.Add( flagOffset ).flag );
		worker?.Validate();
		exit?.Validate();
		construction?.Validate();
		foreach ( var item in itemsOnTheWay )
			assert.AreEqual( item.destination, this );
	}
}
