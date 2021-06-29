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
	public List<List<Stock>> destinationLists = new List<List<Stock>>();
	public List<Worker> returningUnits = new List<Worker>();	// This list is maintained for returning units to store them during save, because they usually have no building
	[Obsolete( "Compatibility with old files", true )]
	public Stock[] destinations 
	{ 
		set 
		{
			for ( int i = 0; i < value.Length; i++ )
			{
				if ( destinationLists[i] == null )
					destinationLists[i] = new List<Stock>();
				if ( value[i] )
					destinationLists[i].Add( value[i] );
			}
		} 
	}
	public static int influenceRange = 10;
	public static int mainBuildingInfluence = 10;
	public static GameObject template, mainTemplate;
	public Cart cart;
	public Ground.Area inputArea = new Ground.Area();
	public Ground.Area outputArea = new Ground.Area();
	public int total;
	public int totalTarget;
	public int maxItems = defaultmaxItems;
	override public string title { get { return main ? "Headquarters" : "Stock"; } set {} }
	static public int defaultmaxItems = 200;
	static public int defaultmaxItemsForMain = 400;
	public World.Timer offersSuspended;     // When this timer is in progress, the stock is not offering items. This is done only for cosmetic reasons, it won't slow the rate at which the stock is providing items.
	static readonly Configuration stockConfiguration = new Configuration
	{
		plankNeeded = 3,
		stoneNeeded = 3,
		flatteningNeeded = true,
		constructionTime = 6000,
		groundTypeNeeded = GroundNode.Type.aboveWater
	};
	static readonly Configuration mainConfiguration = new Configuration
	{
		huge = true
	};

	[Obsolete( "Compatibility for old files", true )]
	List<int> target = new List<int>();

	public class Cart : Worker
	{
		public const int capacity = 25;
		public Item.Type itemType;
		public int itemQuantity;
		public Stock destination;
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

		public void TransferItems( Item.Type itemType, Stock destination )
		{
			int typeIndex = (int)itemType;
			boss.content[typeIndex] -= capacity;
			itemQuantity = capacity;
			this.itemType = itemType;

			ScheduleWalkToNeighbour( boss.flag.node );
			DeliverItems( destination );
			ScheduleWalkToNeighbour( destination.flag.node );
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
			destination = null;
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

	public static bool IsNodeSuitable( GroundNode placeToBuild, Player owner, int flagDirection )
	{
		return Building.IsNodeSuitable( placeToBuild, owner, stockConfiguration, flagDirection );
	}

	public Stock Setup( GroundNode node, Player owner, int flagDirection, bool blueprintOnly = false )
	{
		height = 1.5f;
		maxItems = defaultmaxItems;

		CreateMissingArrays();
		if ( base.Setup( node, owner, main ? mainConfiguration : stockConfiguration, flagDirection, blueprintOnly ) == null )
			return null;

		owner.RegisterStock( this );

		return this;
	}

	public Stock SetupMain( GroundNode node, Player owner, int flagDirection )
	{
		main = true;

		node.owner = owner;
		if ( !Setup( node, owner, flagDirection ) )
			return null;

		maxItems = defaultmaxItemsForMain;
		height = 3;
		if ( configuration.flatteningNeeded )
		{
			foreach ( var o in Ground.areas[1] )
				node.Add( o ).SetHeight( node.height );
		}
		construction.done = true;
		content[(int)Item.Type.plank] = 10;
		content[(int)Item.Type.stone] = 5;
		content[(int)Item.Type.soldier] = 10;
		dispenser = worker = Worker.Create().SetupForBuilding( this );
		owner.RegisterInfluence( this );
		flag.ConvertToCrossing( false );
		return this;
	}

	public List<Stock> GetSubcontractors( Item.Type itemType )
	{
		int typeIndex = (int)itemType;
		List<Stock> list = new List<Stock>();
		foreach ( var stock in owner.stocks )
		{
			if ( stock == this )
				continue;
			if ( stock.destinationLists[typeIndex].Contains( this ) )
			list.Add( stock );
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
			for ( int i = 0; i < destinationLists[itemType].Count; i++ )
			{
				var destination = destinationLists[itemType][i];
				if ( destination == null )	// unity like null
				{
					destinationLists[itemType].RemoveAt( i );
					i--;
					continue;
				}

				if (
					content[itemType] >= Cart.capacity &&
					cart.IsIdle( true ) &&
					flag.user == null &&
					destination.total + Cart.capacity <= maxItems &&
					destination.content[itemType] + Cart.capacity <= destination.inputMax[itemType] )
				{
					cart.TransferItems( (Item.Type)itemType, destination );
				}
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
				owner.itemDispatcher.RegisterOffer( this, (Item.Type)itemType, content[itemType], p, outputArea, 0.5f, flag.FreeSpace() == 0, !dispenser.IsIdle() || offersSuspended.inProgress );
			}
		}
	}

	public override int Influence( GroundNode node )
	{
		if ( !main )
			base.Influence( node );

		return Stock.mainBuildingInfluence - node.DistanceFrom( this.node );
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
		while ( destinationLists.Count < (int)Item.Type.total )
			destinationLists.Add( new List<Stock>() );
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
			assert.AreEqual( ( onWay[i] - onWayCounted[i] ) % Cart.capacity, 0 );
	}
}
