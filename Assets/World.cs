using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.PostProcessing;
using System.Linq;
using System.Globalization;
using UnityEditor;
#pragma warning disable 0618

public class World : HiveObject
{
	public new Ground ground;
	public new string name;
	public int saveIndex;
	public int currentSeed;
	public List<Player> players = new List<Player>();
	public Player controllingPlayer;
	public int defeatedSimpletonCount;
	public List<Team> teams = new List<Team>();
	public Building lastAreaInfluencer;
	public new Eye eye;
	public new Light light;
	public bool gameInProgress;
	public new int time;
	public int nextID = 1;
	public int frameSeed;
	public bool roadTutorialShowed;
	public bool createRoadTutorialShowed;
	public string fileName;
	public LinkedList<HiveObject> hiveObjects = new LinkedList<HiveObject>(), newHiveObjects = new LinkedList<HiveObject>();
	[JsonIgnore]
	public bool gameAdvancingInProgress;
	public Speed speed;
	public OperationHandler operationHandler;
	public Water water;
	[JsonIgnore]
	public new Network network;
	[JsonIgnore]
	public int advanceCharges;
	[JsonIgnore]
	public float lastNetworkSlowdown;


	static public bool massDestroy;
	static System.Random rnd;
	static public World instance;
	static public int soundMaxDistance = 7;
	static public int layerIndexMapOnly;
	static public int layerIndexGround;
	static public int layerIndexPPVolume;
	static public int layerIndexHighlightVolume;
	static public int layerIndexWater;
	static public int layerIndexBuildings;
	static public int layerIndexResources;
	static public int layerIndexUnits;
	static public Shader defaultShader;
	static public Shader defaultColorShader;
	static public Shader defaultMapShader;
	static public Shader defaultTextureShader;
	static public Shader defaultCutoutTextureShader;
	static public GameObject nodes;
	static public GameObject itemsJustCreated, playersAndTeams;

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

	public static void CRC( int code, OperationHandler.Event.CodeLocation caller )
	{
		if ( oh == null )
			return;

		instance.operationHandler.RegisterEvent( OperationHandler.Event.Type.CRC, caller, code );
		instance.operationHandler.currentCRCCode += code;
	}

	public string nextSaveFileName { get { return $"{name} ({saveIndex})"; } }

	[Obsolete( "Compatibility with old files", true )]
	float lastAutoSave { set {} }
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
	[Obsolete( "Compatibility with old files", true )]
	bool defeatReported { set {} }
	[Obsolete( "Compatibility with old files", true )]
	int replayIndex { set {} }
	[Obsolete( "Compatibility with old files", true )]
	int overseas;
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

	public class Challenge : HiveObject
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
		public int playerCount;
		public int worldSize = 32;
		public int simpletonCount;
		public int simpletonCountToEliminate;
		public List<string> conditions;
		public string conditionsText;
		public bool allowTimeLeftLevels;
		public string bestSolutionReplayFileName;
		public Goal bestSolutionLevel;

		[Obsolete( "Compatibility with old files", true )]
		float soldierProductivityGoal { set {} }
		[Obsolete( "Compatibility with old files", true )]
		bool randomSeed { set {} }

        public static Challenge Create()
		{
			return new GameObject( "Challenge" ).AddComponent<Challenge>();
		}

		public Challenge Setup( Challenge prototype )
		{
			title = prototype.title;
			description = prototype.description;
			reachedLevel = prototype.reachedLevel;
			maintain = prototype.maintain;
			fixedSeed = prototype.fixedSeed;
			seed = prototype.seed;
			productivityGoals = prototype.productivityGoals;
			mainBuildingContent = prototype.mainBuildingContent;
			buildingMax = prototype.buildingMax;
			timeLimit = prototype.timeLimit;
			playerCount = prototype.playerCount;
			worldSize = prototype.worldSize;
			simpletonCount = prototype.simpletonCount;
			allowTimeLeftLevels = prototype.allowTimeLeftLevels;

			// HiveObject.Setup() would be nice to call, but at the moment I don't want to risk it
			return this;
		}

		void Awake()
		{
			id = -1;
		}

		new void Start()
		{
			if ( transform.parent == null )
				transform.SetParent( HiveCommon.world.transform );
			base.Start();
		}

		public void Begin()
		{
			life.Start();
			maintainBronze.Reset();
			maintainSilver.Reset();
			maintainGold.Reset();
			reachedLevel = Goal.none;
		}

		override public void GameLogicUpdate()
		{
			if ( world.challenge != this )
				return;

			var team = root.mainTeam;
			if ( team == null )
				return;
			var currentLevel = Goal.gold;
			conditionsText = "";
			progress = 1;

			if ( !team )
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
					if ( !reversed )
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
				Assert.global.AreEqual( productivityGoals.Count, team.itemProductivityHistory.Count );
				for ( int i = 0; i < productivityGoals.Count; i++ )
					CheckCondition( team.itemProductivityHistory[i].current, productivityGoals[i], true, $"{(Item.Type)i} productivity {{0}}/{{1}}" );
			}

			if ( mainBuildingContent != null )
			{
				for ( int i = 0; i < mainBuildingContent.Count; i++ )
					CheckCondition( team.mainBuilding.itemData[i].content, mainBuildingContent[i], true, $"{(Item.Type)i}s in headquarters {{0}}/{{1}}" );
			}

			if ( buildingMax != null )
			{
				for ( int i = 0; i < buildingMax.Count; i++ )
				{
					if ( buildingMax[i] == int.MaxValue )
						continue;
					string buildingName = i < (int)Building.Type.stock ? ((Workshop.Type)i).ToString() : ((Building.Type)i).ToString();
					CheckCondition( team.buildingCounts[i], buildingMax[i], false, $"number of {buildingName}s {{0}}/{{1}}", true );
				}
			}

			if ( timeLimit > 0 )
				CheckCondition( life.age, timeLimit, allowTimeLeftLevels, null, true );

			if ( simpletonCountToEliminate > 0 )
				CheckCondition( world.defeatedSimpletonCount, simpletonCountToEliminate, false, $"Defeated computer players {{0}}/{{1}}" );

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
			if ( conditions == null )
				return;

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
						mainBuildingContent[(int)itemType] = int.Parse( p[2], CultureInfo.InvariantCulture );
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
						productivityGoals[(int)itemType] = float.Parse( p[2], CultureInfo.InvariantCulture );
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
							buildingMax.Add( int.MaxValue );
						buildingMax[(int)buildingType] = int.Parse( p[2], CultureInfo.InvariantCulture );
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

	public int nodeCount { get { return ground.dimension * ground.dimension; } }
	public override int checksum 
	{ 
		get
		{
			int checksum = base.checksum;
			foreach ( var team in teams )
				checksum += team.checksum;
			return checksum;
		}
	}

	public static void Initialize()
	{
		layerIndexMapOnly = LayerMask.NameToLayer( "Map only" );
		layerIndexGround = LayerMask.NameToLayer( "Ground" );
		layerIndexPPVolume = LayerMask.NameToLayer( "PPVolume" );
		layerIndexHighlightVolume = LayerMask.NameToLayer( "HighlightVolume" );
		layerIndexWater = LayerMask.NameToLayer( "Water" );
		layerIndexBuildings = LayerMask.NameToLayer( "Buildings" );
		layerIndexResources = LayerMask.NameToLayer( "Resources" );
		layerIndexUnits = LayerMask.NameToLayer( "Units" );
		Assert.global.IsTrue( layerIndexMapOnly != -1 && layerIndexBuildings != -1 && layerIndexGround != -1 && layerIndexUnits != -1 && layerIndexWater != -1 && layerIndexPPVolume != -1 && layerIndexHighlightVolume != -1 && layerIndexResources != -1 );
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
		network = Network.Create();
	}

	public new World Setup()
	{
		rnd = new System.Random();
		return this;
	}

	static public AudioSource CreateSoundSource( Component component )
	{
		var soundSource = component.gameObject.AddComponent<AudioSource>();
		soundSource.spatialBlend = 1;
		soundSource.minDistance = 1;
		soundSource.pitch = instance.timeFactor;
		soundSource.maxDistance = Constants.Node.size * World.soundMaxDistance;
		return soundSource;
	}

	static public int NextRnd( OperationHandler.Event.CodeLocation caller, int limit = 0 )
	{
		if ( world.gameInProgress )
			Assert.global.IsTrue( instance.gameAdvancingInProgress );
		int r = 0;
		if ( limit != 0 )
			r = rnd.Next( limit );
		else
			r = rnd.Next();
		oh.RegisterEvent( OperationHandler.Event.Type.rndRequest, caller );
		return r;
	}

	static public float NextFloatRnd( OperationHandler.Event.CodeLocation caller )
	{
		if ( world.gameInProgress )
			Assert.global.IsTrue( instance.gameAdvancingInProgress );
		var r = (float)rnd.NextDouble();
		oh.RegisterEvent( OperationHandler.Event.Type.rndRequestFloat, caller );
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
			instance.NewGame( instance.challenge, true, false );
			root.mainPlayer = instance.players[0];
		}
		massDestroy = false;
		for ( int i = 0; i < timeFactor; i++ )
			Advance();
	}

	public int lastChecksum = 0;

	public bool Advance()
	{
		if ( !oh || advanceCharges == 0 )
			return false;

		if ( !oh.readyForNextGameLogicStep )
		{
			if ( speed == Speed.normal && lastNetworkSlowdown + 1 < Time.unscaledTime )
				SetSpeed( Speed.pause );
			if ( speed == Speed.fast )
			{
				SetSpeed( Speed.normal );
				lastNetworkSlowdown = Time.unscaledTime;
			}
			return false;
		}

		#if DEBUG
		if ( lastChecksum > 0 )
			Assert.global.AreEqual( checksum, lastChecksum, "Game state was modified between World.Advance calls" );
		#endif
		gameAdvancingInProgress = true;
		oh?.RegisterEvent( OperationHandler.Event.Type.frameStart, OperationHandler.Event.CodeLocation.worldNewFrame, time );
		oh.OnBeginGameStep();
		network.OnBeginGameStep();
		rnd = new System.Random( frameSeed );
		CRC( frameSeed, OperationHandler.Event.CodeLocation.worldFrameStart );

		if ( challenge.life.empty )
			challenge.Begin();

		foreach ( var newHiveObject in newHiveObjects )
		{
			Assert.global.IsFalse( hiveObjects.Contains( newHiveObject ) );
			hiveObjects.AddLast( newHiveObject );
		}
		newHiveObjects.Clear();
		List<HiveObject> toRemove = new List<HiveObject>();
		foreach ( var hiveObject in hiveObjects )
		{
			if ( hiveObject && !hiveObject.destroyed )
				hiveObject.GameLogicUpdate();
			else
				toRemove.Add( hiveObject );
		}

		foreach ( var h in toRemove )
			hiveObjects.Remove( h );
		toRemove.Clear();

		frameSeed = NextRnd( OperationHandler.Event.CodeLocation.worldOnEndOfLogicalFrame );
		CRC( frameSeed, OperationHandler.Event.CodeLocation.worldOnEndOfLogicalFrame );
		oh.OnEndGameStep();
		#if DEBUG
		lastChecksum = checksum;
		#endif
		time++;
		gameAdvancingInProgress = false;
		advanceCharges--;
		return true;
	}

	new void Update()
	{
		advanceCharges = (int)timeFactor * Constants.World.allowedAdvancePerFrame;
        if ( oh && oh.orders.Count > Constants.Network.lagTolerance * Constants.World.normalSpeedPerSecond && network.state == Network.State.client )
        {
            if ( speed == Speed.normal )
            {
                Interface.MessagePanel.Create( "Catching up server", autoclose:3 );
                SetSpeed( Speed.fast );
            }
            if ( speed == Speed.pause )
                SetSpeed( Speed.normal );
        }
		base.Update();
	}

	public void Join( string address, int port )
	{
		Log( $"Joining to server {address} port {port}", true );
		Clear();
		Prepare();
		network.Join( address, port );
	}

	public void NewGame( Challenge challenge, bool keepCameraLocation = false, bool resetSettings = true )
	{
		if ( resetSettings )
			settings = ScriptableObject.CreateInstance<Settings>();
		nextID = 1;
		time = 0;
		lastChecksum = 0;
		string pattern = challenge.title + " #{0}";
		name = String.Format( pattern, Interface.FirstUnusedIndex( Application.persistentDataPath + "/Saves", pattern + " (0).json" ) );
		saveIndex = 0;
		SetSpeed( Speed.normal );
		if ( operationHandler )
			Destroy( operationHandler );
		fileName = "";
		roadTutorialShowed = false;
		createRoadTutorialShowed = false;
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

		defeatedSimpletonCount = 0;
		challenge.Register();
		operationHandler = OperationHandler.Create().Setup();
		operationHandler.challenge = challenge;
#if DEBUG
		operationHandler.recordCRC = true;
#endif
		ground = Ground.Create();
		ground.Setup( this, heightMap, forestMap, settings.size );
		GenerateResources();
		water = Water.Create().Setup( ground );
		var mainTeam = Team.Create().Setup( Constants.Player.teamNames.Random(), Constants.Player.teamColors.First() );
		if ( mainTeam )
		{
			teams.Add( mainTeam );
			var mainPlayer = Simpleton.Create().Setup( Constants.Player.names.Random(), mainTeam );
			if ( mainPlayer )
				players.Add( mainPlayer );
		}
		for ( int i = 0; i < challenge.simpletonCount; i++ )
		{
			var team = Team.Create().Setup( Constants.Player.teamNames.Random(), Constants.Player.teamColors[(i+1)%Constants.Player.teamColors.Length] );
			teams.Add( team );
			var player = Simpleton.Create().Setup( Constants.Player.names.Random(), team );	// TODO Avoid using the same name again
			player.active = true;
			players.Add( player );
		}
		ground.RecalculateOwnership();

		water.transform.localPosition = Vector3.up * waterLevel;

		if ( keepCameraLocation )
		{
			eye.x = oldEye.x;
			eye.y = oldEye.y;
			eye.altitude = oldEye.altitude;
			eye.targetAltitude = oldEye.targetAltitude;
			eye.direction = oldEye.direction;
		}
		Interface.ValidateAll( true );
		frameSeed = NextRnd( OperationHandler.Event.CodeLocation.worldNewGame );

		network.SetState( Network.State.server );
		gameInProgress = true;
	}

	new void Start()
	{
		if ( operationHandler == null )
		{
			operationHandler = OperationHandler.Create();
			operationHandler.transform.SetParent( transform );
		}
		if ( water )
			water.transform.localPosition = Vector3.up * waterLevel;

		base.Start();
	}

    public void Load( string fileName )
	{
		ValueType GetValue<ValueType>( object from, string field ) where ValueType : class
		{
			var fieldInfo = from.GetType().GetField( field );
			if ( fieldInfo != null )
				return fieldInfo.GetValue( from ) as ValueType;
			else
				return null;
		}
   		Clear();
		Prepare();
		challenge = null;

		if ( eye )
			Destroy( eye.gameObject );
		World world = Serializer.Read<World>( fileName );
		Assert.global.AreEqual( world, this );
		this.fileName = fileName;
		if ( name == null || name == "" )
			name = "Incredible";
		HiveObject.Log( $"Loading game {fileName} (checksum: {checksum})" );
		if ( lastChecksum != 0 )
			Assert.global.AreEqual( checksum, lastChecksum, "Checksum mismatch in world" );

		foreach ( var water in Resources.FindObjectsOfTypeAll<Water>() )
		{
			if ( water != world.water )
				water.Remove();
		}

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
					challenge.buildingMax.Add( int.MaxValue );
			}
			challenge.Begin();
		}

		if ( ground.nodes.Count == ( ground.dimension + 1 ) * ( ground.dimension + 1 ) )
		{
			List<Node> newNodes = new List<Node>();
			foreach ( var node in ground.nodes )
			{
				if ( node.x != ground.dimension && node.y != ground.dimension )
					newNodes.Add( node );
				else
					node.Remove();
			}
			ground.nodes = newNodes;
		}

		if ( operationHandler == null )
		{
			operationHandler = OperationHandler.Create();
			operationHandler.challenge = challenge;
		}

		foreach ( var player in players )
		{
			if ( !teams.Contains( player.team ) )
				teams.Add( player.team );
			if ( player.name == null )
				player.name = Constants.Player.names.Random();
			if ( player is Simpleton simpleton && simpleton.blockedNodes.Count > 100 )
				simpleton.blockedNodes.Clear();
			if ( player.messages == null )
				player.messages = new LinkedList<Player.Message>();
		}

		foreach ( var team in teams )
		{
			while ( team.stocksHaveNeed.Count < (int)Item.Type.total )
				team.stocksHaveNeed.Add( false );
			if ( team.buildingCounts.Count < (int)Building.Type.total )
			{
				while ( team.buildingCounts.Count < (int)Building.Type.total )
					team.buildingCounts.Add( 0 );
				for ( int i = 0; i < (int)Building.Type.total; i++ )
					team.buildingCounts[i] = 0;

				var buildingList = Resources.FindObjectsOfTypeAll<Building>();
				foreach ( var building in buildingList )
				{
					if ( building.team == team )
						team.buildingCounts[(int)building.type]++;
				}
			}
			if ( team.name == null )
				team.name = Constants.Player.teamNames.Random();
		}

		if ( water == null )	// Fix old files
			water = Water.Create().Setup( ground );

		{
			foreach ( var ho in Resources.FindObjectsOfTypeAll<HiveObject>() )
			{
				if ( ho is Interface )
					continue;
				if ( ho.simpletonData != null )
				{
					ho.simpletonData.hiveObject = ho;
					if ( ho.simpletonData.possiblePartner is Stock )
						ho.simpletonData.possiblePartner = null;
				}
			}
		}
		{
			var list = Resources.FindObjectsOfTypeAll<Road>();
			foreach ( var o in list )
			{
				if ( !o.ready )
				{
					HiveObject.Log( $"Removing road at {o.nodes[0].x}:{o.nodes[0].y}" );
					o.Remove();
				}
				o.ends[0] = o.nodes[0].flag;
				o.ends[1] = o.lastNode.flag;
				if ( GetValue<Player>( o, "owner" ) )
					o.team = GetValue<Player>( o, "owner" ).team;
				if ( !o.team.roads.Contains( o ) && !o.destroyed )
				{
					o.team.roads.Add( o );
					Assert.global.Fail();
				}
			}
		}

		{
			var list = Resources.FindObjectsOfTypeAll<Item>();
			foreach ( var o in list )
			{
				if ( GetValue<Player>( o, "owner" ) )
					o.team = GetValue<Player>( o, "owner" ).team;
				if ( o.flag && o.flag.team != o.team )
					o.SetTeam( o.flag.team );
			}
		}

		{
			var list = Resources.FindObjectsOfTypeAll<GuardHouse>();
			foreach ( var o in list )
			{
				if ( !o.team.guardHouses.Contains( o ) && !o.destroyed )
				{
					o.team.guardHouses.Add( o );
					Assert.global.Fail();
				}
			}
		}

		{
			var list = Resources.FindObjectsOfTypeAll<Unit>();
			foreach ( var o in list )
			{
				if ( o.type == Unit.Type.tinkerer )
					o.currentColor = Color.cyan;
				if ( o.type == Unit.Type.cart )
					o.currentColor = Color.white;
				if ( o.team == null && teams.Count > 0 )
					o.team = teams[0];
				if ( o.type == Unit.Type.hauler && o.road )
				{
					o.haulerRoadBegin.Attach( o.road.ends[0].itemsStored );
					o.haulerRoadEnd.Attach( o.road.ends[1].itemsStored );
				}
				//if ( o.taskQueue.Count > 0 && o.type == Unit.Type.tinkerer && o.itemsInHands[0] != null && o.itemsInHands[0].destination == null )
				//	o.itemsInHands[0].SetRawTarget( o.building );
			}
		}
		{
			foreach ( var node in ground.nodes )
			{
				if ( GetValue<Player>( node, "owner" ) )
					node.team = GetValue<Player>( node, "owner" ).team;
			}
		}
		{
			var list = Resources.FindObjectsOfTypeAll<Building>();
			foreach ( var o in list )
			{
				if ( o.configuration == null )
					o.configuration = new Building.Configuration();
				if ( o.dispenser == null )
					o.dispenser = o.tinkererMate ?? o.tinkerer;
				if ( o.construction.done )
					o.construction.builder = null;
				o.flagDirection = o.node.DirectionTo( o.flag.node );
				if ( o is Workshop s )
				{
					if ( s.tinkerer && s.tinkerer.node == s.node && s.tinkerer.taskQueue.Count == 0 && s.tinkerer.walkTo && s.gatherer )
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
					if ( !s.team.workshops.Contains( s ) && !s.destroyed )
					{
						s.team.workshops.Add( s );
						Assert.global.Fail();
					}
				}

				var t = o as Stock;
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
				if ( GetValue<Player>( o, "owner" ) )
					o.team = GetValue<Player>( o, "owner" ).team;
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
				if ( o.type == Resource.Type.pasturingAnimal && o.animals.Count == 1 )
				{
					Assert.global.IsNull( o.origin );
					o.origin = o.animals[0];
					o.animals.Clear();
				}
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
				if ( GetValue<Player>( f, "owner" ) )
					f.team = GetValue<Player>( f, "owner" ).team;
				if ( !f.team.flags.Contains( f ) && !f.destroyed )
				{
					f.team.flags.Add( f );
					Assert.global.Fail();
				}
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
				if ( GetValue<Player>( d, "owner" ) )
					d.team = GetValue<Player>( d, "owner" ).team;
			}
		}

		{
			foreach ( var node in ground.nodes )
				node.AlignType();
		}
		gameInProgress = true;
		SetSpeed( speed );    // Just for the animators and sound

		HiveObject.Log( $"Time: {time}, Next ID: {nextID}" );
		operationHandler.LoadEvents( System.IO.Path.ChangeExtension( fileName, "bin" ) );

		List<Resource> toRemove = new List<Resource>();
		foreach ( var node in ground.nodes )
		{
			if ( node.x == ground.dimension || node.y == ground.dimension )
			{
				foreach ( var resource in node.resources )
				{
					toRemove.Add( resource );
					if ( resource.registered )
					{
						resource.registered = false;
						hiveObjects.Remove( resource );
					}
				}
			}
		}
		foreach ( var resource in toRemove )
			resource.Remove();

		Interface.ValidateAll( true );
		bool demoMode = fileName.Contains( "demolevel" );
		network.SetState( demoMode ? Network.State.idle : Network.State.server );
	}

	public void Save( string fileName, bool manualSave, bool compact = false )
	{
		controllingPlayer = root.mainPlayer;
		Assert.global.IsFalse( gameAdvancingInProgress, "Trying to save while advancing world" );
		Log( $"Saving game {fileName} (checksum: {checksum})", true );
		if ( fileName.Contains( nextSaveFileName ) )
			saveIndex++;
		this.fileName = fileName;
		if ( root.playerInCharge || manualSave )
		{
			if ( !compact )
			{
				operationHandler.SaveEvents( System.IO.Path.ChangeExtension( fileName, "bin" ) );
				operationHandler.saveFileNames.Add( System.IO.Path.GetFileName( fileName ) );
			}
			else
				operationHandler.PurgeCRCTable();
			Serializer.Write( fileName, this, false );
		}
	}

	public void Prepare()
	{
		var lightObject = new GameObject { layer = World.layerIndexPPVolume };
		light = lightObject.AddComponent<Light>();
		light.type = UnityEngine.LightType.Directional;
		lightObject.name = "Sun";
		light.transform.Rotate( new Vector3( 60, 0, 0 ) );
		light.shadows = LightShadows.Soft;
		light.color = new Color( 1, 1, 1 );
		light.transform.SetParent( transform );
		bool depthOfField = Constants.Eye.depthOfField;
		if ( depthOfField )
		{
			var ppv = lightObject.AddComponent<PostProcessVolume>();
			ppv.isGlobal = true;
			ppv.profile = Instantiate( Resources.Load<PostProcessProfile>( "Post-processing Profile" ) );
			Assert.global.IsNotNull( ppv.profile );
		}

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
		playersAndTeams = new GameObject( "Players and teams" );
		playersAndTeams.transform.SetParent( transform );
		eye = Eye.Create().Setup( this );
	}

	public void Clear()
	{
		gameInProgress = false;

		RemoveElements( teams );
		players.Clear();
		teams.Clear();

		eye?.Remove();
		eye = null;

		operationHandler?.Remove();
		operationHandler = null;

		ground?.Remove();
		ground = null;

		water?.Remove();
		water = null;

		if ( light )
			Destroy( light.gameObject );
		light = null;

		hiveObjects.Clear();
		newHiveObjects.Clear();

		Destroy( transform.Find( "Items just created" )?.gameObject );
		Destroy( transform.Find( "Players and teams" )?.gameObject );
		Destroy( transform.Find( "Nodes" )?.gameObject );

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
		var skinnedRenderer = gameObject.GetComponent<SkinnedMeshRenderer>();
		if ( skinnedRenderer )
		{
			Material[] materials = new Material[skinnedRenderer.materials.Length];
			for ( int i = 0; i < materials.Length; i++ )
				materials[i] = material;
			skinnedRenderer.materials = materials;
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

	public override void Reset()
	{
		ground.Reset();
		foreach ( var team in teams )
			Assert.global.AreEqual( team.firstPossibleEmptyItemSlot, 0 );

		Validate( true );
	}

	public void GenerateResources( float oreStrength = 1 )
	{	
		List<Resource> toRemove = new List<Resource>();
		foreach ( var node in ground.nodes )
		{
			foreach ( var resource in node.resources )
				toRemove.Add( resource );
		}
		foreach ( var resource in toRemove )
			resource.Remove();

		ores.Clear();
		oreCount = 0;
		animalSpawnerCount = 0; 
		int treeCount = 0, rockCount = 0;
		ores.Add( new Ore{ resourceType = Resource.Type.coal, ideal = settings.idealCoal / oreStrength } );
		ores.Add( new Ore{ resourceType = Resource.Type.iron, ideal = settings.idealIron / oreStrength } );
		ores.Add( new Ore{ resourceType = Resource.Type.gold, ideal = settings.idealGold / oreStrength } );
		ores.Add( new Ore{ resourceType = Resource.Type.salt, ideal = settings.idealSalt / oreStrength } );
		ores.Add( new Ore{ resourceType = Resource.Type.stone, ideal = settings.idealStone / oreStrength } );

		int TotalMissing() { int total = 0; foreach ( var ore in ores ) total += ore.missing; return total; };

		Log( $"Total hill spots needed: {TotalMissing()}" );
		foreach ( var node in ground.nodes )
		{
			if ( !node.real )
				continue;
			var r = new System.Random( rnd.Next() );
			if ( r.NextDouble() < settings.forestChance )
				treeCount += node.AddResourcePatch( Resource.Type.tree, 8, 0.6f );
			if ( r.NextDouble() < settings.rocksChance )
				rockCount += node.AddResourcePatch( Resource.Type.rock, 5, 0.5f );

			if ( node.CheckType( Node.Type.land ) )
			{
				foreach ( var o in Ground.areas[1] )
				{
					if ( o && node.Add( o ).type == Node.Type.underWater )
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
					created = CreateOrePatch( ground.GetNode( ( x + randomX ) % ground.dimension, (y + randomY ) % ground.dimension ), oreStrength );

					if ( created )
						break;
				}
				if ( created )
					break;
			}
			if ( !created )
			{
				Log( $"Failed with strength {oreStrength}, trying with {1.25f * oreStrength}" );
				GenerateResources( oreStrength * 1.25f );
				return;
			}
		}

		bool CreateOrePatch( Node node, float strength )
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

				var resourceCount = node.AddResourcePatch( ore.resourceType, settings.size / 6, 10, strength );
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

		Log( "Generated resources:" );
		Log( $" - trees: {treeCount}" );
		Log( $" - rocks: {rockCount}" );
		Log( $" - caves: {animalSpawnerCount}" );
		foreach ( var ore in ores )
			Log( $" - {ore.resourceType}: {ore.resourceCount} (missing: {ore.missing})" );
	}

	public void SetSpeed( Speed speed )
	{
		// Ideally this function would simply change Time.timeScale, but setting that to 0 leads to some problems, for example Physics.Raycast works on a frozen scene 
		// (transformation changes made to objects while timeScale==0 are ignored), which ruins mouse clicks on some objects.
		this.speed = speed;
		var list1 = Resources.FindObjectsOfTypeAll<Animator>();
		foreach ( var o in list1 )
			o.speed = timeFactor;
		var list2 = Resources.FindObjectsOfTypeAll<ParticleSystem>();
		foreach ( var o in list2 )
		{
#if UNITY_EDITOR
			if ( PrefabUtility.IsPartOfAnyPrefab( o ) )
				continue;
#endif
			var mainModule = o.main;
			mainModule.simulationSpeed = timeFactor;
		}		var list3 = Resources.FindObjectsOfTypeAll<AudioSource>();
		foreach ( var o in list3 )
			o.pitch = timeFactor;
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

	public override void Validate( bool chain )
	{
		foreach ( var obj in hiveObjects )
		{
			if ( obj.location == null || obj.destroyed )
				continue;
			assert.IsTrue( obj.location.real, $"Not real object {obj} in the world" );
		}

		if ( !chain )
			return;

		ground?.Validate( true );
		foreach ( var team in teams )
			team.Validate();
		foreach ( var team in teams )
			team.Validate();
		if ( operationHandler )
			Assert.global.AreEqual( challenge, operationHandler.challenge );
		if ( chain )
		{
			eye?.Validate( true );
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
		public int ageinf
		{
			get
			{
				if ( empty )
					return int.MaxValue;
				return instance.time - reference;
			}
		}
		public bool done { get { return !empty && age >= 0; } }
		[SerializeField]
		public bool empty { get { return reference == -1; } }
		public bool inProgress { get { return !empty && !done; } }
	}
}