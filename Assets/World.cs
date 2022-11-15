using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.PostProcessing;
using System.Linq;
using System.Globalization;
using UnityEditor;
using System.Text.RegularExpressions;
using System.Reflection;
#pragma warning disable 0618

public class World : HiveObject
{
	public new Ground ground;
	public new string name;
	public int saveIndex;
	public new Eye eye;
	public new Light light;
	public int nextID = 1;
	public int frameSeed;
	public string fileName;
	public List<HiveObject> hiveObjects = new (), newHiveObjects = new ();
	public List<int> hiveListFreeSlots = new ();
	public List<Workshop.Configuration> workshopConfigurations;
	public List<float> itemTypeUsage = new ();
	public List<float> workshopTypeUsage = new ();
	public Water water;
	public bool main;
	[JsonIgnore]
	public bool dumpPossibleProductions, dumpTypeCountOnSave;

	static public bool massDestroy;
	static public int layerIndexMapOnly;
	static public int layerIndexGround;
	static public int layerIndexPPVolume;
	static public int layerIndexHighlightVolume;
	static public int layerIndexWater;
	static public int layerIndexBuildings;
	static public int layerIndexRoads;
	static public int layerIndexResources;
	static public int layerIndexUnits;
	static public int layerIndexItems;
	static public int layerIndexDecorations;
	static public Shader defaultShader;
	static public Shader defaultColorShader;
	static public Shader defaultMapShader;
	static public Shader defaultTextureShader;
	static public Shader defaultCutoutTextureShader;
	public GameObject nodes;
	public GameObject itemsJustCreated, playersAndTeams;

	public static void CRC( int code, OperationHandler.Event.CodeLocation caller )
	{
		if ( oh == null )
			return;

		game.operationHandler.RegisterEvent( OperationHandler.Event.Type.CRC, caller, code );
		game.operationHandler.currentCRCCode += code;
	}

	public string nextSaveFileName { get { return $"{name} {UIHelpers.TimeToString( time, ignoreSeconds:true, separator:'-' )} ({saveIndex})"; } }

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
	int randomSeed { set {} }
	[Obsolete( "Compatibility with old files", true )]
	bool insideCriticalSection { set {} }
	[Obsolete( "Compatibility with old files", true )]
	bool defeatReported { set {} }
	[Obsolete( "Compatibility with old files", true )]
	int replayIndex { set {} }
	[Obsolete( "Compatibility with old files", true )]
	int overseas;
	public Settings generatorSettings = new ();
	public bool repeating { get { return !generatorSettings.reliefSettings.island; } }

	[System.Serializable]
	public class Ore
	{
		public Resource.Type resourceType;
		public float weight;
		public int resourceCount;
		public static int totalResourceCount;

		[Obsolete( "Compatibility with old files", true )]
		float idealRatio { set {} }
		[Obsolete( "Compatibility with old files", true )]
		float ideal { set {} }

		public float share => (float)resourceCount / totalResourceCount;
		public float satisfaction => share / weight;
	}

	public int oreCount;
	public List<Ore> ores = new ();
	public int animalSpawnerCount, treeCount, rockCount;
	public List<int> maximumPossibleCraft;

	[Serializable]
	public class Settings
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

		public bool randomizeProductionChain = true;
		public float forestChance = 0.006f;
		public float rocksChance = 0.002f;
		public float animalSpawnerChance = 0.001f;
		public HeightMap.Settings reliefSettings = new (), forestSettings = new ();
		public int oreChargesPerNode = Constants.Resource.oreChargePerNodeDefault;

		public int seed;
		[JsonIgnore]
		public bool apply;  // For debug purposes only


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
		[Obsolete( "Compatibility with old files", true )]
		float idealIron { set {} }
		[Obsolete( "Compatibility with old files", true )]
		float idealCoal { set {} }
		[Obsolete( "Compatibility with old files", true )]
		float idealGold { set {} }
		[Obsolete( "Compatibility with old files", true )]
		float idealStone { set {} }
		[Obsolete( "Compatibility with old files", true )]
		float idealSalt { set {} }
		[Obsolete( "Compatibility with old files", true )]
		int mapSize { set {} }
		[Obsolete( "Compatibility with old files", true )]
		bool tileable { set {} }
		[Obsolete( "Compatibility with old files", true )]
		bool island { set {} }
		[Obsolete( "Compatibility with old files", true )]
		float borderLevel { set {} }
		[Obsolete( "Compatibility with old files", true )]
		float randomness { set {} }
		[Obsolete( "Compatibility with old files", true )]
		float noise { set {} }
		[Obsolete( "Compatibility with old files", true )]
		float randomnessDistribution { set {} }
		[Obsolete( "Compatibility with old files", true )]
		bool normalize { set {} }
		[Obsolete( "Compatibility with old files", true )]
		float adjustment { set {} }
		[Obsolete( "Compatibility with old files", true )]
		float squareDiamondRatio { set {} }
	}

	public int nodeCount { get { return ground.dimension * ground.dimension; } }

	public static void Initialize()
	{
		layerIndexMapOnly = LayerMask.NameToLayer( "Map only" );
		layerIndexGround = LayerMask.NameToLayer( "Ground" );
		layerIndexPPVolume = LayerMask.NameToLayer( "PPVolume" );
		layerIndexHighlightVolume = LayerMask.NameToLayer( "HighlightVolume" );
		layerIndexWater = LayerMask.NameToLayer( "Water" );
		layerIndexBuildings = LayerMask.NameToLayer( "Buildings" );
		layerIndexRoads = LayerMask.NameToLayer( "Roads" );
		layerIndexResources = LayerMask.NameToLayer( "Resources" );
		layerIndexUnits = LayerMask.NameToLayer( "Units" );
		layerIndexItems = LayerMask.NameToLayer( "Items" );
		layerIndexDecorations = LayerMask.NameToLayer( "Decorations" );
		Assert.global.IsTrue( layerIndexMapOnly != -1 && layerIndexBuildings != -1 && layerIndexGround != -1 && layerIndexUnits != -1 && layerIndexWater != -1 && layerIndexPPVolume != -1 && layerIndexHighlightVolume != -1 && layerIndexResources != -1 && layerIndexRoads != -1 && layerIndexItems != -1 && layerIndexDecorations != -1 );
		defaultShader = Shader.Find( "Standard" );
		defaultColorShader = Shader.Find( "Unlit/Color" );
		defaultTextureShader = Shader.Find( "Unlit/Texture" );
		defaultCutoutTextureShader = Shader.Find( "Unlit/Transparent Cutout" );
		defaultMapShader = Resources.Load<Shader>( "shaders/Map" );
	}

	public static World Create()
	{
		return new GameObject( "World" ).AddComponent<World>();
	}

	static public AudioSource CreateSoundSource( Component component )
	{
		var soundSource = component.gameObject.AddComponent<AudioSource>();
		soundSource.spatialBlend = 1;
		soundSource.minDistance = 1;
		soundSource.pitch = game.timeFactor;
		soundSource.maxDistance = Constants.Node.size * Constants.World.soundMaxDistance;
		return soundSource;
	}

	public int MaximumPossible( Item.Type itemType, bool rawOnly = false )
	{
		List<(float, Item.Type)> CollectCost( Item.Type itemType )
		{
			List<(float, Item.Type)> result = new ();
			if ( itemType == Item.Type.coal || itemType == Item.Type.iron || itemType == Item.Type.gold || itemType == Item.Type.silver || itemType == Item.Type.copper || itemType == Item.Type.stone )
			{
				result.Add( (1, itemType) );
				return result;
			}
			foreach ( var workshopConfiguration in workshopConfigurations )
			{
				if ( workshopConfiguration.outputType != itemType || workshopConfiguration.generatedInputs == null )
					continue;
				var factor = 1f / workshopConfiguration.outputStackSize;
				foreach ( var input in workshopConfiguration.generatedInputs )
				{
					foreach ( var raw in CollectCost( input ) )
					{
						float weight = raw.Item1;
						for ( int i = 0; i < result.Count; i++ )
						{
							if ( result[i].Item2 == raw.Item2 )
							{
								weight += result[i].Item1;
								result.RemoveAt( i );
								break;
							}
						}
					 	result.Add( ( factor * weight, raw.Item2 ) );
					}
				}
				return result;

			}
			result.Add( (1, itemType) );
			return result;
		}

		switch ( itemType )
		{
			case Item.Type.unknown or Item.Type.total:
				return -1;
			case Item.Type.hide:
				return animalSpawnerCount > 0 ? int.MaxValue : 0;
			case Item.Type.log or Item.Type.fish or Item.Type.grain or Item.Type.apple or Item.Type.water or Item.Type.corn or Item.Type.dung:
				return int.MaxValue;
			case Item.Type.stone or Item.Type.iron or Item.Type.copper or Item.Type.coal or Item.Type.silver or Item.Type.gold or Item.Type.salt:
			{
				int charges = 0;
				foreach ( var resourceType in (Resource.Type[])Enum.GetValues( typeof( Resource.Type ) ) )
				{
					if ( Resource.ItemType( resourceType ) == itemType )
					{
						foreach ( var node in ground.nodes )
						{
							foreach ( var resource in node.resources )
							{
								if ( resource.type != resourceType )
									continue;
								if ( resource.infinite )
									return int.MaxValue;
								charges += resource.charges;
							}
						}
					}
				}
				return charges;
			}
			default:
			{
				if ( rawOnly )
					return 0;
				int possible = 0;
				int source = int.MaxValue;
				foreach ( var raw in CollectCost( itemType ) )
				{
					var max = MaximumPossible( raw.Item2 );
					if ( max != int.MaxValue )
						max = (int)( max / raw.Item1 );
					source = Math.Min( max, source );
				}
				possible += source;
				return possible;
			}
		}
	}

	public void FixedUpdate()
	{
		if ( generatorSettings.apply )
		{
			generatorSettings.apply = false;
			var c = game.challenge;
			c.worldGenerationSettings = game.generatorSettings;
			game.NewGame( game.challenge, true );
			root.mainPlayer = game.players[0];
		}
		massDestroy = false;
	}

	public int lastChecksum = 0;


	public new void Update()
	{
		if ( light )
			light.shadows = HiveCommon.settings.shadows ? ( HiveCommon.settings.softShadows ? LightShadows.Soft : LightShadows.Hard ) : LightShadows.None;

		if ( dumpPossibleProductions )
		{
			foreach ( var itemType in (Item.Type[])Enum.GetValues(typeof(Item.Type)) )
				Log( $"{itemType}: {MaximumPossible( itemType )}" );
			dumpPossibleProductions = false;
		}

		base.Update();
	}

	public bool Join( string address, int port )
	{
		Log( $"Joining to server {address} port {port}", Severity.important );
		Clear();
		Prepare();
		return network.Join( address, port );
	}

	new void Start()
	{
		if ( water )
			water.transform.localPosition = Vector3.up * waterLevel;

		base.Start();
	}

	void UpdateWorkshopConfigurations()
	{
		var originalWorkshopConfigurations = Workshop.LoadConfigurations();

		if ( workshopConfigurations == null )
			return;

		foreach ( var configuration in workshopConfigurations )
		{
			foreach ( var originalConfiguration in originalWorkshopConfigurations )
			{
				if ( configuration.type != originalConfiguration.type )
					continue;

				configuration.inputBufferSize = originalConfiguration.inputBufferSize;
				configuration.gatheredResource = originalConfiguration.gatheredResource;
				configuration.gatheringRange = originalConfiguration.gatheringRange;
				configuration.relaxSpotCountNeeded = originalConfiguration.relaxSpotCountNeeded;
				configuration.producesDung = originalConfiguration.producesDung;
				configuration.commonInputs = originalConfiguration.commonInputs;
			}
		}
	}

	public void Generate()
	{
		var rnd = new System.Random( generatorSettings.seed );
		workshopConfigurations = Workshop.LoadConfigurations();
		GenerateWorkshopInputs( rnd.Next() );

		var heightMap = HeightMap.Create();
		heightMap.Setup( generatorSettings.reliefSettings, rnd.Next() );
		heightMap.Fill();

		var forestMap = HeightMap.Create();
		forestMap.Setup( generatorSettings.forestSettings, rnd.Next() );
		forestMap.Fill();

#if DEBUG
		heightMap.SavePNG( "height.png" );
		forestMap.SavePNG( "forest.png" );
#endif

		Clear();
		Prepare();

		ground = Ground.Create();
		ground.Setup( this, heightMap, forestMap, rnd, generatorSettings.size );
		GenerateResources( rnd.Next() );
		water = Water.Create().Setup( ground );
	}

	public void GenerateWorkshopInputs( int seed )
	{
		if ( !generatorSettings.randomizeProductionChain )
			seed = 0;
		System.Random rnd = new System.Random( seed );
		foreach ( var configuration in workshopConfigurations )
			configuration.generatedInputs = configuration.baseMaterials?.GenerateList( rnd );

		foreach ( var configuration in workshopConfigurations )
		{
			if ( configuration.productionTimeMax >= 0 )
			{
				configuration.productionTime = configuration.productionTimeMin + (int)( (configuration.productionTimeMax - configuration.productionTimeMin) * Math.Pow( rnd.NextDouble(), 2 ) );
				configuration.productionTime -= configuration.productionTime % Constants.World.normalSpeedPerSecond;
			}

			if ( configuration.outputCount > 0 )
				configuration.outputStackSize = (int)Math.Floor( configuration.outputCount + rnd.NextDouble() );
		}

		itemTypeUsage = new ();
		for ( int i = 0; i < (int)Item.Type.total; i++ )
			itemTypeUsage.Add( 0 );
		workshopTypeUsage = new ();
		for ( int i = 0; i < (int)Workshop.Type.total; i++ )
			workshopTypeUsage.Add( 0 );

		void AddWeight( Item.Type itemType, float weight, int level = 0 )
		{
			string indentation = "";
			for ( int i = 0; i < level; i++ )
				indentation += " ";
			Log( indentation + $"{itemType} {weight}" );
			itemTypeUsage[(int)itemType] += weight;
			foreach ( var configuration in workshopConfigurations )
			{
				if ( configuration.outputType != itemType )
					continue;
				var workshopWeight = weight / configuration.outputStackSize;
				workshopTypeUsage[(int)configuration.type] += workshopWeight;
				if ( configuration.generatedInputs == null )
					continue;

				var newWeight = configuration.commonInputs ? workshopWeight / configuration.generatedInputs.Count : workshopWeight;
				if ( newWeight < 0.001 )
					continue;
				foreach ( var input in configuration.generatedInputs )
					AddWeight( input, newWeight, level + 1 );
			}
		}

		AddWeight( Item.Type.plank, 0.5f );
		AddWeight( Item.Type.stone, 0.5f );
		AddWeight( Item.Type.soldier, 1 );
		workshopTypeUsage[(int)Workshop.Type.forester] = workshopTypeUsage[(int)Workshop.Type.woodcutter];
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

		if ( eye )
			Destroy( eye.gameObject );
		World world = Serializer.Read<World>( fileName );
		Assert.global.AreEqual( world, this );
		this.fileName = fileName;
		if ( name == null || name == "" )
			name = "Incredible";
		if ( lastChecksum != 0 && checksum != lastChecksum )
		{
			Log( $"Checksum mismatch in world {name} after load (calculated: {checksum}, stored: {lastChecksum})" );
			lastChecksum = 0;
		}

		UpdateWorkshopConfigurations();

		foreach ( var water in Resources.FindObjectsOfTypeAll<Water>() )
		{
			if ( water != world.water )
				water.Remove();
		}

		if ( ground.worldIndex < 0 )
		{
			for ( int i = 0; i < hiveObjects.Count; i++ )
				if ( hiveObjects[i] )
					hiveObjects[i].worldIndex = i;
		}

		if ( ground.nodes.Count == ( ground.dimension + 1 ) * ( ground.dimension + 1 ) )
		{
			List<Node> newNodes = new ();
			foreach ( var node in ground.nodes )
			{
				if ( node.x != ground.dimension && node.y != ground.dimension )
					newNodes.Add( node );
				else
					node.Remove();
			}
			ground.nodes = newNodes;
		}

		if ( water == null )	// Fix old files
			water = Water.Create().Setup( ground );
		if ( water.ground == null )
			water.ground = ground;

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
		if ( water.world == null )
			water.world = this;
			
		if ( eye.world == null )
			eye.world = this;
		if ( eye.cameraGrid == null )
		{
			eye.cameraGrid = Eye.CameraGrid.Create();
			eye.cameraGrid.Setup( eye );
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
				if ( o.watchStartFlag.source != o.ends[0].itemsStored )
				{
					o.watchStartFlag.Attach( o.ends[0].itemsStored );
					Log( $"Fixing watchStartFlag in road at {o.nodes[0].x}:{o.nodes[0].y}", Severity.error );
				}
				if ( o.watchEndFlag.source != o.ends[1].itemsStored )
				{
					o.watchEndFlag.Attach( o.ends[1].itemsStored );
					Log( $"Fixing watchEndFlag in road at {o.nodes[0].x}:{o.nodes[0].y}", Severity.error );
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
				if ( o.type == Unit.Type.hauler && o.road )
				{
					o.haulerRoadBegin.Attach( o.road.ends[0].itemsStored );
					o.haulerRoadEnd.Attach( o.road.ends[1].itemsStored );
				}
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
			foreach ( var node in ground.nodes )
				node.world = this;
		}
		{
			var list = Resources.FindObjectsOfTypeAll<Building>();
			foreach ( var o in list )
			{
				if ( o.configuration == null )
					o.configuration = new ();
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
					s.configuration = Workshop.GetConfiguration( this, s.type );
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
						t.itemData[j].boss = t;
						t.itemData[j].itemType = (Item.Type)j;
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
				if ( o.charges == 0 && !o.infinite )
					o.Remove();
				if ( o.life.empty )
					o.life.reference = game.time - 15000;
				if ( o.type == Resource.Type.pasturingAnimal && o.animals.Count == 1 )
				{
					Assert.global.IsNull( o.origin );
					o.origin = o.animals[0];
					o.animals.Clear();
				}
			}
		}

		{
			var list = Resources.FindObjectsOfTypeAll<Unit.DoAct>();
			foreach ( var d in list )
			{
				if ( d.act == null )
				{
					d.act = Unit.actLibrary.First();
					Log( $"Working around missing act in DoAct (boss id: {d.boss.id})", Severity.error );
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
					f.flattening = new ();
				if ( f.freeSlotsWatch.source != f.itemsStored )
				{
					Log( $"Fixing freeSlotsWatch in flag at {f.node.x}:{f.node.y} (ID: {f.id})", Severity.error );
					f.freeSlotsWatch.Attach( f.itemsStored );
				}
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

		List<Resource> toRemove = new ();
		foreach ( var node in ground.nodes )
		{
			if ( node.x == ground.dimension || node.y == ground.dimension )
			{
				foreach ( var resource in node.resources )
					toRemove.Add( resource );
			}
		}
		foreach ( var resource in toRemove )
			resource.Remove();

		while ( itemTypeUsage.Count < (int)Item.Type.total )
			itemTypeUsage.Add( 0 );
		while ( workshopTypeUsage.Count < (int)Workshop.Type.total )
			workshopTypeUsage.Add( 0 );
		foreach ( var hiveObject in hiveObjects )
			if ( hiveObject )
				hiveObject.world = this;

		if ( ground.grass.blocks.Count == 0 )
		{
			Log( "Fixing grass", Severity.error );
			ground.grass.Setup();
		}

		Interface.ValidateAll( true );
		network.SetState( game.demo ? Network.State.idle : Network.State.server );
	}

	public void Save( string fileName, bool manualSave, bool compact = false )
	{
		var match = Regex.Match( fileName, @".*/(.*) [\d-]+ \(\d+\)(?: manual| auto| exit)?\.json" );
		if ( match.Success )
			name = match.Groups.Last().Value;
		else
		{
			var freeMatch = Regex.Match( fileName, @".*/(.*)\.json" );
			if ( freeMatch.Success )
				name = freeMatch.Groups.Last().Value;
		}
		saveIndex++;
		this.fileName = fileName;
		if ( root.playerInCharge || manualSave )
		{
			Serializer.Write( fileName, this, false, logTypeCount:dumpTypeCountOnSave );
			dumpTypeCountOnSave = false;
		}
	}

	public void Prepare()
	{
		if ( main )
		{
			var lightObject = new GameObject { layer = World.layerIndexPPVolume };
			light = lightObject.AddComponent<Light>();
			light.type = UnityEngine.LightType.Directional;
			lightObject.name = "Sun";
			light.transform.Rotate( new Vector3( 60, 0, 0 ) );
			light.shadows = LightShadows.Soft;
			light.color = new Color( 1, 1, 1 );
			light.transform.SetParent( transform, false );
			bool depthOfField = Constants.Eye.depthOfField;
			if ( depthOfField )
			{
				var ppv = lightObject.AddComponent<PostProcessVolume>();
				ppv.isGlobal = true;
				ppv.profile = Instantiate( Resources.Load<PostProcessProfile>( "Post-processing Profile" ) );
				Assert.global.IsNotNull( ppv.profile );
			}
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
		nodes.transform.SetParent( transform, false );

		itemsJustCreated = new GameObject( "Items Just Created" );		// Temporary parent for items until they enter the logistic network. If they are just root in the scene, World.Clear would not destroy them.
		itemsJustCreated.transform.SetParent( transform, false );
		playersAndTeams = new GameObject( "Players And Teams" );
		playersAndTeams.transform.SetParent( transform, false );
		eye = Eye.Create();
		eye.Setup( this );
	}

	public virtual void Clear()
	{
		eye?.Remove();
		eye = null;

		ground?.Remove();
		ground = null;

		water?.Remove();
		water = null;

		if ( light )
			Destroy( light.gameObject );
		light = null;

		foreach ( var ho in hiveObjects )
		{
			if ( ho )
				ho.worldIndex = -1;
		}

		for ( int i = 0; i < hiveObjects.Count; i++ )
			assert.IsTrue( hiveObjects[i] == null || hiveObjects[i].destroyed, $"Object not correctly removed: {hiveObjects[i]}" );

		hiveObjects.Clear();
		newHiveObjects.Clear();
		hiveListFreeSlots.Clear();

		Destroy( transform.Find( "Nodes" )?.gameObject );

		massDestroy = true;
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
		{
			for ( int s = 0; s < filter.sharedMesh.subMeshCount; s++ )
				Graphics.DrawMesh( filter.sharedMesh, transform * local, renderer.sharedMaterial, 0, null, s );
		}
		foreach ( Transform c in prefab.transform )
			DrawObject( c.gameObject, transform * local );
	}

	public override void Reset()
	{
		ground.Reset();
		Validate( true );
	}

	public void GenerateResources( int seed )
	{	
		var rnd = new System.Random( seed );
		List<Resource> toRemove = new ();
		foreach ( var node in ground.nodes )
		{
			foreach ( var resource in node.resources )
				toRemove.Add( resource );
		}
		foreach ( var resource in toRemove )
			resource.Remove();

		ores.Clear();
		animalSpawnerCount = 0; 
		treeCount = 0;
		rockCount = 0;
		int rockCharges = 0;

		foreach ( var node in ground.nodes )
		{
			if ( !node.real )
				continue;
			var r = new System.Random( rnd.Next() );
			if ( r.NextDouble() < generatorSettings.forestChance )
				treeCount += node.AddResourcePatch( Resource.Type.tree, 8, 0.6f, rnd, 1 );
			if ( r.NextDouble() < generatorSettings.rocksChance )
			{
				rockCount += node.AddResourcePatch( Resource.Type.rock, 5, 0.5f, rnd, Constants.Resource.rockCharges );
				rockCharges += Constants.Resource.rockCharges;
			}

			if ( node.CheckType( Node.Type.land ) )
			{
				foreach ( var o in Ground.areas[1] )
				{
					if ( o && node.Add( o ).type == Node.Type.underWater )
					{
						node.AddResource( Resource.Type.fish, int.MaxValue );
						break;
					}
				}
			}
		}

		Ore.totalResourceCount = rockCharges;
		void AddOre( Resource.Type resourceType, int charges = 0 )
		{
			float weight = itemTypeUsage[(int)Resource.ItemType( resourceType )];
			if ( weight == 0 )
				return;

			foreach ( var mine in workshopConfigurations )
				if ( mine.outputType == Resource.ItemType( resourceType ) )
					weight /= mine.outputStackSize;

			ores.Add( new Ore{ resourceType = resourceType, weight = weight, resourceCount = charges } );
		}
		AddOre( Resource.Type.coal );
		AddOre( Resource.Type.iron );
		AddOre( Resource.Type.gold );
		AddOre( Resource.Type.salt );
		AddOre( Resource.Type.stone, rockCharges );
		AddOre( Resource.Type.copper );
		AddOre( Resource.Type.silver );
		for ( int patchCount = 0; patchCount < (int)( game.nodeCount * Constants.World.oreCountPerNode ); patchCount++ )
		{
			int randomX = rnd.Next( ground.dimension ), randomY = rnd.Next( ground.dimension );
			if ( Ore.totalResourceCount > 0 )
				ores.Sort( ( a, b ) => a.satisfaction.CompareTo( b.satisfaction ) );
			var ore = ores.First();
			bool go = true;
			for ( int x = 0; x < ground.dimension && go; x++ )
			{
				for ( int y = 0; y < ground.dimension && go; y++ )
				{
					var node = ground.GetNode( ( x + randomX) % ground.dimension, ( y + randomY ) % ground.dimension );
					bool hasOre = false;
					foreach ( var resource in node.resources )
						if ( resource.underGround )
							hasOre = true;

					if ( node.type != Node.Type.hill || hasOre )
						continue;
					go = false;

					var resourceCount = node.AddResourcePatch( ore.resourceType, generatorSettings.size / 6, 10, rnd, generatorSettings.oreChargesPerNode );
					ore.resourceCount += resourceCount;
					Ore.totalResourceCount += resourceCount;
				}
			}
		}

		int idealAnimalSpawnerCount = (int)( generatorSettings.size * generatorSettings.size * generatorSettings.animalSpawnerChance );
		if ( idealAnimalSpawnerCount == 0 )
			idealAnimalSpawnerCount = 1;
		if ( itemTypeUsage[(int)Item.Type.hide] == 0 )
			idealAnimalSpawnerCount = 0;
		while ( animalSpawnerCount != idealAnimalSpawnerCount )
		{
			var location = ground.GetNode( rnd.Next( generatorSettings.size ), rnd.Next( generatorSettings.size ) );
			if ( Resource.Create().Setup( location, Resource.Type.animalSpawner ) != null )
				animalSpawnerCount++;
		}

		maximumPossibleCraft = new ();
		while ( maximumPossibleCraft.Count < (int)Item.Type.total )
			maximumPossibleCraft.Add( MaximumPossible( (Item.Type)maximumPossibleCraft.Count ) );

		Log( "Generated resources:" );
		Log( $" - trees: {treeCount}" );
		Log( $" - rocks: {rockCount}" );
		Log( $" - caves: {animalSpawnerCount}" );
		foreach ( var ore in ores )
			Log( $" - {ore.resourceType}: {ore.resourceCount}" );
	}

	public static int hourTickCount { get { return (int)( 60 * 60 / UnityEngine.Time.fixedDeltaTime ); } }

	public float waterLevel
	{
		get
		{	
			return generatorSettings.waterLevel * generatorSettings.maxHeight;
		}

		[Obsolete( "Compatibility with old files", true )]
		set
		{
			// Compatibility with old files
			generatorSettings.waterLevel = value;
		}
	}

	public override void Validate( bool chain )
	{
		int nulls = 0;
		foreach ( var obj in hiveObjects )
		{
			if ( obj == null )
			{
				nulls++;
				continue;
			}
			if ( obj.destroyed || obj.location == null )
				continue;
			assert.IsTrue( obj.location.real, $"Not real object {obj} in the world" );
		}

		assert.AreEqual( nulls, hiveListFreeSlots.Count );
		assert.AreEqual( workshopTypeUsage.Count, (int)Workshop.Type.total );
		foreach ( var freeSlot in hiveListFreeSlots )
			assert.AreEqual( hiveObjects[freeSlot], null );

		if ( !chain )
			return;

		ground?.Validate( true );
		eye?.Validate( true );
	}
}

public class Game : World
{
	public List<Player> players = new ();
	public Player controllingPlayer;
	public int defeatedSimpletonCount;
	public List<Team> teams = new ();
	public bool roadTutorialShowed;
	public bool createRoadTutorialShowed;
	public bool gameInProgress;
	[JsonIgnore]
	public bool gameAdvancingInProgress;
	public Building lastAreaInfluencer;
	[JsonIgnore]
	public new Network network;
	public string nameOnNetwork;
	[JsonIgnore]
	public int advanceCharges;
	[JsonIgnore]
	public float lastNetworkSlowdown;
	public OperationHandler operationHandler;
	public new int time;
	public Speed speed;
	public System.Random rnd;
	public bool demo;

	static public Game instance;

	[Obsolete( "Compatibility with old files", true )]
	bool autoValidate { set {} }
	[Obsolete( "Compatibility with old files", true )]
	new Settings settings { set { generatorSettings = value; } }
	[Obsolete( "Compatibility with old files", true )]
	int currentSeed { set { generatorSettings.seed = value; } }

	public new static Game Create()
	{
		return new GameObject( "Game" ).AddComponent<Game>();
	}

	Game()
	{
		Assert.global.IsNull( instance );
		rnd = new ();
		instance = this;
		main = true;
	}

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

	public void Awake()
	{
		network = Network.Create();
	}

	new void Update()
	{
		advanceCharges = (int)timeFactor * Constants.World.allowedAdvancePerFrame;
        if ( oh && oh.orders.Count > 0 && network.state == Network.State.client )
        {
            if ( speed == Speed.normal && oh.orders.Count > Constants.Network.lagTolerance * Constants.World.normalSpeedPerSecond )
            {
                Interface.MessagePanel.Create( "Catching up server", autoclose:3 );
                SetSpeed( Speed.fast );
            }
            if ( speed == Speed.pause )
                SetSpeed( Speed.normal );
        }

		base.Update();
	}

	new void Start()
	{
		if ( operationHandler == null )
		{
			operationHandler = OperationHandler.Create();
			operationHandler.transform.SetParent( transform );
		}

		base.Start();
	}

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
			challenge.Begin( this );

		foreach ( var newHiveObject in newHiveObjects )
		{
			Assert.global.IsFalse( hiveObjects.Contains( newHiveObject ) );
			if ( hiveListFreeSlots.Count > 0 )
			{
				int i = hiveListFreeSlots.Last();
				hiveListFreeSlots.RemoveAt( hiveListFreeSlots.Count - 1 );
				assert.AreEqual( hiveObjects[i], null );
				hiveObjects[i] = newHiveObject;
				newHiveObject.worldIndex = i;
			}
			else
			{
				newHiveObject.worldIndex = hiveObjects.Count;
				hiveObjects.Add( newHiveObject );
			}

			if ( newHiveObject.priority )
			{
				int i = newHiveObject.worldIndex;
				while ( i > 0 && ( hiveObjects[i-1] == null || !hiveObjects[i-1].priority ) )
					i--;
				if ( i != newHiveObject.worldIndex && hiveObjects[i] )
				{
					var old = hiveObjects[i];
					hiveObjects[newHiveObject.worldIndex] = old;
					old.worldIndex = newHiveObject.worldIndex;
					hiveObjects[i] = newHiveObject;
					newHiveObject.worldIndex = i;
				}
			}
		}
		newHiveObjects.Clear();
		foreach ( var hiveObject in hiveObjects )
		{
			if ( hiveObject && !hiveObject.destroyed )
				hiveObject.GameLogicUpdate();
		}

		frameSeed = NextRnd( OperationHandler.Event.CodeLocation.worldOnEndOfLogicalFrame );
		CRC( frameSeed, OperationHandler.Event.CodeLocation.worldOnEndOfLogicalFrame );
		oh.OnEndGameStep();
		#if DEBUG
		lastChecksum = checksum;
		#else
		lastChecksum = 0;
		#endif
		time++;
		gameAdvancingInProgress = false;
		advanceCharges--;
		return true;
	}

	public new void FixedUpdate()
	{
		base.FixedUpdate();
		massDestroy = false;
		for ( int i = 0; i < timeFactor; i++ )
			Advance();
	}

	public void NewGame( Challenge challenge, bool keepCameraLocation = false )
	{
		var localChallenge = Challenge.Create().Setup( challenge );
		localChallenge.transform.SetParent( transform );
		generatorSettings = localChallenge.worldGenerationSettings;
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

		Debug.Log( "Starting new game with seed " + challenge.worldGenerationSettings.seed );
		HiveObject.Log( $"\n\nStarting new game with seed {challenge.worldGenerationSettings.seed}\n\n" );

		rnd = new System.Random( generatorSettings.seed );
		Generate();
		Interface.ValidateAll( true );

		this.challenge = localChallenge;
		defeatedSimpletonCount = 0;
		operationHandler = OperationHandler.Create();
		operationHandler.Setup( this );
		operationHandler.challenge = localChallenge;
		operationHandler.challenge.Begin( this );
#if DEBUG
		operationHandler.recordCRC = true;
#endif
		var mainTeam = Team.Create().Setup( this, Constants.Player.teamNames.Random(), Constants.Player.teamColors.First() );
		if ( mainTeam )
		{
			teams.Add( mainTeam );
			var mainPlayer = Simpleton.Create().Setup( Constants.Player.names.Random(), mainTeam );
			if ( mainPlayer )
				players.Add( mainPlayer );
		}
		for ( int i = 0; i < challenge.simpletonCount; i++ )
		{
			var team = Team.Create().Setup( this, Constants.Player.teamNames.Random(), Constants.Player.teamColors[(i+1)%Constants.Player.teamColors.Length] );
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
		demo = false;
	}

    public new void Load( string fileName )
	{
		HiveObject.Log( $"Loading game {fileName} (checksum: {checksum})" );
		base.Load( fileName );
		foreach ( var team in teams )
		{
			while ( team.stocksHaveNeed.Count < (int)Item.Type.total )
				team.stocksHaveNeed.Add( false );
			while ( team.constructionFactors.Count < (int)Building.Type.total )
				team.constructionFactors.Add( 1 );
			while ( team.buildingCounts.Count < (int)Building.Type.total )
				team.buildingCounts.Add( 0 );
		}

		if ( challenge?.productivityGoals != null )
		{
			while ( challenge.productivityGoals.Count < (int)Item.Type.total )
				challenge.productivityGoals.Add( -1 );
		}
		challenge?.ParseConditions( this );

		SetSpeed( speed, true );    // Just for the animators and sound

		HiveObject.Log( $"Time: {time}, Next ID: {nextID}" );
		operationHandler.LoadEvents( System.IO.Path.ChangeExtension( fileName, "bin" ) );

		if ( nameOnNetwork != null && nameOnNetwork != "" )
			network.StartServer( nameOnNetwork );
	}

	public new void Save( string fileName, bool manualSave, bool compact = false )
	{
		controllingPlayer = root.mainPlayer;
		Assert.global.IsFalse( gameAdvancingInProgress, "Trying to save while advancing world" );
		Log( $"Saving game {fileName} (checksum: {checksum})", Severity.important );
		if ( root.playerInCharge || manualSave )
		{
			if ( !compact )
			{
				operationHandler.SaveEvents( System.IO.Path.ChangeExtension( fileName, "bin" ) );
				operationHandler.saveFileNames.Add( System.IO.Path.GetFileName( fileName ) );
			}
			else
				operationHandler.PurgeCRCTable();
		}
		base.Save( fileName, manualSave, compact );
		oh.SaveReplay( fileName.Replace( "/Saves/", "/Replays/" ) );
	}

	new void Validate( bool chain )
	{
		if ( operationHandler )
			Assert.global.AreEqual( challenge, operationHandler.challenge );
		if ( chain )
			operationHandler?.Validate( true );
		base.Validate( chain );
		if ( !chain )
			return;
		foreach ( var team in teams )
			team.Validate();
		foreach ( var team in teams )
			team.Validate();
	}

	public override void Clear()
	{
		gameInProgress = false;

		RemoveElements( teams );
		players.Clear();
		teams.Clear();

		operationHandler?.Remove();
		operationHandler = null;

		challenge?.Remove();
		challenge = null;

		base.Clear();

		nameOnNetwork = null;

		Destroy( transform.Find( "Items just created" )?.gameObject );
		Destroy( transform.Find( "Players and teams" )?.gameObject );

		root.Clear();
	}

	public void SetSpeed( Speed speed, bool force = false )
	{
		if ( this.speed == speed && !force )
			return;

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

	[System.Serializable]
	public class Timer : Serializer.ICustomJson
	{
		public int reference = -1;

		public void Start( int delta = 0 )
		{
			reference = game.time + delta;
		}
		public void Reset()
		{
			reference = -1;
		}

        public void Serialize( JsonWriter writer )
        {
			writer.WriteValue( reference );
        }

        public void Deserialize( JsonReader reader )
        {
			Assert.global.AreEqual( reader.TokenType, JsonToken.Integer );
			if ( reader.Value is Int64 i )
				reference = (int)i;
			reader.Read();
        }

		[SerializeField]
		public int age
		{
			get
			{
				if ( empty )
					return 0;
				return game.time - reference;
			}
		}
		public int ageinf
		{
			get
			{
				if ( empty )
					return int.MaxValue;
				return game.time - reference;
			}
		}
		public bool done { get { return !empty && age >= 0; } }
		[SerializeField]
		public bool empty { get { return reference == -1; } }
		public bool inProgress { get { return !empty && !done; } }
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
		public World.Settings worldGenerationSettings = new ();
		public bool randomizeIslands = true;
		public Goal reachedLevel = Goal.none;
		public int maintain;
		public Timer maintainBronze = new (), maintainSilver = new (), maintainGold = new ();
		public float progress;
		public Timer life = new ();
		public List<float> productivityGoals;
		public List<int> mainBuildingContent;
		public List<int> buildingMax;
		public int timeLimit;
		public int playerCount;
		public int simpletonCount;
		public int simpletonCountToEliminate;
		public List<string> conditions;
		public string conditionsText;
		public bool allowTimeLeftLevels;
		public string bestSolutionReplayFileName;
		public Goal bestSolutionLevel;

		int worldSize { set { worldGenerationSettings.size = value; } }
		bool islandOnly { set { if ( value ) { randomizeIslands = false; worldGenerationSettings.reliefSettings.island = true; } } }
		bool landOnly { set { if ( value ) { randomizeIslands = false; worldGenerationSettings.reliefSettings.island = false; } } }
		bool infiniteResources { set { if ( value ) worldGenerationSettings.oreChargesPerNode = int.MaxValue; } }

		[Obsolete( "Compatibility with old files", true )]
		float soldierProductivityGoal { set {} }
		[Obsolete( "Compatibility with old files", true )]
		bool randomSeed { set {} }
		[Obsolete( "Compatibility with old files", true )]
		int seed { set { worldGenerationSettings.seed = value; } }
		[Obsolete( "Compatibility with old files", true )]
		bool fixedSeed { set {} }
		[Obsolete( "Compatibility with old files", true )]
		bool craftAllSoldiers { set {} }
		[Obsolete( "Compatibility with old files", true )]
		List<int> productivityGoalsByBuildingCount { set {} }

        public static Challenge Create()
		{
			return new GameObject( "Challenge" ).AddComponent<Challenge>();
		}

		public Challenge Setup( Challenge prototype )
		{
			foreach ( var member in GetType().GetMembers() )
			{
				if ( member is FieldInfo field && field.DeclaringType == GetType() )
					field.SetValue( this, field.GetValue( prototype ) );
			}

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
				transform.SetParent( HiveCommon.game.transform );
			base.Start();
		}

		public void Begin( World world )
		{
			life.Start();
			maintainBronze.Reset();
			maintainSilver.Reset();
			maintainGold.Reset();
			ParseConditions( world );
			reachedLevel = Goal.none;
		}

		public void Randomize()
		{
			worldGenerationSettings.seed = Interface.rnd.Next();
			if ( randomizeIslands )
				worldGenerationSettings.reliefSettings.island = Interface.rnd.NextDouble() > 0.5;
		}

		// This used to be GameLogicUpdate, but it seems to be better for the challenge not being part of the world, otherwise
		// saving a replay file involves the whole world
		public void CheckStatus()
		{
			var team = root.mainTeam;
			if ( team == null )
				return;
			var currentLevel = Goal.gold;
			int conditionCount = 0;
			conditionsText = "";
			progress = 1;

			if ( !team )
				return;		// Right after load this happens sometimes

			void CheckCondition( float current, float limit, bool allowLevels, string text = null, bool reversed = false, bool integer = false )
			{
				if ( limit < 0 )
					return;

				conditionCount++;
				if ( text != null )
					conditionsText += String.Format( text, current.ToString( integer ? "n0" : "n2" ), limit.ToString( integer ? "n0" : "n2" ) );	// TODO don't display fractions when number is integer
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
				{
					int stock = team.mainBuilding.itemData[i].content;
					if ( i == (int)Item.Type.soldier )
					{
						foreach ( var post in team.guardHouses )
							stock += post.soldiers.Count;
					}
					CheckCondition( stock, mainBuildingContent[i], true, $"{(Item.Type)i}s in headquarters {{0}}/{{1}}", integer:true );
				}
			}

			if ( buildingMax != null )
			{
				for ( int i = 0; i < buildingMax.Count; i++ )
				{
					if ( buildingMax[i] == int.MaxValue )
						continue;
					string buildingName = i < (int)Building.Type.stock ? ((Workshop.Type)i).ToString() : ((Building.Type)i).ToString();
					CheckCondition( team.buildingCounts[i], buildingMax[i], false, $"number of {buildingName}s {{0}}/{{1}}", true, integer:true );
				}
			}

			if ( timeLimit > 0 )
				CheckCondition( life.age, timeLimit, allowTimeLeftLevels, null, true );

			if ( simpletonCountToEliminate > 0 )
				CheckCondition( game.defeatedSimpletonCount, simpletonCountToEliminate, false, $"Defeated computer players {{0}}/{{1}}", integer:true );

			if ( conditionCount == 0 )
				return;

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

		public void ParseConditions( World world )
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
							mainBuildingContent = new ();
						Item.Type itemType;
						if ( !Enum.TryParse( p[1], out itemType ) )
						{
							print( $"Unknown item: {p[1]}" );
							break;
						}
						while ( mainBuildingContent.Count < (int)Item.Type.total )
							mainBuildingContent.Add( -1 );
						mainBuildingContent[(int)itemType] = int.Parse( p[2], CultureInfo.InvariantCulture );
						if ( p.Length > 3 && p[3] == "allPossible" && world.maximumPossibleCraft != null )
							mainBuildingContent[(int)itemType] += world.maximumPossibleCraft[(int)itemType];
						break;
					}
					case "production":
					{
						if ( productivityGoals == null )
							productivityGoals = new ();
						Item.Type itemType;
						if ( !Enum.TryParse( p[1], out itemType ) )
						{
							print( $"Unknown item: {p[1]}" );
							break;
						}
						while ( productivityGoals.Count < (int)Item.Type.total )
							productivityGoals.Add( -1 );
						productivityGoals[(int)itemType] = float.Parse( p[2], CultureInfo.InvariantCulture );
						if ( p.Length < 4 )
							break;
						float productivityByBuildingWeight = float.Parse( p[3] );
						foreach ( var configuration in world.workshopConfigurations )
						{
							if ( configuration.outputType == itemType )
								productivityGoals[(int)itemType] += productivityByBuildingWeight * configuration.productivity;
						}
						break;
					}
					case "buildingMax":
					{
						if ( buildingMax == null )
							buildingMax = new ();
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
						Log( $"Unknown condition type: {p[0]}" );
						break;
					}
				}
			}
		}

		public class List
		{
			public List<Challenge> list;
		}
	}

	public int NextRnd( OperationHandler.Event.CodeLocation caller, int limit = 0 )
	{
		if ( gameInProgress )
			Assert.global.IsTrue( gameAdvancingInProgress );
		int r = 0;
		if ( limit != 0 )
			r = rnd.Next( limit );
		else
			r = rnd.Next();
		oh?.RegisterEvent( OperationHandler.Event.Type.rndRequest, caller );
		return r;
	}

	public float NextFloatRnd( OperationHandler.Event.CodeLocation caller )
	{
		if ( gameInProgress )
			Assert.global.IsTrue( gameAdvancingInProgress );
		var r = (float)rnd.NextDouble();
		oh?.RegisterEvent( OperationHandler.Event.Type.rndRequestFloat, caller );
		return r;
	}

	[Obsolete( "Compatibility with old files", true )]
	List<Milestone> milestones { set {} }
	[Obsolete( "Compatibility with old files", true )]
	int currentMilestoneIndex { set {} }
	[Obsolete( "Compatibility with old files", true )]
	Timer goalReached { set {} }

	public Challenge challenge;
}
