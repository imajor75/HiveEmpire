using System;
using System.Collections.Generic;
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
	public Transform millWheel;

	public static int woodcutterRange = 8;
	public static int hunterRange = 8;
	public static int goldMineRange = 8;
	public static int ironMineRange = 8;
	public static int coalMineRange = 8;
	public static int saltMineRange = 8;
	public static int stoneMineRange = 8;
	public static int stonemasonRange = 8;
	public static int fisherRange = 8;
	public static int cornfieldGrowthMax = 6000;
	public static int plantingTime = 100;
	public static int[] resourceCutTime = new int[(int)Resource.Type.total];
	static List<Look> looks = new List<Look>();
	public static int mineOreRestTime = 6000;

	class Look
	{
		public string file;
		public GameObject template;
		public List<Type> types = new List<Type>();
		public float height = 1.5f;
	}

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
				Assert.AreEqual( boss, node.resource.hunter );
				node.resource.hunter = null;
			}
			base.Cancel();
		}
		public override bool ExecuteFrame()
		{
			if ( waitTimer++ < resourceCutTime[(int)resourceType] )    // TODO Working on the resource
				return false;

			Resource resource = node.resource;
			if ( resource )
			{
				Assert.AreEqual( resource.type, resourceType );
				Assert.AreEqual( boss, resource.hunter );
				if ( resource.underGround || node == boss.node )
				{
					if ( --resource.charges == 0 )
						resource.Remove();
					else
					{
						if ( resource.underGround )
							resource.keepAwayTimer = mineOreRestTime;
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

	public class PlantWheat : Worker.Task
	{
		public GroundNode node;
		public int waitTimer = 0;
		public bool done;

		public void Setup( Worker boss, GroundNode node )
		{
			base.Setup( boss );
			this.node = node;
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

			Resource.Create().Setup( node, Resource.Type.cornfield );
			done = true;
			Assert.IsNotNull( node.resource );
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
				Assert.AreEqual( resource.type, Resource.Type.pasturingAnimal );
				Assert.AreEqual( resource.node, boss.node );
			}
			base.Validate();
		}
	}

	public static new void Initialize()
	{
		object[] looksData = {
			"Medieval fantasy house/Medieva_fantasy_house",
			"Medieval house/Medieval_house 1", Type.woodcutter, Type.fishingHut, 
			"Baker House/Prefabs/Baker_house", Type.bakery, Type.hunter,
			"Fantasy House/Prefab/Fantasy_House_6", 1.8f, Type.stonemason, Type.sawmill,
			"WatchTower/Tower",
			"Fantasy_Kingdom_Pack_Lite/Perfabs/Building Combination/BuildingAT07", Type.farm, 
			"mill/melnica_mod", Type.mill,
			"Mines/saltmine_final", Type.saltmine,
			"Mines/coalmine_final", Type.coalmine,
			"Mines/ironmine_final", Type.ironmine,
			"Mines/goldmine_final", Type.goldmine,
			"Mines/stonemine_final", Type.stonemine };
		foreach ( var g in looksData )
		{
			string file = g as string;
			if ( file != null )
			{
				Look look = new Look();
				look.file = file;
				looks.Add( look );
			}
			Type? type = g as Type?;
			if ( type != null )
				looks[looks.Count - 1].types.Add( (Type)type );
			float? height = g as float?;
			if ( height != null )
				looks[looks.Count - 1].height = (float)height;
		}
		foreach ( var l in looks )
		{
			object o = Resources.Load( l.file ) as GameObject;
			if ( o == null )
			{
				Debug.Log( "Resource " + l.file + " not found" );
				continue;
			}
			l.template = o as GameObject;
			if ( l.template == null )
				Debug.Log( "Resource " + l.file + " has a root " + o.GetType().Name );
		}
		for ( int i = 0; i < resourceCutTime.Length; i++ )
		{
			if ( Resource.IsUnderGround( (Resource.Type)i ) )
				resourceCutTime[i] = 1000;
			else
				resourceCutTime[i] = 250;
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
				construction.flatteningNeeded = true;
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
		}
		if ( Setup( ground, node, owner ) == null )
			return null;

		return this;
	}

	void AddInput( Item.Type itemType, int size = 6 )
	{
		Assert.IsFalse( commonInputs );
		Buffer b = new Buffer();
		b.itemType = itemType;
		b.size = size;
		buffers.Add( b );
	}

	void AddInputGroup( Item.Type[] itemTypes, int size = 2 )
	{
		Assert.AreEqual( buffers.Count, 0 );
		foreach ( var itemType in itemTypes )
			AddInput( itemType, size );
		commonInputs = true;
	}

	new void Start()
	{
		foreach ( var l in looks )
		{
			if ( l.types.Contains( type ) )
			{
				body = (GameObject)GameObject.Instantiate( l.template, transform );
				height = l.height;
			}
		}
		Assert.IsNotNull( body );
		if ( type == Type.mill )
		{
			millWheel = body.transform.Find( "group1/millWheel" );
			Assert.IsNotNull( millWheel );
		}
		base.Start();
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
		Assert.AreEqual( outputType, itemType );
		Assert.IsTrue( output > 0 );
		Item item = base.SendItem( itemType, destination );
		if ( item != null )
			output--;
		return item;
	}

	public override void ItemOnTheWay( Item item, bool cancel = false )
	{
		if ( construction.ItemOnTheWay( item, cancel ) )
			return;

		foreach ( var b in buffers )
		{
			if ( b.itemType == item.type )
			{
				if ( cancel )
				{
					Assert.IsTrue( b.onTheWay > 0 );
					b.onTheWay--;
				}
				else
				{
					Assert.IsTrue( b.stored + b.onTheWay < b.size );
					b.onTheWay++;
				}
				return;
			}
		}
		Assert.IsTrue( false );
	}

	public override void ItemArrived( Item item )
	{
		if ( construction.ItemArrived( item ) )
			return;

		foreach ( var b in buffers )
		{
			if ( b.itemType == item.type )
			{
				Assert.IsTrue( b.onTheWay > 0 );
				b.onTheWay--;
				Assert.IsTrue( b.onTheWay + b.stored < b.size );
				b.stored++;
				return;
			}
		}
		Assert.IsTrue( false );
	}

	new void FixedUpdate()
	{
		if ( !construction.done )
		{
			base.FixedUpdate();
			return;
		}

		switch ( type )
		{
			case Type.woodcutter:
			{
				if ( worker.IsIdleInBuilding() )
					CollectResource( Resource.Type.tree, woodcutterRange );
				break;
			}
			case Type.stonemason:
			{
				if ( worker.IsIdleInBuilding() )
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
				if ( worker.IsIdleInBuilding() )
					CollectResource( Resource.Type.fish, fisherRange );
				break;
			}
			case Type.farm:
			{
				if ( worker.IsIdleInBuilding() )
				{
					foreach ( var o in Ground.areas[3] )
					{
						GroundNode place = node.Add( o );
						if ( place.building || place.flag || place.road || place.fixedHeight )
							continue;
						Resource cornfield = place.resource;
						if ( cornfield == null || cornfield.type != Resource.Type.cornfield || cornfield.growth < cornfieldGrowthMax )
							continue;
						CollectResourceFromNode( place, Resource.Type.cornfield );
						return;
					}
					foreach ( var o in Ground.areas[3] )
					{
						GroundNode place = node.Add( o );
						if ( place.building || place.flag || place.road || place.fixedHeight || place.resource || place.type != GroundNode.Type.grass )
							continue;
						Resource cornfield = place.resource;
						PlantWheatAt( place );
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
				if ( worker.IsIdleInBuilding() )
					CollectResource( Resource.Type.pasturingAnimal, hunterRange );
				break;
			}
			case Type.goldmine:
			{
				if ( worker.IsIdleInBuilding() )
					CollectResource( Resource.Type.gold, goldMineRange );
				break;
			}
			case Type.ironmine:
			{
				if ( worker.IsIdleInBuilding() )
					CollectResource( Resource.Type.iron, ironMineRange );
				break;
			}
			case Type.coalmine:
			{
				if ( worker.IsIdleInBuilding() )
					CollectResource( Resource.Type.coal, coalMineRange );
				break;
			}
			case Type.saltmine:
			{
				if ( worker.IsIdleInBuilding() )
					CollectResource( Resource.Type.salt, saltMineRange );
				break;
			}
			case Type.stonemine:
			{
				if ( worker.IsIdleInBuilding() )
					CollectResource( Resource.Type.stone, stoneMineRange );
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

		Assert.IsTrue( buffers.Count > 0 );

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
			working = true;
			progress = 0;
		}
		if ( working && worker && worker.IsIdleInBuilding() )
		{
			progress += processSpeed * ground.world.speedModifier;
			if ( progress > 1 )
			{
				output += outputStep;
				working = false;
			}
		}
	}

	void CollectResource( Resource.Type resourceType, int range )
	{
		Assert.IsTrue( worker.taskQueue.Count == 0 );
		Assert.IsTrue( range < Ground.areas.Length );
		if ( range > Ground.areas.Length )
			range = Ground.areas.Length - 1;
		GroundNode target;
		int t = Ground.areas[range].Count;
		int r = ground.world.rnd.Next( t );
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
			if ( resource.type == resourceType && resource.hunter == null && resource.keepAwayTimer < 0 )
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

		Assert.IsTrue( worker.taskQueue.Count == 0 );
		Assert.IsTrue( resourceType == Resource.Type.fish || target.resource.type == resourceType );
		if ( !Resource.IsUnderGround( resourceType ) )
		{
			worker.ScheduleWalkToNeighbour( flag.node );
			worker.ScheduleWalkToNode( target, true );
		}
		var task = ScriptableObject.CreateInstance<GetResource>();
		task.Setup( worker, target, resourceType );
		worker.ScheduleTask( task );
		if ( target.resource )
			target.resource.hunter = worker;
	}

	static void FinishJob( Worker worker, Item.Type itemType )
	{
		worker.ScheduleWalkToNode( worker.building.flag.node );
		if ( itemType != Item.Type.unknown )
		{
			Item item = Item.Create().Setup( itemType, worker.building );
			worker.itemInHands = item;
			item.worker = worker;
			worker.ScheduleDeliverItem( item );
		}
		worker.ScheduleWalkToNeighbour( worker.building.node );
	}

	void PlantWheatAt( GroundNode place )
	{
		Assert.IsTrue( worker.taskQueue.Count == 0 );
		worker.ScheduleWalkToNeighbour( flag.node );
		worker.ScheduleWalkToNode( place, true );
		var task = ScriptableObject.CreateInstance<PlantWheat>();
		task.Setup( worker, place );
		worker.ScheduleTask( task );
	}

	public override void OnClicked()
	{
		Interface.WorkshopPanel.Create().Open( this );
	}

	public override void Validate()
	{
		base.Validate();
		foreach ( Buffer b in buffers )
			Assert.IsTrue( b.stored + b.onTheWay <= b.size );
	}
}
