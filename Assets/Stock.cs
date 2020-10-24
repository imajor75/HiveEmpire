using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

public class Stock : Building
{
	public bool main = false;
	public List<int> content = new List<int>();
	public List<int> target = new List<int>();
	public List<int> onWay = new List<int>();
	public Stock[] destinations = new Stock[(int)Item.Type.total];
	public static int influenceRange = 10;
	public static int mainBuildingInfluence = 10;
	public static GameObject template;
	static Configuration configuration = new Configuration();
	public Cart cart;
	const int cartCapacity = 16;

	public class Cart : Worker
	{
		new public static Cart Create()
		{
			return new GameObject().AddComponent<Cart>();
		}

		public Item.Type itemType;
		public int itemQuantity;
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
				return ResetBoss();

			Cart cart = boss as Cart;
			boss.assert.IsNotNull( cart );
			Stock cartStock = cart.building as Stock;
			if ( cart.itemQuantity > 0 )
			{
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
		template = (GameObject)Resources.Load( "Medieval fantasy house/Medieva_fantasy_house" );
		configuration.plankNeeded = 2;
		configuration.stoneNeeded = 2;
		configuration.flatteningNeeded = true;
	}

	public static Stock Create()
	{
		var buildingObject = (GameObject)GameObject.Instantiate( template );
		buildingObject.transform.localScale = new Vector3( 0.12f, 0.12f, 0.12f );
		buildingObject.transform.Rotate( Vector3.up * -55 );	
		return buildingObject.AddComponent<Stock>();
	}

	public static bool IsItGood( GroundNode placeToBuild, Player owner )
	{
		return Building.IsItGood( placeToBuild, owner, configuration );
	}

	public Stock Setup( GroundNode node, Player owner )
	{
		title = "stock";
		construction.plankNeeded = 3;
		construction.stoneNeeded = 3;
		construction.flatteningNeeded = true;
		height = 2;

		while ( content.Count < (int)Item.Type.total )
		{
			content.Add( 0 );
			target.Add( 0 );
			onWay.Add( 0 );
		}
		if ( base.Setup( node, owner, configuration ) == null )
			return null;

		owner.RegisterStock( this );

		cart = Cart.Create().SetupAsCart( this ) as Cart;

		return this;
	}

	public Stock SetupMain( GroundNode node, Player owner )
	{
		node.owner = owner;
		foreach ( var o in Ground.areas[1] )
			node.Add( o ).owner = owner;
		if ( !Setup( node, owner ) )
			return null;

		title = "headquarter";
		main = true;
		construction = new Construction();
		construction.boss = this;
		construction.done = true;
		if ( construction.flatteningNeeded )
		{
			foreach ( var o in Ground.areas[1] )
				node.Add( o ).SetHeight( node.height );
			construction.flatteningNeeded = false;
		}
		content[(int)Item.Type.plank] = 10;
		content[(int)Item.Type.fish] = 10;
		worker = Worker.Create();
		worker.SetupForBuilding( this );
		owner.RegisterInfluence( this );
		return this;
	}

	public override bool Remove()
	{
		if ( main )
			return false;
		owner.UnregisterStock( this );
		return base.Remove();
	}

	new void Start()
	{
		base.Start();
		if ( main )
			name = "Headquarters";
		else
			name = "Stock " + node.x + ", " + node.y;
		while ( content.Count < (int)Item.Type.total )
			content.Add( 0 );
		while ( target.Count < (int)Item.Type.total )
			target.Add( 0 );
		while ( onWay.Count < (int)Item.Type.total )
			onWay.Add( 0 );
		Array.Resize( ref destinations, (int)Item.Type.total );
	}

	new public void Update()
    {
		base.Update();

		if ( !construction.done )
			return;

		for ( int itemType = 0; itemType < (int)Item.Type.total; itemType++ )
		{
			owner.itemDispatcher.RegisterRequest( this, (Item.Type)itemType, int.MaxValue, ItemDispatcher.Priority.stock );
			if ( content.Count > itemType && content[itemType] > 0 && flag.FreeSpace() > 3 )
				owner.itemDispatcher.RegisterOffer( this, (Item.Type)itemType, content[itemType], ItemDispatcher.Priority.stock );
			int missing = target[itemType] - content[itemType] - onWay[itemType];
			if ( missing > 0 )
				owner.itemDispatcher.RegisterRequest( this, (Item.Type)itemType, missing, ItemDispatcher.Priority.high );

			if ( destinations[itemType] && content[itemType] >= cartCapacity && cart.IsIdle( true ) && flag.user == null )
			{
				var target = destinations[itemType];
				cart.itemQuantity = cartCapacity;
				cart.itemType = (Item.Type)itemType;
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
			}
		}
    }

	new void FixedUpdate()
	{
		base.FixedUpdate();
		if ( worker == null && construction.done )
		{
			worker = Worker.Create();
			worker.SetupForBuilding( this );
		}
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

	public override void Validate()
	{
		base.Validate();
		int[] onWayCounted = new int[(int)Item.Type.total];
		foreach ( var item in itemsOnTheWay )
			onWayCounted[(int)item.type]++;
		for ( int i = 0; i < onWayCounted.Length; i++ )
			assert.IsTrue( ( onWay[i] - onWayCounted[i] ) % Stock.cartCapacity == 0 );
	}
}
