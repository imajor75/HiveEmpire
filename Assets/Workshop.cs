using System.Collections.Generic;
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
	GameObject body;

	public static int woodcutterRange = 8;
	public static int stonemasonRange = 8;
	public static int fisherRange = 8;

	[System.Serializable]
	public class Buffer
	{
		public int size = 8;
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
		total,
		unknown = -1
	}

	public class CutResource : Worker.Task
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
			if ( waitTimer++ < 100 )    // TODO Working on the resource
				return false;

			Item.Type itemType = Item.Type.unknown;
			Resource resource = node.resource;
			if ( resource )
			{
				Assert.AreEqual( resource.type, resourceType );
				Assert.AreEqual( boss, resource.hunter );
				if ( node == boss.node )
				{
					itemType = Resource.ItemType( resourceType );
					if ( --resource.charges == 0 )
						resource.Remove();
				}
				else
					resource.keepAwayTimer = 500;   // TODO Settings
				resource.hunter = null;
			}
			if ( resourceType == Resource.Type.fish )
				itemType = Item.Type.fish;
			FinishJob( boss, itemType );
			return true;
		}
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
				outputType = Item.Type.log;
				working = true;
				construction.plankNeeded = 2;
				construction.flatteningNeeded = true;
				body = (GameObject)GameObject.Instantiate( templates[1], transform );
				break;
			}
			case Type.stonemason:
			{
				outputType = Item.Type.stone;
				working = true;
				construction.plankNeeded = 2;
				construction.flatteningNeeded = true;
				body = (GameObject)GameObject.Instantiate( templates[2], transform );
				break;
			}
			case Type.sawmill:
			{
				Buffer b = new Buffer();
				b.itemType = Item.Type.log;
				buffers.Add( b );
				outputType = Item.Type.plank;
				working = false;
				construction.plankNeeded = 2;
				construction.flatteningNeeded = true;
				body = (GameObject)GameObject.Instantiate( templates[3], transform );
				height = 2;
				break;
			}
			case Type.fishingHut:
			{
				outputType = Item.Type.fish;
				working = true;
				construction.plankNeeded = 2;
				construction.flatteningNeeded = false;
				body = (GameObject)GameObject.Instantiate( templates[1], transform );
				break;
			}

		}
		if ( Setup( ground, node, owner ) == null )
			return null;

		return this;
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
				if ( !working && output < outputMax && buffers[0].stored > 0 )
				{
					working = true;
					progress = 0;
					buffers[0].stored--;
				}
				if ( working && worker && worker.IsIdleInBuilding() )
				{
					progress += 0.0015f * ground.speedModifier;
					if ( progress > 1 )
					{
						output++;
						working = false;
					}
				}
				break;
			}
			case Type.fishingHut:
			{
				if ( worker.IsIdleInBuilding() )
					CollectResource( Resource.Type.fish, fisherRange );
				break;
			}
		}
	}

	void CollectResource( Resource.Type resourceType, int range )
	{
		Assert.IsTrue( worker.taskQueue.Count == 0 );
		Assert.IsTrue( range < Ground.areas.Length );
		if ( range > Ground.areas.Length )
			range = Ground.areas.Length - 1;
		GroundNode prey;
		int t = Ground.areas[range].Count;
		int r = Ground.rnd.Next( t );
		for ( int j = 0; j < t; j++ )
		{
			var o = Ground.areas[range][(j+r)%t];
			prey = node.Add( o );
			if ( resourceType == Resource.Type.fish )
			{
				int water = 0;
				for ( int i = 0; i < GroundNode.neighbourCount; i++ )
					if ( prey.Neighbour( i ).type == GroundNode.Type.underWater )
						water++;

				if ( water > 0 && prey.type != GroundNode.Type.underWater && prey.resource == null )
				{
					// TODO Randomly select the spot
					CollectResourceFromNode( prey, resourceType );
					return;
				}
				continue;
			}
			Resource resource = prey.resource;
			if ( resource == null )
				continue;
			if ( resource.type == resourceType && resource.hunter == null && resource.keepAwayTimer < 0 )
			{
				resource.hunter = worker;
				CollectResourceFromNode( prey, resourceType );
				return;
			}
		}
	}

	void CollectResourceFromNode( GroundNode prey, Resource.Type resourceType )
	{
		Assert.IsTrue( worker.taskQueue.Count == 0 );
		Assert.IsTrue( resourceType == Resource.Type.fish || prey.resource.type == resourceType );
		worker.ScheduleWalkToNeighbour( flag.node );
		worker.ScheduleWalkToNode( prey, true );
		var task = ScriptableObject.CreateInstance<CutResource>();
		task.Setup( worker, prey, resourceType );
		worker.ScheduleTask( task );
	}

	public override void OnClicked()
	{
		WorkshopPanel.Open( this );
	}

	public override void Validate()
	{
		base.Validate();
		foreach ( Buffer b in buffers )
			Assert.IsTrue( b.stored + b.onTheWay <= b.size );
	}
}
