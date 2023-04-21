using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;

public class Stock : Attackable
{
	public bool main = false;
	public List<ItemTypeData> itemData = new ();
	public List<Unit> returningUnits = new ();	// This list is maintained for returning units to store them during save, because they usually have no building
	public Cart cart;
	public Ground.Area inputArea = new ();
	public Ground.Area outputArea = new ();
	public int total;
	public int totalTarget;
	public int maxItems = Constants.Stock.defaultmaxItems;
	public Game.Timer offersSuspended = new ();     // When this timer is in progress, the stock is not offering items. This is done only for cosmetic reasons, it won't slow the rate at which the stock is providing items.
	public Game.Timer resupplyTimer = new ();
	public bool fullReported, fullReportedCart;

	override public string title { get { return main ? "Headquarters" : "Stock"; } set {} }
	override public bool wantFoeClicks { get { return main; } }
	override public UpdateStage updateMode => UpdateStage.turtle;
	
	override public int checksum
	{
		get
		{
			int checksum = base.checksum;
			if ( cart )
				checksum += cart.checksum;
			foreach ( var data in itemData )
				checksum += data.content;
			return checksum;
		}
	}

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

	public override int defenderCount { get { return main ? team.soldierCount : 0; } }
	public override Unit GetDefender()
	{
		assert.IsTrue( team.soldierCount > 0 );
		assert.IsTrue( main );
		team.soldierCount--;
		return Unit.Create().SetupAsSoldier( this );
	}

	public override List<Ground.Area> areas
	{
		get
		{
			var areas = new List<Ground.Area>();
			areas.Add( inputArea );
			areas.Add( outputArea );
			return areas;
		}
	}

	[Obsolete( "Compatibility with old files", true )]
	public Stock[] destinations 
	{ 
		set 
		{
			for ( int i = 0; i < value.Length; i++ )
			{
				if ( outputRoutes[i] == null )
					outputRoutes[i] = new ();
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
		public int cartOutput = Constants.Stock.defaultCartOutput, cartOutputTemporary = Constants.Stock.defaultCartOutput, cartInput = Constants.Stock.defaultCartInput;
		public int cartPledged;
		public List<Route> outputRoutes = new ();
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
				if ( route.start == null || route.start.itemData[typeIndex].cartOutput < Constants.Stock.cartCapacity || route.end == null || route.end.itemData[typeIndex].cartInput == 0 )
					route.Remove();
				else
					i++;
			}

			foreach ( var stock in boss.team.stocks )
			{
				if ( stock == boss )
					continue;
				if ( cartOutput > 0 && stock.itemData[typeIndex].cartInput > 0 )
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

		public ref int ChannelValue( Channel channel )
		{
			switch ( channel )
			{
				case Channel.inputMin: return ref inputMin;
				case Channel.inputMax: return ref inputMax;
				case Channel.outputMin: return ref outputMin;
				case Channel.outputMax: return ref outputMax;
				case Channel.cartInput: return ref cartInput;
				case Channel.cartOutput: return ref cartOutput;
				case Channel.cartOutputTemporary: return ref cartOutputTemporary;
			}
			throw new Exception();
		}

		public int ChangeChannelValue( Channel channel, int newValue )
		{
			var old = ChannelValue( channel );
			ChannelValue( channel ) = newValue;
			if ( channel == Channel.inputMax && old != 0 && newValue == 0 )
				boss.CancelOrders( itemType );
			boss.team.UpdateStockRoutes();
			return old;
		}
	}

	public enum Channel
	{
		inputMin,
		inputMax,
		outputMin,
		outputMax,
		cartInput,
		cartOutput,
		cartOutputTemporary,
	}

	[Obsolete( "Compatibility for old files", true )]
	List<int> target = new ();

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
			stockUnderConstruction,
			unknown
		}

		public enum Type
		{
			manual,
			automatic
		}

		public int pledged => end.itemData[(int)itemType].cartPledged;

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
			{
				state = State.stockUnderConstruction;
				return false;
			}

			int itemIndex = (int)itemType;
			int minimalQuantity = start.itemData[itemIndex].cartOutputTemporary > 0 ? start.itemData[itemIndex].cartOutputTemporary : start.itemData[itemIndex].cartOutput;
			int expectedDelivery = Math.Min( minimalQuantity, Constants.Stock.cartCapacity );
			int atDest = end.itemData[itemIndex].content + end.itemData[itemIndex].onWay;

			if ( atDest + expectedDelivery > end.itemData[itemIndex].inputMax || atDest >= end.itemData[itemIndex].cartInput )
			{
				state = State.destinationNotAccepting;
				return false;
			}
			if ( start.itemData[itemIndex].content < minimalQuantity )
			{
				state = State.noSourceItems;
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

	public class Cart : Unit
	{
		public int itemQuantity;
		public Item.Type itemType;
		public Route currentRoute;
		public Stock destination;
		public bool back;
		public const int frameCount = 8;
		public Stock boss { get { return building as Stock; } }
		readonly GameObject[] frames = new GameObject[frameCount];
		public SpriteRenderer onMap;
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

			var task = new DeliverStackTask();
			task.Setup( this, destination );
			ScheduleTask( task );
		}

		public void TransferItems( Route route )
		{
			assert.AreEqual( route.start, boss );
			int typeIndex = (int)route.itemType;
			itemQuantity = Math.Min( Constants.Stock.cartCapacity, boss.itemData[typeIndex].content );
			boss.itemData[typeIndex].content -= itemQuantity;
			boss.itemData[typeIndex].cartOutputTemporary = 0;
			route.end.itemData[typeIndex].cartPledged += itemQuantity;
			boss.contentChange.Trigger();
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

			onMap = new GameObject( "Cart content on map" ).AddComponent<SpriteRenderer>();
			onMap.transform.SetParent( transform, false );
			onMap.transform.localPosition = Vector3.up * 6;
			onMap.transform.localRotation = Quaternion.Euler( 90, 0, 0 );
			onMap.transform.localScale = Vector3.one * 0.3f;
			onMap.material.renderQueue = 4003;
			onMap.gameObject.layer = World.layerIndexMapOnly;

			UpdateLook();
		}

		public override void GameLogicUpdate( UpdateStage stage )
		{
			if ( itemQuantity > 0 && destination == null )
			{
				currentRoute.state = Route.State.unknown;
				currentRoute = null;
				destination = null; // Real null, not the unity style fake one
				ResetTasks();
				DeliverItems( boss );
			}
			base.GameLogicUpdate( stage );
		}

		override public void FindTask()
		{
			if ( boss == null )
			{
				type = Type.unemployed;
				RegisterAsReturning();
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
		}

		public void UpdateLook()
		{
			if ( frames[0] == null )	// This is true if start was not yet called (rare case)
				return;

			if ( itemQuantity > 0 )
			{
				for ( int i = 0; i < Math.Min( frameCount, itemQuantity ); i++ )
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
						Eradicate( f.transform.GetChild( 0 ).gameObject );
			}
			if ( taskQueue.Count == 0 && walkTo == null )
				SetActive( false );
			if ( itemQuantity > 0 )
				onMap.sprite = Item.sprites[(int)itemType];
			else
				onMap.sprite = null;
		}

		public override Node LeaveExclusivity()
		{
			var result = base.LeaveExclusivity();
			if ( result )
				road = null;
			return result;
		}

		public override void Validate( bool chain )
		{
			base.Validate( chain );
			assert.IsTrue( type == Unit.Type.cart || type == Unit.Type.unemployed );
			if ( building )		// Can be null, if the user removed the stock
				assert.IsTrue( building is Stock );
			if ( road && exclusiveMode )
			{
				int index = IndexOnRoad();
				assert.IsTrue( index >= 0 );
				assert.AreEqual( road.haulerAtNodes[index], this );
			}
			if ( currentRoute != null && currentRoute.start )
			{
				assert.AreEqual( destination, currentRoute.end );
				assert.AreEqual( boss, currentRoute.start );	// TODO Triggered, currentRoute.start was null, currentRoute.end is also null, but destination is null as well. It seems currentRounte is some illegal object
				assert.AreEqual( itemType, currentRoute.itemType );
			}
		}

		new void Update()
		{
			onMap.transform.rotation = Quaternion.Euler( 90, (float)( eye.direction / Math.PI * 180 ), 0 );
			base.Update();
		}
	}

	public class DeliverStackTask : Unit.Task
	{
		public Stock stock;

		public void Setup( Unit boss, Stock stock )
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
						float rate = ((float)cart.itemQuantity) / ( time - cart.currentRoute.lastDelivery );
						if ( cart.currentRoute.averageTransferRate == 0 )
							cart.currentRoute.averageTransferRate = rate;
						else
							cart.currentRoute.averageTransferRate = cart.currentRoute.averageTransferRate * 0.5f + rate * 0.5f;
					}
					cart.currentRoute.itemsDelivered += cart.itemQuantity;
					cart.currentRoute.lastDelivery = time;
					cart.currentRoute.state = Route.State.unknown;
					cart.currentRoute = null;
				}
				cart.itemsDelivered += cart.itemQuantity;
				stock.itemData[(int)cart.itemType].content += cart.itemQuantity;
				stock.contentChange.Trigger();
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

	public static SiteTestResult IsNodeSuitable( Node placeToBuild, Team owner, int flagDirection, bool ignoreTreesAndRocks = true )
	{
		return Building.IsNodeSuitable( placeToBuild, owner, stockConfiguration, flagDirection, ignoreTreesAndRocks );
	}

	public Stock Setup( Node node, Team owner, int flagDirection, bool blueprintOnly = false, Resource.BlockHandling block = Resource.BlockHandling.block )
	{
		height = 1.5f;
		maxItems = Constants.Stock.defaultmaxItems;

		if ( base.Setup( node, owner, main ? mainConfiguration : stockConfiguration, flagDirection, blueprintOnly, block ) == null )
			return null;

		if ( !blueprintOnly )
			owner.RegisterStock( this );
		while ( itemData.Count < (int)Item.Type.total )
			itemData.Add( new ItemTypeData( this, (Item.Type)itemData.Count ) );

		return this;
	}

	override public void Materialize()
	{
		team.RegisterStock( this );
		base.Materialize();
	}

	public Stock SetupAsMain( Node node, Team team, int flagDirection )
	{
		main = true;

		node.team = team;
		if ( !Setup( node, team, flagDirection ) )
			return null;

		maxItems = Constants.Stock.defaultmaxItemsForMain;
		height = 3;
		if ( configuration.flatteningNeeded )
		{
			foreach ( var o in Ground.areas[1] )
			{
				if ( !o )
					continue;
				node.Add( o ).SetHeight( node.height );
			}
		}
		construction.done = true;
		itemData[(int)Item.Type.plank].content = Constants.Stock.startPlankCount;
		itemData[(int)Item.Type.plank].inputMax = 50;
		itemData[(int)Item.Type.stone].content = Constants.Stock.startStoneCount;
		itemData[(int)Item.Type.stone].inputMax = 50;
		itemData[(int)Item.Type.soldier].content = Constants.Stock.startSoldierCount;
		dispenser = tinkerer = Unit.Create().SetupForBuilding( this );
		team.RegisterInfluence( this );
		flag.ConvertToCrossing();
		return this;
	}

	public List<Route> GetInputRoutes( Item.Type itemType )
	{
		int typeIndex = (int)itemType;
		List<Route> list = new ();
		foreach ( var stock in team.stocks )
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

	public override void Remove()
	{
		destroyed = true;
		team.UnregisterStock( this );
		team.UpdateStockRoutes();
		if ( main )
			team.Defeat();
		base.Remove();
		if ( cart )
			cart.Remove();
		RemoveElements( returningUnits );
	}

	new void Start()
	{
		base.Start();

		if ( destroyed )
			return;
			
		if ( main )
		{
			name = "Headquarters";
			soundSource.clip = Resources.Load<AudioClip>( "soundEffects/gong" );
			soundSource.loop = false;
		}
		else
			name = $"Stock {node.x}, {node.y}";
	}

	override public GameObject Template()
	{
		return main ? mainTemplate : template;
	}

	public override void UnitCallback( Unit unit, float floatData, bool boolData )
	{
		if ( unit.type == Unit.Type.soldier )
		{
			if ( unit.team != team )
			{
				Remove();
				return;
			}

			itemData[(int)Item.Type.soldier].content++;
			contentChange.Trigger();
			soundSource.Play();
		}
		assert.IsTrue( returningUnits.Contains( unit ) );
		returningUnits.Remove( unit );

		foreach ( var item in unit.itemsInHands )
		{
			if ( item == null )
				continue;

			// TODO Sometimes this item keeps alive with all the destinations? Suspicious
			item.CancelTrip();
			item.SetRawTarget( this );
			item.Arrived();
			item.transform.SetParent( ground.transform );
		}
		unit.itemsInHands[0] = unit.itemsInHands[1] = null;

		unit.Remove();
	}

	public override void GameLogicUpdate( UpdateStage stage )
	{
		base.GameLogicUpdate( stage );

		if ( main && !resupplyTimer.inProgress )
		{
			resupplyTimer.Start( Constants.Stock.resupplyPeriod );
			if ( itemData[(int)Item.Type.plank].content < Constants.Stock.minimumPlank )
				itemData[(int)Item.Type.plank].content = Constants.Stock.minimumPlank;
		}

		if ( !construction.done || blueprintOnly )
			return;

		if ( !reachable )
			return;

		if ( tinkerer == null && construction.done && !blueprintOnly )
			dispenser = tinkerer = Unit.Create().SetupForBuilding( this );
		if ( tinkererMate == null )
		{
			tinkererMate = Unit.Create().SetupForBuilding( this, true );
			tinkererMate.ScheduleWait( 100, true );
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
		int CRC = total;

		for ( int itemType = 0; itemType < (int)Item.Type.total; itemType++ )
		{
			if ( itemType == (int)Item.Type.soldier )
				continue;

			Route best = null;
			List<Route> toRemove = new ();
			foreach ( var route in itemData[itemType].outputRoutes )
			{
				if ( route.end == null )	// unity like null
				{
					toRemove.Add( route );
					continue;
				}

				CRC += route.end.id;
				if ( route.IsAvailable() )
				{
					if ( best == null || best.pledged > route.pledged )
						best = route;
				}
				else if ( route.state == Route.State.noFreeSpaceAtDestination && !route.end.fullReportedCart )
				{
					route.end.fullReportedCart = true;
					team.SendMessage( $"Stock full, cart couldn't deliver {(Item.Type)itemType}", route.end );
				}
			}
			foreach ( var dead in toRemove )
				itemData[itemType].outputRoutes.Remove( dead );

			if ( best != null )
			{
				cart.TransferItems( best );
				best.end.fullReportedCart = false;
			}

			CRC += itemData[itemType].inputMin + itemData[itemType].inputMax + itemData[itemType].outputMin + itemData[itemType].outputMax + itemData[itemType].content;
			int current = itemData[itemType].content + itemData[itemType].onWay;
			if ( maxItems > total )
			{
				var p = ItemDispatcher.Priority.stock;
				if ( current < itemData[itemType].inputMin || current < itemData[itemType].cartOutput )
					p = ItemDispatcher.Priority.high;
				if ( current > itemData[itemType].inputMax )
					p = ItemDispatcher.Priority.zero;
				team.itemDispatcher.RegisterRequest( this, (Item.Type)itemType, Math.Min( maxItems - total, itemData[itemType].inputMax - current ), p, inputArea ); // TODO Should not order more than what fits
				if ( total < maxItems - Constants.Stock.fullTolerance )
					fullReported = false;
			}
			else if ( !fullReported )
			{
				fullReported = true;
				team.SendMessage( "Stock full", this );
			}
			if ( itemData.Count > itemType )
			{
				var p = ItemDispatcher.Priority.stock;
				if ( current < itemData[itemType].outputMin || current < itemData[itemType].cartOutput )
					p = ItemDispatcher.Priority.zero;
				if ( current > itemData[itemType].outputMax )
					p = ItemDispatcher.Priority.high;
				team.itemDispatcher.RegisterOffer( this, (Item.Type)itemType, itemData[itemType].content, p, outputArea, flag.freeSlots == 0, !dispenser.IsIdle() || offersSuspended.inProgress );
			}
		}
		World.CRC( CRC, OperationHandler.Event.CodeLocation.stockCriticalUpdate );
	}

	public override int Influence( Node node )
	{
		if ( !main )
			base.Influence( node );

		return Constants.Stock.influenceRange - node.DistanceFrom( this.node );
	}

	public override void OnClicked( Interface.MouseButton button, bool show = false )
	{
		base.OnClicked( button, show );
		if ( button != Interface.MouseButton.left )
			return;

		if ( construction.done )
		{
			if ( team == root.mainTeam )
				Interface.StockPanel.Create().Open( this, show );
			else
			{
				assert.IsTrue( main );
				Interface.GuardHousePanel.Create().Open( this, show );
			}
		}
	}

	public override Item SendItem( Item.Type itemType, Building destination, ItemDispatcher.Priority priority )
	{
		assert.IsTrue( itemData[(int)itemType].content > 0 );	// TODO Triggered?
		Item item = base.SendItem( itemType, destination, priority );
		if ( item != null )
		{
			itemData[(int)itemType].content--;
			contentChange.Trigger();
			dispenser = tinkerer.IsIdle() ? tinkerer : tinkererMate;
			offersSuspended.Start( Constants.Game.normalSpeedPerSecond );	// Cosmetic reasons only
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
		contentChange.Trigger();
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
		foreach ( var data in itemData )
		{
			int countedOnWayByCart = 0;
			foreach ( var stock in team.stocks )
			{
				if ( stock.cart == null )
					continue;
				if ( stock.cart.destination == this && stock.cart.itemType == data.itemType )
					countedOnWayByCart += stock.cart.itemQuantity;
			}			
			assert.AreEqual( data.onWay, countedOnWayByCart + onWayCounted[(int)data.itemType]);
		}
		for ( int j = 0; j < itemData.Count; j++ )
		{
			assert.AreEqual( this, itemData[j].boss );
			assert.AreEqual( j, (int)itemData[j].itemType );
			if ( itemData[j].cartOutput >= Constants.Stock.cartCapacity && team.stocksHaveNeed[j] && itemData[j].cartInput == 0 )
				assert.AreNotEqual( itemData[j].outputRoutes.Count, 0, $"Invalid route for {(Item.Type)j} in {this}" );
		}
		foreach ( var ret in returningUnits )
			assert.IsTrue( ret.type == Unit.Type.soldier || ret.type == Unit.Type.unemployed || ret.type == Unit.Type.cart );
	}
}
