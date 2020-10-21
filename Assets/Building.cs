using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEngine;

[SelectionBase]
abstract public class Building : Assert.Base
{
	public string title;
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
	public GroundNode.Type groundTypeNeeded = GroundNode.Type.land;
	public bool huge;
	public static List<Ground.Offset> singleArea = new List<Ground.Offset>();
	public static List<Ground.Offset> hugeArea = new List<Ground.Offset>();

	[System.Serializable]
	public class Configuration
	{
		public GroundNode.Type groundTypeNeeded = GroundNode.Type.land;

		public int plankNeeded = 2;
		public int stoneNeeded = 0;
		public bool flatteningNeeded = true;
		public bool huge = false;
	}

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
		public List<GroundNode> flatteningArea = new List<GroundNode>();

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
				var area = boss.huge ? hugeArea : singleArea;
				foreach ( var o in area )
				{
					GroundNode basis = boss.node.Add( o );
					foreach ( var b in Ground.areas[1] )
					{
						GroundNode node = basis.Add( b );
						if ( !flatteningArea.Contains( node ) )
							flatteningArea.Add( node );
					}
				}
				flatteningArea.Remove( boss.node );
				boss.assert.IsTrue( flatteningArea.Count == 6 || flatteningArea.Count == 13, "Area has " + flatteningArea.Count + " nodes" );
				foreach ( var node in flatteningArea )
					node.fixedHeight = true;
			}
		}

		public void Remove()
		{
			worker?.Remove();
		}

		public void Update( Building building )
		{
			if ( done )
				return;

			int plankMissing = plankNeeded - plankOnTheWay - plankArrived;
			boss.owner.itemDispatcher.RegisterRequest( building, Item.Type.plank, plankMissing, ItemDispatcher.Priority.high );
			int stoneMissing = stoneNeeded - stoneOnTheWay - stoneArrived;
			boss.owner.itemDispatcher.RegisterRequest( building, Item.Type.stone, stoneMissing, ItemDispatcher.Priority.high );
		}

		public void FixedUpdate()
		{
			if ( done )
				return;

			// TODO Try to find a path only if the road network has been changed
			if ( worker == null && Path.Between( boss.owner.mainBuilding.flag.node, boss.flag.node, PathFinder.Mode.onRoad ) != null )
			{
				if ( timeSinceCreated > 50 )
				{
					Building main = boss.owner.mainBuilding;
					worker = Worker.Create();
					worker.SetupForConstruction( boss );
				}
				else
					timeSinceCreated++;
			}
			if ( worker == null || !worker.IsIdle( true ) )
				return;
			if ( flatteningNeeded && flatteningCorner < flatteningArea.Count )
			{
				flatteningCounter++;
				if ( flatteningCounter > flatteningTime / 6 )
				{
					flatteningCounter = 0;
					flatteningArea[flatteningCorner++].SetHeight( boss.node.height );
				}
				return;
			}
			progress += 0.001f*boss.ground.world.timeFactor;	// TODO This should be different for each building type
			float maxProgress = ((float)plankArrived+stoneArrived)/(plankNeeded+stoneNeeded);
			if ( progress > maxProgress )
				progress = maxProgress;

			if ( progress < 1 )
				return;

			done = true;
			worker.Remove();
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

			boss.assert.IsTrue( false, "Item is not expected (" + item.type + ")" );
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
		singleArea.Add( new Ground.Offset( 0, 0, 0 ) );

		hugeArea.Add( new Ground.Offset( 0, 0, 0 ) );
		hugeArea.Add( new Ground.Offset( -1, 0, 1 ) );
		hugeArea.Add( new Ground.Offset( -1, 1, 1 ) );
		hugeArea.Add( new Ground.Offset( 0, 1, 1 ) );

		Construction.Initialize();
	}

	static public bool IsItGood( GroundNode placeToBuild, Player owner, Configuration configuration )
	{
		var area = configuration.huge ? hugeArea : singleArea;

		foreach ( var o in area )
		{
			var basis = placeToBuild.Add( o );
			if ( basis.IsBlocking() )
				return false;
			if ( basis.owner != owner )
				return false;
			if ( configuration.flatteningNeeded )
			{
				foreach ( var b in Ground.areas[1] )
				{
					if ( basis.Add( b ).owner != owner )
						return false;
				}
			}
			if ( !basis.CheckType( configuration.groundTypeNeeded ) )
				return false;
		}
		GroundNode flagLocation = placeToBuild.Add( flagOffset );
		return flagLocation.flag || Flag.IsItGood( flagLocation, owner );
	}

	public Building Setup( GroundNode node, Player owner, Configuration configuration )
	{
		if ( !IsItGood( node, owner, configuration ) )
		{
			Destroy( gameObject );
			return null;
		}
		var flagNode = node.Add( flagOffset );
		Flag flag = flagNode.flag;
		if ( flag == null )
			flag = Flag.Create().Setup( flagNode, owner );
		if ( flag == null )
		{
			Debug.Log( "Flag couldn't be created" );
			Destroy( gameObject );
			return null;
		}

		ground = node.ground;
		this.flag = flag;
		this.owner = owner;
		flag.building = this;

		this.node = node;
		construction.Setup( this );
		var area = configuration.huge ? hugeArea : singleArea;
		foreach ( var o in area )
		{
			var basis = node.Add( o );
			basis.building = this;
		}

		return this;
	}

	public void Start()
	{
		name = "Building " + node.x + ", " + node.y;
		transform.SetParent( ground.transform );
		UpdateBody();
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
			flag.ReserveItem( item );
			worker.SchedulePickupItem( item );
			worker.ScheduleWalkToNeighbour( flag.node );
			worker.ScheduleDeliverItem( item );
			worker.ScheduleWalkToNeighbour( node );
			worker.itemInHands = item;
			item.worker = worker;
		}
		return item;
	}

	public virtual void ItemOnTheWay( Item item, bool cancel = false )
	{
		if ( cancel )
		{
			item.assert.IsTrue( itemsOnTheWay.Contains( item ) );
			itemsOnTheWay.Remove( item );
		}
		else
			itemsOnTheWay.Add( item );
		construction.ItemOnTheWay( item, cancel );
	}

	public virtual void ItemArrived( Item item )
	{
		item.assert.IsTrue( itemsOnTheWay.Contains( item ) );
		itemsOnTheWay.Remove( item );
		construction.ItemArrived( item );
	}

	virtual public void OnClicked()
	{
		if ( !construction.done )
			Interface.ConstructionPanel.Create().Open( construction );
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
		construction.Remove();

		var list = itemsOnTheWay.GetRange( 0, itemsOnTheWay.Count );
		foreach ( var item in list )
			item.CancelTrip();
		if ( !exit.Remove() )
			return false;
		if ( worker != null && !worker.Remove() )
			return false;

		foreach ( var o in construction.flatteningArea )
			o.fixedHeight = false;
		var area = huge ? hugeArea : singleArea;
		foreach ( var o in area )
		{
			var basis = node.Add( o );
			assert.AreEqual( basis.building, this );
			basis.building = null;
		}
		flag.building = null;
		owner?.versionedBuildingDelete.Trigger();
		Destroy( gameObject );
		return true;
	}

	public virtual int Influence( GroundNode node )
	{
		return 0;
	}

	public void UpdateBody()
	{
		var area = huge ? hugeArea : singleArea;
		Vector3 position = new Vector3();
		foreach ( var o in area )
			position += node.Add( o ).Position();
		transform.localPosition = position / area.Count;
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
