using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;

public class Stock : Building, Worker.Callback.IHandler
{
	public bool main = false;
	public List<int> content = new List<int>();
	public List<int> onWay = new List<int>();
	public List<int> inputMin = new List<int>();
	public List<int> inputMax = new List<int>();
	public List<int> outputMin = new List<int>();
	public List<int> outputMax = new List<int>();
	public List<List<Route>> outputRoutes = new List<List<Route>>();
	public Versioned outputRouteVersion = new Versioned();
	public List<Worker> returningUnits = new List<Worker>();	// This list is maintained for returning units to store them during save, because they usually have no building
	public Cart cart;
	public Ground.Area inputArea = new Ground.Area();
	public Ground.Area outputArea = new Ground.Area();
	public int total;
	public int totalTarget;
	public int maxItems = Constants.Stock.defaultmaxItems;
	public World.Timer offersSuspended;     // When this timer is in progress, the stock is not offering items. This is done only for cosmetic reasons, it won't slow the rate at which the stock is providing items.

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

	public void AddNewRoute( Item.Type itemType, Stock destination )
	{
		foreach ( var route in outputRoutes[(int)itemType] )
			if ( route.end == destination && route.itemType == itemType )
				return;

		var newRoute = new Route();
		newRoute.start = this;
		newRoute.end = destination;
		newRoute.itemType = itemType;
		outputRoutes[(int)itemType].Add( newRoute );
		outputRouteVersion.Trigger();
		destination.inputMax[(int)itemType] = Math.Max( (int)( Constants.Stock.cartCapacity * 1.5f ), destination.inputMax[(int)itemType] );
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

		[Obsolete( "Compatibility with old files", true )]
		float averateTransferRate { set { averageTransferRate = value; } }

		public int itemsDelivered;

		public void Remove()
		{
			int itemTypeIndex = (int)itemType;
			Assert.global.IsTrue( start.outputRoutes[itemTypeIndex].Contains( this ) );
			start.outputRoutes[itemTypeIndex].Remove( this );
		}

		public void MoveUp()
		{
			var list = start.outputRoutes[(int)itemType];
			int i = list.IndexOf( this );
			if ( i < 1 )
				return;
			list[i] = list[i-1];
			list[i-1] = this;
			start.outputRouteVersion.Trigger();
		}

		public void MoveDown()
		{
			var list = start.outputRoutes[(int)itemType];
			int i = list.IndexOf( this );
			if ( i < 0 || i == list.Count-1 )
				return;
			list[i] = list[i+1];
			list[i+1] = this;
			start.outputRouteVersion.Trigger();
		}

		public bool IsAvailable()
		{
			if ( state == State.inProgress )
				return false;

			int itemIndex = (int)itemType;
			if ( start.content[itemIndex] < Constants.Stock.cartCapacity )
			{
				state = State.noSourceItems;
				return false;
			}
			if ( end.content[itemIndex] + end.onWay[itemIndex] + Constants.Stock.cartCapacity > end.inputMax[itemIndex] )
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

			destination.onWay[(int)itemType] += itemQuantity;
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
			boss.content[typeIndex] -= Constants.Stock.cartCapacity;
			itemQuantity = Constants.Stock.cartCapacity;
			this.itemType = route.itemType;
			currentRoute = route;
			route.state = Route.State.inProgress;

			ScheduleWalkToNeighbour( boss.flag.node );
			DeliverItems( route.end );
			ScheduleWalkToNeighbour( route.end.flag.node );
			ScheduleWalkToFlag( boss.flag, true );
			ScheduleWalkToNeighbour( node );

			if ( !boss.flag.crossing )
			{
				boss.flag.user = this;
				exclusiveFlag = boss.flag;
			}
			onRoad = true;
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

		public new void FixedUpdate()
		{
			if ( itemQuantity > 0 && destination == null )
			{
				currentRoute.state = Route.State.unknown;
				currentRoute = null;
				destination = null; // Real null, not the unity style fake one
				ResetTasks();
				DeliverItems( boss );
			}
			base.FixedUpdate();
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
				DeliverItems( destination ?? boss );
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
			if ( road && onRoad )
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
				stock.onWay[(int)cart.itemType] -= cart.itemQuantity;
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
						cart.currentRoute.averageTransferRate = cart.currentRoute.averageTransferRate * 0.5f + rate * 0.5f;
					}
					cart.currentRoute.itemsDelivered += cart.itemQuantity;
					cart.currentRoute.lastDelivery = World.instance.time;
					cart.currentRoute.state = Route.State.unknown;
					cart.currentRoute = null;
				}
				cart.itemsDelivered += cart.itemQuantity;
				stock.content[(int)cart.itemType] += cart.itemQuantity;
				if ( stock != cartStock )
					stock.onWay[(int)cart.itemType] -= cart.itemQuantity;
				cart.itemQuantity = 0;
				cart.UpdateLook();
			}
			if ( cartStock != stock )
			{
				// Not at home yet, so the following tasks supposed to get the cart back
				boss.assert.IsTrue( boss.taskQueue.Count > 1 );
				if ( !stock.flag.crossing )
				{
					if ( stock.flag.user )
						return false;

					stock.flag.user = boss;
					boss.exclusiveFlag = stock.flag;
				}
				boss.onRoad = true;

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

		CreateMissingArrays();
		if ( base.Setup( node, owner, main ? mainConfiguration : stockConfiguration, flagDirection, blueprintOnly ) == null )
			return null;

		owner.RegisterStock( this );

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
		content[(int)Item.Type.plank] = Constants.Stock.startPlankCount;
		content[(int)Item.Type.stone] = Constants.Stock.startStoneCount;
		content[(int)Item.Type.soldier] = Constants.Stock.startSoldierCount;
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
			foreach ( var r in stock.outputRoutes[typeIndex] )
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
			content[(int)Item.Type.soldier]++;
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

	new void FixedUpdate()
	{
		base.FixedUpdate();
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
			int count = content[itemType] + onWay[itemType];
			total += count;
			totalTarget += Math.Max( count, inputMin[itemType] );
		}

		for ( int itemType = 0; itemType < (int)Item.Type.total; itemType++ )
		{
			for ( int i = 0; i < outputRoutes[itemType].Count; i++ )
			{
				var destination = outputRoutes[itemType][i].end;
				if ( destination == null )	// unity like null
				{
					outputRoutes[itemType].RemoveAt( i );
					i--;
					continue;
				}

				if ( outputRoutes[itemType][i].IsAvailable() )
					cart.TransferItems( outputRoutes[itemType][i] );
			}

			int current = content[itemType] + onWay[itemType];
			if ( maxItems > total )
			{
				var p = ItemDispatcher.Priority.stock;
				if ( current < inputMin[itemType] )
					p = ItemDispatcher.Priority.high;
				if ( current > inputMax[itemType] )
					p = ItemDispatcher.Priority.zero;
				owner.itemDispatcher.RegisterRequest( this, (Item.Type)itemType, Math.Min( maxItems - total, inputMax[itemType] - current ), p, inputArea ); // TODO Should not order more than what fits
			}
			if ( content.Count > itemType )
			{
				var p = ItemDispatcher.Priority.stock;
				if ( current < outputMin[itemType] )
					p = ItemDispatcher.Priority.zero;
				if ( current > outputMax[itemType] )
					p = ItemDispatcher.Priority.high;
				owner.itemDispatcher.RegisterOffer( this, (Item.Type)itemType, content[itemType], p, outputArea, 0.5f, flag.freeSlots == 0, !dispenser.IsIdle() || offersSuspended.inProgress );
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
		assert.IsTrue( content[(int)itemType] > 0 );	// TODO Triggered?
		Item item = base.SendItem( itemType, destination, priority );
		if ( item != null )
		{
			content[(int)itemType]--;
			dispenser = worker.IsIdle() ? worker : workerMate;
			offersSuspended.Start( 50 );	// Cosmetic reasons only
		}

		return item;
	}

	public override void ItemOnTheWay( Item item, bool cancel = false )
	{
		if ( cancel )
			onWay[(int)item.type]--;
		else
			onWay[(int)item.type]++;
		base.ItemOnTheWay( item, cancel );
	}

	public override void ItemArrived( Item item )
	{
		base.ItemArrived( item );

		assert.IsTrue( onWay[(int)item.type] > 0 );
		onWay[(int)item.type]--;
		if ( !construction.done )
			return;

		while ( content.Count <= (int)item.type )
			content.Add( 0 );
		content[(int)item.type]++;
	}

	public void ClearSettings()
	{
		for ( int i = 0; i < (int)Item.Type.total; i++ )
		{
			inputMin[i] = inputMax[i] = outputMin[i] = 0;
			outputMax[i] = maxItems / 4;
		}

		inputArea.center = outputArea.center = node;
		inputArea.radius = outputArea.radius = 4;
	}

	public override void Reset()
	{
		base.Reset();
		for ( int i = 0; i < (int)Item.Type.total; i++ )
			content[i] = 0;
	}

	public void CreateMissingArrays()
	{
		while ( content.Count < (int)Item.Type.total )
			content.Add( 0 );
		while ( inputMin.Count < (int)Item.Type.total )
			inputMin.Add( 0 );
		while ( inputMax.Count < (int)Item.Type.total )
			inputMax.Add( 0 );
		while ( outputMin.Count < (int)Item.Type.total )
			outputMin.Add( 0 );
		while ( outputMax.Count < (int)Item.Type.total )
			outputMax.Add(  maxItems / 4 );
		while ( onWay.Count < (int)Item.Type.total )
			onWay.Add( 0 );
		while ( outputRoutes.Count < (int)Item.Type.total )
			outputRoutes.Add( new List<Route>() );
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
			assert.AreEqual( ( onWay[i] - onWayCounted[i] ) % Constants.Stock.cartCapacity, 0 );
	}
}
