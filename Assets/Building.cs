using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;

[SelectionBase]
abstract public class Building : HiveObject
{
	virtual public string title
	{
		get	{ throw new NotImplementedException(); }
		set {} // for compatibility with old files
	}
	public string moniker;
	public string nick { get { return moniker ?? title; } }
	public Player owner;
	public Worker worker, workerMate, dispenser;	// dispenser is either the worker or the mate, it can also change
	public Flag flag;
	public int flagDirection;
	public Node node;
	public Construction construction = new Construction();
	public float height = 1.5f;
	public float levelBrake = 1;
	public List<Item> itemsOnTheWay = new List<Item>();
	static List<Ground.Offset> foundationHelper;
	public Configuration configuration;

	protected GameObject body;
	GameObject highlightArrow;
	[JsonIgnore]
	public Road exit;
	public AudioSource soundSource;
	[JsonIgnore]
	public List<MeshRenderer> renderers;

	[Obsolete( "Compatibility with old files", true )]
	Node.Type groundTypeNeeded;
	[Obsolete( "Compatibility with old files", true )]
	bool huge
	{
		set
		{
			if ( configuration == null )
				configuration = new Configuration();
			configuration.huge = value;
		}
	}
	public Ground ground
	{
		get { return World.instance.ground; }
		[Obsolete( "Compatibility for old files", true )]
		set {}
	}

	public enum Type
	{
		stock = Workshop.Type.total,
		headquarters,
		guardHouse,
		total
	}

	public Type type 
	{
		get
		{
			if ( this is Workshop workshop )
				return (Type)workshop.type;
			if ( this is Stock stock )
				return stock.main ? Type.headquarters : Type.stock;
			assert.IsTrue( this is GuardHouse );
			return Type.guardHouse;
		}
	}

	public void UpdateIsolated()
	{
		if ( Path.Between( flag.node, owner.mainBuilding.flag.node, PathFinder.Mode.onRoad, this ) == null )
		{
			isolated = true;
			roadNetworkChangeListener.Attach( owner.versionedRoadNetworkChanged );
		}
		else
			isolated = false;
	}

	public Watch roadNetworkChangeListener = new Watch();
	public bool isolated;
	public bool reachable
	{
		get
		{
			if ( isolated && roadNetworkChangeListener.Check() )
				UpdateIsolated();
			return !isolated;
		}
	}

	public List<Ground.Offset> foundation
	{
		get
		{
			return GetFoundation( configuration.huge, flagDirection );
		}
	}
	static public List<Ground.Offset> GetFoundation( bool huge, int flagDirection )
	{
		var list = new List<Ground.Offset> { new Ground.Offset( 0, 0, 0 ) };
		if ( huge )
		{
			list.Add( foundationHelper[( flagDirection + 2 ) % Constants.Node.neighbourCount] );
			list.Add( foundationHelper[( flagDirection + 3 ) % Constants.Node.neighbourCount] );
			list.Add( foundationHelper[( flagDirection + 4 ) % Constants.Node.neighbourCount] );
		}
		return list;
	}
	bool currentHighlight;
	float currentLevel;
	static int highlightID;

	[System.Serializable]
	public class Configuration
	{
		public Node.Type groundTypeNeeded = Node.Type.land;
		public Node.Type groundTypeNeededAtEdge = Node.Type.anything;

		public int plankNeeded = Constants.Building.defaultPlankNeeded;
		public int stoneNeeded = Constants.Building.defaultStoneNeeded;
		public bool flatteningNeeded = true;
		public bool huge = false;
		public int constructionTime = 0;
	}

	[System.Serializable]
	public class Flattening : Worker.Callback.IHandler
	{
		public int corner;
		public bool permanent;
		public bool flattened = false;	// I would rather call this 'done', but unity gives an error message then
		public HiveObject ignoreDuringWalking;
		public World.Timer suspend = new World.Timer();
		public float level;
		public Worker worker;
		public List<Node> area;

		[Obsolete( "Compatibility with old files", true )]
		bool flatteningCorner;
		[Obsolete( "Compatibility with old files", true )]
		List<Node> flatteningArea;
		[Obsolete( "Compatibility with old files", true )]
		bool flatteningNeeded;
		[Obsolete( "Compatibility with old files", true )]
		bool done;

		public void Setup( List<Node> area, bool permanent = true, HiveObject ignoreDuringWalking = null )
		{
			flattened = false;
			this.area = area;
			this.ignoreDuringWalking = ignoreDuringWalking;
			this.permanent = permanent;
			corner = 0;
			level = 0;
			foreach ( var o in area )
			{
				if ( o.staticHeight > 0 )
				{
					level = o.staticHeight;
					break;
				}
				level += o.height / area.Count;
			}
			if ( permanent )
				foreach ( var node in area )
					node.staticHeight = level;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns>True if the function call was useful</returns>
		public bool CriticalUpdate()
		{
			if ( area == null )
				return false;

			if ( flattened )
			{
				worker?.Remove( true );
				worker = null;
				return false;
			}

			if ( worker == null )
			{	// TODO Check the path before creating the worker
				worker = Worker.Create();
				worker.SetupForFlattening( area[0].flag );
			}
			if ( worker == null || !worker.IsIdle( false ) )
				return true;
			if ( corner < area.Count )
			{
				if ( worker && worker.IsIdle() )
				{
					Node node = area[corner++];
					if ( node.fixedHeight && node.staticHeight != level )
						return true;
					if ( node.type == Node.Type.underWater )
						return true;
					float dif = node.height - level;
					if ( Math.Abs( dif ) > 0.001f )
					{
						worker.ScheduleWalkToNode( node, true, false, null, ignoreDuringWalking );
						worker.ScheduleDoAct( Worker.shovelingAct );
						worker.ScheduleCall( this );
					}
				}
				return true;
			}

			flattened = true;
			return false;
		}

		public void Callback( Worker worker )
		{
			if ( permanent || worker.node.fixedHeight == false )
				worker.node.SetHeight( level );
		}
	}

	[System.Serializable]
	public class Construction : Flattening
	{
		public Building boss;
		public bool done;
		public float progress;
		public int plankOnTheWay;
		public int plankArrived;
		public int stoneOnTheWay;
		public int stoneArrived;
		public int plankMissing { get { return boss.configuration.plankNeeded - plankOnTheWay - plankArrived; } }
		public int stoneMissing { get { return boss.configuration.stoneNeeded - stoneOnTheWay - stoneArrived; } }
		public static Shader shader;
		public static int sliceLevelID;
		public Worker.DoAct hammering;

		[Obsolete( "Compatibility with old files", true )]
		int timeSinceCreated;
		[Obsolete( "Compatibility with old files", true )]
		int duration;
		[Obsolete( "Compatibility with old files", true )]
		int plankNeeded;
		[Obsolete( "Compatibility with old files", true )]
		int stoneNeeded;
		[Obsolete( "Compatibility with old files", true )]
		int flatteningNeeded;

		static public void Initialize()
		{
			shader = Resources.Load<Shader>( "shaders/Construction" );
			Assert.global.IsNotNull( shader );
			sliceLevelID = Shader.PropertyToID( "_SliceLevel" );
		}

		public void Setup( Building boss )
		{
			this.boss = boss;
			if ( !boss.configuration.flatteningNeeded )
				return;
			
			List<Node> flatteningArea = new List<Node>();
			var area = boss.foundation;
			flatteningArea.Add( boss.node );
			foreach ( var o in area )
			{
				Node basis = boss.node.Add( o );
				foreach ( var b in Ground.areas[1] )
				{
					Node node = basis.Add( b );
					if ( !flatteningArea.Contains( node ) )
						flatteningArea.Add( node );
				}
			}
			boss.assert.IsTrue( flatteningArea.Count == 7 || flatteningArea.Count == 14, "Area has " + flatteningArea.Count + " nodes" );
			Setup( flatteningArea, true, boss );
		}

		public bool Remove( bool takeYourTime )
		{
			hammering?.Stop();
			worker?.Remove( takeYourTime );
			return true;
		}

		new public void CriticalUpdate()
		{
			if ( done || suspend.inProgress || boss.blueprintOnly )
				return;

			if ( boss.reachable )
			{
				int plankMissing = boss.configuration.plankNeeded - plankOnTheWay - plankArrived;
				boss.owner.itemDispatcher.RegisterRequest( boss, Item.Type.plank, plankMissing, ItemDispatcher.Priority.high, Ground.Area.global, boss.owner.plankForConstructionWeight.weight );
				int stoneMissing = boss.configuration.stoneNeeded - stoneOnTheWay - stoneArrived;
				boss.owner.itemDispatcher.RegisterRequest( boss, Item.Type.stone, stoneMissing, ItemDispatcher.Priority.high, Ground.Area.global, boss.owner.stoneForConstructionWeight.weight );
			}

			if ( worker == null && Path.Between( boss.owner.mainBuilding.flag.node, boss.flag.node, PathFinder.Mode.onRoad, boss ) != null )
			{
				worker = Worker.Create();
				worker.SetupForConstruction( boss );
				return;
			}

			if ( worker == null )
			{
				suspend.Start( 200 );
				return;
			};

			if ( boss.configuration.flatteningNeeded && !flattened && base.CriticalUpdate() )
				return;

		if ( !worker.IsIdle() )
				return;

			if ( progress == 0 )
			{
				var o = new Ground.Offset( 0, -1, 1 );
				Node node = boss.node.Add( o );
				if ( worker.node != node )
				{
					worker.ScheduleWalkToNode( node, true, false, null, boss );
					return;
				}

				worker.TurnTo( boss.node );
				hammering = ScriptableObject.CreateInstance<Worker.DoAct>();
				hammering.Setup( worker, Worker.constructingAct );
				hammering.Start();
			}
			progress += 1f / boss.configuration.constructionTime;
			float maxProgress = ((float)plankArrived+stoneArrived)/(boss.configuration.plankNeeded+boss.configuration.stoneNeeded);
			if ( progress >= maxProgress )
			{
				progress = maxProgress;
				hammering.Stop();
			}
			else
				hammering.Start();

			if ( progress < 1 )
				return;

			done = true;
			worker.ScheduleWalkToNeighbour( boss.flag.node );
			worker.type = Worker.Type.unemployed;
			worker = null;
			hammering = null;
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
					boss.assert.IsTrue( plankArrived + plankOnTheWay <= boss.configuration.plankNeeded );
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
					boss.assert.IsTrue( stoneArrived + stoneOnTheWay <= boss.configuration.stoneNeeded );
				}
				return true;			}

			boss.assert.Fail( "Item is not expected (" + item.type + ")" );
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
				boss.assert.IsTrue( plankArrived + plankOnTheWay <= boss.configuration.plankNeeded );
				return true;
			}
			if ( item.type == Item.Type.stone )
			{
				boss.assert.IsTrue( stoneOnTheWay > 0 );
				stoneOnTheWay--;
				stoneArrived++;
				boss.assert.IsTrue( stoneArrived + stoneOnTheWay <= boss.configuration.stoneNeeded );
				return true;
			}

			boss.assert.Fail();
			return false;
		}

		public void Validate( bool chain )
		{
			if ( chain )
				worker?.Validate( true );
			if ( !done )
				boss.assert.AreEqual( plankOnTheWay + stoneOnTheWay, boss.itemsOnTheWay.Count );
		}
	}

	public static void Initialize()
	{
		foundationHelper = new List<Ground.Offset>
		{
			new Ground.Offset( 0, -1, 1 ),
			new Ground.Offset( 1, -1, 1 ),
			new Ground.Offset( 1, 0, 1 ),
			new Ground.Offset( 0, 1, 1 ),
			new Ground.Offset( -1, 1, 1 ),
			new Ground.Offset( -1, 0, 1 )
		};

		highlightID = Shader.PropertyToID( "_StencilRef" );
		Construction.Initialize();
	}

	static public SiteTestResult IsNodeSuitable( Node placeToBuild, Player owner, Configuration configuration, int flagDirection )
	{
		var area = GetFoundation( configuration.huge, flagDirection );

		bool edgeCondition = false;
		foreach ( var o in area )
		{
			var basis = placeToBuild.Add( o );
			if ( basis.block )
				return new SiteTestResult( SiteTestResult.Result.blocked );
			if ( basis.owner != owner )
				return new SiteTestResult( SiteTestResult.Result.outsideBorder );
			foreach ( var b in Ground.areas[1] )
			{
				var perim = basis.Add( b );
				if ( configuration.flatteningNeeded )
				{
					if ( perim.owner != owner )
						return new SiteTestResult( SiteTestResult.Result.outsideBorder );
					if ( perim.fixedHeight )
						return new SiteTestResult( SiteTestResult.Result.heightAlreadyFixed );
				}
				if ( perim.CheckType( configuration.groundTypeNeededAtEdge ) )
					edgeCondition = true;
				
			}
			foreach ( var p in Ground.areas[1] )
				if ( basis.Add( p ).building )
					return new SiteTestResult( SiteTestResult.Result.buildingTooClose );
			if ( !basis.CheckType( configuration.groundTypeNeeded ) )
				return new SiteTestResult( SiteTestResult.Result.wrongGroundType, configuration.groundTypeNeeded );
		}
		if ( !edgeCondition )
			return new SiteTestResult( SiteTestResult.Result.wrongGroundTypeAtEdge, configuration.groundTypeNeededAtEdge );
		Node flagLocation = placeToBuild.Neighbour( flagDirection );
		if ( flagLocation.flag && flagLocation.flag.crossing )
			return new SiteTestResult( SiteTestResult.Result.crossingInTheWay );
		if ( flagLocation.validFlag )
			return new SiteTestResult( SiteTestResult.Result.fit );

		return Flag.IsNodeSuitable( flagLocation, owner );
	}

	public Building Setup(  Node node, Player owner, Configuration configuration, int flagDirection, bool blueprintOnly = false )
	{
		this.configuration = configuration;
		if ( !IsNodeSuitable( node, owner, configuration, flagDirection ) )
		{
			DestroyThis();
			return null;
		}
		var flagNode = node.Neighbour( flagDirection );
		Flag flag = flagNode.validFlag;
		if ( flag == null )
			flag = Flag.Create().Setup( flagNode, owner, blueprintOnly );
		if ( flag == null )
		{
			Debug.Log( "Flag couldn't be created" );
			DestroyThis();
			return null;
		}

		this.flag = flag;
		this.flagDirection = flagDirection;
		this.owner = owner;
		this.blueprintOnly = blueprintOnly;

		this.node = node;
		construction.Setup( this );
		var area = foundation;
		foreach ( var o in area )
		{
			var basis = node.Add( o );
			basis.building = this;
		}

		if ( !blueprintOnly )
			owner.buildingCounts[(int)type]++;
		base.Setup();
		return this;
	}

	public override void Materialize()
	{
		owner.buildingCounts[(int)type]++;

		if ( flag.blueprintOnly )
			flag.Materialize();
		base.Materialize();
	}

	new public void Start()
	{
		name = $"Building {node.x}:{node.y}";
		ground.Link( this );
		UpdateBody();
		renderers = new List<MeshRenderer>();

		soundSource = World.CreateSoundSource( this );

		body = Instantiate( Template(), transform );
		body.layer = World.layerIndexPickable;
		body.transform.RotateAround( node.position, Vector3.up, 60 * ( 1 - flagDirection ) );

		World.CollectRenderersRecursive( body, renderers );
		float lowerLimit = transform.position.y;
		float upperLimit = lowerLimit + height;
		float level = upperLimit;
		if ( !construction.done )
			level = lowerLimit + ( upperLimit - lowerLimit ) * (float)Math.Pow( construction.progress, levelBrake );
		foreach ( var renderer in renderers )
		{
			foreach ( var m in renderer.materials )
			{
				m.shader = Construction.shader;
				m.SetFloat( Construction.sliceLevelID, level );
			}
		}

		assert.IsNull( exit, "Building already has an exit road" );
		exit = Road.Create();
		exit.SetupAsBuildingExit( this, blueprintOnly );
		highlightArrow = Instantiate( Resources.Load<GameObject>( "prefabs/others/gem" ) );
		highlightArrow.transform.SetParent( transform );
		highlightArrow.transform.localScale = Vector3.one * 3f;
		base.Start();
	}

	public virtual GameObject Template()
	{
		throw new System.NotImplementedException();
	}

	public void Update()
	{
		UpdateLook();
	}

	public override void CriticalUpdate()
	{
		construction.CriticalUpdate();
	}

	public virtual Item SendItem( Item.Type itemType, Building destination, ItemDispatcher.Priority priority )
	{
		if ( dispenser == null || !dispenser.IsIdle( true ) || flag.freeSlots == 0 )
			return null;

		dispenser.SetActive( true );
		// TODO Don't create the item, if there is no path between this and destination
		Item item = Item.Create().Setup( itemType, this, destination, priority );
		if ( item != null )
		{
			flag.ReserveItem( item );
			dispenser.SchedulePickupItem( item );
			dispenser.ScheduleWalkToNeighbour( flag.node );
			dispenser.ScheduleDeliverItem( item );
			dispenser.ScheduleWalkToNeighbour( node );
			item.worker = dispenser;
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

	public override void OnClicked( bool show = false )
	{
		if ( !construction.done )
			Interface.ConstructionPanel.Create().Open( construction, show );
	}

	public void UpdateLook()
	{
		float lowerLimit = transform.position.y;
		float upperLimit = lowerLimit + height;
		float level = upperLimit;
		if ( !construction.done && !blueprintOnly )
			level = lowerLimit + ( upperLimit - lowerLimit ) * (float)Math.Pow( construction.progress, levelBrake );

		bool highlight = Interface.root.highlightType == Interface.HighlightType.buildingType && Interface.root.highlightBuildingTypes.Contains( type );

		if ( currentHighlight != highlight || currentLevel != level )
		{
			currentLevel = level;
			currentHighlight = highlight;
			foreach ( var r in renderers )
			{
				foreach ( var m in r.materials )
				{
					m.SetFloat( Construction.sliceLevelID, level );
					m.SetInt( highlightID, highlight ? 1 : 0 );
				}
			}
		}

		if ( Interface.root.highlightType == Interface.HighlightType.area && Interface.root.highlightArea != null && Interface.root.highlightArea.IsInside( node ) )
		{
			highlightArrow.transform.localPosition = Vector3.up * ( ( float )( height + 0.3f * Math.Sin( 2 * Time.time ) ) );
			highlightArrow.transform.rotation = Quaternion.Euler( 0, Time.time * 200, 0 );
			highlightArrow.SetActive( true );
		}
		else
			highlightArrow.SetActive( false );
	}

	public override bool Remove( bool takeYourTime )
	{
		construction.Remove( takeYourTime );
		if ( !blueprintOnly )
			owner.buildingCounts[(int)type]--;

		var list = itemsOnTheWay.GetRange( 0, itemsOnTheWay.Count );
		foreach ( var item in list )
			item.CancelTrip();
		if ( !exit.Remove( takeYourTime ) )		// TODO null reference exception happened here when trying to build a woodcutter in a forest
			return false;
		if ( worker != null && !worker.Remove() )
			return false;
		if ( workerMate != null && !workerMate.Remove() )
			return false;

		if ( construction.area != null )	// Should never be null, but old saves are having this.
			foreach ( var o in construction.area )
				o.fixedHeight = false;
		var area = foundation;
		foreach ( var o in area )
		{
			var basis = node.Add( o );
			assert.AreEqual( basis.building, this );
			basis.building = null;
		}
		if ( flag.blueprintOnly )
			flag.Remove();
		owner?.versionedBuildingDelete.Trigger();
		DestroyThis();
		return true;
	}

	public virtual int Influence( Node node )
	{
		return 0;
	}

	public void UpdateBody()
	{
		transform.localPosition = node.position;
		exit?.RebuildMesh( true );
	}

	public override void Reset()
	{
		worker?.Reset();
		workerMate?.Reset();
	}

	public override Node location { get { return node; } }

	public override void Validate( bool chain )
	{
		assert.IsTrue( flag.Buildings().Contains( this ) );
		assert.AreEqual( this, node.building );
		assert.AreEqual( flag, node.Neighbour( flagDirection ).flag );
		foreach ( var item in itemsOnTheWay )
		{
			assert.IsNotNull( item );	// TODO Triggered for a sawmill (6 items are in the itemsOnTheWay array, one of them is missing. The missing item has a flag and a nextFlag, and a valid path, it also has a worker.
			// Originated at a stock. The worker has another log in hands. The path of the item has 7 roads, progress is 4
			// Triggered again for a barrack, itemsOnTheWay has 6 entries, three beer, two bow and one missing. Missing item is a bow, destination is the barrack
			// has a flag, but no nextFlag and worker. Item is still far away.

			assert.AreEqual( item.destination, this );
		}
		if ( !chain )
			return;

		worker?.Validate( true );
		workerMate?.Validate( true );
		exit?.Validate( true );
		construction?.Validate( true );
		assert.IsTrue( World.instance.players.Contains( owner ) );
		assert.IsTrue( registered );
	}
}
