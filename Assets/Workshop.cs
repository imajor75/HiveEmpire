﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class Workshop : Building
{
	public int output;
	public float progress;
	public bool working;
	public Type type = Type.unknown;
	public List<Buffer> buffers = new List<Buffer>();
	GameObject body;
	Transform millWheel;
	public GroundNode resourcePlace;
	public int itemsProduced;
	public Productivity productivity = new Productivity( 0.5f );
	AudioSource soundSource;
	static public MediaTable<AudioClip, Type> processingSounds;
	GameObject mapIndicator;
	Material mapIndicatorMaterial;
	public Configuration configuration;
	public static Configuration[] configurations;

	public static int[] resourceCutTime = new int[(int)Resource.Type.total];
	static MediaTable<GameObject, Type> looks;
	public static int mineOreRestTime = 6000;

	public class Configuration
	{
		public class Input
		{
			public Item.Type itemType;
			public int bufferSize = 6;
			public int stackSize = 1;
		}
		public Type type;
		public int plankNeeded = 2;
		public int stoneNeeded = 0;
		public bool flatteningNeeded = true;

		public Resource.Type gatheredResource;
		public int gatheringRange = 8;

		public Item.Type outputType = Item.Type.unknown;
		public int outputStackSize = 1;
		public float processSpeed = 0.0015f;
		public int outputMax = 8;

		public bool commonInputs = false;
		public Input[] inputs;
	}

	public struct Productivity
	{
		public Productivity( float current )
		{
			this.current = current;
			counter = workCounter = 0;
			weight = 0.5f;
			timinglength = 3000;
		}
		public void FixedUpdate( Workshop boss )
		{
			counter += (int)World.instance.speedModifier;
			if ( boss.IsWorking() )
				workCounter += (int)World.instance.speedModifier;
			if ( counter >= timinglength )
			{
				float p = (float)workCounter/counter;
				current = current * ( 1 - weight ) + p * weight;
				counter = workCounter = 0;
			}
		}
		public float current;
		public int counter;
		public int workCounter;
		public float weight;
		public int timinglength;
	}

	[System.Serializable]
	public class Buffer
	{
		public int size = 6;
		public int stored;
		public int onTheWay;
		public int stackSize = 1;
		public ItemDispatcher.Priority priority = ItemDispatcher.Priority.high;
		public Item.Type itemType;
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
		total,
		unknown = -1
	}

	public class GetResource : Worker.Task
	{
		public GroundNode node;
		public Resource.Type resourceType;
		public int waitTimer = 0;
		public Item item;

		public void Setup( Worker boss, GroundNode node, Resource.Type resourceType, Item item )
		{
			base.Setup( boss );
			this.node = node;
			this.item = item;
			this.resourceType = resourceType;
		}
		public override void Validate()
		{
			if ( node.resource && resourceType != Resource.Type.fish )
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
			item.Remove();
			base.Cancel();
		}
		public override bool ExecuteFrame()
		{
			if ( !boss.soundSource.isPlaying )
			{
				boss.soundSource.clip = Worker.resourceGetSounds.GetMediaData( resourceType );
				boss.soundSource.loop = true;
				boss.soundSource.Play();
			}

			if ( waitTimer++ < resourceCutTime[(int)resourceType] )    // TODO Working on the resource
				return false;

			boss.soundSource.Stop();
			Resource resource = node.resource;
			if ( resource && resourceType != Resource.Type.fish )
			{
				if ( resourceType != Resource.Type.expose )
					boss.assert.AreEqual( resourceType, resource.type, "Resource types are different (expecting "+ resourceType.ToString()+" but was "+ resource.type.ToString() +")" );	// TODO Fired once (maybe fisherman met a tree?)
				boss.assert.AreEqual( boss, resource.hunter );
				if ( Resource.IsUnderGround( resourceType ) || node == boss.node )
				{
					if ( resourceType == Resource.Type.expose )
						resource.exposed = Resource.exposeMax;
					else
					{
						if ( --resource.charges == 0 )
							resource.Remove();
						else
						{
							if ( resource.underGround )
								resource.keepAwayTimer = mineOreRestTime;
						}
					}
				}
				else
					resource.keepAwayTimer = 500;   // TODO Settings
				boss.assert.AreEqual( resource.hunter, boss );
				resource.hunter = null;
			}
			FinishJob( boss, item );
			return true;
		}
	}

	public class Plant : Worker.Task
	{
		public GroundNode node;
		public int waitTimer = 0;
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
			if ( waitTimer > 0 )
			{
				waitTimer--;
				return false;
			}

			if ( done )
				return true;
			if ( boss.node != node )
				return true;
			if ( node.building || node.flag || node.road || node.fixedHeight || node.resource || node.type != GroundNode.Type.grass )
				return true;

			Resource.Create().Setup( node, resourceType );
			done = true;
			boss.assert.IsNotNull( node.resource );
			waitTimer = 100;
			boss.ScheduleWalkToNode( boss.building.flag.node );
			boss.ScheduleWalkToNeighbour( boss.building.node );
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
				resource.Remove();
				return true;
			}
			return false;

		}

		public override void Cancel()
		{
			if ( resource )
			{
				resource.animals.Clear();
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
		using ( var sw = new StreamReader( "workshops.json" ) )
		using ( var reader = new JsonTextReader( sw ) )
		{
			var serializer = JsonSerializer.Create();
			configurations = serializer.Deserialize<Configuration[]>( reader );
		}

		object[] looksData = {
			"Medieval fantasy house/Medieva_fantasy_house",
			"Medieval house/Medieval_house 1", 1.1f, Type.fishingHut, 
			"Baker House/Prefabs/Baker_house", 1.4f, Type.bakery, Type.hunter,
			"Fantasy House/Prefab/Fantasy_House_6", 1.8f, Type.stonemason, Type.sawmill,
			"WatchTower/Tower",
			"Fantasy_Kingdom_Pack_Lite/Perfabs/Building Combination/BuildingAT07", 1.5f, Type.farm, 
			"mill/melnica_mod", 2.0f, Type.mill,
			"Mines/saltmine_final", 1.5f, Type.saltmine,
			"Mines/coalmine_final", 1.1f, Type.coalmine,
			"Mines/ironmine_final", 1.5f, Type.ironmine,
			"Mines/goldmine_final", 1.5f, Type.goldmine,
			"Mines/stonemine_final", 1.5f, Type.stonemine,
			"Forest/woodcutter_final", 1.1f, Type.woodcutter,
			"Forest/forester_final", 1.1f, Type.forester,
			"Ores/geologist_final", 0.8f, Type.geologist,
			"SAdK/smelter_final", 1.5f, Type.smelter,
			"SAdK/weaponmaker_final", 1.5f, Type.weaponmaker,
			"SAdK/bowmaker_final", 1.5f, Type.bowmaker,
			"Stylized Well/Well/Prefab", 1f, Type.well };
		looks.Fill( looksData );
		object[] sounds = {
			"handsaw", Type.sawmill,
			"SAdK/bowmaker", Type.bowmaker,
			"SAdK/smelter", Type.smelter,
			"SAdK/weaponmaker", Type.weaponmaker,
			"windmill", Type.mill };
		processingSounds.Fill( sounds );
		for ( int i = 0; i < resourceCutTime.Length; i++ )
		{
			if ( Resource.IsUnderGround( (Resource.Type)i ) )
				resourceCutTime[i] = 1000;
			else
				resourceCutTime[i] = 500;
		}
	}

	public static Workshop Create()
	{
		var buildingObject = new GameObject();
		return buildingObject.AddComponent<Workshop>();
	}

	public Workshop Setup( Ground ground, GroundNode node, Player owner, Type type )
	{
		this.type = type;
		buffers.Clear();
		foreach ( var c in configurations )
			if ( c.type == type )
				configuration = c;
		assert.IsNotNull( configuration );
		foreach ( var input in configuration.inputs )
		{
			Buffer b = new Buffer();
			b.itemType = input.itemType;
			b.size = input.bufferSize;
			b.stackSize = input.stackSize;
			buffers.Add( b );

		}
		if ( Setup( ground, node, owner ) == null )
			return null;

		return this;
	}

	public bool IsWorking()
	{
		return worker != null && !worker.IsIdle( true ); 
	}

	new void Start()
	{
		var m = looks.GetMedia( type );
		body = (GameObject)GameObject.Instantiate( m.data, transform );
		height = m.floatData;
		assert.IsNotNull( body );
		if ( type == Type.mill )
		{
			millWheel = body.transform.Find( "group1/millWheel" );
			assert.IsNotNull( millWheel );
		}
		base.Start();
		string name = type.ToString();
		this.name = name.First().ToString().ToUpper() + name.Substring( 1 );

		soundSource = World.CreateSoundSource( this );

		mapIndicator = GameObject.CreatePrimitive( PrimitiveType.Plane );
		mapIndicator.transform.SetParent( transform, false );
		World.SetLayerRecursive( mapIndicator, World.layerIndexMapOnly );
		mapIndicatorMaterial = mapIndicator.GetComponent<MeshRenderer>().material = new Material( World.defaultShader );
		mapIndicator.transform.position = node.Position() + new Vector3( 0, 2, GroundNode.size * 0.5f );
		mapIndicator.SetActive( false );
	}

	new void Update()
	{
		base.Update();

		if ( !construction.done )
			return;

		foreach ( Buffer b in buffers )
		{
			int missing = b.size-b.stored-b.onTheWay;
			if ( missing > 0 )
				owner.itemDispatcher.RegisterRequest( this, b.itemType, missing, b.priority );
		}
		if ( output > 0 && flag.FreeSpace() > 0 && worker.IsIdle( true ) )
			owner.itemDispatcher.RegisterOffer( this, configuration.outputType, output, ItemDispatcher.Priority.high );

		mapIndicator.SetActive( true );
		mapIndicator.transform.localScale = new Vector3( GroundNode.size * productivity.current / 10, 1, GroundNode.size * 0.02f );
		mapIndicatorMaterial.color = Color.Lerp( Color.red, Color.green, productivity.current );
	}

	public override Item SendItem( Item.Type itemType, Building destination )
	{
		assert.AreEqual( configuration.outputType, itemType );
		assert.IsTrue( output > 0 );
		Item item = base.SendItem( itemType, destination );
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
		assert.IsTrue( false );
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
		assert.IsTrue( false );
	}

	new void FixedUpdate()
	{
		productivity.FixedUpdate( this );

		if ( !construction.done )
		{
			base.FixedUpdate();
			return;
		}

		if ( worker == null )
		{
			worker = Worker.Create();
			worker.SetupForBuilding( this );
		}

		if ( type == Type.farm )
		{
			if ( worker.IsIdle( true ) )
			{
				foreach ( var o in Ground.areas[3] )
				{
					GroundNode place = node.Add( o );
					if ( place.building || place.flag || place.road || place.fixedHeight )
						continue;
					Resource cornfield = place.resource;
					if ( cornfield == null || cornfield.type != Resource.Type.cornfield || !cornfield.IsReadyToBeHarvested() )
						continue;
					CollectResourceFromNode( place, Resource.Type.cornfield );
					return;
				}
				foreach ( var o in Ground.areas[3] )
				{
					GroundNode place = node.Add( o );
					if ( place.building || place.flag || place.road || place.fixedHeight || place.resource || place.type != GroundNode.Type.grass )
						continue;
					PlantAt( place, Resource.Type.cornfield );
					return;
				}
			}
		}
		if ( type == Type.forester )
		{
			if ( worker.IsIdle( true ) )
			{
				var o = Ground.areas[configuration.gatheringRange];
				for ( int i = 0; i < o.Count; i++ )
				{
					int randomOffset = World.rnd.Next( o.Count );
					int x = (i + randomOffset) % o.Count;
					GroundNode place = node.Add( o[x] );
					if ( place.building || place.flag || place.road || place.fixedHeight || place.resource || place.type != GroundNode.Type.grass )
						continue;
					PlantAt( place, Resource.Type.tree );
					return;
				}
			}
		}
		if ( type == Type.mill && working )
			millWheel?.Rotate( 0, 0, 1 );
	}

	bool UseInput( int count = 1 )
	{
		bool common = configuration.commonInputs;
		if ( count == 0 )
			return true;

		assert.IsTrue( buffers.Count > 0 );

		int min = int.MaxValue, sum = 0;
		foreach ( var b in buffers )
		{
			sum += b.stored;
			if ( min > b.stored )
				min = b.stored;
		}
		if ( (common && sum < count) || (common && min < count) )
			return false;

		foreach ( var b in buffers )
		{
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
		if ( !working && output + configuration.outputStackSize <= configuration.outputMax && UseInput() && worker.IsIdle( true ) )
		{
			soundSource.loop = true;
			soundSource.clip = processingSounds.GetMediaData( type );
			soundSource.Play();
			working = true;
			progress = 0;
		}
		if ( working )
		{
			progress += configuration.processSpeed * ground.world.speedModifier;
			if ( progress > 1 )
			{
				output += configuration.outputStackSize;
				working = false;
				soundSource.Stop();
				itemsProduced += configuration.outputStackSize;
			}
		}
	}

	void CollectResource( Resource.Type resourceType, int range )
	{
		if ( !worker.IsIdle( true ) )
			return;
		if ( configuration.outputType != Item.Type.unknown && flag.FreeSpace() == 0 )
			return;

		resourcePlace = null;
		assert.IsTrue( worker.IsIdle() );
		assert.IsTrue( range < Ground.areas.Length );
		if ( range > Ground.areas.Length )
			range = Ground.areas.Length - 1;
		GroundNode target;
		int t = Ground.areas[range].Count;
		int r = World.rnd.Next( t );
		for ( int j = 0; j < t; j++ )
		{
			var o = Ground.areas[range][(j+r)%t];
			target = node.Add( o );
			if ( resourceType == Resource.Type.fish )
			{
				int water = 0;
				for ( int i = 0; i < GroundNode.neighbourCount; i++ )
					if ( target.Neighbour( i ).type == GroundNode.Type.underWater )
						water++;

				if ( water > 0 && target.type != GroundNode.Type.underWater && target.resource == null )
				{
					CollectResourceFromNode( target, resourceType );
					return;
				}
				continue;
			}
			Resource resource = target.resource;
			if ( resource == null || resource.hunter != null )
				continue;
			if ( resourceType == Resource.Type.expose )
			{
				if ( resource.underGround && resource.exposed < 0 )
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

	void CollectResourceFromNode( GroundNode target, Resource.Type resourceType )
	{
		if ( !UseInput() )
			return;

		Item item = null;
		if ( configuration.outputType != Item.Type.unknown )
		{
			item = Item.Create().Setup( configuration.outputType, this );
			flag.ReserveItem( item );
			item.worker = worker;
			worker.reservation = flag;
		}
		assert.IsTrue( worker.IsIdle() );
		assert.IsTrue( resourceType == Resource.Type.expose || resourceType == Resource.Type.fish || target.resource.type == resourceType );
		if ( !Resource.IsUnderGround( resourceType ) )
		{
			worker.ScheduleWalkToNeighbour( flag.node );
			worker.ScheduleWalkToNode( target, true );
		}
		resourcePlace = target;
		var task = ScriptableObject.CreateInstance<GetResource>();
		task.Setup( worker, target, resourceType, item );
		worker.ScheduleTask( task );
		if ( target.resource )
			target.resource.hunter = worker;
	}

	static void FinishJob( Worker worker, Item item )
	{
		if ( item != null )
		{
			worker.SchedulePickupItem( item );
			( (Workshop)worker.building ).itemsProduced++;
		}
		worker.ScheduleWalkToNode( worker.building.flag.node );
		if ( item != null )
			worker.ScheduleDeliverItem( item );
		worker.ScheduleWalkToNeighbour( worker.building.node );
	}

	void PlantAt( GroundNode place, Resource.Type resourceType )
	{
		assert.IsTrue( worker.IsIdle() );
		worker.ScheduleWalkToNeighbour( flag.node );
		worker.ScheduleWalkToNode( place, true );
		var task = ScriptableObject.CreateInstance<Plant>();
		task.Setup( worker, place, resourceType );
		worker.ScheduleTask( task );
	}

	public override void OnClicked()
	{
		if ( construction.done )
			Interface.WorkshopPanel.Create().Open( this );
		else
			Interface.ConstructionPanel.Create().Open( this.construction );
	}

	void OnDrawGizmos()
	{
		if ( Selection.Contains( gameObject ) && resourcePlace != null )
		{
			Gizmos.color = Color.red;
			Gizmos.DrawLine( node.Position() + Vector3.up * GroundNode.size, resourcePlace.Position() );
		}
	}

	public override void Validate()
	{
		base.Validate();
		int itemsOnTheWayCount = 0;
		foreach ( Buffer b in buffers )
		{
			assert.IsTrue( b.stored + b.onTheWay <= b.size );
			itemsOnTheWayCount += b.onTheWay;
		}
		if ( construction.done )
			assert.AreEqual( itemsOnTheWayCount, itemsOnTheWay.Count );
	}
}
