using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

abstract public class Building : MonoBehaviour
{
	public Worker worker;
	public Flag flag;
	public Ground ground;
	public GroundNode node;
	[JsonIgnore]
	public Road exit;
	static public GameObject templateA;
	static public GameObject templateB;
	[JsonIgnore]
	public List<MeshRenderer> renderers;
	public Construction construction;

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

		static public void Initialize()
		{
			shader = (Shader)Resources.Load( "Construction" );
			Assert.IsNotNull( shader );
			sliceLevelID = Shader.PropertyToID( "_SliceLevel" );
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
			if ( worker == null && Path.Between( boss.ground.mainBuilding.flag.node, boss.flag.node, PathFinder.Mode.onRoad ) != null )
			{
				if ( timeSinceCreated > 50 )
				{
					Building main = boss.ground.mainBuilding;
					worker = WorkerBoy.Create();
					worker.SetupForConstruction( boss );
				}
				else
					timeSinceCreated++;
			}
			if ( worker == null || !worker.IsIdleInBuilding() )
				return;
			progress += 0.001f*boss.ground.speedModifier;	// TODO This should be different for each building type
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
					Assert.IsTrue( plankOnTheWay >= 0 );
				}
				else
				{
					plankOnTheWay++;
					Assert.IsTrue( plankArrived + plankOnTheWay <= plankNeeded );
				}
				return true;
			}
			if ( item.type == Item.Type.stone )
			{
				if ( cancel )
				{
					stoneOnTheWay--;
					Assert.IsTrue( stoneOnTheWay >= 0 );
				}
				else
				{
					stoneOnTheWay++;
					Assert.IsTrue( stoneArrived + stoneOnTheWay <= stoneNeeded );
				}
				return true;			}

			Assert.IsTrue( false );
			return false;
		}

		public virtual bool ItemArrived( Item item )
		{
			if ( done )
				return false;

			if ( item.type == Item.Type.plank )
			{
				Assert.IsTrue( plankOnTheWay > 0 );
				plankOnTheWay--;
				plankArrived++;
				Assert.IsTrue( plankArrived + plankOnTheWay <= plankNeeded );
				return true;
			}
			if ( item.type == Item.Type.stone )
			{
				Assert.IsTrue( stoneOnTheWay > 0 );
				stoneOnTheWay--;
				stoneArrived++;
				Assert.IsTrue( stoneArrived + stoneOnTheWay <= stoneNeeded );
				return true;
			}

			Assert.IsTrue( false );
			return false;
		}

		public void Validate()
		{
			worker?.Validate();
		}
	}

	public static void Initialize()
	{
		templateA = (GameObject)Resources.Load( "Medieval fantasy house/Medieva_fantasy_house" );
		Assert.IsNotNull( templateA );
		templateB = (GameObject)Resources.Load( "Medieval house/Medieval_house" );
		Assert.IsNotNull( templateB );
		Construction.Initialize();
	}

	public Building Setup( Ground ground, GroundNode node )
	{
		if ( node.flag || node.building || node.road )
		{
			Debug.Log( "Node is already occupied" );
			Destroy( gameObject );
			return null;
		}
		var flagNode = ground.GetNode( node.x + 1, node.y - 1 );
		Flag flag = Flag.Create().Setup( ground, flagNode );
		if ( flag == null )
		{
			Debug.Log( "Flag couldn't be created" );
			Destroy( gameObject );
			return null;
		}

		this.ground = ground;
		this.flag = flag;
		flag.building = this;

		this.node = node;
		if ( construction == null )
		{
			construction = new Construction();
			construction.boss = this;
		}
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

		Assert.IsNull( exit, "Building already has an exit road" );
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
		if ( worker == null && construction.done )
		{
			worker = WorkerWoman.Create();
			worker.SetupForBuilding( this );
		}

		UpdateLook();
	}

	public virtual Item SendItem( Item.Type itemType, Building destination )
	{
		if ( worker == null || !worker.IsIdleInBuilding() )
			return null;

		// TODO Don't create the item, if there is no path between this and destination
		Item item = Item.Create().Setup( itemType, this, destination );
		if ( item != null )
		{
			worker.SchedulePickupItem( item );
			worker.ScheduleWalkToNeighbour( flag.node );
			worker.ScheduleDeliverItem( item );
			worker.ScheduleWalkToNeighbour( node );
		}
		return item;
	}

	public virtual void ItemOnTheWay( Item item, bool cancel = false )
	{
		Assert.IsTrue( false );
	}

	public virtual void ItemArrived( Item item )
	{
		Assert.IsTrue( false );
	}

	virtual public void OnClicked()
	{
		Assert.IsTrue( false );
	}

	public void UpdateLook()
	{
		float lowerLimit = transform.position.y;
		float upperLimit = lowerLimit + 1.5f;
		float level = upperLimit;
		if ( !construction.done )
			level = lowerLimit+(upperLimit-lowerLimit)*construction.progress;

		foreach ( var r in renderers )
			foreach ( var m in r.materials )
				m.SetFloat( Construction.sliceLevelID, level );
	}

	public virtual void Remove()
	{
		exit.Remove();
		worker?.Remove();
		node.building = null;
		flag.building = null;
		Destroy( gameObject );
	}

	virtual public void Validate()
	{
		Assert.AreEqual( this, flag.building );
		Assert.AreEqual( this, node.building );
		Assert.AreEqual( flag, ground.GetNode( node.x + 1, node.y - 1 ).flag );
		worker?.Validate();
		exit?.Validate();
		construction?.Validate();
	}
}
