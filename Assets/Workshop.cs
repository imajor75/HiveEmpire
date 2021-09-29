using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

public class Workshop : Building, Worker.Callback.IHandler
{
	public int output;
	public Ground.Area outputArea = new Ground.Area();
	public ItemDispatcher.Priority outputPriority = ItemDispatcher.Priority.low;
	public float progress;
	public bool working;
	new public Type type = Type.unknown;
	public List<Buffer> buffers = new List<Buffer>();
	public float millWheelSpeed = 0;
	public Node resourcePlace;
	public Mode mode = Mode.whenNeeded;
	public int itemsProduced;
	public World.Timer resting = new World.Timer();
	public Productivity productivity = new Productivity( 0.5f );
	public LinkedList<PastStatus> statuses = new LinkedList<PastStatus>();
	public Status currentStatus = Status.unknown;
	public World.Timer statusDuration = new World.Timer();
	
	override public string title { get { return type.ToString().GetPrettyName(); } set{} }
	public Configuration productionConfiguration { get { return base.configuration as Configuration; } set { base.configuration = value; } }

	public static Configuration[] configurations;
	static MediaTable<GameObject, Type> looks;
	static public MediaTable<AudioClip, Type> processingSounds;
	static Texture2D mapIndicatorTexture;
	
	ParticleSystem smoke;
	public Transform millWheel;

	GameObject mapIndicator;
	Material mapIndicatorMaterial;

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

		public Resource.Type gatheredResource = Resource.Type.unknown;
		public int gatheringRange = Constants.Workshop.defaultGatheringRange;

		public Item.Type outputType = Item.Type.unknown;
		public int outputStackSize = 1;
		public int productionTime = Constants.Workshop.defaultProductionTime;
		public int relaxSpotCountNeeded = Constants.Workshop.defaultRelaxSpotNeeded;
		public int maxRestTime = Constants.Workshop.defaultMaxRestTime;
		public int outputMax = Constants.Workshop.defaultOutputMax;

		[Obsolete( "Compatibility with old files", true )]
		float processSpeed { set { productionTime = (int)( 1 / value ); } }

		public bool commonInputs = false;	// If true, the workshop will work with the input buffers separately, if any has any item it will work (f.e. mines). Otherwise each input is needed.
		public Input[] inputs;
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
	public class PastStatus
	{
		public Status status;
		public int length;
		public int startTime;
	}

	public class Productivity
	{
		public Productivity()
		{
			weight = Constants.Workshop.productivityWeight;
			timinglength = Constants.Workshop.productivityTimingLength;
		}
		public Productivity( float current )
		{
			this.current = current;
			workCounter = 0;
			weight = Constants.Workshop.productivityWeight;
			timinglength = Constants.Workshop.productivityTimingLength;
		}
		public void FixedUpdate( Workshop boss )
		{
			if ( timer.empty )
				timer.Start();
			if ( boss.working )
				workCounter++;
			if ( timer.age >= timinglength )
			{
				float p = (float)workCounter / timer.age;
				current = current * ( 1 - weight ) + p * weight;
				workCounter = 0;
				timer.Start();
			}
		}
		public float current;
		public World.Timer timer = new World.Timer();
		public int workCounter;
		public float weight;
		public int timinglength;
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
		public int stackSize = 1;
		public ItemDispatcher.Priority priority = ItemDispatcher.Priority.high;
		public int stored;
		public int onTheWay;
		public int important = Constants.Workshop.defaultImportantInBuffer;
		public Ground.Area area = new Ground.Area();
		public Player.InputWeight weight;
		public bool disabled;
		public bool optional;
	}

	new public enum Type
	{
		woodcutter,
		sawmill,
		stonemason,
		fishingHut,
		farm,
		mill,
		bakery,
		hunter,
		saltMine,
		ironMine,
		coalMine,
		stoneMine,
		goldMine,
		forester,
		_geologistObsolete,	// Obsolete, kept here only to remain compatible with old files
		bowMaker,
		smelter,
		weaponMaker,
		well,
		brewery,
		butcher,
		barrack,
		goldBarMaker,
		total,
		unknown = -1
	}

	public class GetResource : Worker.Task
	{
		public Resource resource;
		[Obsolete( "Compatibility with old files" ), JsonIgnore]
		public Node node;
		[Obsolete( "Compatibility with old files" ), JsonIgnore]
		public Resource.Type resourceType;
		public World.Timer timer = new World.Timer();
		[Obsolete( "Compatibility with old files", true )]
		Item item
		{
			set
			{
				value.nextFlag.CancelItem( value );
				value.Remove( false );
			}
		}


		public void Setup( Worker boss, Resource resource )
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
				Cancel();
				return finished;
			}
			if ( resource == null )
				return finished;
			if ( timer.empty )
				timer.Start( (boss.building as Workshop).productionConfiguration.productionTime );

			if ( !timer.done )    // TODO Working on the resource
				return needModeCalls;

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
					resource.Remove( true );
				else
				{
					if ( resource.underGround )
						resource.keepAway.Start( (int)( Constants.Workshop.mineOreRestTime / resource.strength ) );
					if ( resource.type == Resource.Type.fish )
						resource.keepAway.Start( Constants.Workshop.fishRestTime );
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

	public class Plant : Worker.Task
	{
		public Node node;
		public World.Timer wait = new World.Timer();
		public bool done;
		public Resource.Type resourceType;

		public void Setup( Worker boss, Node node, Resource.Type resourceType )
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
				boss.animator?.SetBool( Worker.sowingID, false );
				return true;
			}
			if ( boss.node != node || node.building || node.flag || node.road || !node.CheckType( Node.Type.land ) )
			{
				( boss.building as Workshop ).SetWorking( false );
				return true;
			}

			if ( node.block.IsBlocking( Node.Block.Type.workers ) )
				return true;

			Resource.Create().Setup( node, resourceType );
			done = true;
			wait.Start( 300 );
			boss.animator?.SetBool( Worker.sowingID, true );
			boss.ScheduleWalkToNode( boss.building.flag.node );
			boss.ScheduleWalkToNeighbour( boss.building.node );
			boss.ScheduleCall( boss.building as Workshop );
			return false;
		}
	}

	public class Pasturing : Worker.Task
	{
		public Resource resource;
		[Obsolete( "Compatibility with old files", true )]
		int timer;
		public World.Timer pasturingTimer = new World.Timer();
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
				resource.animals.Clear();
				resource.Remove( false );
				return true;
			}
			return false;
		}

		public override void Cancel()
		{
			if ( resource )
			{
				resource.animals.Clear();
				resource.Remove( false );
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
		using ( var sw = new StreamReader( Application.streamingAssetsPath + "/workshops.json" ) )
		using ( var reader = new JsonTextReader( sw ) )
		{
			var serializer = JsonSerializer.Create();
			configurations = serializer.Deserialize<Configuration[]>( reader );
			foreach ( var c in configurations )
				if ( c.constructionTime == 0 )
					c.constructionTime = 1000 * ( c.plankNeeded + c.stoneNeeded );
		}

		object[] looksData = {
			"prefabs/buildings/fishingHut", 1.25f, Type.fishingHut,
			"prefabs/buildings/bakery", 3.6f, Type.bakery,
			"prefabs/buildings/hunter", 1.6f, Type.hunter,
			"prefabs/buildings/sawmill", 2.9f, Type.sawmill,
			"prefabs/buildings/smallCabin", 1.4f, Type.stonemason,
			"prefabs/buildings/farm", 1.8f, Type.farm,
			"prefabs/buildings/mill", 3.5f, Type.mill,
			"Mines/saltmine_final", 1.5f, Type.saltMine,
			"Mines/coalmine_final", 1.1f, Type.coalMine,
			"Mines/ironmine_final", 1.5f, Type.ironMine,
			"Mines/goldmine_final", 1.5f, Type.goldMine,
			"Mines/stonemine_final", 1.5f, Type.stoneMine,
			"Forest/woodcutter_final", 1.2f, Type.woodcutter,
			"Forest/forester_final", 1.33f, Type.forester,
			"SAdK/smelter_final", 2f, Type.smelter,
			"prefabs/buildings/weaponmaker", 1.9f, Type.weaponMaker,
			"prefabs/buildings/bowmaker", 2.5f, Type.bowMaker,
			"prefabs/buildings/brewery", 1.4f, Type.brewery,
			"prefabs/buildings/slaughterhouse", 1.5f, Type.butcher,
			"SAdK/barrack_final", 1.8f, Type.barrack,
			"SAdK/coinmaker_final", 2f, Type.goldBarMaker,
			"prefabs/buildings/well", 1.1f, Type.well };
		looks.Fill( looksData );
		object[] sounds = {
			"handsaw", 1.0f, Type.sawmill,
			"smelter", 1.0f, Type.smelter,
			"windmill", 1.0f, Type.mill,
			"brewery", 0.7f, Type.brewery,
			"coinmaker", 0.5f, Type.goldBarMaker,
			"pig", 1.0f, Type.butcher,
			"kneading", 1.0f, Type.bakery,
			"weaponforging", 0.7f, Type.weaponMaker,
			"rasp", 1.0f, Type.bowMaker,
			"fight", 1.0f, Type.barrack,
			"pickaxe_deep", 1.0f, Type.goldMine,
			"pickaxe_deep", 1.0f, Type.saltMine,
			"pickaxe_deep", 1.0f, Type.coalMine,
			"pickaxe_deep", 1.0f, Type.stoneMine,
			"pickaxe_deep", 1.0f, Type.ironMine
 		};
		processingSounds.fileNamePrefix = "effects/";
		processingSounds.Fill( sounds );	// bool equals "dont loop"
		mapIndicatorTexture = Resources.Load<Texture2D>( "icons/brick" );
	}

	public static Workshop Create()
	{
		return new GameObject().AddComponent<Workshop>();
	}

	public Workshop Setup( Node node, Player owner, Type type, int flagDirection, bool blueprintOnly = false, Resource.BlockHandling block = Resource.BlockHandling.block )
	{
		this.type = type;
		buffers.Clear();

		RefreshConfiguration();

		if ( Setup( node, owner, configuration, flagDirection, blueprintOnly, block ) == null )
			return null;

		return this;
	}

	static public Configuration GetConfiguration( Type type )
	{
		foreach ( var c in configurations )
		{
			if ( c.type == type )
				return c;
		}
		return null;
	}

	void RefreshConfiguration()
	{
		configuration = GetConfiguration( type );
		assert.IsNotNull( configuration );

		if ( productionConfiguration.inputs == null )
		{
			buffers.Clear();
			return;
		}

		var newList = new List<Buffer>();
		for ( int i = 0; i < productionConfiguration.inputs.Length; i++ )
		{
			var input = productionConfiguration.inputs[i];
			int j = 0;
			while ( j < buffers.Count )
			{
				var b = buffers[j];
				if ( b.itemType == input.itemType )
				{
					b.itemType = input.itemType;
					b.size = input.bufferSize;
					b.stackSize = input.stackSize;
					b.optional = productionConfiguration.commonInputs;
					newList.Add( b );
					break;
				}
				j++;
			}
			if ( j == buffers.Count )
				newList.Add( new Buffer( input.itemType, input.bufferSize ) );
			foreach ( var b in buffers )
				b.weight = owner.FindInputWeight( type, b.itemType );
		}
		assert.AreEqual( newList.Count, productionConfiguration.inputs.Length );
		buffers = newList;
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
		if ( type == Type.mill )
			millWheel = body.transform.Find( "SM_Bld_Preset_House_Windmill_01_Blades_Optimized" );
		string name = type.ToString();
		this.name = name.First().ToString().ToUpper() + name.Substring( 1 ) + $" {node.x}:{node.y}";

		mapIndicator = GameObject.CreatePrimitive( PrimitiveType.Plane );
		mapIndicator.transform.SetParent( transform, false );
		World.SetLayerRecursive( mapIndicator, World.layerIndexMapOnly );
		mapIndicatorMaterial = mapIndicator.GetComponent<MeshRenderer>().material = new Material( World.defaultShader );
		mapIndicatorMaterial.renderQueue = 4001;
		mapIndicatorMaterial.mainTexture = mapIndicatorTexture;
		mapIndicator.transform.position = node.position + Vector3.up * 3;
		mapIndicator.SetActive( false );

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
			}
			PlayWorkingSound();
		}
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
		owner.ItemProduced( productionConfiguration.outputType, productionConfiguration.outputStackSize );
		resting.Start( restTime );
		ChangeStatus( Status.resting );
		SetWorking( false );
	}

	public bool gatherer { get { return productionConfiguration.gatheredResource != Resource.Type.unknown; } }

	public void ChangeStatus( Status status )
	{
		if ( currentStatus == status )
		{
			assert.IsTrue( statusDuration.done );
			return;
		}

		if ( currentStatus != Status.unknown )
			statuses.AddLast( new PastStatus { status = currentStatus, length = statusDuration.age, startTime = statusDuration.reference } );
		currentStatus = status;
		statusDuration.Start();
	}

	public void FixedUpdate()
	{
		productivity.FixedUpdate( this );

		if ( !construction.done || blueprintOnly )
		{
			return;
		}

		if ( type == Type.barrack && output > 0 )
		{
			output--;
			Worker.Create().SetupAsSoldier( this ).ScheduleWalkToNeighbour( flag.node, true );
		}

		if ( gatherer && worker.IsIdle() && worker.node == node )
			SetWorking( false );

		while ( statuses.Count > 0 && time - statuses.First().startTime > Constants.Workshop.maxSavedStatusTime )
			statuses.RemoveFirst();

		if ( reachable )
		{
			int freeSpaceAtFlag = flag.freeSlots;
			foreach ( Buffer b in buffers )
			{
				if ( b.disabled )
					continue;
				int missing = b.size-b.stored-b.onTheWay;
				var priority = b.stored <= b.important ? b.priority : ItemDispatcher.Priority.low;
				float weight = b.weight != null ? b.weight.weight : 0.5f;
				owner.itemDispatcher.RegisterRequest( this, b.itemType, missing, priority, b.area, weight );
			}
			if ( productionConfiguration.outputType != Item.Type.unknown && mode != Mode.always )
			{
				bool noDispenser = dispenser == null || !dispenser.IsIdle( true );
				owner.itemDispatcher.RegisterOffer( this, productionConfiguration.outputType, output, outputPriority, outputArea, 0.5f, freeSpaceAtFlag == 0, noDispenser );
			}

			if ( mode == Mode.always && output > 0 && dispenser.IsIdle() && freeSpaceAtFlag > 2 )
				SendItem( productionConfiguration.outputType, null, ItemDispatcher.Priority.high );
		}

		mapIndicator.SetActive( true );
		mapIndicator.transform.localScale = new Vector3( Constants.Node.size * productivity.current / 10, 1, Constants.Node.size * 0.02f );
		mapIndicator.transform.rotation = Quaternion.Euler( 0, (float)( eye.direction / Math.PI * 180 ), 0 );
		mapIndicatorMaterial.color = Color.Lerp( Color.red, Color.white, productivity.current );
	}

	public override void CriticalUpdate()
	{
		base.CriticalUpdate();

		if ( !blueprintOnly )
		{
			int disabledBufferCRC = 1;
			foreach ( var buffer in buffers )
				if ( buffer.disabled )
					disabledBufferCRC++;
			World.CRC( disabledBufferCRC, OperationHandler.Event.CodeLocation.workshopDisabledBuffers );
		}

		if ( !construction.done || blueprintOnly )
			return;

		if ( worker == null )
			dispenser = worker = Worker.Create().SetupForBuilding( this );
		if ( workerMate == null )
		{
			dispenser = workerMate = Worker.Create().SetupForBuilding( this, true );
			workerMate.ScheduleWait( 100, true );
		}

		switch ( type )
		{
			case Type.farm:
			{
				if ( worker.IsIdle( true ) && mode != Mode.sleeping && !resting.inProgress )
				{
					if ( output < productionConfiguration.outputMax )
					{
						foreach ( var o in Ground.areas[3] )
						{
							Node place = node.Add( o );
							if ( place.building || place.flag || place.road || place.fixedHeight )
								continue;
							foreach ( var resource in place.resources )
							{
								if ( resource.type != Resource.Type.cornfield || resource.hunter || !resource.IsReadyToBeHarvested() )
									continue;
								CollectResourceFromNode( resource );
								return;
							}
							ChangeStatus( Status.waitingForResource );
						}
					}
					else
						ChangeStatus( Status.waitingForOutputSlot );
					foreach ( var o in Ground.areas[3] )
					{
						Node place = node.Add( o );
						if ( place.block || !place.CheckType( Node.Type.grass ) )
							continue;
						PlantAt( place, Resource.Type.cornfield );
						return;
					}
					worker.ScheduleWait( 300 );
				}
				break;
			}
			case Type.forester:
			{
				if ( worker.IsIdle( true ) && mode != Mode.sleeping && !resting.inProgress )
				{
					ChangeStatus( Status.waitingForResource );
					var o = Ground.areas[productionConfiguration.gatheringRange];
					for ( int i = 0; i < o.Count; i++ )
					{
						int randomOffset = World.NextRnd( OperationHandler.Event.CodeLocation.workshopForester, o.Count );
						int x = (i + randomOffset) % o.Count;
						Node place = node.Add( o[x] );
						{
							if ( place.block || !place.CheckType( Node.Type.forest ) || place.fixedHeight )
								continue;
							int blockedAdjacentNodes = 0;
							foreach ( var j in Ground.areas[1] )
							{
								if ( place.Add( j ).block.IsBlocking( Node.Block.Type.workers ) )
									blockedAdjacentNodes++;
							}
							if ( blockedAdjacentNodes >= 2 )
								continue;
						}
						PlantAt( place, Resource.Type.tree );
						return;
					}
					worker.ScheduleWait( 300 );
				}
				break;
			}
			default:
			{
				if ( gatherer )
					CollectResource( productionConfiguration.gatheredResource, productionConfiguration.gatheringRange );
				else
					ProcessInput();

				if ( millWheel )
				{
					if ( type == Type.mill && working )
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

	bool UseInput( int count = 1 )
	{
		bool common = productionConfiguration.commonInputs;
		if ( count == 0 || buffers.Count == 0 )
			return true;

		int min = int.MaxValue, sum = 0, minIndex = 0;
		for ( int i = 0; i < buffers.Count; i++ )
		{
			var b = buffers[i];
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

		int o = World.NextRnd( OperationHandler.Event.CodeLocation.workshopBufferSelection );
		for ( int i = 0; i < buffers.Count; i++ )
		{
			var b = buffers[(i + o) % buffers.Count];
			if ( common )
			{
				int used = Math.Min( b.stored, count );
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
		if ( !working && worker.IsIdle( true ) && mode != Mode.sleeping && !resting.inProgress )
		{
			if ( output + productionConfiguration.outputStackSize > productionConfiguration.outputMax )
			{
				ChangeStatus( Status.waitingForOutputSlot );
				return;
			}
			if ( UseInput() )
			{
				SetWorking( true );
				progress = 0;
			}
		}
		if ( working )
		{
			progress += 1f / productionConfiguration.productionTime;
			World.CRC( (int)( 10000 * progress ), OperationHandler.Event.CodeLocation.workshopWorkProgress );
			if ( progress > 1 )
			{
				output += productionConfiguration.outputStackSize;
				SetWorking( false );
				itemsProduced += productionConfiguration.outputStackSize;
				if ( productionConfiguration.outputType != Item.Type.unknown )
					owner.ItemProduced( productionConfiguration.outputType, productionConfiguration.outputStackSize );
				resting.Start( restTime );
				ChangeStatus( Status.resting );
			}
		}
	}

	void CollectResource( Resource.Type resourceType, int range )
	{
		if ( !worker || !worker.IsIdle( true ) || mode == Mode.sleeping )
			return;
		if ( output + productionConfiguration.outputStackSize > productionConfiguration.outputMax )
		{
			ChangeStatus( Status.waitingForOutputSlot );
			return;
		}
		if ( resting.inProgress )
			return;

		resourcePlace = null;
		assert.IsTrue( worker.IsIdle() );
		assert.IsTrue( range < Ground.areas.Length );
		if ( range > Ground.areas.Length )
			range = Ground.areas.Length - 1;
		Node target;
		int t = Ground.areas[range].Count;
		int r = World.NextRnd( OperationHandler.Event.CodeLocation.workshopCollectResource, t );
		for ( int j = -1; j < t; j++ )
		{
			if ( j < 0 )
				target = node;
			else
				target = node.Add( Ground.areas[range][(j+r)%t] );
			foreach ( var resource in target.resources )
			{
				if ( resource.hunter != null )
					continue;
				if ( resource.type == resourceType && resource.IsReadyToBeHarvested() )
				{
					CollectResourceFromNode( resource );
					return;
				}
			}
		}
		ChangeStatus( Status.waitingForResource );
	}

	/// <summary>
	/// 
	/// </summary>
	/// <returns>A number between zero and one, zero means the work just started, one means it is ready.</returns>
	public float GetProgress()
	{
		if ( !gatherer )
			return progress;

		var getResourceTask = worker.FindTaskInQueue<GetResource>();
		if ( getResourceTask == null )
			return 0;

		return ( (float)getResourceTask.timer.age ) / productionConfiguration.productionTime + 1;
	}

	void CollectResourceFromNode( Resource resource )
	{
		if ( !UseInput() || flag.freeSlots == 0 )
			return;

		assert.IsTrue( worker.IsIdle() );
		worker.SetActive( true );
		if ( resource.underGround )
			worker.SetStandingHeight( -10 );	// Moving the miner go underground, to avoid it being picked by mouse clicks
		if ( !resource.underGround )
		{
			worker.ScheduleWalkToNeighbour( flag.node );
			worker.ScheduleWalkToNode( resource.node, true, false, Worker.resourceCollectAct[(int)resource.type] );
		}
		resourcePlace = resource.node;
		var task = ScriptableObject.CreateInstance<GetResource>();
		task.Setup( worker, resource );
		worker.ScheduleTask( task );
		resource.hunter = worker;
		SetWorking( true );
	}

	static void FinishJob( Worker worker, Item.Type itemType )
	{
		Item item = null;
		if ( itemType != Item.Type.unknown )
			item = Item.Create().Setup( itemType, worker.building );
		if ( item != null )
		{
			item.SetRawTarget( worker.building );
			item.worker = worker;
			worker.SchedulePickupItem( item );
		}
		worker.ScheduleWalkToNode( worker.building.flag.node );
		worker.ScheduleWalkToNeighbour( worker.building.node );
		if ( item != null )
			worker.ScheduleDeliverItem( item );
		worker.ScheduleCall( worker.building as Workshop );
	}

	public void PlayWorkingSound()
	{
		soundSource.loop = true;
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

	public void Callback( Worker worker )
	{
		// Worker returned back from gathering resource
		worker.SetStandingHeight( 0 );	// Bring miners back to the surface
		assert.AreEqual( worker, this.worker );	// TODO Triggered shortly after removing a flag and roads. Triggered in a farm, not close to where the flag and roads were removed
		SetWorking( false );
	}

	void PlantAt( Node place, Resource.Type resourceType )
	{
		assert.IsTrue( worker.IsIdle() );
		worker.SetActive( true );
		worker.ScheduleWalkToNeighbour( flag.node );
		worker.ScheduleWalkToNode( place, true );
		var task = ScriptableObject.CreateInstance<Plant>();
		task.Setup( worker, place, resourceType );
		worker.ScheduleTask( task );
		SetWorking( true );
	}

	public override void OnClicked( bool show = false )
	{
		base.OnClicked( show );
		if ( construction.done )
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
		progress = 0;
	}

	public const int relaxAreaSize = 3;
	public int relaxSpotCount 
	{
		get
		{
			int relaxSpotCount = 0;
			foreach ( var o in Ground.areas[relaxAreaSize] )
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
		assert.IsFalse( working && worker.node == node && worker.taskQueue.Count == 0 && worker.walkTo && gatherer );
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
				// If an incoming item is missing, then that can only happen if the worker is just gathering it
				assert.IsTrue( gatherer );
				assert.IsFalse( worker.IsIdle() );
			}
		}
		if ( currentStatus != Status.unknown && !World.massDestroy )
			assert.IsTrue( statusDuration.done );
	}
}
