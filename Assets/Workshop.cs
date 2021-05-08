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
	public bool soldierWasCreatedLastTime;	// Only used by the barack
	public Type type = Type.unknown;
	public List<Buffer> buffers = new List<Buffer>();
	GameObject body;
	public Transform millWheel;
	public float millWheelSpeed = 0;
	public GroundNode resourcePlace;
	public int itemsProduced;
	public Productivity productivity = new Productivity( 0.5f );
	AudioSource soundSource;
	static public MediaTable<AudioClip, Type> processingSounds;
	GameObject mapIndicator;
	Material mapIndicatorMaterial;
	static Texture2D mapIndicatorTexture;
	public Configuration productionConfiguration { get { return base.configuration as Configuration; } set { base.configuration = value; } }
	public static Configuration[] configurations;

	static MediaTable<GameObject, Type> looks;
	public static int mineOreRestTime = 8000;
	public static int fishRestTime = 8000;
	ParticleSystem smoke;
	public Mode mode = Mode.whenNeeded;

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
			public int bufferSize = 6;
			public int stackSize = 1;
		}
		public Type type;

		public Resource.Type gatheredResource = Resource.Type.unknown;
		public int gatheringRange = 6;

		public Item.Type outputType = Item.Type.unknown;
		public int outputStackSize = 1;
		public int productionTime = 1500;
		public int outputMax = 6;

		[Obsolete( "Compatibility with old files", true )]
		float processSpeed { set { productionTime = (int)( 1 / value ); } }

		public bool commonInputs = false;	// If true, the workshop will work with the input buffers separately, if any has any item it will work (f.e. mines). Otherwise each input is needed.
		public Input[] inputs;
	}

	public struct Productivity
	{
		public Productivity( float current )
		{
			this.current = current;
			workCounter = 0;
			weight = 0.5f;
			timinglength = 3000;
			timer.reference = 0;
		}
		public void FixedUpdate( Workshop boss )
		{
			if ( timer.empty )
				timer.Start();
			if ( boss.IsWorking() )
				workCounter += (int)World.instance.timeFactor;
			if ( timer.age >= timinglength )
			{
				float p = (float)workCounter/timer.age;
				current = current * ( 1 - weight ) + p * weight;
				workCounter = 0;
				timer.Start();
			}
		}
		public float current;
		public World.Timer timer;
		public int workCounter;
		public float weight;
		public int timinglength;
	}

	[System.Serializable]
	public class Buffer
	{
		public Buffer() { }
		public Buffer( Item.Type itemType = Item.Type.unknown, int size = 6 )
		{
			this.itemType = itemType;
			this.size = size;
		}
		public Item.Type itemType;
		public int size = 6;
		public int stackSize = 1;
		public ItemDispatcher.Priority priority = ItemDispatcher.Priority.high;
		public int stored;
		public int onTheWay;
		public int important = 3;
		public Ground.Area area = new Ground.Area();
		public Player.InputWeight weight;
	}

	public enum Type
	{
		woodcutter,
		sawmill,
		stonemason,
		fishingHut,
		farm,
		mill,
		bakery,
		hunter,
		saltmine,
		ironmine,
		coalmine,
		stonemine,
		goldmine,
		forester,
		geologist,
		bowmaker,
		smelter,
		weaponmaker,
		well,
		brewery,
		butcher,
		barrack,
		coinmaker,
		total,
		unknown = -1
	}

	public class GetResource : Worker.Task
	{
		public GroundNode node;
		public Resource.Type resourceType;
		public World.Timer timer;
		[Obsolete( "Compatibility with old files", true )]
		Item item
		{
			set
			{
				value.nextFlag.CancelItem( value );
				value.Remove( false );
			}
		}


		public void Setup( Worker boss, GroundNode node, Resource.Type resourceType )
		{
			base.Setup( boss );
			this.node = node;
			this.resourceType = resourceType;
		}
		public override void Validate()
		{
			if ( node.resource )
				boss.assert.AreEqual( boss, node.resource.hunter );
			base.Validate();
		}
		public override void Cancel()
		{
			if ( node.resource )
			{
				boss.assert.AreEqual( boss, node.resource.hunter );
				node.resource.hunter = null;
			}
			base.Cancel();
		}
		public override bool ExecuteFrame()
		{
			if ( boss.node != node && !Resource.IsUnderGround( resourceType ) )
			{
				Cancel();
				return true;
			}
			if ( timer.empty )
				timer.Start( (boss.building as Workshop).productionConfiguration.productionTime );

			if ( !timer.done )    // TODO Working on the resource
				return false;

			Resource resource = node.resource;
			bool underGround = Resource.IsUnderGround( resourceType );
			if ( resourceType != Resource.Type.expose )
				boss.assert.AreEqual( resourceType, resource.type, "Resource types are different (expecting " + resourceType.ToString() + " but was " + resource.type.ToString() + ")" );   // TODO Fired once (maybe fisherman met a tree?)
			boss.assert.AreEqual( boss, resource.hunter );
			if ( underGround || node == boss.node )
			{
				if ( resourceType == Resource.Type.expose )
					resource.exposed.Start( Resource.exposeMax );
				else
				{
					resource.gathered.Start();
					if ( !resource.infinite && --resource.charges == 0 )
						resource.Remove( false );
					else
					{
						if ( resource.underGround )
							resource.keepAway.Start( mineOreRestTime );
						if ( resource.type == Resource.Type.fish )
							resource.keepAway.Start( fishRestTime );
					}
				}
			}
			else
				resource.keepAway.Start( 500 );   // TODO Settings
			boss.assert.AreEqual( resource.hunter, boss );
			resource.hunter = null;
			if ( underGround )
				( boss.building as Workshop )?.ItemGathered();
			else
				FinishJob( boss, Resource.ItemType( resourceType ) );
			return true;
		}
	}

	public class Plant : Worker.Task
	{
		public GroundNode node;
		public World.Timer wait;
		public bool done;
		public Resource.Type resourceType;

		public void Setup( Worker boss, GroundNode node, Resource.Type resourceType )
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
			if ( boss.node != node || node.building || node.flag || node.road || node.fixedHeight || node.resource || !node.CheckType( GroundNode.Type.land ) )
			{
				( boss.building as Workshop ).SetWorking( false );
				return true;
			}

			Resource.Create().Setup( node, resourceType );
			done = true;
			boss.assert.IsNotNull( node.resource );
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
		public int timer;
		public override bool ExecuteFrame()
		{
			if ( resource == null )
			{
				resource = Resource.Create().SetupAsPrey( boss );
				timer = 100;
				if ( resource == null )
					return true;

				return false;
			}
			if ( timer-- > 0 )
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
		using ( var sw = new StreamReader( "Assets/StreamingAssets/workshops.json" ) )
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
			"prefabs/buildings/hunter", 1.1f, Type.hunter,
			"prefabs/buildings/sawmill", 2.9f, Type.sawmill,
			"prefabs/buildings/smallCabin", 1.4f, Type.stonemason,
			"prefabs/buildings/farm", 1.8f, Type.farm,
			"prefabs/buildings/mill", 3.5f, Type.mill,
			"Mines/saltmine_final", 1.5f, Type.saltmine,
			"Mines/coalmine_final", 1.1f, Type.coalmine,
			"Mines/ironmine_final", 1.5f, Type.ironmine,
			"Mines/goldmine_final", 1.5f, Type.goldmine,
			"Mines/stonemine_final", 1.5f, Type.stonemine,
			"Forest/woodcutter_final", 1.1f, Type.woodcutter,
			"Forest/forester_final", 1.33f, Type.forester,
			"prefabs/buildings/geologist", 0.8f, Type.geologist,
			"SAdK/smelter_final", 2f, Type.smelter,
			"prefabs/buildings/weaponmaker", 1.9f, Type.weaponmaker,
			"prefabs/buildings/bowmaker", 2.5f, Type.bowmaker,
			"prefabs/buildings/brewery", 1.4f, Type.brewery,
			"prefabs/buildings/slaughterhouse", 1.5f, Type.butcher,
			"SAdK/barrack_final", 1.5f, Type.barrack,
			"SAdK/coinmaker_final", 2f, Type.coinmaker,
			"prefabs/buildings/well", 1.1f, Type.well };
		looks.Fill( looksData );
		object[] sounds = {
			"handsaw", Type.sawmill,
			"SAdK/smelter", Type.smelter,
			"windmill", Type.mill };
		processingSounds.Fill( sounds );
		mapIndicatorTexture = Resources.Load<Texture2D>( "simple UI & icons/button/board" );
	}

	public static Workshop Create()
	{
		return new GameObject().AddComponent<Workshop>();
	}

	public Workshop Setup( GroundNode node, Player owner, Type type, int flagDirection, bool blueprintOnly = false )
	{
		this.type = type;
		title = type.ToString();
		buffers.Clear();

		RefreshConfiguration();

		if ( Setup( node, owner, configuration, flagDirection, blueprintOnly ) == null )
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

	public bool IsWorking()
	{
		return worker != null && !worker.IsIdle( true );
	}

	public new void Start()
	{
		var m = looks.GetMedia( type );
		body = Instantiate( m.data, transform );
		body.layer = World.layerIndexPickable;
		height = m.floatData;
		levelBrake = height / 2;    // TODO Better way?
		if ( levelBrake < 1 )
			levelBrake = 1;
		assert.IsNotNull( body );
		if ( type == Type.mill )
			millWheel = body.transform.Find( "SM_Bld_Preset_House_Windmill_01_Blades_Optimized" );
		base.Start();
		string name = type.ToString();
		this.name = name.First().ToString().ToUpper() + name.Substring( 1 );

		soundSource = World.CreateSoundSource( this );

		mapIndicator = GameObject.CreatePrimitive( PrimitiveType.Plane );
		mapIndicator.transform.SetParent( transform, false );
		World.SetLayerRecursive( mapIndicator, World.layerIndexMapOnly );
		mapIndicatorMaterial = mapIndicator.GetComponent<MeshRenderer>().material = new Material( World.defaultShader );
		mapIndicatorMaterial.mainTexture = mapIndicatorTexture;
		mapIndicator.transform.position = node.position + new Vector3( 0, 2, GroundNode.size * 0.5f );
		mapIndicator.SetActive( false );

		RefreshConfiguration();

		smoke = body.transform.Find( "smoke" )?.GetComponent<ParticleSystem>();
		if ( working && smoke )
		{
			var a = smoke.main;
			a.simulationSpeed = 1;
			smoke.Simulate( 10 );
			smoke.Play();
			var b = smoke.main;
			b.simulationSpeed = World.instance.timeFactor;
		}
		body.transform.RotateAround( node.position, Vector3.up, 60 * ( 1 - flagDirection ) );
	}

	public override Item SendItem( Item.Type itemType, Building destination, ItemDispatcher.Priority priority )
	{
		assert.AreEqual( productionConfiguration.outputType, itemType );
		assert.IsTrue( output > 0 );
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
		SetWorking( false );
	}

	public bool gatherer { get { return productionConfiguration.gatheredResource != Resource.Type.unknown; } }

	public new void FixedUpdate()
	{
		productivity.FixedUpdate( this );

		if ( !construction.done || blueprintOnly )
		{
			base.FixedUpdate();
			return;
		}

		Profiler.BeginSample( "Logistics" );
		int freeSpaceAtFlag = flag.FreeSpace();
		foreach ( Buffer b in buffers )
		{
			int missing = b.size-b.stored-b.onTheWay;
			var priority = b.stored <= b.important ? b.priority : ItemDispatcher.Priority.low;
			float weight = b.weight != null ? b.weight.weight : 0.5f;
			owner.itemDispatcher.RegisterRequest( this, b.itemType, missing, priority, b.area, weight );
		}
		if ( productionConfiguration.outputType != Item.Type.unknown )
		{
			bool noDispenser = dispenser == null || !dispenser.IsIdle( true );
			owner.itemDispatcher.RegisterOffer( this, productionConfiguration.outputType, output, outputPriority, outputArea, 0.5f, freeSpaceAtFlag == 0, noDispenser );
		}

		if ( mode == Mode.always && output > 0 && dispenser.IsIdle() && freeSpaceAtFlag > 2 )
			SendItem( productionConfiguration.outputType, null, ItemDispatcher.Priority.high );
		Profiler.EndSample();

		mapIndicator.SetActive( true );
		mapIndicator.transform.localScale = new Vector3( GroundNode.size * productivity.current / 10, 1, GroundNode.size * 0.02f );
		mapIndicatorMaterial.color = Color.Lerp( Color.red, Color.white, productivity.current );


		if ( worker == null )
			dispenser = worker = Worker.Create().SetupForBuilding( this );
		if ( workerMate == null )
		{
			dispenser = workerMate = Worker.Create().SetupForBuilding( this, true );
			workerMate.ScheduleWait( 100, true );
		}

		if ( gatherer && worker.IsIdle() && worker.node == node )
			SetWorking( false );

		Profiler.BeginSample( "Internal" );
		switch ( type )
		{
			case Type.farm:
			{
				Profiler.BeginSample( "Farm" );
				if ( worker.IsIdle( true ) && mode != Mode.sleeping )
				{
					if ( output < productionConfiguration.outputMax )
					{
						foreach ( var o in Ground.areas[3] )
						{
							GroundNode place = node.Add( o );
							if ( place.building || place.flag || place.road || place.fixedHeight )
								continue;
							Resource cornfield = place.resource;
							if ( cornfield == null || cornfield.type != Resource.Type.cornfield || cornfield.hunter || !cornfield.IsReadyToBeHarvested() )
								continue;
							CollectResourceFromNode( place, Resource.Type.cornfield );
							return;
						}
					}
					foreach ( var o in Ground.areas[3] )
					{
						GroundNode place = node.Add( o );
						if ( place.IsBlocking( true ) || place.fixedHeight || !place.CheckType( GroundNode.Type.grass ) )
							continue;
						PlantAt( place, Resource.Type.cornfield );
						return;
					}
					worker.ScheduleWait( 300 );
				}
				Profiler.EndSample();
				break;
			}
			case Type.forester:
			{
				Profiler.BeginSample( "Forester" );
				if ( worker.IsIdle( true ) && mode != Mode.sleeping )
				{
					var o = Ground.areas[productionConfiguration.gatheringRange];
					for ( int i = 0; i < o.Count; i++ )
					{
						int randomOffset = World.rnd.Next( o.Count );
						int x = (i + randomOffset) % o.Count;
						GroundNode place = node.Add( o[x] );
						{
							if ( place.IsBlocking( true ) || !place.CheckType( GroundNode.Type.forest ) || place.fixedHeight )
								continue;
							int blockedAdjacentNodes = 0;
							foreach ( var j in Ground.areas[1] )
							{
								if ( place.Add( j ).IsBlocking() )
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
				Profiler.EndSample();
				break;
			}
			case Type.barrack:
			{
				if ( mode == Mode.sleeping )
					return;

				Profiler.BeginSample( "Barrack" );
				if ( buffers[0].stored > 0 && buffers[1].stored > 0 && ( soldierWasCreatedLastTime == false || buffers[2].stored == 0 ) )
				{
					buffers[0].stored--;
					buffers[1].stored--;
					owner.soldiersProduced++;
					print( "Soldier produced" );
					soldierWasCreatedLastTime = true;
				}
				if ( buffers[0].stored > 0 && buffers[2].stored > 0 && ( soldierWasCreatedLastTime || buffers[1].stored == 0 ) )
				{
					buffers[0].stored--;
					buffers[2].stored--;
					owner.bowmansProduced++;
					print( "Bowman produced" );
					soldierWasCreatedLastTime = false;
				}
				if ( buffers[3].stored > 0 )
				{
					buffers[3].stored--;
					owner.coinsProduced++;
					print( "Coin produced" );
				};
				Profiler.EndSample();
				break;
			}
			default:
			{
				Profiler.BeginSample( "Default" );
				if ( gatherer )
					CollectResource( productionConfiguration.gatheredResource, productionConfiguration.gatheringRange );
				else
					ProcessInput();

				if ( type == Type.mill && working )
					millWheelSpeed += 0.01f;
				else
					millWheelSpeed -= 0.01f;
				if ( millWheelSpeed > 1 )
					millWheelSpeed = 1;
				if ( millWheelSpeed < 0 )
					millWheelSpeed = 0;
				millWheel?.Rotate( 0, 0, World.instance.timeFactor * millWheelSpeed );
				Profiler.EndSample();
				break;
			}
		}
		Profiler.EndSample();
	}

	bool UseInput( int count = 1 )
	{
		bool common = productionConfiguration.commonInputs;
		if ( count == 0 || buffers.Count == 0 )
			return true;

		int min = int.MaxValue, sum = 0;
		foreach ( var b in buffers )
		{
			sum += b.stored;
			if ( min > b.stored )
				min = b.stored;
		}
		if ( ( common && sum < count ) || ( !common && min < count ) )
			return false;

		int o = World.rnd.Next();
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
		if ( productionConfiguration.outputType == Item.Type.unknown )
			return;

		if ( !working && output + productionConfiguration.outputStackSize <= productionConfiguration.outputMax && worker.IsIdle( true ) && mode != Mode.sleeping && UseInput() )
		{
			SetWorking( true );
			progress = 0;
		}
		if ( working )
		{
			progress += ground.world.timeFactor / productionConfiguration.productionTime;
			if ( progress > 1 )
			{
				output += productionConfiguration.outputStackSize;
				SetWorking( false );
				itemsProduced += productionConfiguration.outputStackSize;
				owner.ItemProduced( productionConfiguration.outputType, productionConfiguration.outputStackSize );
			}
		}
	}

	void CollectResource( Resource.Type resourceType, int range )
	{
		if ( !worker.IsIdle( true ) || mode == Mode.sleeping )
			return;
		if ( output >= productionConfiguration.outputMax )
			return;

		resourcePlace = null;
		assert.IsTrue( worker.IsIdle() );
		assert.IsTrue( range < Ground.areas.Length );
		if ( range > Ground.areas.Length )
			range = Ground.areas.Length - 1;
		GroundNode target;
		int t = Ground.areas[range].Count;
		int r = World.rnd.Next( t );
		for ( int j = -1; j < t; j++ )
		{
			if ( j < 0 )
				target = node;
			else
				target = node.Add( Ground.areas[range][(j+r)%t] );
			Resource resource = target.resource;
			if ( resource == null || resource.hunter != null )
				continue;
			if ( resourceType == Resource.Type.expose )
			{
				if ( resource.underGround && ( resource.exposed.done || resource.exposed.empty ) )
				{
					CollectResourceFromNode( target, resourceType );
					return;
				}
			}
			if ( resource.type == resourceType && resource.IsReadyToBeHarvested() )
			{
				CollectResourceFromNode( target, resourceType );
				return;
			}
		}
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

	void CollectResourceFromNode( GroundNode target, Resource.Type resourceType )
	{
		if ( !UseInput() || flag.FreeSpace() == 0 )
			return;

		assert.IsTrue( worker.IsIdle() );
		worker.gameObject.SetActive( true );
		assert.IsTrue( resourceType == Resource.Type.expose || resourceType == Resource.Type.fish || target.resource.type == resourceType );
		if ( !Resource.IsUnderGround( resourceType ) )
		{
			worker.ScheduleWalkToNeighbour( flag.node );
			worker.ScheduleWalkToNode( target, true, false, Worker.resourceCollectAct[(int)resourceType] );
		}
		resourcePlace = target;
		var task = ScriptableObject.CreateInstance<GetResource>();
		task.Setup( worker, target, resourceType );
		worker.ScheduleTask( task );
		if ( target.resource )
			target.resource.hunter = worker;
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

	public void SetWorking( bool working )
	{
		if ( this.working == working )
			return;

		this.working = working;
		if ( working )
		{
			smoke?.Play();
			soundSource.loop = true;
			soundSource.clip = processingSounds.GetMediaData( type );
			soundSource.Play();
		}
		else
		{
			smoke?.Stop();
			soundSource.Stop();
		}
	}

	public void Callback( Worker worker )
	{
		// Worker returned back from gathering resource
		assert.AreEqual( worker, this.worker );
		SetWorking( false );
	}

	void PlantAt( GroundNode place, Resource.Type resourceType )
	{
		assert.IsTrue( worker.IsIdle() );
		worker.gameObject.SetActive( true );
		worker.ScheduleWalkToNeighbour( flag.node );
		worker.ScheduleWalkToNode( place, true );
		var task = ScriptableObject.CreateInstance<Plant>();
		task.Setup( worker, place, resourceType );
		worker.ScheduleTask( task );
		SetWorking( true );
	}

	public override void OnClicked()
	{
		if ( construction.done )
			Interface.WorkshopPanel.Create().Open( this );
		else
			Interface.ConstructionPanel.Create().Open( this.construction );
	}

	public void OnDrawGizmos()
	{
#if DEBUG
		if ( Selection.Contains( gameObject ) && resourcePlace != null )
		{
			Gizmos.color = Color.red;
			Gizmos.DrawLine( node.position + Vector3.up * GroundNode.size, resourcePlace.position );
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
	}
}
