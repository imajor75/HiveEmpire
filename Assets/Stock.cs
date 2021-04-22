using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

public class Stock : Building
{
	public bool main = false;
	public List<int> content = new List<int>();
	public List<int> onWay = new List<int>();
	public List<int> inputMin = new List<int>();
	public List<int> inputMax = new List<int>();
	public List<int> outputMin = new List<int>();
	public List<int> outputMax = new List<int>();
	public Stock[] destinations = new Stock[(int)Item.Type.total];
	public static int influenceRange = 10;
	public static int mainBuildingInfluence = 10;
	public static GameObject template, mainTemplate;
	static readonly Configuration configuration = new Configuration();
	static readonly Configuration mainConfiguration = new Configuration();
	public Cart cart;
	public Ground.Area inputArea = new Ground.Area();
	public Ground.Area outputArea = new Ground.Area();
	public int total;
	public int totalTarget;
	static public int maxItems = 200;
	GameObject body;

	[Obsolete( "Compatibility for old files", true )]
	public List<int> target = new List<int>();

	public class Cart : Worker
	{
		new public static Cart Create()
		{
			return new GameObject().AddComponent<Cart>();
		}

		public override void Reset()
		{
			base.Reset();
			itemQuantity = 0;
			destination = null;
		}

		public const int capacity = 20;
		public Item.Type itemType;
		public int itemQuantity;
		public Stock destination;
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
			}
			if ( stock == cartStock )
			{
				if ( cart.exclusiveFlag )
				{
					cart.exclusiveFlag.user = null;
					cart.exclusiveFlag = null;
				}
			}
			return true;
		}
	}

	public static new void Initialize()
	{
		mainTemplate = Resources.Load<GameObject>( "prefabs/buildings/main" );
		template = Resources.Load<GameObject>( "prefabs/buildings/stock" );

		configuration.plankNeeded = 2;
		configuration.stoneNeeded = 2;
		configuration.flatteningNeeded = true;
		mainConfiguration.huge = true;
	}

	public static Stock Create()
	{
		return new GameObject().AddComponent<Stock>();
	}

	public static bool IsNodeSuitable( GroundNode placeToBuild, Player owner, int flagDirection )
	{
		return Building.IsNodeSuitable( placeToBuild, owner, configuration, flagDirection );
	}

	public Stock Setup( GroundNode node, Player owner, int flagDirection, bool blueprintOnly = false )
	{
		title = "stock";
		construction.plankNeeded = 3;
		construction.stoneNeeded = 3;
		construction.flatteningNeeded = true;
		height = 1.5f;

		while ( content.Count < (int)Item.Type.total )
		{
			content.Add( 0 );
			onWay.Add( 0 );
			inputMin.Add( 0 );
			inputMax.Add( maxItems / 20 );
			outputMin.Add( 0 );
			outputMax.Add( maxItems / 20 );
		}
		if ( base.Setup( node, owner, main ? mainConfiguration : configuration, flagDirection, blueprintOnly ) == null )
			return null;

		owner.RegisterStock( this );

		return this;
	}

	public Stock SetupMain( GroundNode node, Player owner, int flagDirection )
	{
		main = true;
		huge = true;

		node.owner = owner;
		if ( !Setup( node, owner, flagDirection ) )
			return null;

		title = "headquarter";
		height = 3;
		construction = new Construction
		{
			boss = this,
			done = true
		};
		if ( construction.flatteningNeeded )
		{
			foreach ( var o in Ground.areas[1] )
				node.Add( o ).SetHeight( node.height );
			construction.flatteningNeeded = false;
		}
		content[(int)Item.Type.plank] = 10;
		content[(int)Item.Type.fish] = 10;
		worker = Worker.Create().SetupForBuilding( this );
		owner.RegisterInfluence( this );
		return this;
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
		if ( main )
			body = Instantiate( mainTemplate, transform );
		else
			body = Instantiate( template, transform );
		body.layer = World.layerIndexPickable;

		base.Start();
		if ( main )
			name = "Headquarters";
		else
			name = "Stock " + node.x + ", " + node.y;
		while ( content.Count < (int)Item.Type.total )
			content.Add( 0 );
		while ( inputMin.Count < (int)Item.Type.total )
			inputMin.Add( 0 );
		while ( inputMax.Count < (int)Item.Type.total )
			inputMax.Add( maxItems / 20 );
		while ( outputMin.Count < (int)Item.Type.total )
			outputMin.Add( 0 );
		while ( outputMax.Count < (int)Item.Type.total )
			outputMax.Add( maxItems / 20 );
		while ( onWay.Count < (int)Item.Type.total )
			onWay.Add( 0 );
		Array.Resize( ref destinations, (int)Item.Type.total );

		body.transform.RotateAround( node.Position, Vector3.up, 60 * ( 1 - flagDirection ) );
	}

	new public void Update()
	{
		base.Update();

		if ( !construction.done || blueprintOnly )
			return;

		if ( cart == null )
			cart = Cart.Create().SetupAsCart( this ) as Cart;

		total = totalTarget = 0;
		for ( int itemType = 0; itemType < (int)Item.Type.total; itemType++ )
		{
			int count = content[itemType] + onWay[itemType];
			total += count;
			totalTarget += Math.Max( count, inputMin[itemType] );
		}

		for ( int itemType = 0; itemType < (int)Item.Type.total; itemType++ )
		{
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
			if ( content.Count > itemType && content[itemType] > 0 && flag.FreeSpace() > 3 )
			{
				var p = ItemDispatcher.Priority.stock;
				if ( current < outputMin[itemType] )
					p = ItemDispatcher.Priority.zero;
				if ( current > outputMax[itemType] )
					p = ItemDispatcher.Priority.high;
				owner.itemDispatcher.RegisterOffer( this, (Item.Type)itemType, content[itemType], p, outputArea );
			}

			if ( 
				destinations[itemType] && 
				content[itemType] >= Cart.capacity && 
				cart.IsIdle( true ) && 
				flag.user == null && 
				destinations[itemType].total + Cart.capacity <= maxItems &&
				destinations[itemType].content[itemType] < destinations[itemType].inputMax[itemType] )
			{
				content[itemType] -= Cart.capacity;
				var target = destinations[itemType];
				cart.itemQuantity = Cart.capacity;
				cart.itemType = (Item.Type)itemType;
				cart.destination = target;
				target.onWay[itemType] += cart.itemQuantity;

				cart.ScheduleWalkToNeighbour( flag.node );
				cart.ScheduleWalkToFlag( target.flag, true );
				cart.ScheduleWalkToNeighbour( target.node );

				var task = ScriptableObject.CreateInstance<DeliverStackTask>();
				task.Setup( cart, target );
				cart.ScheduleTask( task );

				cart.ScheduleWalkToNeighbour( target.flag.node );
				cart.ScheduleWalkToFlag( flag, true );
				cart.ScheduleWalkToNeighbour( node );

				flag.user = cart;
				cart.exclusiveFlag = flag;
				cart.onRoad = true;
				cart.gameObject.SetActive( true );
			}
		}
    }

	new void FixedUpdate()
	{
		base.FixedUpdate();
		if ( worker == null && construction.done && !blueprintOnly )
			worker = Worker.Create().SetupForBuilding( this );
	}

	public override int Influence( GroundNode node )
	{
		if ( !main )
			base.Influence( node );

		return Stock.mainBuildingInfluence - node.DistanceFrom( this.node );
	}

	public override void OnClicked()
	{
		if ( construction.done )
			Interface.StockPanel.Create().Open( this );
		else
			Interface.ConstructionPanel.Create().Open( construction );
	}

	public override Item SendItem( Item.Type itemType, Building destination, ItemDispatcher.Priority priority )
	{
		assert.IsTrue( content[(int)itemType] > 0 );
		Item item = base.SendItem( itemType, destination, priority );
		if ( item != null )
			content[(int)itemType]--;

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
			inputMin[i] = inputMax[i] = outputMin[i] = outputMax[i] = 0;

		inputArea.center = outputArea.center = node;
		inputArea.radius = outputArea.radius = 4;
	}

	public override void Reset()
	{
		base.Reset();
		for ( int i = 0; i < (int)Item.Type.total; i++ )
			content[i] = 0;
	}

	public override void Validate()
	{
		base.Validate();
		cart?.Validate();
		if ( cart )
			assert.AreEqual( cart.building, this );		// TODO Fired
		int[] onWayCounted = new int[(int)Item.Type.total];
		foreach ( var item in itemsOnTheWay )
			onWayCounted[(int)item.type]++;
		for ( int i = 0; i < onWayCounted.Length; i++ )
			assert.AreEqual( ( onWay[i] - onWayCounted[i] ) % Cart.capacity,		0 );
	}
}
