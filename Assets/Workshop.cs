using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

public class Workshop : Building
{
	public int output;
	public Ground.Area outputArea = new ();
	public ItemDispatcher.Priority outputPriority = ItemDispatcher.Priority.low;
	public float craftingProgress;
	public bool working;
	new public Type type = Type.unknown;
	public List<Buffer> buffers = new ();
	public Item.Type lastUsedInput = Item.Type.unknown;
	public float millWheelSpeed = 0;
	public Node resourcePlace;
	public Mode mode = Mode.whenNeeded;
	public int itemsProduced;
	public Game.Timer resting = new ();
	public LinkedList<PastStatus> statuses = new ();
	public Status currentStatus = Status.unknown;
	public int statusProduction;
	public Game.Timer statusDuration = new ();
	public bool outOfResourceReported;
	public Resource dungPile;
	public Game.Timer allowFreeStone = new ();
	public Game.Timer suspendGathering = new ();
	
	override public string title { get { return type.ToString().GetPrettyName(); } set{} }
	public Configuration productionConfiguration { get { return base.configuration as Configuration; } set { base.configuration = value; } }

	static MediaTable<GameObject, Type> looks;
	static public MediaTable<AudioClip, Type> processingSounds;
	static Texture2D mapIndicatorTexture;
	static public Sprite[] sprites = new Sprite[(int)Type.total];
	
	ParticleSystem smoke;
	public Transform millWheel;

	public override List<Ground.Area> areas
	{
		get
		{
			var areas = new List<Ground.Area>();
			areas.Add( outputArea );
			foreach ( var buffer in buffers )
				areas.Add( buffer.area );
			return areas;
		}
	}
	override public int checksum
	{
		get
		{
			int checksum = base.checksum;
			return checksum + (int)( craftingProgress * 1000 );
		}
	}

	public float maxOutput
	{
		get
		{
			var productionSec = productionConfiguration.productionTime * Time.fixedDeltaTime;
			if ( productionSec == 0 )
				productionSec = productionConfiguration.approximatedProductionTime * Time.fixedDeltaTime;
			var restSec = restTime * Time.fixedDeltaTime;
			return productionConfiguration.outputStackSize*60f/(productionSec+restSec);			
		}
	}

	[Obsolete( "Compatibility with old files", true )]
	float[] lastCalculatedProductivity { set {} }
	[Obsolete( "Compatibility with old files", true )]
	Game.Timer[] productivityCalculationTimer { set{} }
	[Obsolete( "Compatibility with old files", true )]
	int[] lastProductivityCalculationTimeRange { set{} }
	[Obsolete( "Compatibility with old files", true )]
	Productivity productivity { set {} }
	[Obsolete( "Compatibility with old files", true )]
	float lastCalculatedMaxOutput { set {} }
	[Obsolete( "Compatibility with old files", true )]
	Game.Timer maxOutputCalculationTimer { set {} }
	[Obsolete( "Compatibility with old files", true )]
	int itemsProducedAtLastCheck { set {} }

	[Obsolete( "Compatibility with old files", true )]
	struct Productivity
	{
		public float current;
		public Game.Timer timer;
		public int workCounter;
		public float weight;
		public int timinglength;
	}

	class Configurations
	{
		public List<Configuration> list;
	}

	class ConfigurationTypeConverter : Serializer.TypeConverter
	{
		public override object ChangeType( object value, System.Type conversionType )
		{
			if ( value.GetType() == typeof( string ) && conversionType == typeof( Configuration.Material ) )
				return (Workshop.Configuration.Material)(string)value;
			return base.ChangeType( value, conversionType );
		}
	}

	static public List<Configuration> LoadConfigurations()
	{

		var workshopConfigurations = Serializer.Read<Configurations>( Application.streamingAssetsPath + "/workshops.json", new ConfigurationTypeConverter() ).list;
		foreach ( var c in workshopConfigurations )
			if ( c.constructionTime == 0 )
				c.constructionTime = 1000 * ( c.plankNeeded + c.stoneNeeded );
		return workshopConfigurations;
	}

	public float CalculateProductivity( bool maximumPossible = false, int timeRange = Constants.Workshop.productivityPeriod )
	{
		var startTime = time - timeRange;
		int itemsProduced = 0;
			
		int wastedTime = 0, usedTime = 0;
		for ( var statusNode = statuses.Last; statusNode != null; statusNode = statusNode.Previous )
		{
			var status = statusNode.Value;
			int statusTime = status.length;
			if ( status.startTime < startTime )
				statusTime -= startTime - status.startTime;
			if ( statusTime > 0 )
			{
				if ( status.status == Status.waitingForOutputSlot || status.status == Status.waitingForInput0 || status.status == Status.waitingForInput1 || status.status == Status.waitingForInput2 || status.status == Status.waitingForInput3 )
					wastedTime += status.length;
				else
					usedTime += status.length;
				itemsProduced += status.itemsProduced;
			}
			if ( status.startTime < startTime )
				break;
		}

		var result = (float)(itemsProduced) / timeRange * Constants.World.normalSpeedPerSecond * 60;
		if ( maximumPossible )
		{
			if ( usedTime > 0 )
				result *= (usedTime + wastedTime) / usedTime;
			else
				return productionConfiguration.productivity;

		}

		return result;
	}

	[Obsolete( "Compatibility with old files", true )]
	List<PastStatus> previousPastStatuses { set {} }
	[Obsolete( "Compatibility with old files", true )]
	bool soldierWasCreatedLastTime;	// Only used by the barack
	[Obsolete( "Compatibility with old files", true )]
	List<PastStatus> pastStatuses { set {} }
	[Obsolete( "Compatibility with old files", true )]
	int savedStatusTicks { set {} }	

	public enum Mode
	{
		unknown,
		sleeping,
		whenNeeded,
		always
	}

	public void SetMode( Mode mode )
	{
		if ( mode == Mode.sleeping && this.mode != Mode.sleeping )
			CancelOrders();
		this.mode = mode;
	}

	public void SetBufferEnabled( Buffer buffer, bool enabled )
	{
		if ( !enabled )
			CancelOrders( buffer.itemType );
		buffer.disabled = !enabled;
	}

	[System.Serializable]
	public new class Configuration : Building.Configuration
	{
		[System.Serializable]
		public class Input
		{
			public Item.Type itemType;
			public int bufferSize = Constants.Workshop.defaultBufferSize;
			public int stackSize = 1;
		}
		public Type type;

		public int inputBufferSize = Constants.Workshop.defaultBufferSize;
		public Resource.Type gatheredResource = Resource.Type.unknown;
		public int gatheringRange = Constants.Workshop.defaultGatheringRange;

		public Item.Type outputType = Item.Type.unknown;
		public int outputStackSize = 1;
		public float outputCount = 1.2f;
		public int productionTime = Constants.Workshop.defaultProductionTime;
		public int productionTimeMin = -1, productionTimeMax = -1;
		public int approximatedProductionTime = Constants.Workshop.defaultProductionTime;
		public int relaxSpotCountNeeded = Constants.Workshop.defaultRelaxSpotNeeded;
		public int maxRestTime = Constants.Workshop.defaultMaxRestTime;
		public int outputMax = Constants.Workshop.defaultOutputMax;
		public bool producesDung = false;
		public float productivity {get { return (float)Constants.World.normalSpeedPerSecond * 60 / ( productionTime == 0 ? approximatedProductionTime : productionTime ); } }

		[Obsolete( "Compatibility with old files", true )]
		float processSpeed { set { productionTime = (int)( 1 / value ); } }
		[Obsolete( "Compatibility with old files", true )]
		Input[] inputs { set {} }

		public bool commonInputs = false;	// If true, the workshop will work with the input buffers separately, if any has any item it will work (f.e. mines). Otherwise each input is needed.
		public List<Item.Type> generatedInputs;
		public Material baseMaterials;

		[System.Serializable]
		public class Material
		{
			virtual public List<Item.Type> GenerateList( System.Random rnd )
			{
				var result = new List<Item.Type>();

				if ( type != Item.Type.unknown )
				{
					result.Add( type );
					return result;
				}

				var remainingOptions = new List<Material>();
				var remainingChances = new List<float>();
				for ( int i = 0; i < options.Count; i++ )
				{
					remainingOptions.Add( options[i] );
					if ( chances != null )
						remainingChances.Add( chances[i] );
					else
						remainingChances.Add( 1 );
				}
				int targetCount = rnd.Next( min, max + 1 );

				while ( result.Count < targetCount )
				{
					float totalChance = 0;
					foreach ( var chance in remainingChances )
						totalChance += chance;

					var selection = rnd.NextDouble() * totalChance;

					float currentChance = 0;
					for ( int i = 0; i < remainingOptions.Count; i++ )
					{
						if ( selection < currentChance + remainingChances[i] )
						{
							result = result.Concat( remainingOptions[i].GenerateList( rnd ) ).ToList();
							remainingOptions.RemoveAt( i );
							remainingChances.RemoveAt( i );
							break;
						}
						currentChance += remainingChances[i];
					}

				}

				return result;
			}

			static public implicit operator Material( string value )
			{
				return new Material { type = Enum.Parse<Item.Type>( value ) };
			}

			Item.Type type = Item.Type.unknown;
			public List<Material> options;
			public List<float> chances;
			public int min = 1, max = 1;
			public List<Material> include { set { options = value; min = max = value.Count; } }
		}
	}

	public enum Status
	{
		working,
		waitingForAnyInput,
		waitingForInput0,
		waitingForInput1,
		waitingForInput2,
		waitingForInput3,
		waitingForOutputSlot,
		waitingForResource,
		resting,
		total,
		unknown = -1
	}

	[Serializable]
	public struct PastStatus
	{
		public Status status;
		public int length;
		public int startTime;
		public int itemsProduced;
	}

	[System.Serializable]
	public class Buffer
	{
		public Buffer() { }
		public Buffer( Item.Type itemType = Item.Type.unknown, int size = Constants.Workshop.defaultBufferSize )
		{
			this.itemType = itemType;
			this.size = size;
		}
		public Item.Type itemType;
		public int size = Constants.Workshop.defaultBufferSize;
		[Obsolete( "Compatibility with old files", true )]
		int stackSize { set {} }
		public ItemDispatcher.Priority priority = ItemDispatcher.Priority.high;
		public int stored;
		public int onTheWay;
		public int important = Constants.Workshop.defaultImportantInBuffer;
		public Ground.Area area = new ();
		public Team.InputWeight weight;
		public bool disabled;
		public bool optional;
		public bool bored;
	}

	new public enum Type
	{
		woodcutter,
		sawmill,
		stonemason,
		fishingHut,
		wheatFarm,
		cornFarm,
		mill,
		cornMill,
		bakery,
		hunter,
		saltMine,
		ironMine,
		copperMine,
		coalMine,
		stoneMine,
		goldMine,
		silverMine,
		forester,
		_geologistObsolete,	// Obsolete, kept here only to remain compatible with old files
		bowMaker,
		steelSmelter,
		sterlingSmelter,
		weaponMaker,
		well,
		brewery,
		butcher,
		barrack,
		jeweler,
		appleGatherer,
		dungCollector,
		charcoalKiln,
		confectionery,
		dairy,
		poultryRun,
		cheeseFactory,
		slingMaker,
		fishFryer,
		total,
		unknown = -1,
		construction = -2
	}

	public class GetResource : Unit.Task
	{
		public Resource resource;
		[Obsolete( "Compatibility with old files" ), JsonIgnore]
		public Node node;
		[Obsolete( "Compatibility with old files" ), JsonIgnore]
		public Resource.Type resourceType;
		public Game.Timer timer = new ();
		[Obsolete( "Compatibility with old files", true )]
		Item item
		{
			set
			{
				value.nextFlag.CancelItem( value );
				value.Remove();
			}
		}


		public void Setup( Unit boss, Resource resource )
		{
			base.Setup( boss );
			this.resource = resource;
		}
		public override void Validate()
		{
			boss.assert.AreEqual( boss, resource.hunter );
			base.Validate();
		}
		public override void Cancel()
		{
			boss.assert.AreEqual( resource.hunter, boss );
			resource.hunter = null;
			base.Cancel();
		}
		public override bool ExecuteFrame()
		{
			if ( boss.node != resource.node && !resource.underGround )
			{
				// Probably no path to resource
				Cancel();
				resource.keepAway.Start( Constants.Workshop.keepAwayOnNoPath );
				return finished;
			}
			if ( resource == null )
				return finished;
			if ( timer.empty )
				timer.Start( (boss.building as Workshop).productionConfiguration.productionTime );

			if ( !timer.done )
				return needMoreCalls;

			if ( resource )
				ProcessResource( resource );
			return finished;
		}

		void ProcessResource( Resource resource )
		{
			boss.assert.AreEqual( boss, resource.hunter );
			if ( resource.underGround || resource.node == boss.node )
			{
				resource.gathered.Start();
				if ( !resource.infinite && --resource.charges == 0 )
					resource.Remove();
				else
				{
					if ( resource.underGround )
						resource.keepAway.Start( (int)( Constants.Workshop.mineOreRestTime ) );
					if ( resource.type == Resource.Type.fish )
						resource.keepAway.Start( Constants.Workshop.fishRestTime );
					if ( resource.type == Resource.Type.dung )
						resource.keepAway.Start( Constants.Workshop.dungRestTime );
				}
			}
			else
			{
				resource.keepAway.Start( 500 );   // TODO Settings, is this called at all?
				boss.assert.IsTrue( false );
			}
			boss.assert.AreEqual( resource.hunter, boss );
			resource.hunter = null;
			if ( resource.underGround )
				( boss.building as Workshop )?.ItemGathered();
			else
				FinishJob( boss, Resource.ItemType( resource.type ) );
		}
	}

	public class Plant : Unit.Task
	{
		public Node node;
		public Game.Timer wait = new ();
		public bool done;
		public Resource.Type resourceType;

		public void Setup( Unit boss, Node node, Resource.Type resourceType )
		{
			base.Setup( boss );
			this.node = node;
			this.resourceType = resourceType;
		}
		public override bool ExecuteFrame()
		{
			if ( !wait.empty && !wait.done )
				return false;

			if ( done )
			{
				boss.animator?.SetBool( Unit.sowingID, false );
				return true;
			}
			if ( boss.node != node )
				node.suspendPlanting.Start( Constants.Workshop.keepAwayOnNoPath );
			if ( boss.node != node || node.building || node.flag || node.road || !node.CheckType( Node.Type.land ) )
			{
				int id = 0;
				if ( node.building )
					id = node.building.id;
				if ( node.flag )
					id = node.flag.id;
				if ( node.road )
					id = node.road.id;
				World.CRC( ( id << 16 ) + (int)node.type, OperationHandler.Event.CodeLocation.unitTaskPlant );
				( boss.building as Workshop ).SetWorking( false );
				return true;
			}

			if ( node.block.IsBlocking( Node.Block.Type.units ) )
				return true;

			Resource.Create().Setup( node, resourceType );
			done = true;
			wait.Start( Constants.Workshop.gathererHarvestTime );
			boss.animator?.SetBool( Unit.sowingID, true );
			boss.ScheduleWalkToNode( boss.building.flag.node );
			boss.ScheduleWalkToNeighbour( boss.building.node );
			boss.ScheduleCall( boss.building as Workshop );
			return false;
		}
	}

	public class Pasturing : Unit.Task
	{
		public Resource resource;
		[Obsolete( "Compatibility with old files", true )]
		int timer;
		public Game.Timer pasturingTimer = new ();
		public override bool ExecuteFrame()
		{
			if ( pasturingTimer.empty )
			{
				resource = Resource.Create().SetupAsPrey( boss );
				pasturingTimer.Start( Constants.Workshop.pasturingTime );
				if ( resource == null )
					return true;

				return false;
			}
			if ( pasturingTimer.inProgress )
				return false;

			if ( resource.hunter == null )
			{
				resource.origin = null;
				resource.Remove();
				return true;
			}
			return false;
		}

		public override void Cancel()
		{
			if ( resource )
			{
				resource.origin = null;
				resource.Remove();
			}
			base.Cancel();
		}

		public override void Validate()
		{
			if ( resource )
			{
				boss.assert.AreEqual( resource.type, Resource.Type.pasturingAnimal );
				boss.assert.AreEqual( resource.node, boss.node );
			}
			base.Validate();
		}
	}

	public static new void Initialize()
	{
		object[] looksData = {
			"prefabs/buildings/fishingHut", 1.25f, Type.fishingHut,
			"prefabs/buildings/bakery", 3.6f, Type.bakery, Type.confectionery, Type.cheeseFactory, Type.fishFryer,
			"prefabs/buildings/hunter", 1.6f, Type.hunter,
			"prefabs/buildings/sawmill", 2.9f, Type.sawmill,
			"prefabs/buildings/smallCabin", 1.4f, Type.stonemason,
			"prefabs/buildings/farm", 1.8f, Type.wheatFarm, Type.cornFarm,
			"prefabs/buildings/mill", 3.5f, Type.mill, Type.cornMill,
			"Mines/saltmine_final", 1.5f, Type.saltMine,
			"Mines/coalmine_final", 1.1f, Type.coalMine,
			"Mines/ironmine_final", 1.5f, Type.ironMine,
			"Mines/ironmine_final", 1.5f, Type.copperMine,
			"Mines/goldmine_final", 1.5f, Type.goldMine,
			"Mines/goldmine_final", 1.5f, Type.silverMine,
			"Mines/stonemine_final", 1.5f, Type.stoneMine,
			"Forest/woodcutter_final", 1.2f, Type.woodcutter, Type.appleGatherer,
			"Forest/forester_final", 1.33f, Type.forester, Type.dungCollector, Type.charcoalKiln,
			"SAdK/smelter_final", 2f, Type.steelSmelter, Type.sterlingSmelter,
			"prefabs/buildings/weaponmaker", 1.9f, Type.weaponMaker,
			"prefabs/buildings/bowmaker", 2.5f, Type.bowMaker, Type.slingMaker,
			"prefabs/buildings/brewery", 1.4f, Type.brewery,
			"prefabs/buildings/slaughterhouse", 1.5f, Type.butcher, Type.dairy, Type.poultryRun,
			"SAdK/barrack_final", 1.8f, Type.barrack,
			"SAdK/coinmaker_final", 2f, Type.jeweler,
			"prefabs/buildings/well", 1.1f, Type.well };
		looks.Fill( looksData );
		object[] sounds = {
			"handsaw", 1.0f, Type.sawmill,
			"smelter", 1.0f, Type.steelSmelter, Type.sterlingSmelter,
			"windmill", 1.0f, Type.mill, Type.cornMill,
			"brewery", 0.7f, Type.brewery,
			"coinmaker", 0.5f, Type.jeweler,
			"pig", 1.0f, Type.butcher,
			"chicken", 1.0f, Type.poultryRun,
			"cow", 1.0f, Type.dairy,
			"kneading", 1.0f, Type.bakery, Type.confectionery,
			"weaponforging", 0.7f, Type.weaponMaker,
			"rasp", 1.0f, Type.bowMaker, Type.slingMaker,
			"fight", 1.0f, Type.barrack,
			"pickaxe_deep", 1.0f, Type.goldMine, Type.saltMine, Type.coalMine, Type.stoneMine, Type.ironMine, Type.silverMine, Type.copperMine
 		};
		processingSounds.fileNamePrefix = "soundEffects/";
		processingSounds.Fill( sounds );	// bool equals "dont loop"
		mapIndicatorTexture = Resources.Load<Texture2D>( "icons/brick" );

		var dl = new GameObject( "Temporary Directional Light" );
		var l = dl.AddComponent<Light>();
		l.type = LightType.Directional;
		l.color = new Color( .7f, .7f, .7f );
		dl.transform.rotation = Quaternion.LookRotation( RuntimePreviewGenerator.PreviewDirection );

		RuntimePreviewGenerator.BackgroundColor = new Color( 0.5f, 0.5f, 0.5f, 0 );
		for ( int i = 0; i < (int)Type.total; i++ )
		{
			var look = looks.GetMediaData( (Type)i );
			if ( look == null )
				continue;
			var handle = new GameObject( "Temporary For Workshop Thumbnail" ).transform;
			Instantiate( look ).transform.SetParent( handle );
			var smoke = World.FindChildRecursive( handle, "smoke" );
			if ( smoke )
			{
				smoke.SetParent( null );
				Destroy( smoke.gameObject );
			}
			Texture2D tex = RuntimePreviewGenerator.GenerateModelPreview( handle, 256, 256 );
			sprites[i] = Sprite.Create( tex, new Rect( 0.0f, 0.0f, tex.width, tex.height ), new Vector2( 0.5f, 0.5f ) );
			Assert.global.IsNotNull( sprites[i] );
			Destroy( handle.gameObject );
		}

		Destroy( dl );
	}

	public static Workshop Create()
	{
		return new GameObject().AddComponent<Workshop>();
	}

	public Workshop Setup( Node node, Team owner, Type type, int flagDirection, bool blueprintOnly = false, Resource.BlockHandling block = Resource.BlockHandling.block )
	{
		this.type = type;
		this.team = owner;
		buffers.Clear();

		RefreshConfiguration();
		
		if ( Setup( node, owner, configuration, flagDirection, blueprintOnly, block ) == null )
			return null;

		team.workshops.Add( this );

		if ( type == Type.woodcutter || type == Type.forester || type == Type.wheatFarm || type == Type.cornFarm )
		{
			foreach ( var nearbyNodeOffset in Ground.areas[productionConfiguration.gatheringRange] )
			{
				var nearbyNode = node + nearbyNodeOffset;
				if ( ( type == Type.wheatFarm || type == Type.cornFarm ) && nearbyNode.type == Node.Type.grass )
					nearbyNode.valuable = true;
				if ( ( type == Type.woodcutter || type == Type.forester ) && nearbyNode.type == Node.Type.forest )
					nearbyNode.valuable = true;
			}
		}

		return this;
	}

	public override void Remove()
	{
		if ( type == Type.woodcutter || type == Type.forester || type == Type.wheatFarm || type == Type.cornFarm )
		{
			foreach ( var nearbyNode in Ground.areas[productionConfiguration.gatheringRange] )
				node.Add( nearbyNode ).valuable = false;
		}
		dungPile?.Remove();
		team.workshops.Remove( this );
		base.Remove();
	}

	static public Configuration GetConfiguration( World world, Type type )
	{
		foreach ( var c in world.workshopConfigurations )
		{
			if ( c.type == type )
				return c;
		}
		return null;
	}

	void RefreshConfiguration()
	{
		configuration = GetConfiguration( game, type );
		assert.IsNotNull( configuration );

		if ( productionConfiguration.generatedInputs == null )
		{
			buffers.Clear();
			return;
		}

		var newList = new List<Buffer>();
		for ( int i = 0; i < productionConfiguration.generatedInputs.Count; i++ )
		{
			var input = productionConfiguration.generatedInputs[i];
			int j = 0;
			while ( j < buffers.Count )
			{
				var b = buffers[j];
				if ( b.itemType == input )
				{
					b.size = productionConfiguration.inputBufferSize;
					b.optional = productionConfiguration.commonInputs;
					newList.Add( b );
					break;
				}
				j++;
			}
			if ( j == buffers.Count )
				newList.Add( new Buffer( input, productionConfiguration.inputBufferSize ) );
		}
		assert.AreEqual( newList.Count, productionConfiguration.generatedInputs.Count );
		buffers = newList;
		foreach ( var b in buffers )
			b.weight = team.FindInputWeight( type, b.itemType );
	}

	public new void Start()
	{
		height = looks.GetMedia( type ).floatData;
		levelBrake = height / 2;    // TODO Better way?
		if ( levelBrake < 1 )
			levelBrake = 1;
		base.Start();
		if ( destroyed )
			return;
		assert.IsNotNull( body );
		if ( type == Type.mill || type == Type.cornMill )
			millWheel = body.transform.Find( "SM_Bld_Preset_House_Windmill_01_Blades_Optimized" );
		string name = type.ToString();
		this.name = name.First().ToString().ToUpper() + name.Substring( 1 ) + $" {node.x}:{node.y}";

		RefreshConfiguration();

		smoke = body.transform.Find( "smoke" )?.GetComponent<ParticleSystem>();
		if ( working )
		{ 
			if ( smoke )
			{
				var a = smoke.main;
				a.simulationSpeed = 1;
				smoke.Simulate( 10 );
				smoke.Play();
				a.simulationSpeed = game.timeFactor;
			}
			PlayWorkingSound();
		}

		while ( statuses.Count > 0 && statuses.First().startTime + statuses.First().length < time - Constants.Workshop.maxSavedStatusTime )
			statuses.RemoveFirst();
	}

	public override GameObject Template()
	{
		return looks.GetMediaData( type );
	}

	public override Item SendItem( Item.Type itemType, Building destination, ItemDispatcher.Priority priority )
	{
		assert.AreEqual( productionConfiguration.outputType, itemType );
		assert.IsTrue( output > 0 );		// TODO Triggered for stone, in a stonemason, destination stock
		Item item = base.SendItem( itemType, destination, priority );
		if ( item != null )
			output--;
		return item;
	}

	public override void ItemOnTheWay( Item item, bool cancel = false )
	{
		base.ItemOnTheWay( item, cancel );
		if ( !construction.done )
			return;

		foreach ( var b in buffers )
		{
			if ( b.itemType == item.type )
			{
				if ( cancel )
				{
					assert.IsTrue( b.onTheWay > 0 );
					b.onTheWay--;
				}
				else
				{
					assert.IsTrue( b.stored + b.onTheWay < b.size );
					b.onTheWay++;
				}
				return;
			}
		}
		assert.IsTrue( gatherer );
		assert.AreEqual( productionConfiguration.outputType, item.type );
		assert.IsTrue( output < productionConfiguration.outputMax );
	}

	public override void ItemArrived( Item item )
	{
		base.ItemArrived( item );

		if ( !construction.done )
			return;

		foreach ( var b in buffers )
		{
			if ( b.itemType == item.type )
			{
				assert.IsTrue( b.onTheWay > 0 );
				b.onTheWay--;
				assert.IsTrue( b.onTheWay + b.stored < b.size );
				b.stored++;
				return;
			}
		}

		assert.AreEqual( productionConfiguration.outputType, item.type );
		ItemGathered();
	}

	public void ItemGathered()
	{
		// Gatherer arrived back from harvest
		assert.IsTrue( gatherer );
		assert.IsTrue( output < productionConfiguration.outputMax );
		output += productionConfiguration.outputStackSize;
		itemsProduced += productionConfiguration.outputStackSize;
		team.ItemProduced( productionConfiguration.outputType, productionConfiguration.outputStackSize );
		statusProduction += productionConfiguration.outputStackSize;
		resting.Start( restTime );
		ChangeStatus( Status.resting );
		SetWorking( false );
	}

	public bool gatherer { get { return productionConfiguration != null && productionConfiguration.gatheredResource != Resource.Type.unknown; } }

	public void ChangeStatus( Status status )
	{
		if ( currentStatus == status )
		{
			assert.IsTrue( statusDuration.done );
			return;
		}

		while ( statuses.Count > 0 && statuses.First().startTime + statuses.First().length < time - Constants.Workshop.maxSavedStatusTime )
			statuses.RemoveFirst();
		if ( currentStatus != Status.unknown )
			statuses.AddLast( new PastStatus { status = currentStatus, length = statusDuration.age, startTime = statusDuration.reference, itemsProduced = statusProduction } );
		currentStatus = status;
		statusDuration.Start();
		statusProduction = 0;
	}

	public override void GameLogicUpdate()
	{
		base.GameLogicUpdate();

		if ( !construction.done || blueprintOnly )
			return;

		if ( productionConfiguration.producesDung && dungPile == null )
			dungPile = Resource.Create().Setup( node, Resource.Type.dung, int.MaxValue, true );

		if ( type == Type.barrack && output > 0 )
		{
			output--;
			Unit.Create().SetupAsSoldier( this ).ScheduleWalkToNeighbour( flag.node, true );
		}

		if ( tinkerer == null )
			dispenser = tinkerer = Unit.Create().SetupForBuilding( this );
		if ( tinkererMate == null )
		{
			dispenser = tinkererMate = Unit.Create().SetupForBuilding( this, true );
			tinkererMate.ScheduleWait( 100, true );
		}

		if ( gatherer && tinkerer.IsIdle() && tinkerer.node == node )
			SetWorking( false );

		if ( reachable )
		{
			int freeSpaceAtFlag = flag.freeSlots;
			foreach ( Buffer b in buffers )
			{
				if ( b.disabled || mode == Mode.sleeping )
					continue;
				int missing = b.size-b.stored-b.onTheWay;
				var priority = b.stored <= b.important ? b.priority : ItemDispatcher.Priority.low;
				float weight = b.weight != null ? b.weight.weight : 0.5f;
				team.itemDispatcher.RegisterRequest( this, b.itemType, missing, priority, b.area, weight );
			}
			if ( productionConfiguration.outputType != Item.Type.unknown && mode != Mode.always )
			{
				bool noDispenser = dispenser == null || !dispenser.IsIdle( true );
				team.itemDispatcher.RegisterOffer( this, productionConfiguration.outputType, output, outputPriority, outputArea, 0.5f, freeSpaceAtFlag == 0, noDispenser );
			}

			if ( mode == Mode.always && output > 0 && dispenser.IsIdle() && freeSpaceAtFlag > 2 )
				SendItem( productionConfiguration.outputType, null, ItemDispatcher.Priority.high );
		}

		if ( !blueprintOnly )
		{
			int disabledBufferCRC = 1;
			foreach ( var buffer in buffers )
				if ( buffer.disabled )
					disabledBufferCRC++;
			World.CRC( disabledBufferCRC, OperationHandler.Event.CodeLocation.workshopDisabledBuffers );
		}

		switch ( type )
		{
			case Type.wheatFarm:
			case Type.cornFarm:
			{
				var resourceType = type == Type.cornFarm ? Resource.Type.cornField : Resource.Type.wheatField;
				if ( tinkerer.IsIdle( true ) && mode != Mode.sleeping && !resting.inProgress && !suspendGathering.inProgress )
				{
					if ( CollectResource( productionConfiguration.gatheredResource, productionConfiguration.gatheringRange, false, currentStatus != Status.waitingForInput0 ) )
						return;

					if ( !UseInput( 1, true ) )
						return;

					foreach ( var o in Ground.areas[productionConfiguration.gatheringRange] )
					{
						Node place = node.Add( o );
						if ( place.block || !place.CheckType( Node.Type.grass ) || place.suspendPlanting.inProgress )
							continue;
						UseInput();
						PlantAt( place, resourceType );
						return;
					}
					suspendGathering.Start( Constants.Workshop.gathererSleepTimeAfterFail );
				}
				break;
			}
			case Type.forester:
			{
				if ( tinkerer.IsIdle( true ) && !suspendGathering.inProgress && mode != Mode.sleeping && !resting.inProgress )
				{
					ChangeStatus( Status.waitingForResource );
					foreach ( var offset in Ground.areas[productionConfiguration.gatheringRange] )
					{
						Node place = node.Add( offset );
						{
							if ( place.block || !place.CheckType( Node.Type.forest ) || place.fixedHeight || place.suspendPlanting.inProgress )
								continue;
							int blockedAdjacentNodes = 0;
							foreach ( var j in Ground.areas[1] )
							{
								if ( j && place.Add( j ).block.IsBlocking( Node.Block.Type.units ) )
									blockedAdjacentNodes++;
							}
							if ( blockedAdjacentNodes >= 2 )
								continue;
						}
						PlantAt( place, Resource.Type.tree );
						return;
					}
					suspendGathering.Start( Constants.Workshop.gathererSleepTimeAfterFail );
				}
				break;
			}
			default:
			{
				if ( gatherer )
				{
					if ( tinkerer.IsIdle() && !suspendGathering.inProgress && !CollectResource( productionConfiguration.gatheredResource, productionConfiguration.gatheringRange ) )
						suspendGathering.Start( Constants.Workshop.gathererSleepTimeAfterFail );
				}
				else
					ProcessInput();

				if ( millWheel )
				{
					if ( ( type == Type.mill || type == Type.cornMill ) && working )
						millWheelSpeed += 0.01f;
					else
						millWheelSpeed -= 0.01f;
					if ( millWheelSpeed > 1 )
						millWheelSpeed = 1;
					if ( millWheelSpeed < 0 )
						millWheelSpeed = 0;
					millWheel.Rotate( 0, 0, millWheelSpeed );
				}
				break;
			}
		}
	}

	bool UseInput( int count = 1, bool checkOnly = false )
	{
		bool common = productionConfiguration.commonInputs;
		if ( count == 0 || buffers.Count == 0 )
			return true;

		int min = int.MaxValue, sum = 0, minIndex = 0;
		for ( int i = 0; i < buffers.Count; i++ )
		{
			var b = buffers[i];
			if ( b.bored )
				continue;
			sum += b.stored;
			if ( min > b.stored )
			{
				min = b.stored;
				minIndex = i;
			}
		}
		if ( common && sum < count )
		{
			ChangeStatus( Status.waitingForAnyInput );
			return false;
		}
		if ( !common && min < count )
		{
			ChangeStatus( Status.waitingForInput0 + minIndex );
			return false;
		}

		if ( checkOnly )
			return true;

		int o = game.NextRnd( OperationHandler.Event.CodeLocation.workshopBufferSelection );
		for ( int i = 0; i < buffers.Count; i++ )
		{
			var b = buffers[(i + o) % buffers.Count];
			if ( common )
			{
				int used = Math.Min( b.stored, count );
				if ( used > 0 )
				{
					if ( lastUsedInput == b.itemType )
						b.bored = true;
					else
						lastUsedInput = b.itemType;

					foreach ( var otherBuffer in buffers )
						if ( otherBuffer != b )
							otherBuffer.bored = false;
				}
				count -= used;
				b.stored -= used;
			}
			else
				b.stored -= count;
		}
		return true;
	}

	void ProcessInput()
	{
		if ( !working && tinkerer.IsIdle( true ) && mode != Mode.sleeping && !resting.inProgress )
		{
			if ( output + productionConfiguration.outputStackSize > productionConfiguration.outputMax )
			{
				ChangeStatus( Status.waitingForOutputSlot );
				return;
			}
			if ( UseInput() )
				SetWorking( true );
		}
		if ( working )
		{
			craftingProgress += 1f / productionConfiguration.productionTime;
			World.CRC( (int)( 10000 * craftingProgress ), OperationHandler.Event.CodeLocation.workshopWorkProgress );
			if ( craftingProgress > 1 )
			{
				output += productionConfiguration.outputStackSize;
				SetWorking( false );
				itemsProduced += productionConfiguration.outputStackSize;
				if ( productionConfiguration.outputType != Item.Type.unknown )
				{
					team.ItemProduced( productionConfiguration.outputType, productionConfiguration.outputStackSize );
					statusProduction += productionConfiguration.outputStackSize;
				}
				resting.Start( restTime );
				ChangeStatus( Status.resting );
			}
		}
	}

	bool CollectResource( Resource.Type resourceType, int range, bool useInputs = true, bool adjustStatus = true )
	{
		if ( !tinkerer || !tinkerer.IsIdle( true ) || mode == Mode.sleeping )
			return false;
		if ( output + productionConfiguration.outputStackSize > productionConfiguration.outputMax )
		{
			if ( adjustStatus )
				ChangeStatus( Status.waitingForOutputSlot );
			return false;
		}
		if ( resting.inProgress )
			return false;

		resourcePlace = null;
		assert.IsTrue( tinkerer.IsIdle() );
		assert.IsTrue( range < Ground.areas.Length );
		if ( range > Ground.areas.Length )
			range = Ground.areas.Length - 1;
		Node target;
		foreach ( var offset in Ground.areas[range] )
		{
			target = node.Add( offset );
			foreach ( var resource in target.resources )
			{
				if ( resource.hunter != null )
					continue;
				if ( resource.type == resourceType && resource.IsReadyToBeHarvested() )
				{
					if ( useInputs && !UseInput() )
					{
						if ( type != Type.stoneMine || allowFreeStone.inProgress )
							return false;
						Log( "Stone mine giving one stone for free due to long starvation" );
					}
					allowFreeStone.Start( Constants.Workshop.freeStoneTimePeriod );
					return CollectResourceFromNode( resource );
				}
			}
		}
		if ( ( type == Type.woodcutter || type == Type.stonemason ) && !outOfResourceReported )
		{
			outOfResourceReported = true;
			team.SendMessage( "Out of resources", this );
		}
		if ( adjustStatus )
			ChangeStatus( Status.waitingForResource );
		return false;
	}

	/// <summary>
	/// 
	/// </summary>
	/// <returns>A number between zero and one, zero means the work just started, one means it is ready.</returns>
	public float progress
	{
		get
		{
			if ( !gatherer || tinkerer == null )
				return craftingProgress;

			var getResourceTask = tinkerer.FindTaskInQueue<GetResource>();
			if ( getResourceTask == null )
				return 0;

			return ( (float)getResourceTask.timer.age ) / productionConfiguration.productionTime + 1;
		}
		[Obsolete( "Compatibility with old files", true )]
		set {}
	}

	bool CollectResourceFromNode( Resource resource )
	{
		if ( flag.freeSlots == 0 )
			return false;

		assert.IsTrue( tinkerer.IsIdle() );
		tinkerer.SetActive( true );
		if ( resource.underGround )
			tinkerer.SetStandingHeight( -10 );	// Moving the miner go underground, to avoid it being picked by mouse clicks
		if ( !resource.underGround )
		{
			tinkerer.ScheduleWalkToNeighbour( flag.node );
			tinkerer.ScheduleWalkToNode( resource.node, true, false, Unit.resourceCollectAct[(int)resource.type] );
		}
		resourcePlace = resource.node;
		var task = ScriptableObject.CreateInstance<GetResource>();
		task.Setup( tinkerer, resource );
		tinkerer.ScheduleTask( task );
		resource.hunter = tinkerer;
		SetWorking( true );
		return true;
	}

	static void FinishJob( Unit unit, Item.Type itemType )
	{
		Item item = null;
		if ( itemType != Item.Type.unknown )
			item = Item.Create().Setup( itemType, unit.building );
		if ( item != null )
		{
			item.SetRawTarget( unit.building );
			item.hauler = unit;
			unit.SchedulePickupItem( item );
		}
		unit.ScheduleWalkToNode( unit.building.flag.node );
		unit.ScheduleWalkToNeighbour( unit.building.node );
		if ( item != null )
			unit.ScheduleDeliverItem( item );
		unit.ScheduleCall( unit.building as Workshop );
	}

	public void PlayWorkingSound()
	{
		var sound = processingSounds.GetMedia( type );
		soundSource.clip = sound.data;
		soundSource.volume = sound.floatData;
		soundSource.loop = !sound.boolData;
		soundSource.Play();
	}

	public void SetWorking( bool working )
	{
		if ( this.working == working )
			return;

		craftingProgress = 0;
		this.working = working;
		if ( working && soundSource )
		{
			smoke?.Play();
			PlayWorkingSound();
		}
		else
		{
			smoke?.Stop();
			soundSource?.Stop();
		}

		if ( working )
			ChangeStatus( Status.working );
	}

	public override void UnitCallback( Unit tinkerer, float floatData, bool boolData )
	{
		// Tinkerer returned back from gathering resource
		tinkerer.SetStandingHeight( 0 );	// Bring miners back to the surface
		assert.AreEqual( tinkerer, this.tinkerer );	// TODO Triggered shortly after removing a flag and roads. Triggered in a farm, not close to where the flag and roads were removed
		SetWorking( false );
	}

	void PlantAt( Node place, Resource.Type resourceType )
	{
		assert.IsTrue( tinkerer.IsIdle() );
		tinkerer.SetActive( true );
		tinkerer.ScheduleWalkToNeighbour( flag.node );
		tinkerer.ScheduleWalkToNode( place, true );
		var task = ScriptableObject.CreateInstance<Plant>();
		task.Setup( tinkerer, place, resourceType );
		tinkerer.ScheduleTask( task );
		SetWorking( true );
	}

	public override void OnClicked( Interface.MouseButton button, bool show = false )
	{
		base.OnClicked( button, show );
		if ( construction.done && button == Interface.MouseButton.left )
			Interface.WorkshopPanel.Create().Open( this, Interface.WorkshopPanel.Content.everything, show );
	}

	public void OnDrawGizmos()
	{
#if DEBUG
		if ( Selection.Contains( gameObject ) && resourcePlace != null )
		{
			Gizmos.color = Color.red;
			Gizmos.DrawLine( node.position + Vector3.up * Constants.Node.size, resourcePlace.position );
		}
#endif
	}

	public override void Reset()
	{
		base.Reset();
		foreach ( var b in buffers )
			b.stored = 0;
		output = 0;
		SetWorking( false );
	}

	public int relaxSpotCount 
	{
		get
		{
			int relaxSpotCount = 0;
			foreach ( var o in Ground.areas[Constants.Workshop.relaxAreaSize] )
			{
				if ( IsNodeGoodForRelax( this.node + o ) )
					relaxSpotCount++;
			}
			return relaxSpotCount;
		}
	}

	public static bool IsNodeGoodForRelax( Node node )
	{
		if ( node.block )
		{
			if ( node.building || node.flag )
				return false;;
			if ( node.road && node.road.ready )
				return false;
		}
		return true;
	}

	public int restTime
	{
		get
		{
			var f = (float)( relaxSpotCount ) / productionConfiguration.relaxSpotCountNeeded;
			if ( f > 1 )
				f = 1;
			return (int)( productionConfiguration.maxRestTime * ( 1 - f ) );
		}
	}

	public int ResourcesLeft( bool charges = true )
	{
		int left = 0;
		foreach ( var o in Ground.areas[productionConfiguration.gatheringRange] )
		{
			var other = node + o;
			foreach ( var resource in other.resources )
			{
				if ( resource == null || resource.type != productionConfiguration.gatheredResource )
					continue;
				if ( resource.infinite || !charges )
					left++;
				else
					left += resource.charges;
			}
		}
		return left;
	}

	public string GetStatusText( Status status )
	{
		return status switch
		{
			Workshop.Status.working => "Working",
			Workshop.Status.waitingForAnyInput => "Waiting for input",
			Workshop.Status.waitingForInput0 => $"Waiting for {buffers[0].itemType.ToString().GetPrettyName( false )}",
			Workshop.Status.waitingForInput1 => $"Waiting for {buffers[1].itemType.ToString().GetPrettyName( false )}",
			Workshop.Status.waitingForInput2 => $"Waiting for {buffers[2].itemType.ToString().GetPrettyName( false )}",
			Workshop.Status.waitingForInput3 => $"Waiting for {buffers[3].itemType.ToString().GetPrettyName( false )}",
			Workshop.Status.waitingForOutputSlot => "Waiting for output slot",
			Workshop.Status.waitingForResource => type != Type.forester ? "Waiting for resource" : "Waiting for free spot",
			Workshop.Status.resting => "Resting",
			_ => "Unknown"
		};
	}

	public override void Validate( bool chain )
	{
		assert.IsFalse( working && tinkerer.node == node && tinkerer.taskQueue.Count == 0 && tinkerer.walkTo && gatherer );
		base.Validate( chain );
		int itemsOnTheWayCount = 0;
		foreach ( Buffer b in buffers )
		{
			assert.IsTrue( b.stored + b.onTheWay <= b.size );
			itemsOnTheWayCount += b.onTheWay;
			assert.IsTrue( b.stored >= 0 && b.stored <= b.size, "Invalid store count for " + b.itemType + " (" + b.stored + ")" );
		}
		if ( construction.done )
		{
			int missing = itemsOnTheWay.Count - itemsOnTheWayCount;
			assert.IsTrue( missing >= 0 && missing < 2 );
			if ( missing == 1 )
			{
				// If an incoming item is missing, then that can only happen if the tinkerer is just gathering it
				assert.IsTrue( gatherer );
				assert.IsFalse( tinkerer.IsIdle() );
			}
		}
		if ( currentStatus != Status.unknown && !World.massDestroy )
			assert.IsTrue( statusDuration.done );	// Triggered once after pause and then switching to fast
		assert.IsTrue( team.workshops.Contains( this ) );
	}
}
