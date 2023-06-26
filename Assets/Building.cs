using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

[SelectionBase]
abstract public class Building : VisibleHiveObject
{
	virtual public string title
	{
		get	{ throw new NotImplementedException(); }
		set {} // for compatibility with old files
	}
	public string moniker;
	public string nick 
	{ 
		get 
		{ 
			if ( moniker != null && moniker != "" )
				return moniker;
				
			return title; 
		} 
	}
	public Unit tinkerer, tinkererMate, dispenser;	// dispenser is either the tinkerer or the mate, it can also change
	public Flag flag;
	public int flagDirection;
	public Node node;
	public Construction construction = new ();
	public float height = 1.5f;
	public float levelBrake = 1;
	public List<Item> itemsOnTheWay = new ();
	static List<Ground.Offset> foundationHelper;
	public Configuration configuration;
	public bool itemDispatchedThisFrame;
	public Versioned contentChange = new ();
	public bool changedSide;

	public GameObject body;
	GameObject highlightArrow;
	Material spriteMaterial;
	[JsonIgnore]
	public Road exit;
	public AudioSource soundSource;
	[JsonIgnore]
	public List<MeshRenderer> renderers;
	override public UpdateStage updateMode => UpdateStage.realtime | UpdateStage.turtle;

	[JsonIgnore]
	public static MediaTable<Sprite, Type> sprites;

	[Obsolete( "Compatibility with old files", true )]
	public Player owner;
	[Obsolete( "Compatibility with old files", true )]
	Node.Type groundTypeNeeded;
	[Obsolete( "Compatibility with old files", true )]
	bool huge
	{
		set
		{
			if ( configuration == null )
				configuration = new ();
			configuration.huge = value;
		}
	}
	public virtual List<Ground.Area> areas { get { throw new NotImplementedException(); } }
	public override int checksum 
	{ 
		get 
		{
			int checksum = base.checksum;
			if ( tinkerer )
				checksum += tinkerer.checksum;
			if ( tinkererMate )
				checksum += tinkererMate.checksum;
			return checksum;
		}
	}

	public enum Type
	{
		stock = Workshop.Type.total,
		headquarters,
		guardHouse,
		total,
		unknown = -1
	}

	public Type type 
	{
		get
		{
			if ( this is Workshop workshop )
				return (Type)workshop.kind;
			if ( this is Stock stock )
				return stock.main ? Type.headquarters : Type.stock;
			assert.IsTrue( this is GuardHouse );
			return Type.guardHouse;
		}
	}

	public void UpdateIsolated()
	{
		if ( Path.Between( flag.node, team.mainBuilding.flag.node, PathFinder.Mode.onRoad, this ) == null )
		{
			isolated = true;
			roadNetworkChangeListener.Attach( team.versionedRoadNetworkChanged );
		}
		else
			isolated = false;
	}

	public Watch roadNetworkChangeListener = new ();
	public bool isolated;
	public bool reachable
	{
		get
		{
			if ( isolated && roadNetworkChangeListener.status )
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
	public class Flattening
	{
		public int corner;
		public bool permanent;
		public bool flattened = false;	// I would rather call this 'done', but unity gives an error message then
		public HiveObject ignoreDuringWalking;
		public Game.Timer suspend = new ();
		public float level;
		public Unit builder;
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
			float minLevel = game.generatorSettings.waterLevel * game.generatorSettings.maxHeight + 0.1f;
			if ( level < minLevel )
				level = minLevel;
			if ( permanent )
				foreach ( var node in area )
					node.staticHeight = level;
		}

		virtual public void Remove()
		{
			builder?.Remove();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns>True if the function call was useful</returns>
		public bool GameLogicUpdate( UpdateStage stage )
		{
			if ( area == null || area.Count == 0 )	// If area exists, its size is never 0, but sometimes unity just creates an empty list here with no elements at all
				return false;

			if ( flattened )
			{
				builder?.Retire();
				builder = null;
				return false;
			}

			if ( builder == null )
			{	// TODO Check the path before creating the builder
				builder = Unit.Create();
				builder.SetupForFlattening( area[0].flag );
			}
			if ( builder == null || !builder.IsIdle( false ) )
				return true;
			if ( corner < area.Count )
			{
				if ( builder && builder.IsIdle() )
				{
					Node node = area[corner++];
					if ( node.fixedHeight && node.staticHeight != level )
						return true;
					if ( node.type == Node.Type.underWater )
						return true;
					float dif = node.height - level;
					if ( Math.Abs( dif ) > 0.001f )
					{
						builder.ScheduleWalkToNode( node, true, false, null, ignoreDuringWalking );
						builder.ScheduleDoAct( Unit.shovelingAct );
						builder.ScheduleCall( ground, level, permanent );
					}
				}
				return true;
			}

			flattened = true;
			return false;
		}
	}

	[System.Serializable]
	public class Construction : Flattening, Serializer.IReferenceUser
	{
		public Building boss;
		public bool done;
		public int plankOnTheWay;
		public int plankArrived;
		public int stoneOnTheWay;
		public int stoneArrived;
		public int plankMissing { get { return boss.configuration.plankNeeded - plankOnTheWay - plankArrived; } }
		public int stoneMissing { get { return boss.configuration.stoneNeeded - stoneOnTheWay - stoneArrived; } }
		public int materialUsed;
		public Game.Timer materialProgress = new ();
		public static Shader shader;
		public static int sliceLevelID, sliceSpriteID;
		public Unit.DoAct hammering;
		public float progress
		{
			get
			{
				if ( done )
					return 1;
				float timePerMaterial = boss.configuration.constructionTime / ( boss.configuration.plankNeeded + boss.configuration.stoneNeeded );
				float current = timePerMaterial * materialUsed;
				if ( !materialProgress.empty )
				{
					current += timePerMaterial;
					if ( !materialProgress.done )
						current += materialProgress.age;
				}
				return current / boss.configuration.constructionTime;
			}
			[Obsolete( "Compatibility with old files", true )]
			set {}
		}

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

		void Serializer.IReferenceUser.OnDeadReference( MemberInfo member, HiveObject reference )
		{
			boss = null;
		}

		static public void Initialize()
		{
			shader = Resources.Load<Shader>( "shaders/Construction" );
			Assert.global.IsNotNull( shader );
			sliceLevelID = Shader.PropertyToID( "_SliceLevel" );
			sliceSpriteID = Shader.PropertyToID( "_Slice" );
		}

		public void Setup( Building boss )
		{
			this.boss = boss;
			if ( !boss.configuration.flatteningNeeded )
				return;
			
			List<Node> flatteningArea = new ();
			var area = boss.foundation;
			flatteningArea.Add( boss.node );
			foreach ( var o in area )
			{
				Node basis = boss.node.Add( o );
				foreach ( var b in Ground.areas[1] )
				{
					if ( !b )
						continue;
					Node node = basis.Add( b );
					if ( !flatteningArea.Contains( node ) )
						flatteningArea.Add( node );
				}
			}
			boss.assert.IsTrue( flatteningArea.Count == 7 || flatteningArea.Count == 14, "Area has " + flatteningArea.Count + " nodes" );
			Setup( flatteningArea, true, boss );
		}

		override public void Remove()
		{
			hammering?.StopAct();
			base.Remove();
		}

		new public void GameLogicUpdate( UpdateStage stage )
		{
			if ( done || suspend.inProgress || boss.blueprintOnly )
				return;

			if ( boss.reachable && stage == UpdateStage.turtle )
			{
				int plankMissing = boss.configuration.plankNeeded - plankOnTheWay - plankArrived;
				boss.team.itemDispatcher.RegisterRequest( boss, Item.Type.plank, plankMissing, ItemDispatcher.Category.work, Ground.Area.global, boss.team.plankForConstructionWeight.weight * boss.team.constructionFactors[(int)boss.type] );
				int stoneMissing = boss.configuration.stoneNeeded - stoneOnTheWay - stoneArrived;
				boss.team.itemDispatcher.RegisterRequest( boss, Item.Type.stone, stoneMissing, ItemDispatcher.Category.work, Ground.Area.global, boss.team.stoneForConstructionWeight.weight * boss.team.constructionFactors[(int)boss.type] );
			}

			if ( builder == null && Path.Between( boss.team.mainBuilding.flag.node, boss.flag.node, PathFinder.Mode.onRoad, boss ) != null )
			{
				builder = Unit.Create();
				builder.SetupForConstruction( boss );
				return;
			}

			if ( builder == null )
			{
				suspend.Start( 200 );
				return;
			};

			if ( boss.configuration.flatteningNeeded && !flattened && base.GameLogicUpdate( stage ) )
				return;

			if ( !builder.IsIdle() )
				return;

			if ( hammering == null )
			{
				Node node = boss.node.Neighbour( 0 );
				foreach ( var offset in Ground.areas[1] )
				{
					if ( !offset )
						continue;
					var candidate = boss.node.Add( offset );
					if ( candidate.type != Node.Type.underWater )
					{
						node = candidate;
						break;
					}
				}

				if ( builder.node != node )
				{
					builder.ScheduleWalkToNode( node, true, false, null, boss );
					return;
				}

				builder.TurnTo( boss.node );
				hammering = new Unit.DoAct();
				hammering.Setup( builder, Unit.constructingAct );
			}

			if ( materialProgress.inProgress )
				return;

			int total = boss.configuration.plankNeeded + boss.configuration.stoneNeeded;

			if ( materialProgress.done )
			{
				materialUsed++;
				hammering.StopAct();
				materialProgress.Reset();
			}

			if ( materialUsed < plankArrived + stoneArrived )
			{
				hammering.StartAct();
				materialProgress.Start( boss.configuration.constructionTime / total );
			}

			if ( materialUsed != total )
				return;

			done = true;
			builder.ScheduleWalkToNeighbour( boss.flag.node );
			builder.type = Unit.Type.unemployed;
			builder.RegisterAsReturning();
			builder = null;
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

			boss.team.ItemProcessed( item.type );

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
				builder?.Validate( true );
			if ( done )
				return;

			boss.assert.AreEqual( plankOnTheWay + stoneOnTheWay, boss.itemsOnTheWay.Count );
			if ( hammering != null )
				boss.assert.IsNotNull( hammering.act );
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
		sprites.Fill();

		sprites.fileNameGenerator = ( type ) =>
		{
			string prefix = "sprites/buildings/";
			if ( (int)type < (int)Workshop.Type.total )
				return prefix + ((Workshop.Type)type).ToString();
			return prefix + type.ToString();
		};
		sprites.missingMediaHandler = GenerateSprite;
	}

	static Sprite GenerateSprite( Type type )
	{
		if ( (int)type < Workshop.sprites.Length )
			return Workshop.sprites[(int)type];
		return null;
	}

	static public SiteTestResult IsNodeSuitable( Node placeToBuild, Team team, Configuration configuration, int flagDirection, bool ignoreBlockingResources = true, Action<Node> nodeAction = null )
	{
		var area = GetFoundation( configuration.huge, flagDirection );

		bool edgeCondition = false;
		foreach ( var o in area )
		{
			var basis = placeToBuild.Add( o );
			if ( nodeAction != null )
				nodeAction( basis );
			if ( basis.block )
			{
				bool resourceBlocking = false;
				foreach ( var resource in basis.resources )
				{
					if ( resource.type == Resource.Type.tree || resource.type == Resource.Type.rock || resource.type == Resource.Type.cornField || resource.type == Resource.Type.wheatField )
					resourceBlocking = true;
				}
				if ( !ignoreBlockingResources || !resourceBlocking )
					return new SiteTestResult( SiteTestResult.Result.blocked );
			}
			if ( basis.team != team )
				return new SiteTestResult( SiteTestResult.Result.outsideBorder );
			foreach ( var b in Ground.areas[1] )
			{
				if ( !b )
					continue;
				var perim = basis.Add( b );
				if ( configuration.flatteningNeeded )
				{
					if ( perim.team != team )
						return new SiteTestResult( SiteTestResult.Result.outsideBorder );
					if ( perim.fixedHeight )
						return new SiteTestResult( SiteTestResult.Result.heightAlreadyFixed );
				}
				if ( perim.CheckType( configuration.groundTypeNeededAtEdge ) )
					edgeCondition = true;
				
			}
			foreach ( var p in Ground.areas[1] )
			{
				if ( !p )
					continue;
				if ( basis.Add( p ).building )
					return new SiteTestResult( SiteTestResult.Result.buildingTooClose );
			}
			if ( !basis.CheckType( configuration.groundTypeNeeded ) )
				return new SiteTestResult( SiteTestResult.Result.wrongGroundType, configuration.groundTypeNeeded );
		}
		if ( !edgeCondition )
			return new SiteTestResult( SiteTestResult.Result.wrongGroundTypeAtEdge, configuration.groundTypeNeededAtEdge );
		Node flagLocation = placeToBuild.Neighbour( flagDirection );
		if ( flagLocation.flag && flagLocation.flag.crossing )
			return new SiteTestResult( SiteTestResult.Result.crossingInTheWay );
		if ( flagLocation.validFlag && flagLocation.flag.team == team )
			return new SiteTestResult( SiteTestResult.Result.fit );

		return Flag.IsNodeSuitable( flagLocation, team, ignoreBlockingResources:ignoreBlockingResources );
	}

	public Building Setup( Node node, Team team, Configuration configuration, int flagDirection, bool blueprintOnly = false, Resource.BlockHandling block = Resource.BlockHandling.block )
	{
		this.configuration = configuration;
		if ( !IsNodeSuitable( node, team, configuration, flagDirection, block == Resource.BlockHandling.ignore || block == Resource.BlockHandling.remove ) )
		{
			base.Remove();
			return null;
		}
		
		var flagNode = node.Neighbour( flagDirection );
		Flag flag = flagNode.validFlag;
		if ( flag == null )
			flag = Flag.Create().Setup( flagNode, team, blueprintOnly, block:block );
		if ( flag == null )
		{
			Debug.Log( "Flag couldn't be created" );
			base.Remove();
			return null;
		}

		this.flag = flag;
		this.flagDirection = flagDirection;
		this.team = team;
		this.blueprintOnly = blueprintOnly;

		this.node = node;
		construction.Setup( this );
		var area = foundation;
		foreach ( var o in area )
		{
			var basis = node.Add( o );
			if ( block == Resource.BlockHandling.remove )
				Resource.RemoveFromGround( basis );
			basis.building = this;
		}

		if ( !blueprintOnly )
			team.buildingCounts[(int)type]++;
		base.Setup( node.world );
		return this;
	}

	public override void Materialize()
	{
		team.buildingCounts[(int)type]++;
		foreach ( var o in foundation )
		{
			Resource.RemoveFromGround( node + o );
			node.Add( o ).RemoveDecorationsAround();
		}

		if ( flag.blueprintOnly )
			flag.Materialize();
		base.Materialize();
	}

	new public void Start()
	{
		if ( destroyed )
		{
			base.Start();
			return;
		}

		name = $"Building {node.x}:{node.y}";
		ground.Link( this );
		UpdateBody();
		renderers = new ();

		soundSource = World.CreateSoundSource( this );

		body = Instantiate( Template() );
		body.transform.RotateAround( Vector3.zero, Vector3.up, 60 * ( 1 - flagDirection ) );
		body.transform.SetParent( transform, false );
		World.SetLayerRecursive( body, Constants.World.layerIndexBuildings );
		var smoke = body.transform.Find( "smoke" )?.GetComponent<ParticleSystem>();
		if ( smoke )
		{
			var mainModule = smoke.main;
			mainModule.simulationSpeed = game.timeFactor;
		}

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

	new public void Update()
	{
		if ( destroyed )
			return;

		UpdateLook();
		base.Update();
	}

	public override void GameLogicUpdate( UpdateStage stage )
	{
		itemDispatchedThisFrame = false;
		construction.GameLogicUpdate( stage );
	}

	public virtual Item SendItem( Item.Type itemType, Building destination, ItemDispatcher.Category priority )
	{
		if ( dispenser == null || !dispenser.IsIdle( true ) || flag.freeSlots == 0 )
			return null;

		itemDispatchedThisFrame = true;
		dispenser.SetActive( true );
		// TODO Don't create the item, if there is no path between this and destination
		Item item = Item.Create().Setup( itemType, this, destination, priority );
		if ( item != null )
		{
			flag.ReserveItem( item );
			dispenser.SchedulePickupItems( item );
			dispenser.ScheduleWalkToNeighbour( flag.node );
			dispenser.ScheduleDeliverItems( item );
			dispenser.ScheduleWalkToNeighbour( node );
			item.hauler = dispenser;
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

	public void CancelOrders( Item.Type itemType = Item.Type.unknown )
	{
		List<Item> toCancel = new ();
		foreach ( var item in itemsOnTheWay )
			if ( itemType == Item.Type.unknown || itemType == item.type )
				toCancel.Add( item );

		foreach ( var item in toCancel )
			item.CancelTrip();
	}

	public override void OnClicked( Interface.MouseButton button, bool show = false )
	{
		if ( button == Interface.MouseButton.right)
		{
			if ( type == Type.headquarters )
				return;

			var controller = Interface.Controller.Create();
			controller.transform.SetParent( root.transform, false );
			controller.AddOption( Interface.Icon.hammer, "Build another instance", Clone );
			controller.AddOption( Interface.Icon.destroy, "Delete this building", () => oh.ScheduleRemoveBuilding( this ) );
			if ( this is Workshop workshop )
			{
				controller.AddOption( Interface.Icon.clock, "Change working mode to normal (work when needed)", () => oh.ScheduleChangeWorkshopRunningMode( workshop, Workshop.Mode.whenNeeded ) );
				controller.AddOption( Interface.Icon.alarm, "Change working mode to always work", () => oh.ScheduleChangeWorkshopRunningMode( workshop, Workshop.Mode.always ) );
				controller.AddOption( Interface.Icon.sleeping, "Change working mode to sleep", () => oh.ScheduleChangeWorkshopRunningMode( workshop, Workshop.Mode.sleeping ) );
			}
			controller.Open();
			return;
		}
		if ( button == Interface.MouseButton.left && !construction.done && team == root.mainTeam )
			Interface.ConstructionPanel.Create().Open( construction, show );
	}

	void Clone()
	{
		if ( type == Type.headquarters )
			return;
		if ( type == Type.stock )
			Interface.NewBuildingPanel.Create( Interface.NewBuildingPanel.Construct.stock );
		if ( type == Type.guardHouse )
			Interface.NewBuildingPanel.Create( Interface.NewBuildingPanel.Construct.guardHouse );
		if ( type < Type.stock )
			Interface.NewBuildingPanel.Create( Interface.NewBuildingPanel.Construct.workshop, (Workshop.Type)type );
	}

	public void UpdateLook()
	{
		float lowerLimit = transform.position.y;
		float upperLimit = lowerLimit + height;
		float level = upperLimit;
		if ( !construction.done && !blueprintOnly )
			level = lowerLimit + ( upperLimit - lowerLimit ) * (float)Math.Pow( construction.progress, levelBrake );

		if ( currentLevel != level )
		{
			currentLevel = level;
			foreach ( var r in renderers )
			{
				foreach ( var m in r.materials )
					m.SetFloat( Construction.sliceLevelID, level );
			}
		}
		spriteMaterial.SetFloat( Construction.sliceSpriteID, construction.progress );

		if ( !eye.enabled || eye.highlight == null )
			return;

		if ( eye.highlight.type == Eye.Highlight.Type.area && eye.highlight.area != null && eye.highlight.area.IsInside( node ) )
		{
			highlightArrow.transform.localPosition = Vector3.up * ( ( float )( height + 0.3f * Math.Sin( 2 * Time.time ) ) );
			highlightArrow.transform.rotation = Quaternion.Euler( 0, Time.time * 200, 0 );
			highlightArrow.SetActive( true );
		}
		else
			highlightArrow.SetActive( false );
	}

	public override void Remove()
	{
		if ( construction.done )
		{
			team.mainBuilding.itemData[(int)Item.Type.plank].content += configuration.plankNeeded / 2;
			team.mainBuilding.itemData[(int)Item.Type.stone].content += configuration.stoneNeeded / 2;
		}

		construction.Remove();
		if ( !blueprintOnly )
			team.buildingCounts[(int)type]--;

		var list = itemsOnTheWay.GetRange( 0, itemsOnTheWay.Count );
		foreach ( var item in list )
			item.CancelTrip();
		if ( exit )
			exit.Remove();		// TODO null reference exception happened here when trying to build a woodcutter in a forest. Again when trying to build a bow maker
		if ( tinkerer )
			tinkerer.Remove();
		if ( tinkererMate )
			tinkererMate.Remove();

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
		team?.versionedBuildingDelete.Trigger();
		base.Remove();
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
		tinkerer?.Reset();
		tinkererMate?.Reset();
	}

	public void SetTeam( Team team )
	{
		this.team.SendMessage( "Military building lost to enemy", this );
		if ( Influence( node ) != 0 && this.team )
			this.team.UnregisterInfuence( this );
		if ( this.team )
			this.team.buildingCounts[(int)type]--;

		var buildingsAround = flag.Buildings();
		foreach ( var other in buildingsAround )
		{
			if ( other != this )
				other.Remove();
		}

		foreach ( var road in flag.roadsStartingHere )
		{
			if ( road )
				road.Remove();
		}

		this.team = team;
		flag.SetTeam( team );

		if ( team && Influence( node ) > 0 )
			team.RegisterInfluence( this );
		if ( team )
			team.buildingCounts[(int)type]++;
		changedSide = true;
	}

	override public Sprite GetVisualSprite( VisualType visualType )
	{
		if ( visualType == VisualType.nice2D )
			return sprites.GetMediaData( type );

		return null;
	}

	override public GameObject CreateVisual( VisualType visualType )
	{
		if ( visualType != VisualType.functional )
		{
			var sprite = base.CreateVisual( visualType );
			spriteMaterial = sprite.GetComponent<SpriteRenderer>().material;
			return sprite;
		}

		return Interface.BuildingMapWidget.Create( this ).gameObject;
	}


	public override Node location => node;
	public override Vector3 position
	{
		get
		{
			Vector3 result = new ();
			foreach ( var offset in foundation )
				result += node.Add( offset ).GetPositionRelativeTo( node );
			result /= foundation.Count;

			return result;
		}
	}


	public override void Validate( bool chain )
	{
		if ( destroyed )
			return;
			
		assert.IsTrue( flag.Buildings().Contains( this ) );
		assert.AreEqual( team, flag.team );
		assert.AreEqual( this, node.building );
		assert.AreEqual( flag, node.Neighbour( flagDirection ).flag );
		foreach ( var item in itemsOnTheWay )
		{
			assert.IsNotNull( item );	// TODO Triggered for a sawmill (6 items are in the itemsOnTheWay array, one of them is missing. The missing item has a flag and a nextFlag, and a valid path, it also has a hauler.
			// Originated at a stock. The hauler has another log in hands. The path of the item has 7 roads, progress is 4
			// Triggered again for a barrack, itemsOnTheWay has 6 entries, three beer, two bow and one missing. Missing item is a bow, destination is the barrack
			// has a flag, but no nextFlag and hauler. Item is still far away.

			assert.AreEqual( item.destination, this );
		}
		if ( !chain )
			return;

		tinkerer?.Validate( true );
		tinkererMate?.Validate( true );
		exit?.Validate( true );
		construction?.Validate( true );
		if ( team && !team.destroyed )
			assert.IsTrue( game.teams.Contains( team ) );
		base.Validate( chain );
	}
}
