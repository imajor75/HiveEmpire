using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;

public class Stock : Building, Worker.Callback.IHandler
{
	public bool main = false;
	public List<ItemTypeData> itemData = new List<ItemTypeData>();
	public List<Worker> returningUnits = new List<Worker>();	// This list is maintained for returning units to store them during save, because they usually have no building
	public Cart cart;
	public Ground.Area inputArea = new Ground.Area();
	public Ground.Area outputArea = new Ground.Area();
	public int total;
	public int totalTarget;
	public int maxItems = Constants.Stock.defaultmaxItems;
	public World.Timer offersSuspended = new World.Timer();     // When this timer is in progress, the stock is not offering items. This is done only for cosmetic reasons, it won't slow the rate at which the stock is providing items.

	override public string title { get { return main ? "Headquarters" : "Stock"; } set {} }

	static readonly Configuration stockConfiguration = new Configuration
	{
		plankNeeded = Constants.Stock.plankNeeded,
		stoneNeeded = Constants.Stock.stoneNeeded,
		flatteningNeeded = Constants.Stock.flatteningNeeded,
		constructionTime = Constants.Stock.constructionTime,
		groundTypeNeeded = Constants.Stock.groundTypeNeeded
	};
	static readonly Configuration mainConfiguration = new Configuration	{ huge = true };

	public static GameObject template, mainTemplate;

	[Obsolete( "Compatibility with old files", true )]
	public Stock[] destinations 
	{ 
		set 
		{
			for ( int i = 0; i < value.Length; i++ )
			{
				if ( outputRoutes[i] == null )
					outputRoutes[i] = new List<Route>();
				if ( value[i] )
					outputRoutes[i].Add( new Route { start = this, end = value[i], itemType = (Item.Type)i } );
			}
		} 
	}
	[Obsolete( "Compatibility with old files", true )]
	List<List<Stock>> destinationLists
	{
		set
		{
			for ( int i = 0; i < value.Count; i++ )
			{
				while ( outputRoutes.Count <= i )
					outputRoutes.Add( new List<Route>() );
				foreach ( var s in value[i] )
					outputRoutes[i].Add( new Route { start = this, end = s, itemType = (Item.Type)i } );

			}

		}
	}

	[Obsolete( "Compatibility with old files", false ), JsonIgnore]
	public List<int> content;
	[Obsolete( "Compatibility with old files", false ), JsonIgnore]
	public List<int> onWay;
	[Obsolete( "Compatibility with old files", false ), JsonIgnore]
	public List<int> inputMin;
	[Obsolete( "Compatibility with old files", false ), JsonIgnore]
	public List<int> inputMax;
	[Obsolete( "Compatibility with old files", false ), JsonIgnore]
	public List<int> outputMin;
	[Obsolete( "Compatibility with old files", false ), JsonIgnore]
	public List<int> outputMax;
	[Obsolete( "Compatibility with old files", false ), JsonIgnore]
	public List<List<Route>> outputRoutes;
	[Obsolete( "Compatibility with old files", true ), JsonIgnore]
	public Versioned outputRouteVersion;

	[Serializable]
	public class ItemTypeData
	{
		[Obsolete( "Compatibility with old files", true )]
		public ItemTypeData()
		{}
		public ItemTypeData( Stock boss, Item.Type itemType )
		{
			this.boss = boss;
			this.itemType = itemType;
		}
		public int content;
		public int onWay;
		public int inputMax = Constants.Stock.defaultInputMax, inputMin = Constants.Stock.defaultInputMin;
		public int outputMax = Constants.Stock.defaultOutputMax, outputMin = Constants.Stock.defaultOutputMin;
		public int cartOutput = Constants.Stock.defaultCartOutput, cartInput = Constants.Stock.defaultCartInput;
		public List<Route> outputRoutes = new List<Route>();
		public Stock boss;
		public Item.Type itemType;

		[Obsolete( "Compatibility with old files", true )]
		CartOrder cartOrder
		{
			set
			{
				cartInput = value switch
				{
					CartOrder.getHigh => 15,
					CartOrder.getMedium => 10,
					CartOrder.getLow => 5,
					_ => 0
				};
				cartOutput = value switch
				{
					CartOrder.offerHigh => Constants.Stock.cartCapacity + 0,
					CartOrder.offerMedium => Constants.Stock.cartCapacity + 5,
					CartOrder.offerLow => Constants.Stock.cartCapacity + 10,
					_ => 0
				};
			}
		}

		public void UpdateRoutes()
		{
			int typeIndex = (int)itemType;
			for ( int i = 0; i < outputRoutes.Count; )
			{
				var route = outputRoutes[i];
				if ( route.start.itemData[typeIndex].cartOutput < Constants.Stock.cartCapacity || route.end.itemData[typeIndex].cartInput == 0 )
					route.Remove();
				else
					i++;
			}

			foreach ( var stock in boss.owner.stocks )
			{
				if ( stock == boss )
					continue;
				if ( cartOutput >= Constants.Stock.cartCapacity && stock.itemData[typeIndex].cartInput > 0 )
					AddNewRoute( stock );
			}	

			outputRoutes.Sort( (x, y) => y.priority.CompareTo( x.priority ) );
		}

		public Route AddNewRoute( Stock destination )
		{
			var itemTypeIndex = (int)itemType;
			foreach ( var route in outputRoutes )
			{
				if ( route.end == destination && route.itemType == itemType )
					return route;
			}

			var newRoute = new Route();
			newRoute.start = boss;
			newRoute.end = destination;
			newRoute.itemType = itemType;
			outputRoutes.Add( newRoute );
			return newRoute;
		}

		public enum CartOrder
		{
			getLow = -3,
			getMedium = -2,
			getHigh = -1,
			ignore = 0,
			offerLow = 1,
			offerMedium = 2,
			offerHigh = 3
		}
		public Route GetRouteForDestination( Stock destination )
		{
			foreach ( var route in outputRoutes )
			if ( route.end == destination )
				return route;
			return null;
		}
	}

	[Obsolete( "Compatibility for old files", true )]
	List<int> target = new List<int>();

	[Serializable]
	public class Route
	{
		public Stock start, end;
		public Item.Type itemType;
		public int lastDelivery;
		public float averageTransferRate;
		public State state;
		public int priority;

		public enum State
		{
			noSourceItems,
			destinationNotAccepting,
			noFreeSpaceAtDestination,
			noFreeCart,
			flagJammed,
			inProgress,
			unknown
		}

		public enum Type
		{
			manual,
			automatic
		}

		[Obsolete( "Compatibility with old files", true )]
		float averateTransferRate { set { averageTransferRate = value; } }
		[Obsolete( "Compatibility with old files", true )]
		Type type { set {} }

		public int itemsDelivered;

		public void Remove()
		{
			var itemData = start.itemData[(int)itemType];
			Assert.global.IsTrue( itemData.outputRoutes.Contains( this ) );
			itemData.outputRoutes.Remove( this );
		}

		public bool IsAvailable()
		{
			if ( state == State.inProgress )
				return false;
			
			if ( !start.construction.done || !end.construction.done )
				return false;

			int itemIndex = (int)itemType;
			if ( start.itemData[itemIndex].content < Constants.Stock.cartCapacity )
			{
				state = State.noSourceItems;
				return false;
			}
			if ( end.itemData[itemIndex].content + end.itemData[itemIndex].onWay + Constants.Stock.cartCapacity > end.itemData[itemIndex].inputMax )
			{
				state = State.destinationNotAccepting;
				return false;
			}
			if ( !start.cart.IsIdle( true ) )
			{
				state = State.noFreeCart;
				return false;
			}
			if ( start.flag.user )
			{
				state = State.flagJammed;
				return false;
			}
			if ( end.total + Constants.Stock.cartCapacity > end.maxItems )
			{
				state = State.noFreeSpaceAtDestination;
				return false;
			}
			state = State.unknown;
			return true;
		}

		public ItemTypeData endData
		{
			get
			{
				return end.itemData[(int)itemType];
			}
		}

		public ItemTypeData startData
		{
			get
			{
				return start.itemData[(int)itemType];
			}
		}
	}

	public class Cart : Worker
	{
		public int itemQuantity;
		public Item.Type itemType;
		public Route currentRoute;
		public Stock destination;
		public bool back;
		public const int frameCount = 8;
		public Stock boss { get { return building as Stock; } }
		readonly GameObject[] frames = new GameObject[8];
		new public static Cart Create()
		{
			return new GameObject().AddComponent<Cart>();
		}

		public void DeliverItems( Stock destination )
		{
			this.destination = destination;

			destination.itemData[(int)itemType].onWay += itemQuantity;
			ScheduleWalkToFlag( destination.flag, true );
			ScheduleWalkToNeighbour( destination.node );

			var task = ScriptableObject.CreateInstance<DeliverStackTask>();
			task.Setup( this, destination );
			ScheduleTask( task );
		}

		public void TransferItems( Route route )
		{
			assert.AreEqual( route.start, boss );
			int typeIndex = (int)route.itemType;
			boss.itemData[typeIndex].content -= Constants.Stock.cartCapacity;
			itemQuantity = Constants.Stock.cartCapacity;
			this.itemType = route.itemType;
			currentRoute = route;
			route.state = Route.State.inProgress;

			ScheduleWalkToNeighbour( boss.flag.node );
			DeliverItems( route.end );
			ScheduleWalkToNeighbour( route.end.flag.node );
			ScheduleWalkToFlag( boss.flag, true );
			ScheduleWalkToNeighbour( node );

			SetActive( true );
			UpdateLook();
		}

		public override void Reset()
		{
			base.Reset();
			itemQuantity = 0;
			currentRoute.state = Route.State.unknown;
			currentRoute = null;
		}

		new public void Start()
		{
			base.Start();

			for ( int i = 0; i < frameCount; i++ )
			{
				frames[i] = World.FindChildRecursive( body.transform, $"frame{i}" )?.gameObject;
				assert.IsNotNull( frames[i] );
			}

			UpdateLook();
		}

		public override void CriticalUpdate()
		{
			if ( itemQuantity > 0 && destination == null )
			{
				currentRoute.state = Route.State.unknown;
				currentRoute = null;
				destination = null; // Real null, not the unity style fake one
				ResetTasks();
				DeliverItems( boss );
			}
			base.CriticalUpdate();
		}

		override public void FindTask()
		{
			if ( boss == null )
			{
				type = Type.unemployed;
				return;
			}
			if ( node != boss.node )
			{
				if ( ScheduleGetToFlag() )
					DeliverItems( destination ?? boss );
				else
					ScheduleWalkToNode( boss.flag.node );	// Giving up the delivery
				return;
			}
			assert.IsTrue( itemQuantity == 0 );
			destination = null;	// Theoretically not needed
			SetActive( false );
		}

		public void UpdateLook()
		{
			if ( itemQuantity > 0 )
			{
				for ( int i = 0; i < frameCount; i++ )
				{
					var itemBody = Instantiate( Item.looks.GetMediaData( itemType ) );
					itemBody.transform.rotation *= Quaternion.Euler( -Constants.Item.yawAtFlag[(int)itemType], 0, 0 );
					itemBody.transform.SetParent( frames[i].transform, false );
				}
			}
			else
			{
				foreach ( var f in frames )
					if ( f.transform.childCount > 0 )
						Destroy( f.transform.GetChild( 0 ).gameObject );
			}
		}

		public override void Validate( bool chain )
		{
			base.Validate( chain );
			assert.IsTrue( type == Worker.Type.cart || type == Worker.Type.unemployed );
			if ( building )		// Can be null, if the user removed the stock
				assert.IsTrue( building is Stock );
			if ( road && exclusiveMode )
			{
				int index = IndexOnRoad();
				assert.IsTrue( index >= 0 );
				assert.AreEqual( road.workerAtNodes[index], this );
			}
			if ( currentRoute != null && currentRoute.start )
			{
				assert.AreEqual( destination, currentRoute.end );
				assert.AreEqual( boss, currentRoute.start );	// TODO Triggered, currentRoute.start was null, currentRoute.end is also null, but destination is null as well. It seems currentRounte is some illegal object
				assert.AreEqual( itemType, currentRoute.itemType );
			}
		}
	}

	public class DeliverStackTask : Worker.Task
	{
		public Stock stock;

		public void Setup( Worker boss, Stock stock )
		{
			base.Setup( boss );
			this.stock = stock;
		}

		public override void Cancel()
		{
			Cart cart = boss as Cart;
			if ( stock )
				stock.itemData[(int)cart.itemType].onWay -= cart.itemQuantity;
			base.Cancel();
		}

		public override bool ExecuteFrame()
		{
			if ( stock == null )
				return ResetBossTasks();

			Cart cart = boss as Cart;
			boss.assert.IsNotNull( cart );
			Stock cartStock = cart.building as Stock;
			if ( cart.itemQuantity > 0 )
			{
				if ( cart.currentRoute != null )
				{
					if ( cart.currentRoute.lastDelivery > 0 )
					{
						float rate = ((float)cart.itemQuantity) / ( World.instance.time - cart.currentRoute.lastDelivery );
						if ( cart.currentRoute.averageTransferRate == 0 )
							cart.currentRoute.averageTransferRate = rate;
						else
							cart.currentRoute.averageTransferRate = cart.currentRoute.averageTransferRate * 0.5f + rate * 0.5f;
					}
					cart.currentRoute.itemsDelivered += cart.itemQuantity;
					cart.currentRoute.lastDelivery = World.instance.time;
					cart.currentRoute.state = Route.State.unknown;
					cart.currentRoute = null;
				}
				cart.itemsDelivered += cart.itemQuantity;
				stock.itemData[(int)cart.itemType].content += cart.itemQuantity;
				if ( stock != cartStock )
					stock.itemData[(int)cart.itemType].onWay -= cart.itemQuantity;
				cart.itemQuantity = 0;
				cart.UpdateLook();
			}
			boss.assert.AreEqual( cart.destination, stock );
			cart.destination = null;
			return true;
		}
	}

	public static new void Initialize()
	{
		mainTemplate = Resources.Load<GameObject>( "prefabs/buildings/main" );
		template = Resources.Load<GameObject>( "prefabs/buildings/stock" );
	}

	public static Stock Create()
	{
		return new GameObject().AddComponent<Stock>();
	}

	public static SiteTestResult IsNodeSuitable( Node placeToBuild, Player owner, int flagDirection )
	{
		return Building.IsNodeSuitable( placeToBuild, owner, stockConfiguration, flagDirection );
	}

	public Stock Setup( Node node, Player owner, int flagDirection, bool blueprintOnly = false )
	{
		height = 1.5f;
		maxItems = Constants.Stock.defaultmaxItems;

		if ( base.Setup( node, owner, main ? mainConfiguration : stockConfiguration, flagDirection, blueprintOnly ) == null )
			return null;

		owner.RegisterStock( this );
		while ( itemData.Count < (int)Item.Type.total )
			itemData.Add( new ItemTypeData( this, (Item.Type)itemData.Count ) );

		return this;
	}

	public Stock SetupMain( Node node, Player owner, int flagDirection )
	{
		main = true;

		node.owner = owner;
		if ( !Setup( node, owner, flagDirection ) )
			return null;

		maxItems = Constants.Stock.defaultmaxItemsForMain;
		height = 3;
		if ( configuration.flatteningNeeded )
		{
			foreach ( var o in Ground.areas[1] )
				node.Add( o ).SetHeight( node.height );
		}
		construction.done = true;
		itemData[(int)Item.Type.plank].content = Constants.Stock.startPlankCount;
		itemData[(int)Item.Type.stone].content = Constants.Stock.startStoneCount;
		itemData[(int)Item.Type.soldier].content = Constants.Stock.startSoldierCount;
		dispenser = worker = Worker.Create().SetupForBuilding( this );
		owner.RegisterInfluence( this );
		flag.ConvertToCrossing( false );
		return this;
	}

	public List<Route> GetInputRoutes( Item.Type itemType )
	{
		int typeIndex = (int)itemType;
		List<Route> list = new List<Route>();
		foreach ( var stock in owner.stocks )
		{
			if ( stock == this )
				continue;
			foreach ( var r in stock.itemData[typeIndex].outputRoutes )
			{
				if ( r.end == this )
					list.Add( r );
			}
		}
		return list;
	}

	public override bool Remove( bool takeYourTime )
	{
		if ( main )
			return false;
		owner.UnregisterStock( this );
		return base.Remove( takeYourTime );
	}

	new void Start()
	{
		base.Start();
		if ( main )
		{
			name = "Headquarters";
			soundSource.clip = Resources.Load<AudioClip>( "effects/gong" );
			soundSource.loop = false;
		}
		else
			name = $"Stock {node.x}, {node.y}";
	}

	override public GameObject Template()
	{
		return main ? mainTemplate : template;
	}

	public void Callback( Worker worker )
	{
		if ( worker.type == Worker.Type.soldier )
		{
			itemData[(int)Item.Type.soldier].content++;
			soundSource.Play();
		}
		assert.IsTrue( returningUnits.Contains( worker ) );
		returningUnits.Remove( worker );

		foreach ( var item in worker.itemsInHands )
		{
			if ( item == null )
				continue;

			// TODO Sometimes this item keeps alive with all the destinations? Suspicious
			item.CancelTrip();
			item.SetRawTarget( this );
			item.Arrived();
			item.transform.SetParent( node.ground.transform );
		}
		worker.itemsInHands[0] = worker.itemsInHands[1] = null;

		worker.DestroyThis();
}

	public override void CriticalUpdate()
	{
		base.CriticalUpdate();
		if ( !construction.done || blueprintOnly )
			return;

		if ( !reachable )
			return;

		if ( worker == null && construction.done && !blueprintOnly )
			dispenser = worker = Worker.Create().SetupForBuilding( this );
		if ( workerMate == null )
		{
			workerMate = Worker.Create().SetupForBuilding( this, true );
			workerMate.ScheduleWait( 100, true );
		}
		if ( cart == null )
			cart = Cart.Create().SetupAsCart( this ) as Cart;

		total = totalTarget = 0;
		for ( int itemType = 0; itemType < (int)Item.Type.total; itemType++ )
		{
			if ( itemType == (int)Item.Type.soldier )
				continue;
			int count = itemData[itemType].content + itemData[itemType].onWay;
			total += count;
			totalTarget += Math.Max( count, itemData[itemType].inputMin );
		}

		for ( int itemType = 0; itemType < (int)Item.Type.total; itemType++ )
		{
			for ( int i = 0; i < itemData[itemType].outputRoutes.Count; i++ )
			{
				var destination = itemData[itemType].outputRoutes[i].end;
				if ( destination == null )	// unity like null
				{
					itemData[itemType].outputRoutes.RemoveAt( i );
					i--;
					continue;
				}

				if ( itemData[itemType].outputRoutes[i].IsAvailable() )
					cart.TransferItems( itemData[itemType].outputRoutes[i] );
			}

			int current = itemData[itemType].content + itemData[itemType].onWay;
			if ( maxItems > total )
			{
				var p = ItemDispatcher.Priority.stock;
				if ( current < itemData[itemType].inputMin )
					p = ItemDispatcher.Priority.high;
				if ( current > itemData[itemType].inputMax )
					p = ItemDispatcher.Priority.zero;
				owner.itemDispatcher.RegisterRequest( this, (Item.Type)itemType, Math.Min( maxItems - total, itemData[itemType].inputMax - current ), p, inputArea ); // TODO Should not order more than what fits
			}
			if ( itemData.Count > itemType )
			{
				var p = ItemDispatcher.Priority.stock;
				if ( current < itemData[itemType].outputMin )
					p = ItemDispatcher.Priority.zero;
				if ( current > itemData[itemType].outputMax )
					p = ItemDispatcher.Priority.high;
				owner.itemDispatcher.RegisterOffer( this, (Item.Type)itemType, itemData[itemType].content, p, outputArea, 0.5f, flag.freeSlots == 0, !dispenser.IsIdle() || offersSuspended.inProgress );
			}
		}
	}

	public override int Influence( Node node )
	{
		if ( !main )
			base.Influence( node );

		return Constants.Stock.influenceRange - node.DistanceFrom( this.node );
	}

	public override void OnClicked( bool show = false )
	{
		base.OnClicked( show );
		if ( construction.done )
			Interface.StockPanel.Create().Open( this, show );
	}

	public override Item SendItem( Item.Type itemType, Building destination, ItemDispatcher.Priority priority )
	{
		assert.IsTrue( itemData[(int)itemType].content > 0 );	// TODO Triggered?
		Item item = base.SendItem( itemType, destination, priority );
		if ( item != null )
		{
			itemData[(int)itemType].content--;
			dispenser = worker.IsIdle() ? worker : workerMate;
			offersSuspended.Start( Constants.World.normalSpeedPerSecond );	// Cosmetic reasons only
		}

		return item;
	}

	public override void ItemOnTheWay( Item item, bool cancel = false )
	{
		if ( cancel )
			itemData[(int)item.type].onWay--;
		else
			itemData[(int)item.type].onWay++;
		base.ItemOnTheWay( item, cancel );
	}

	public override void ItemArrived( Item item )
	{
		base.ItemArrived( item );

		assert.IsTrue( itemData[(int)item.type].onWay > 0 );
		itemData[(int)item.type].onWay--;
		if ( !construction.done )
			return;

		while ( itemData.Count <= (int)item.type )
			itemData.Add( new ItemTypeData( this, (Item.Type)itemData.Count ) );
		itemData[(int)item.type].content++;
	}

	public void ClearSettings()
	{
		for ( int i = 0; i < (int)Item.Type.total; i++ )
		{
			itemData[i].inputMin = itemData[i].inputMax = itemData[i].outputMin = 0;
			itemData[i].outputMax = maxItems / 4;
		}

		inputArea.center = outputArea.center = node;
		inputArea.radius = outputArea.radius = 4;
	}

	public override void Reset()
	{
		base.Reset();
		for ( int i = 0; i < (int)Item.Type.total; i++ )
			itemData[i].content = 0;
	}

	public override void Validate( bool chain )
	{
		base.Validate( chain );
		if ( chain )
			cart?.Validate( true );
		if ( cart )
			assert.AreEqual( cart.building, this );		// TODO Fired
		int[] onWayCounted = new int[(int)Item.Type.total];
		foreach ( var item in itemsOnTheWay )
			onWayCounted[(int)item.type]++;
		for ( int i = 0; i < onWayCounted.Length; i++ )
			assert.AreEqual( ( itemData[i].onWay - onWayCounted[i] ) % Constants.Stock.cartCapacity, 0 );
		for ( int j = 0; j < itemData.Count; j++ )
		{
			assert.AreEqual( this, itemData[j].boss );
			assert.AreEqual( j, (int)itemData[j].itemType );
			if ( itemData[j].cartOutput >= Constants.Stock.cartCapacity && owner.stocksHaveNeed[j] && itemData[j].cartInput == 0 )
				assert.AreNotEqual( itemData[j].outputRoutes.Count, 0 );
		}
	}
}
