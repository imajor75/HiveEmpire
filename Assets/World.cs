using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.PostProcessing;

public class World : HiveCommon
{
	public new Ground ground;
	public new string name;
	public int saveIndex, replayIndex;
	public int currentSeed;
	public List<Player> players = new List<Player>();
	public new Eye eye;
	public bool gameInProgress;
	public new int time;
	public int nextID = 1;
	public int frameSeed;
	public int overseas = 2;
	public bool roadTutorialShowed;
	public bool createRoadTutorialShowed;
	public string fileName;
	public LinkedList<HiveObject> hiveObjects = new LinkedList<HiveObject>(), newHiveObjects = new LinkedList<HiveObject>();
	[JsonIgnore]
	public bool fixedOrderCalls;
	public Speed speed;
	public OperationHandler operationHandler;

	static public bool massDestroy;
	static System.Random rnd;
	static public World instance;
	static public int soundMaxDistance = 7;
	static public int layerIndexNotOnMap;
	static public int layerIndexMapOnly;
	static public int layerIndexPickable;
	static public int layerIndexGround;
	static public int layerIndexPPVolume;
	static public Shader defaultShader;
	static public Shader defaultColorShader;
	static public Shader defaultMapShader;
	static public Shader defaultTextureShader;
	static public Shader defaultCutoutTextureShader;
	static public Water water;
	static public GameObject nodes;
	static public GameObject itemsJustCreated;

	public float timeFactor 
	{
		[Obsolete( "Compatibility with old files", true )]
		private set
		{
			if ( value == 0 )
				speed = Speed.pause;
			if ( value == 1 )
				speed = Speed.normal;
			if ( value == 8 )
				speed = Speed.fast;
		}
		get
		{
			return speed switch
			{
				Speed.pause => 0,
				Speed.normal => 1,
				Speed.fast => Constants.World.fastSpeedFactor,
				_ => 1
			};
		}
	}

	public static int CRC
	{
		set
		{
			// if ( instance.operationHandler.recordCRC && instance.time > 10 )
			 	HiveObject.Log( $"CRC {oh.currentCRCCode}: {value} from {Assert.Caller()}" );
			instance.operationHandler.currentCRCCode += value;
		}
	}

	public string nextSaveFileName { get { return $"{name} ({saveIndex})"; } }
	public string nextReplayFileName { get { return $"{name} ({replayIndex})"; } }

	[Obsolete( "Compatibility with old files", true )]
	bool victory { set {} }
	[Obsolete( "Compatibility with old files", true )]
	float maxHeight { set {} }
	[Obsolete( "Compatibility with old files", true )]
	float hillLevel { set {} }
	[Obsolete( "Compatibility with old files", true )]
	float mountainLevel { set {} }
	[Obsolete( "Compatibility with old files", true )]
	float forestGroundChance { set {} }
	[Obsolete( "Compatibility with old files", true )]
	float rockChance { set {} }
	[Obsolete( "Compatibility with old files", true )]
	float animalSpawnerChance { set {} }
	[Obsolete( "Compatibility with old files", true )]
	float ironChance { set {} }
	[Obsolete( "Compatibility with old files", true )]
	float coalChance { set {} }
	[Obsolete( "Compatibility with old files", true )]
	float stoneChance { set {} }
	[Obsolete( "Compatibility with old files", true )]
	float saltChance { set {} }
	[Obsolete( "Compatibility with old files", true )]
	float goldChance { set {} }
	[Obsolete( "Compatibility with old files", true )]
	Goal currentWinLevel { set {} }
	[Obsolete( "Compatibility with old files", true )]
	int randomSeed { set {} }
	[Obsolete( "Compatibility with old files", true )]
	bool insideCriticalSection { set {} }
	public Settings settings;

	[System.Serializable]
	public class Ore
	{
		public Resource.Type resourceType;
		public float ideal;		// Number of ores per node
		public int resourceCount;
		public int missing
		{
			get
			{
				int idealCount = (int)( HiveCommon.world.nodeCount * ideal );
				return Math.Max( 0, idealCount - resourceCount );
			}

		}

		[Obsolete( "Compatibility with old files", true )]
		float idealRatio { set {} }
	}

	public enum Goal
	{
		none,
		bronze,
		silver,
		gold
	}

	public enum Speed
	{
		pause,
		normal,
		fast
	}

	public class Milestone
	{
		public Goal goal;
		public int maintain;
		public float productivityGoal
		{
			get
			{
				var d = HiveCommon.ground.dimension;
				var groundSize = d * d;
				var goldGoal = groundSize / 512.0f;
				if ( goal == Goal.gold )
					return goldGoal;
				if ( goal == Goal.silver )
					return goldGoal * 0.75f;
				if ( goal == Goal.bronze )
					return goldGoal * 0.5f;

				Assert.global.Fail();
				return float.MaxValue;
			}
		}

		public override string ToString()
		{
			return $"reach {productivityGoal.ToString( "N2" )} soldier/min and maintain it for {UIHelpers.TimeToString( maintain )}";
		}
	}

	public class Challenge : HiveCommon
	{
		public string title, description;
		public Goal reachedLevel = Goal.none;
		public int maintain;
		public bool fixedSeed;
		public int seed;
		public Timer maintainBronze = new Timer(), maintainSilver = new Timer(), maintainGold = new Timer();
		public float progress;
		public Timer life = new Timer();
		public List<float> productivityGoals;
		public List<int> mainBuildingContent;
		public List<int> buildingMax;
		public int timeLimit;
		public int worldSize = 32;
		public List<string> conditions;
		public string conditionsText;

		[Obsolete( "Compatibility with old files", true )]
		float soldierProductivityGoal { set {} }
		[Obsolete( "Compatibility with old files", true )]
		bool randomSeed { set {} }

		public static Challenge Create()
		{
			return new GameObject( "Challenge" ).AddComponent<Challenge>();
		}

		void Start()
		{
			if ( transform.parent == null )
				transform.SetParent( HiveCommon.world.transform );
		}

		public void Begin()
		{
			life.Start();
			maintainBronze.Reset();
			maintainSilver.Reset();
			maintainGold.Reset();
			reachedLevel = Goal.none;
		}

		void FixedUpdate()
		{
			if ( world.challenge != this )
				return;

			var player = root.mainPlayer;
			var currentLevel = Goal.gold;
			conditionsText = "";
			progress = 1;

			if ( !player )
				return;		// Right after load this happens sometimes

			void CheckCondition( float current, float limit, bool allowLevels, string text = null, bool reversed = false )
			{
				if ( limit < 0 )
					return;

				if ( text != null )
					conditionsText += String.Format( text, current.ToString( "n2" ), limit.ToString( "n2" ) );	// TODO don't display fractions when number is integer
				if ( current < limit * 0.01f )
				{
					currentLevel = Goal.none;
					progress = 0;
					if ( text != null )
					{
						var currentLevel = reversed ? Goal.gold : Goal.none;
						conditionsText += $" (level: {currentLevel})\n";
					}
					return;
				}
				if ( reversed )
				{
					current = 1 / current;
					limit = 1 / limit;
				}
				var localProgress = current / limit;
				var localLevel = Goal.gold;
				if ( allowLevels )
				{
					if ( localProgress < 1 )
						localLevel = Goal.silver;
					if ( localProgress < (reversed ? 2f/3 : 3f/4) )
						localLevel = Goal.bronze;
					if ( localProgress < 0.5f )
						localLevel = Goal.none;
					if ( progress > localProgress && !reversed )
						progress = localProgress;
				}
				else
					if ( localProgress < 1 )
						localLevel = Goal.none;
				if ( text != null )
					conditionsText += $" (level: {localLevel})\n";
				if ( localLevel < currentLevel )
					currentLevel = localLevel;
			}

			if ( productivityGoals != null )
			{
				for ( int i = 0; i < productivityGoals.Count; i++ )
					CheckCondition( player.itemProductivityHistory[i].current, productivityGoals[i], true, $"{(Item.Type)i} productivity {{0}}/{{1}}" );
			}

			if ( mainBuildingContent != null )
			{
				for ( int i = 0; i < mainBuildingContent.Count; i++ )
					CheckCondition( player.mainBuilding.itemData[i].content, mainBuildingContent[i], true, $"{(Item.Type)i}s in headquarters {{0}}/{{1}}" );
			}

			if ( buildingMax != null )
			{
				for ( int i = 0; i < buildingMax.Count; i++ )
				{
					string buildingName = i < (int)Building.Type.stock ? ((Workshop.Type)i).ToString() : ((Building.Type)i).ToString();
					CheckCondition( player.buildingCounts[i], buildingMax[i], false, $"number of {buildingName}s {{0}}/{{1}}", true );
				}
			}

			if ( timeLimit > 0 )
				CheckCondition( life.age, timeLimit, false, null, true );

			void CheckGoal( Goal goal, Timer timer )
			{
				if ( reachedLevel >= goal )
					return;
				if ( currentLevel >= goal )
				{
					if ( timer.empty )
						timer.Start( maintain );
					if ( timer.done )
					{
						reachedLevel = goal;
						root.OnGoalReached( goal );
						return;
					}
				}
				else
					timer.Reset();
			}

			CheckGoal( Goal.gold, maintainGold );
			CheckGoal( Goal.silver, maintainSilver );
			CheckGoal( Goal.bronze, maintainBronze );
		}

		public void ParseConditions()
		{
			foreach ( var condition in conditions )
			{
				var p = condition.Split( ' ' );
				switch ( p[0] )
				{
					case "headquarters":
					{
						if ( mainBuildingContent == null )
							mainBuildingContent = new List<int>();
						Item.Type itemType;
						if ( !Enum.TryParse( p[1], out itemType ) )
						{
							print( $"Unknown item: {p[1]}" );
							break;
						}
						while ( mainBuildingContent.Count < (int)Item.Type.total )
							mainBuildingContent.Add( -1 );
						mainBuildingContent[(int)itemType] = int.Parse( p[2] );
						break;
					}
					case "production":
					{
						if ( productivityGoals == null )
							productivityGoals = new List<float>();
						Item.Type itemType;
						if ( !Enum.TryParse( p[1], out itemType ) )
						{
							print( $"Unknown item: {p[1]}" );
							break;
						}
						while ( productivityGoals.Count < (int)Item.Type.total )
							productivityGoals.Add( -1 );
						productivityGoals[(int)itemType] = float.Parse( p[2] );
						break;
					}
					case "buildingMax":
					{
						if ( buildingMax == null )
							buildingMax = new List<int>();
						Building.Type buildingType;
						if ( !Enum.TryParse( p[1], out buildingType ) )
						{
							Workshop.Type workshopType;
							if ( !Enum.TryParse( p[1], out workshopType ) )
							{
								print( $"Unknown building: {p[1]}" );
								break;
							}
							buildingType = (Building.Type)workshopType;
						}
						while ( buildingMax.Count < (int)Building.Type.total )
							buildingMax.Add( -1 );
						buildingMax[(int)buildingType] = int.Parse( p[2] );
						break;
					}
					default:
					{
						print( $"Unknown condition type: {p[0]}" );
						break;
					}
				}
			}

			conditions.Clear();
		}

		public class List
		{
			public List<Challenge> list;
		}
	}

	[Obsolete( "Compatibility with old files", true )]
	List<Milestone> milestones { set {} }
	[Obsolete( "Compatibility with old files", true )]
	int currentMilestoneIndex { set {} }
	[Obsolete( "Compatibility with old files", true )]
	Timer goalReached { set {} }

	public Challenge challenge;

	public int oreCount;
	public List<Ore> ores = new List<Ore>();
	public int animalSpawnerCount;

	public class Settings : HeightMap.Settings
	{
		[Range(16, 128)]
		public int size = 48;

		public float maxHeight = 6;
		[Range(0.0f, 1.0f)]
		public float waterLevel = 0.25f;
		[Range(0.0f, 1.0f)]
		public float hillLevel = 0.6f;
		[Range(0.0f, 1.0f)]
		public float mountainLevel = 0.8f;
		[Range(0.0f, 1.0f)]
		public float forestGroundChance = 0.45f;

		public float forestChance = 0.006f;
		public float rocksChance = 0.002f;
		public float animalSpawnerChance = 0.001f;
		public float idealIron = 0.05f;
		public float idealCoal = 0.1f;
		public float idealGold = 0.04f;
		public float idealSalt = 0.02f;
		public float idealStone = 0.01f;

		[Obsolete( "Compatibility with old files", true )]
		float ironChance;
		[Obsolete( "Compatibility with old files", true )]
		float coalChance;
		[Obsolete( "Compatibility with old files", true )]
		float stoneChance;
		[Obsolete( "Compatibility with old files", true )]
		float saltChance;
		[Obsolete( "Compatibility with old files", true )]
		float goldChance;
		[Obsolete( "Compatibility with old files", true )]
		float oreChance;
		[Obsolete( "Compatibility with old files", true )]
		float ironRatio { set {} }
		[Obsolete( "Compatibility with old files", true )]
		float coalRatio { set {} }
		[Obsolete( "Compatibility with old files", true )]
		float goldRatio { set {} }
		[Obsolete( "Compatibility with old files", true )]
		float saltRatio { set {} }
		[Obsolete( "Compatibility with old files", true )]
		float stoneRatio { set {} }

		[JsonIgnore]
		public bool apply;  // For debug purposes only
	}

	public int nodeCount
	{
		get
		{
			return ground.dimension * ground.dimension;
		}
	}

	public static void Initialize()
	{
		layerIndexNotOnMap = LayerMask.NameToLayer( "Not on map" );
		layerIndexMapOnly = LayerMask.NameToLayer( "Map only" );
		layerIndexPickable = LayerMask.NameToLayer( "Pickable" );
		layerIndexGround = LayerMask.NameToLayer( "Ground" );
		layerIndexPPVolume = LayerMask.NameToLayer( "PPVolume" );
		Assert.global.IsTrue( layerIndexMapOnly != -1 && layerIndexNotOnMap != -1 );
		defaultShader = Shader.Find( "Standard" );
		defaultColorShader = Shader.Find( "Unlit/Color" );
		defaultTextureShader = Shader.Find( "Unlit/Texture" );
		defaultCutoutTextureShader = Shader.Find( "Unlit/Transparent Cutout" );
		defaultMapShader = Resources.Load<Shader>( "shaders/Map" );
	}

	World()
	{
		Assert.global.IsNull( instance );
		instance = this;
	}

	public static World Create()
	{
		return new GameObject( "World" ).AddComponent<World>();
	}

	public void Awake()
	{
		settings = ScriptableObject.CreateInstance<Settings>();
	}

	public World Setup()
	{
		rnd = new System.Random();
		return this;
	}

	static public AudioSource CreateSoundSource( Component component )
	{
		var soundSource = component.gameObject.AddComponent<AudioSource>();
		soundSource.spatialBlend = 1;
		soundSource.minDistance = 1;
		soundSource.pitch = instance.speed == Speed.fast ? Constants.World.fastSpeedFactor : 1;
		soundSource.maxDistance = Constants.Node.size * World.soundMaxDistance;
		return soundSource;
	}

	static public int NextRnd( int limit = 0 )
	{
		Assert.global.IsTrue( instance.fixedOrderCalls );
		int r = 0;
		if ( limit != 0 )
			r = rnd.Next( limit );
		else
			r = rnd.Next();
		//if ( time > 10 )
			HiveObject.Log( $"Rnd requested from {Assert.Caller()}, {Assert.Caller(3)}: {r}" );
		return r;
	}

	static public float NextFloatRnd()
	{
		Assert.global.IsTrue( instance.fixedOrderCalls );
		var r = (float)rnd.NextDouble();
		//if ( time > 10 )
			HiveObject.Log( $"Rnd requested from {Assert.Caller()}, {Assert.Caller(3)}: {r.ToString()}" );
		return r;
	}

	void FixedUpdate()
	{
		if ( settings.apply )
		{
			settings.apply = false;
			var c = instance.challenge;
			c.fixedSeed = true;
			c.seed = instance.currentSeed;
			instance.NewGame( instance.challenge, true );
			root.mainPlayer = instance.players[0];
		}
		massDestroy = false;

		time++;
		HiveObject.Log( $"========= new frame ({time}, seed: {frameSeed}) ==========" );
		rnd = new System.Random( frameSeed );
		fixedOrderCalls = true;
		CRC = NextRnd();
		foreach ( var player in players )
			player.FixedUpdate();

		if ( challenge.life.empty )
			challenge.Begin();

		foreach ( var newHiveObject in newHiveObjects )
		{
			Assert.global.IsFalse( hiveObjects.Contains( newHiveObject ) );
			hiveObjects.AddLast( newHiveObject );
		}
		newHiveObjects.Clear();
		foreach ( var hiveObject in hiveObjects )
		{
			if ( hiveObject && !hiveObject.destroyed )
				hiveObject.CriticalUpdate();
		}
		fixedOrderCalls = false;
	}

	public void OnEndOfLogicalFrame()
	{
		frameSeed = NextRnd();
	}
		
	public void NewGame( Challenge challenge, bool keepCameraLocation = false )
	{
		fixedOrderCalls = true;
		nextID = 1;
		time = -1;
		string pattern = challenge.title + " #{0}";
		name = String.Format( pattern, Interface.FirstUnusedIndex( Application.persistentDataPath + "/Saves", pattern + " (0).json" ) );
		saveIndex = 0;
		SetSpeed( Speed.normal );
		if ( operationHandler )
			Destroy( operationHandler );
		fileName = "";
		roadTutorialShowed = false;
		createRoadTutorialShowed = false;
		overseas = 2;
		var oldEye = eye;

		this.challenge = challenge;
		settings.size = challenge.worldSize;
		var seed = challenge.seed;
		Debug.Log( "Starting new game with seed " + seed );
		HiveObject.Log( $"\n\nStarting new game with seed {seed}\n\n" );

		rnd = new System.Random( seed );
		currentSeed = seed;

		var heightMap = HeightMap.Create();
		heightMap.Setup( settings, rnd.Next() );
		heightMap.Fill();

		var forestMap = HeightMap.Create();
		forestMap.Setup( settings, rnd.Next() );
		forestMap.Fill();

#if DEBUG
		heightMap.SavePNG( "height.png" );
		forestMap.SavePNG( "forest.png" );
#endif

		Clear();
		Prepare();
		Interface.ValidateAll( true );

		operationHandler = OperationHandler.Create().Setup();
		operationHandler.challenge = challenge;
#if DEBUG
		operationHandler.recordCRC = true;
#endif
		eye = Eye.Create().Setup( this );
		ground = Ground.Create();
		ground.Setup( this, heightMap, forestMap, settings.size );
		GenerateResources();
		water = Water.Create().Setup( ground );
		var mainPlayer = Player.Create().Setup();
		if ( mainPlayer )
			players.Add( mainPlayer );
		ground.RecalculateOwnership();
		gameInProgress = true;

		water.transform.localPosition = Vector3.up * waterLevel;

		foreach ( var player in players )
			player.Start();

		if ( keepCameraLocation )
		{
			eye.x = oldEye.x;
			eye.y = oldEye.y;
			eye.altitude = oldEye.altitude;
			eye.targetAltitude = oldEye.targetAltitude;
			eye.direction = oldEye.direction;
			eye.viewDistance = oldEye.viewDistance;
		}
		Interface.ValidateAll( true );
		frameSeed = NextRnd();
		fixedOrderCalls = false;
	}

	void Start()
	{
		if ( operationHandler == null )
		{
			operationHandler = OperationHandler.Create();
			operationHandler.transform.SetParent( transform );
		}
		foreach ( var player in players )
			player.Start();
		water.transform.localPosition = Vector3.up * waterLevel;
	}

    public void Load( string fileName )
	{
		HiveObject.Log( $"\n\nLoading game {fileName}\n\n" );
   		Clear();
		Prepare();
		Interface.ValidateAll( true );
		challenge = null;

		World world = Serializer.Read<World>( fileName );
		Assert.global.AreEqual( world, this );
		this.fileName = fileName;
		if ( name == null || name == "" )
			name = "Incredible";

		if ( !challenge )
		{
			challenge = Challenge.Create();
			challenge.timeLimit = Constants.World.normalSpeedPerSecond * 60;
			challenge.mainBuildingContent = new List<int>();
			for ( int i = 0; i < (int)Item.Type.total; i++ )
			{
				if ( i == (int)Item.Type.soldier )
					challenge.mainBuildingContent.Add( 10000 );
				else
					challenge.mainBuildingContent.Add( -1 );
			}
			challenge.buildingMax = new List<int>();
			for ( int i = 0; i < (int)Building.Type.total; i++ )
			{
				if ( i == (int)Building.Type.guardHouse )
					challenge.buildingMax.Add( 4 );
				else
					challenge.buildingMax.Add( -1 );
			}
			challenge.Begin();
		}

		if ( operationHandler == null )
		{
			operationHandler = OperationHandler.Create();
			operationHandler.challenge = challenge;
			operationHandler.finishedFrameIndex = time;
		}

		foreach ( var player in players )
		{
			while ( player.stocksHaveNeed.Count < (int)Item.Type.total )
				player.stocksHaveNeed.Add( false );
			if ( player.buildingCounts.Count < (int)Building.Type.total )
			{
				while ( player.buildingCounts.Count < (int)Building.Type.total )
					player.buildingCounts.Add( 0 );
				for ( int i = 0; i < (int)Building.Type.total; i++ )
					player.buildingCounts[i] = 0;

				var buildingList = Resources.FindObjectsOfTypeAll<Building>();
				foreach ( var building in buildingList )
				{
					if ( building.owner == player )
						player.buildingCounts[(int)building.type]++;
				}
			}
			player.Start();
		}

		water = Water.Create().Setup( ground );
		water.transform.localPosition = Vector3.up * waterLevel;

		{
			var list = Resources.FindObjectsOfTypeAll<Road>();
			foreach ( var o in list )
			{
				if ( !o.ready )
				{
					HiveObject.Log( $"Removing road at {o.nodes[0].x}:{o.nodes[0].y}" );
					o.Remove( false );
				}
				o.ends[0] = o.nodes[0].flag;
				o.ends[1] = o.lastNode.flag;
			}
		}

		{
			var list = Resources.FindObjectsOfTypeAll<Worker>();
			foreach ( var o in list )
			{
				if ( o.type == Worker.Type.tinkerer )
					o.currentColor = Color.cyan;
				if ( o.type == Worker.Type.cart )
					o.currentColor = Color.white;
				if ( o.owner == null && players.Count > 0 )
					o.owner = players[0];
				//if ( o.taskQueue.Count > 0 && o.type == Worker.Type.tinkerer && o.itemsInHands[0] != null && o.itemsInHands[0].destination == null )
				//	o.itemsInHands[0].SetRawTarget( o.building );
			}
		}
		{
			var list = Resources.FindObjectsOfTypeAll<Building>();
			foreach ( var o in list )
			{
				if ( o.configuration == null )
					o.configuration = new Building.Configuration();
				if ( o.dispenser == null )
					o.dispenser = o.workerMate ?? o.worker;
				if ( o.construction.done )
					o.construction.worker = null;
				o.flagDirection = o.node.DirectionTo( o.flag.node );
				if ( o is Workshop s )
				{
					if ( s.worker && s.worker.node == s.node && s.worker.taskQueue.Count == 0 && s.worker.walkTo && s.gatherer )
						s.working = false;
					if ( s.outputPriority == ItemDispatcher.Priority.stock )
						s.outputPriority = ItemDispatcher.Priority.low;
					if ( s.mode == Workshop.Mode.unknown )
					{
						if ( s.outputPriority == ItemDispatcher.Priority.low )
							s.mode = Workshop.Mode.whenNeeded;
						if ( s.outputPriority == ItemDispatcher.Priority.high )
							s.mode = Workshop.Mode.always;
					}
					foreach ( var b in s.buffers )
						if ( b.stored > b.size )
							b.stored = b.size;
				}

				var t = o as Stock;
				if ( t && t.cart == null )
					t.cart = Stock.Cart.Create().SetupAsCart( t ) as Stock.Cart;
				if ( t )
				{
					while ( t.itemData.Count < (int)Item.Type.total )
						t.itemData.Add( new Stock.ItemTypeData( t, (Item.Type)t.itemData.Count ) );
					for ( int j = 0; j < t.itemData.Count; j++ )
					{
						if ( t.itemData[j].boss == null )
						{
							t.itemData[j].boss = t;
							t.itemData[j].itemType = (Item.Type)j;
						}
					}
#pragma warning disable 0618
					if ( t.content != null )
					{
						Assert.global.IsNotNull( t.inputMin );
						Assert.global.IsNotNull( t.inputMax );
						Assert.global.IsNotNull( t.outputMin );
						Assert.global.IsNotNull( t.outputMax );
						Assert.global.IsNotNull( t.onWay );
						Assert.global.IsNotNull( t.outputRoutes );
						for ( int i = 0; i < t.content.Count; i++ )
						{
							t.itemData[i].content = t.content[i];
							t.itemData[i].inputMin = t.inputMin[i];
							t.itemData[i].inputMax = t.inputMax[i];
							t.itemData[i].outputMin = t.outputMin[i];
							t.itemData[i].outputMax = t.outputMax[i];
							t.itemData[i].onWay = t.onWay[i];
							t.itemData[i].outputRoutes = t.outputRoutes[i];
						}
						t.content = null;
						t.inputMin = t.inputMax = t.outputMin = t.outputMax = null;
						t.onWay = null;
						t.outputRoutes = null;
					}
#pragma warning restore 0618
				}
			}
		}
		{
			var list = Resources.FindObjectsOfTypeAll<Resource>();
			foreach ( var o in list )
			{
				if ( o.charges > 1000 )
					o.infinite = true;
				if ( o.life.empty )
					o.life.reference = instance.time - 15000;
			}
		}
		{
			var list = Resources.FindObjectsOfTypeAll<Workshop.GetResource>();
			foreach ( var o in list )
			{
#pragma warning disable 0618
				if ( o.node != null )
				{
					foreach ( var resource in o.node.resources )
						if ( resource.type == o.resourceType )
							o.resource = resource;
				}
#pragma warning restore 0618
			}
		}
		{
			var list = Resources.FindObjectsOfTypeAll<Flag>();
			foreach ( var f in list )
			{
				if ( f.flattening == null )
					f.flattening = new Building.Flattening();
				if ( !f.freeSlotsWatch.isAttached )
					f.freeSlotsWatch.Attach( f.itemsStored );
			}
		}

		{
			var list = Resources.FindObjectsOfTypeAll<ItemDispatcher>();
			foreach ( var d in list )
			{
				while ( d.markets.Count < (int)Item.Type.total )
				{
					var market = ScriptableObject.CreateInstance<ItemDispatcher.Market>();
					market.Setup( d, (Item.Type)d.markets.Count );
					d.markets.Add( market );
				}
			}
		}

		{
			foreach ( var node in ground.nodes )
				node.AlignType();
		}
		gameInProgress = true;
		SetSpeed( speed );    // Just for the animators and sound

		HiveObject.Log( $"Next ID: {nextID}" );

		Interface.ValidateAll( true );
	}

	public void Save( string fileName )
	{
		if ( System.IO.Path.GetFileNameWithoutExtension( fileName ) == nextSaveFileName )
			saveIndex++;
		this.fileName = fileName;
		operationHandler.saveFileNames.Add( System.IO.Path.GetFileName( fileName ) );
		Serializer.Write( fileName, this, false );
	}

	public void SaveReplay( string name )
	{
		if ( System.IO.Path.GetFileNameWithoutExtension( name ) == nextReplayFileName )
			replayIndex++;
		operationHandler.SaveReplay( name );
	}

	public void Prepare()
	{
		var lightObject = new GameObject { layer = World.layerIndexPPVolume };
		var light = lightObject.AddComponent<Light>();
		light.type = UnityEngine.LightType.Directional;
		lightObject.name = "Sun";
		light.transform.Rotate( new Vector3( 60, 0, 0 ) );
		light.shadows = LightShadows.Soft;
		light.color = new Color( 1, 1, 1 );
		light.transform.SetParent( transform );
		var ppv = lightObject.AddComponent<PostProcessVolume>();
		ppv.profile = Resources.Load<PostProcessProfile>( "Post-processing Profile" );
		ppv.isGlobal = true;
		Assert.global.IsNotNull( ppv.profile );

		{
			// HACK The event system needs to be recreated after the main camera is destroyed,
			// otherwise there is a crash in unity
			Destroy( GameObject.FindObjectOfType<EventSystem>().gameObject );
			var esObject = new GameObject( "Event System" );
			esObject.AddComponent<EventSystem>();
			esObject.AddComponent<StandaloneInputModule>();
		}

		nodes = new GameObject( "Nodes" );
		nodes.transform.SetParent( transform );

		itemsJustCreated = new GameObject( "Items just created" );		// Temporary parent for items until they enter the logistic network. If they are just root in the scene, World.Clear would not destroy them.
		itemsJustCreated.transform.SetParent( transform );
	}

	public void Clear()
	{
		gameInProgress = false;
		players.Clear();
		eye = null;
		Destroy( operationHandler );
		operationHandler = null;
		// We could simply destroy each children, which would destroy the whole scene tree, in the end destroying the same objects
		// but if we do that, then granchilds are only destroyed at a later stage of the frame, making it possible that these objects are
		// still getting calls like Update. Those calls cause a lot of trouble for objects which supposed to be destroyed already.
		DestroyChildRecursive( transform );	

		foreach ( var ho in Resources.FindObjectsOfTypeAll<HiveObject>() )
		{
			ho.noAssert = true;
			ho.destroyed = true;	// To prevent decoration only roads getting an ID and registered after load
		}
		hiveObjects.Clear();
		newHiveObjects.Clear();

		massDestroy = true;
		root.Clear();
	}

	public static Transform FindChildRecursive( Transform parent, string substring )
	{
		foreach ( Transform child in parent )
		{
			if ( child.name.Contains( substring ) )
				return child;
			Transform grandChild = FindChildRecursive( child, substring );
			if ( grandChild )
				return grandChild;
		}

		return null;
	}

	public static void DestroyChildRecursive( Transform parent )
	{
		foreach ( Transform child in parent )
		{
			DestroyChildRecursive( child );
			Destroy( child.gameObject );
		}
	}

	public static void SetLayerRecursive( GameObject gameObject, int layer )
	{
		gameObject.layer = layer;
		foreach ( Transform child in gameObject.transform )
			SetLayerRecursive( child.gameObject, layer );
	}

	public static void SetMaterialRecursive( GameObject gameObject, Material material )
	{
		var renderer = gameObject.GetComponent<MeshRenderer>();
		if ( renderer )
		{
			Material[] materials = new Material[renderer.materials.Length];
			for ( int i = 0; i < materials.Length; i++ )
				materials[i] = material;
			renderer.materials = materials;
		}
		foreach ( Transform child in gameObject.transform )
			SetMaterialRecursive( child.gameObject, material );
	}

	public static void CollectRenderersRecursive( GameObject gameObject, List<MeshRenderer> list )
	{
		var renderer = gameObject.GetComponent<MeshRenderer>();
		if ( renderer != null )
			list.Add( renderer );
		foreach ( Transform child in gameObject.transform )
			CollectRenderersRecursive( child.gameObject, list );
	}

	public enum BlendMode
	{
		Opaque,
		Cutout,
		Fade,
		Transparent
	}

	public static void SetRenderMode( Material standardShaderMaterial, BlendMode blendMode )
	{
		switch ( blendMode )
		{
			case BlendMode.Opaque:
				standardShaderMaterial.SetInt( "_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One );
				standardShaderMaterial.SetInt( "_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero );
				standardShaderMaterial.SetInt( "_ZWrite", 1 );
				standardShaderMaterial.DisableKeyword( "_ALPHATEST_ON" );
				standardShaderMaterial.DisableKeyword( "_ALPHABLEND_ON" );
				standardShaderMaterial.DisableKeyword( "_ALPHAPREMULTIPLY_ON" );
				standardShaderMaterial.renderQueue = -1;
				break;
			case BlendMode.Cutout:
				standardShaderMaterial.SetInt( "_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One );
				standardShaderMaterial.SetInt( "_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero );
				standardShaderMaterial.SetInt( "_ZWrite", 1 );
				standardShaderMaterial.EnableKeyword( "_ALPHATEST_ON" );
				standardShaderMaterial.DisableKeyword( "_ALPHABLEND_ON" );
				standardShaderMaterial.DisableKeyword( "_ALPHAPREMULTIPLY_ON" );
				standardShaderMaterial.renderQueue = 2450;
				break;
			case BlendMode.Fade:
				standardShaderMaterial.SetInt( "_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha );
				standardShaderMaterial.SetInt( "_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha );
				standardShaderMaterial.SetInt( "_ZWrite", 0 );
				standardShaderMaterial.DisableKeyword( "_ALPHATEST_ON" );
				standardShaderMaterial.EnableKeyword( "_ALPHABLEND_ON" );
				standardShaderMaterial.DisableKeyword( "_ALPHAPREMULTIPLY_ON" );
				standardShaderMaterial.renderQueue = 3000;
				break;
			case BlendMode.Transparent:
				standardShaderMaterial.SetInt( "_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One );
				standardShaderMaterial.SetInt( "_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha );
				standardShaderMaterial.SetInt( "_ZWrite", 0 );
				standardShaderMaterial.DisableKeyword( "_ALPHATEST_ON" );
				standardShaderMaterial.DisableKeyword( "_ALPHABLEND_ON" );
				standardShaderMaterial.EnableKeyword( "_ALPHAPREMULTIPLY_ON" );
				standardShaderMaterial.renderQueue = 3000;
				break;
		}
	}

	public static void DrawObject( GameObject prefab, Matrix4x4 transform )
	{
		MeshFilter filter = prefab.GetComponent<MeshFilter>();
		MeshRenderer renderer = prefab.GetComponent<MeshRenderer>();
		var local = Matrix4x4.TRS( prefab.transform.localPosition, prefab.transform.localRotation, prefab.transform.localScale );
		if ( filter && renderer )
			Graphics.DrawMesh( filter.sharedMesh, transform * local, renderer.sharedMaterial, 0 );
		foreach ( Transform c in prefab.transform )
			DrawObject( c.gameObject, transform * local );
	}

	public void Reset()
	{
		ground.Reset();
		foreach ( var player in players )
			Assert.global.AreEqual( player.firstPossibleEmptyItemSlot, 0 );

		Validate( true );
	}

	public void GenerateResources()
	{	
		ores.Clear();
		oreCount = 0;
		animalSpawnerCount = 0;
		ores.Add( new Ore{ resourceType = Resource.Type.coal, ideal = settings.idealCoal } );
		ores.Add( new Ore{ resourceType = Resource.Type.iron, ideal = settings.idealIron } );
		ores.Add( new Ore{ resourceType = Resource.Type.gold, ideal = settings.idealGold } );
		ores.Add( new Ore{ resourceType = Resource.Type.salt, ideal = settings.idealSalt } );
		ores.Add( new Ore{ resourceType = Resource.Type.stone, ideal = settings.idealStone } );

		int TotalMissing() { int total = 0; foreach ( var ore in ores ) total += ore.missing; return total; };

		foreach ( var node in ground.nodes )
		{
			var r = new System.Random( rnd.Next() );
			if ( r.NextDouble() < settings.forestChance )
				node.AddResourcePatch( Resource.Type.tree, 8, 0.6f );
			if ( r.NextDouble() < settings.rocksChance )
				node.AddResourcePatch( Resource.Type.rock, 5, 0.5f );

			if ( node.CheckType( Node.Type.land ) )
			{
				foreach ( var o in Ground.areas[1] )
				{
					if ( node.Add( o ).type == Node.Type.underWater )
					{
						node.AddResource( Resource.Type.fish );
						break;
					}
				}
			}
		}

		while ( TotalMissing() > 0 ) 
		{
			int randomX = rnd.Next( ground.dimension ), randomY = rnd.Next( ground.dimension );
			bool created = false;
			for ( int x = 0; x < ground.dimension; x++ )
			{
				for ( int y = 0; y < ground.dimension; y++ )
				{
					created = CreateOrePatch( ground.GetNode( ( x + randomX ) % ground.dimension, (y + randomY ) % ground.dimension ) );

					if ( created )
						break;
				}
				if ( created )
					break;
			}
			if ( !created )
			{
				print( "Could not create enough underground resources, not enough hills in the world? ");
				//Assert.global.Fail();
				break;
			}
		}

		bool CreateOrePatch( Node node )
		{
			bool hasOre = false;
			foreach ( var resource in node.resources )
				if ( resource.underGround )
					hasOre = true;

			if ( node.type != Node.Type.hill || hasOre )
				return false;

			foreach ( var ore in ores )
			{
				if ( ore.missing == 0 )
					continue;

				var resourceCount = node.AddResourcePatch( ore.resourceType, settings.size / 6, 10 );
				ore.resourceCount += resourceCount;
				oreCount += resourceCount;
				return resourceCount > 0;
			}
			Assert.global.Fail();
			return true;
		}


		int idealAnimalSpawnerCount = (int)( settings.size * settings.size * settings.animalSpawnerChance );
		if ( idealAnimalSpawnerCount == 0 )
			idealAnimalSpawnerCount = 1;
		while ( animalSpawnerCount != idealAnimalSpawnerCount )
		{
			var location = ground.GetNode( rnd.Next( settings.size ), rnd.Next( settings.size ) );
			if ( Resource.Create().Setup( location, Resource.Type.animalSpawner ) != null )
				animalSpawnerCount++;
		}
	}

	public void SetSpeed( Speed speed )
	{
		this.speed = speed;
		var list3 = Resources.FindObjectsOfTypeAll<AudioSource>();
		foreach ( var o in list3 )
			o.pitch = timeFactor;
		float scale = speed == Speed.pause ? 0 : 1;
		if ( speed != Speed.pause )
			Time.fixedDeltaTime = 1f / ( timeFactor * Constants.World.normalSpeedPerSecond );
		Time.timeScale = scale;
	}

	public static int hourTickCount { get { return (int)( 60 * 60 / UnityEngine.Time.fixedDeltaTime ); } }

	public float waterLevel
	{
		get
		{	
			return settings.waterLevel * settings.maxHeight;
		}

		[Obsolete( "Compatibility with old files", true )]
		set
		{
			// Compatibility with old files
			settings.waterLevel = value;
		}
	}

	public void Validate( bool chain )
	{
		if ( !chain )
			return;
		ground.Validate( true );
		foreach ( var player in players )
			player.Validate();
		Assert.global.AreEqual( challenge, operationHandler.challenge );
		if ( chain )
		{
			eye.Validate( true );
			operationHandler?.Validate( true );
		}
	}

	[System.Serializable]
	public class Timer
	{
		public int reference = -1;

		public void Start( int delta = 0 )
		{
			reference = instance.time + delta;
		}
		public void Reset()
		{
			reference = -1;
		}
		[SerializeField]
		public int age
		{
			get
			{
				if ( empty )
					return 0;
				return instance.time - reference;
			}
		}
		public bool done { get { return !empty && age >= 0; } }
		[SerializeField]
		public bool empty { get { return reference == -1; } }
		public bool inProgress { get { return !empty && !done; } }
	}
}

[System.Serializable]
public struct SerializableColor
{
	public float r, g, b, a;
	public static implicit operator SerializableColor( Color unityColor )
	{
		SerializableColor s;
		s.r = unityColor.r;
		s.g = unityColor.g;
		s.b = unityColor.b;
		s.a = unityColor.a;
		return s;
	}
	public static implicit operator Color( SerializableColor serializableColor )
	{
		Color s;
		s.r = serializableColor.r;
		s.g = serializableColor.g;
		s.b = serializableColor.b;
		s.a = serializableColor.a;
		return s;
	}
}
