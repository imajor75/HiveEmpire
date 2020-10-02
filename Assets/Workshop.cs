using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.Assertions;

public class Workshop : Building
{
	public int output;
	public Item.Type outputType = Item.Type.unknown;
	public int outputMax = 8;
	public float progress;
	public bool working;
	public Type type = Type.unknown;
	public List<Buffer> buffers = new List<Buffer>();
	public bool commonInputs = false;
	GameObject body;
	public int inputStep = 1;
	public int outputStep = 1;
	public float processSpeed = 0.0015f;
	Transform millWheel;
	public GroundNode resourcePlace = Worker.zero;
	AudioSource soundSource;
	static public MediaTable<AudioClip, Type> processingSounds;

	public static int woodcutterRange = 8;
	public static int foresterRange = 8;
	public static int hunterRange = 8;
	public static int goldMineRange = 8;
	public static int ironMineRange = 8;
	public static int coalMineRange = 8;
	public static int saltMineRange = 8;
	public static int stoneMineRange = 8;
	public static int stonemasonRange = 8;
	public static int fisherRange = 8;
	public static int geologistRange = 8;
	public static int plantingTime = 100;
	public static int[] resourceCutTime = new int[(int)Resource.Type.total];
	static MediaTable<GameObject, Type> looks;
	public static int mineOreRestTime = 6000;

	[System.Serializable]
	public class Buffer
	{
		public int size = 6;
		public int stored;
		public int onTheWay;
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
		total,
		unknown = -1
	}

	public class GetResource : Worker.Task
	{
		public GroundNode node;
		public Resource.Type resourceType;
		public int waitTimer = 0;

		public void Setup( Worker boss, GroundNode node, Resource.Type resourceType )
		{
			base.Setup( boss );
			this.node = node;
			this.resourceType = resourceType;
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
			if ( resource )
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
				resource.hunter = null;
			}
			FinishJob( boss, Resource.ItemType( resourceType ) );
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
			waitTimer = plantingTime;
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
		object[] looksData = {
			"Medieval fantasy house/Medieva_fantasy_house",
			"Medieval house/Medieval_house 1", 1.1f, Type.fishingHut, 
			"Baker House/Prefabs/Baker_house", 1.4f, Type.bakery, Type.hunter,
			"Fantasy House/Prefab/Fantasy_House_6", 1.8f, Type.stonemason, Type.sawmill,
			"WatchTower/Tower",
			"Fantasy_Kingdom_Pack_Lite/Perfabs/Building Combination/BuildingAT07", 1.5f, Type.farm, 
			"mill/melnica_mod", 2.0f, Type.mill,
			"Mines/saltmine_final", 1.5f, Type.saltmine,
			"Mines/coalmine_final", 1.5f, Type.coalmine,
			"Mines/ironmine_final", 1.5f, Type.ironmine,
			"Mines/goldmine_final", 1.5f, Type.goldmine,
			"Mines/stonemine_final", 1.5f, Type.stonemine,
			"Forest/woodcutter_final", 1.1f, Type.woodcutter,
			"Forest/forester_final", 1.1f, Type.forester,
			"Ores/geologist_final", 0.8f, Type.geologist };
		looks.Fill( looksData );
		object[] sounds = {
			"handsaw", Type.sawmill };
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
		switch ( type )
		{
			case Type.woodcutter:
			{
				inputStep = 0;
				outputType = Item.Type.log;
				construction.plankNeeded = 2;
				construction.flatteningNeeded = false;
				break;
			}
			case Type.forester:
			{
				inputStep = 0;
				construction.plankNeeded = 2;
				construction.flatteningNeeded = false;
				break;
			}
			case Type.stonemason:
			{
				inputStep = 0;
				outputType = Item.Type.stone;
				construction.plankNeeded = 2;
				construction.flatteningNeeded = true;
				break;
			}
			case Type.sawmill:
			{
				AddInput( Item.Type.log );
				outputType = Item.Type.plank;
				construction.plankNeeded = 2;
				construction.flatteningNeeded = true;
				break;
			}
			case Type.fishingHut:
			{
				inputStep = 0;
				outputType = Item.Type.fish;
				construction.plankNeeded = 2;
				construction.flatteningNeeded = false;
				break;
			}
			case Type.farm:
			{
				inputStep = 0;
				outputType = Item.Type.grain;
				construction.plankNeeded = 2;
				construction.stoneNeeded = 2;
				construction.flatteningNeeded = true;
				break;
			}
			case Type.mill:
			{
				AddInput( Item.Type.grain );
				outputType = Item.Type.flour;
				construction.plankNeeded = 2;
				construction.flatteningNeeded = false;
				break;
			}
			case Type.bakery:
			{
				AddInput( Item.Type.salt );
				AddInput( Item.Type.flour );
				outputType = Item.Type.pretzel;
				construction.plankNeeded = 2;
				construction.stoneNeeded = 2;
				construction.flatteningNeeded = false;
				break;
			}
			case Type.hunter:
			{
				inputStep = 0;
				outputType = Item.Type.hide;
				construction.plankNeeded = 1;
				construction.flatteningNeeded = false;
				break;
			}
			case Type.coalmine:
			{
				Item.Type[] types = { Item.Type.fish, Item.Type.pretzel };
				AddInputGroup( types );
				outputType = Item.Type.coal;
				construction.plankNeeded = 2;
				construction.flatteningNeeded = false;
				construction.groundTypeNeeded = GroundNode.Type.hill;
				break;
			}
			case Type.stonemine:
			{
				Item.Type[] types = { Item.Type.fish, Item.Type.pretzel };
				AddInputGroup( types );
				outputType = Item.Type.stone;
				construction.plankNeeded = 2;
				construction.flatteningNeeded = false;
				construction.groundTypeNeeded = GroundNode.Type.hill;
				break;
			}
			case Type.ironmine:
			{
				Item.Type[] types = { Item.Type.fish, Item.Type.pretzel };
				AddInputGroup( types );
				outputType = Item.Type.iron;
				construction.plankNeeded = 2;
				construction.flatteningNeeded = false;
				construction.groundTypeNeeded = GroundNode.Type.hill;
				break;
			}
			case Type.goldmine:
			{
				Item.Type[] types = { Item.Type.fish, Item.Type.pretzel };
				AddInputGroup( types );
				outputType = Item.Type.gold;
				construction.plankNeeded = 2;
				construction.flatteningNeeded = false;
				construction.groundTypeNeeded = GroundNode.Type.hill;
				break;
			}
			case Type.saltmine:
			{
				Item.Type[] types = { Item.Type.fish, Item.Type.pretzel };
				AddInputGroup( types );
				outputType = Item.Type.salt;
				construction.plankNeeded = 2;
				construction.flatteningNeeded = false;
				construction.groundTypeNeeded = GroundNode.Type.hill;
				break;
			}
			case Type.geologist:
			{
				inputStep = 0;
				construction.plankNeeded = 2;
				construction.flatteningNeeded = false;
				break;
			}
		}
		if ( Setup( ground, node, owner ) == null )
			return null;

		return this;
	}

	void AddInput( Item.Type itemType, int size = 6 )
	{
		assert.IsFalse( commonInputs );
		Buffer b = new Buffer();
		b.itemType = itemType;
		b.size = size;
		buffers.Add( b );
	}

	void AddInputGroup( Item.Type[] itemTypes, int size = 2 )
	{
		assert.AreEqual( buffers.Count, 0 );
		foreach ( var itemType in itemTypes )
			AddInput( itemType, size );
		commonInputs = true;
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
				ItemDispatcher.lastInstance.RegisterRequest( this, b.itemType, missing, b.priority );
		}
		if ( output > 0 && flag.FreeSpace() > 0 )
			ItemDispatcher.lastInstance.RegisterOffer( this, outputType, output, ItemDispatcher.Priority.high );
	}

	public override Item SendItem( Item.Type itemType, Building destination )
	{
		assert.AreEqual( outputType, itemType );
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

		switch ( type )
		{
			case Type.woodcutter:
			{
				if ( worker.IsIdle( true ) )
					CollectResource( Resource.Type.tree, woodcutterRange );
				break;
			}
			case Type.stonemason:
			{
				if ( worker.IsIdle( true ) )
					CollectResource( Resource.Type.rock, stonemasonRange );
				break;
			}
			case Type.sawmill:
			{
				ProcessInput();
				break;
			}
			case Type.fishingHut:
			{
				if ( worker.IsIdle( true ) )
					CollectResource( Resource.Type.fish, fisherRange );
				break;
			}
			case Type.farm:
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
				break;
			}
			case Type.forester:
			{
				if ( worker.IsIdle( true ) )
				{
					var o = Ground.areas[foresterRange];
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
				break;
			}
			case Type.mill:
			{
				ProcessInput();
				if ( working )
					millWheel?.Rotate( 0, 0, 1 );
				break;
			}
			case Type.bakery:
			{
				ProcessInput();
				break;
			}
			case Type.hunter:
			{
				CollectResource( Resource.Type.pasturingAnimal, hunterRange );
				break;
			}
			case Type.goldmine:
			{
				CollectResource( Resource.Type.gold, goldMineRange );
				break;
			}
			case Type.ironmine:
			{
				CollectResource( Resource.Type.iron, ironMineRange );
				break;
			}
			case Type.coalmine:
			{
				CollectResource( Resource.Type.coal, coalMineRange );
				break;
			}
			case Type.saltmine:
			{
				CollectResource( Resource.Type.salt, saltMineRange );
				break;
			}
			case Type.stonemine:
			{
				CollectResource( Resource.Type.stone, stoneMineRange );
				break;
			}
			case Type.geologist:
			{
				CollectResource( Resource.Type.expose, geologistRange );
				break;
			}
		}
	}

	bool UseInput( int count = 0 )
	{
		if ( count == 0 )
			count = inputStep;
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
		if ( (commonInputs && sum < count) || (!commonInputs && min < count) )
			return false;

		foreach ( var b in buffers )
		{
			if ( commonInputs )
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
		if ( !working && output + outputStep <= outputMax && UseInput() )
		{
			soundSource.loop = true;
			soundSource.clip = processingSounds.GetMediaData( type );
			soundSource.Play();
			working = true;
			progress = 0;
		}
		if ( working && worker && worker.IsIdle( true ) )
		{
			progress += processSpeed * ground.world.speedModifier;
			if ( progress > 1 )
			{
				output += outputStep;
				working = false;
				soundSource.Stop();
			}
		}
	}

	void CollectResource( Resource.Type resourceType, int range )
	{
		if ( !worker.IsIdle( true ) )
			return;
		if ( outputType != Item.Type.unknown && flag.FreeSpace() == 0 )
			return;

		resourcePlace = Worker.zero;
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
			if ( resource == null )
				continue;
			if ( resourceType == Resource.Type.expose )
			{
				if ( resource.underGround && resource.exposed < 0 )
				{
					CollectResourceFromNode( target, resourceType );
					return;
				}
			}
			if ( resource.type == resourceType && resource.hunter == null && resource.IsReadyToBeHarvested() )
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

		if ( outputType != Item.Type.unknown )
		{
			flag.reserved++;
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
		task.Setup( worker, target, resourceType );
		worker.ScheduleTask( task );
		if ( target.resource )
			target.resource.hunter = worker;
	}

	static void FinishJob( Worker worker, Item.Type itemType )
	{
		Item item = null;
		if( itemType != Item.Type.unknown )
		{
			item = Item.Create().Setup( itemType, worker.building );
			worker.SchedulePickupItem( item );
		}
		worker.ScheduleWalkToNode( worker.building.flag.node );
		if ( itemType != Item.Type.unknown )
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
		Interface.WorkshopPanel.Create().Open( this );
	}

	void OnDrawGizmos()
	{
		if ( Selection.Contains( gameObject ) && resourcePlace != Worker.zero )
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
