using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine;

public class Stock : Attackable
{
	public bool main = false;
	public List<ItemTypeData> itemData = new ();
	public List<Unit> returningUnits = new ();	// This list is maintained for returning units to store them during save, because they usually have no building
	public Ground.Area inputArea = new ();
	public Ground.Area outputArea = new ();
	public int total;
	public int totalTarget;
	public int maxItems = Constants.Stock.defaultmaxItems;
	public Game.Timer offersSuspended = new ();     // When this timer is in progress, the stock is not offering items. This is done only for cosmetic reasons, it won't slow the rate at which the stock is providing items.
	public Game.Timer resupplyTimer = new ();
	public bool fullReported, fullReportedCart;

	override public string title => main ? "Headquarters" : "Stock";
	override public bool wantFoeClicks { get { return main; } }
	override public UpdateStage updateMode => UpdateStage.turtle;
	
	override public int checksum
	{
		get
		{
			int checksum = base.checksum;
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
		int cartOutput { set {} }
		int cartOutputTemporary { set {} }
		int cartInput { set {} }
		int cartPledged { set {} }
		public float importance = 0.1f;
		public Stock boss;
		public Item.Type itemType;

		public ref int ChannelValue( Channel channel )
		{
			switch ( channel )
			{
				case Channel.inputMin: return ref inputMin;
				case Channel.inputMax: return ref inputMax;
				case Channel.outputMin: return ref outputMin;
				case Channel.outputMax: return ref outputMax;
			}
			throw new Exception();
		}

		public float ChangeChannelValue( Channel channel, float newValue )
		{
			if ( channel == Channel.importance )
			{
				var oldImportance = importance;
				importance = newValue;
				return oldImportance;
			}
			var old = ChannelValue( channel );
			ChannelValue( channel ) = (int)newValue;
			if ( channel == Channel.inputMax && old != 0 && newValue == 0 )
				boss.CancelOrders( itemType );
			return old;
		}
		public ItemDispatcher.Category inputPriority 
		{
			get
			{
				if ( content < inputMin	- 1 )
					return ItemDispatcher.Category.prepare;
				if ( content > inputMax - 1 )
					return ItemDispatcher.Category.zero;
				return ItemDispatcher.Category.reserve;
			}
		}
		public ItemDispatcher.Category outputPriority 
		{
			get
			{
				if ( content < outputMin )
					return ItemDispatcher.Category.zero;
				if ( content > outputMax )
					return ItemDispatcher.Category.prepare;
				return ItemDispatcher.Category.reserve;
			}
		}

		public int spaceLeftForCart => inputMax - content;
	}

	public enum Channel
	{
		inputMin,
		inputMax,
		outputMin,
		outputMax,
		importance
	}

	[Obsolete( "Compatibility for old files", true )]
	List<int> target = new ();

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

	public override void Remove()
	{
		destroyed = true;
		team.UnregisterStock( this );
		if ( main )
			team.Defeat();
		base.Remove();
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
			soundSource.Play();		// TODO: when preparation is in progress sounds should not play
		}

		if ( unit is Cart cart )
		{
			// A cart arrived to pick up items
			cart.PickupItems( cart.itemType, this, cart.destination );
			return;
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

			CRC += itemData[itemType].inputMin + itemData[itemType].inputMax + itemData[itemType].outputMin + itemData[itemType].outputMax + itemData[itemType].content;
			int current = itemData[itemType].content + itemData[itemType].onWay;
			if ( maxItems > total )
			{
				team.itemDispatcher.RegisterRequest( this, (Item.Type)itemType, Math.Min( maxItems - total, itemData[itemType].inputMax - current ), itemData[itemType].inputPriority, inputArea, itemData[itemType].importance ); // TODO Should not order more than what fits
				if ( total < maxItems - Constants.Stock.fullTolerance )
					fullReported = false;
			}
			else if ( !fullReported )
			{
				fullReported = true;
				team.SendMessage( "Stock full", this );
			}
			if ( itemData.Count > itemType )
				team.itemDispatcher.RegisterOffer( this, (Item.Type)itemType, itemData[itemType].content, itemData[itemType].outputPriority, outputArea, flag.freeSlots == 0, !dispenser.IsIdle() || offersSuspended.inProgress );
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

	public override Item SendItem( Item.Type itemType, Building destination, ItemDispatcher.Category priority )
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
		int[] onWayCounted = new int[(int)Item.Type.total];
		foreach ( var item in itemsOnTheWay )
			onWayCounted[(int)item.type]++;
		foreach ( var data in itemData )
		{
			int countedOnWayByCart = 0;
			if ( team.cart.itemType == data.itemType && team.cart.destination == this )
				countedOnWayByCart += team.cart.itemQuantity;
			assert.AreEqual( data.onWay, countedOnWayByCart + onWayCounted[(int)data.itemType], $"itemType: {data.itemType}, host: {this}" );
		}
		for ( int j = 0; j < itemData.Count; j++ )
		{
			assert.AreEqual( this, itemData[j].boss );
			assert.AreEqual( j, (int)itemData[j].itemType );
		}
		foreach ( var ret in returningUnits )
			assert.IsTrue( ret.type == Unit.Type.soldier || ret.type == Unit.Type.unemployed || ret.type == Unit.Type.cart );
	}
}
