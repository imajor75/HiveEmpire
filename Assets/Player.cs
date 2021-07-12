using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;

public class Player : ScriptableObject
{
	public Stock mainBuilding;
	public List<float> itemHaulPriorities = new List<float>();
	public ItemDispatcher itemDispatcher;
	public Versioned versionedRoadDelete = new Versioned();
	public Versioned versionedBuildingDelete = new Versioned();
	public List<Building> influencers = new List<Building>();
	public World.Timer productivityTimer;
	public List<Chart> itemProductivityHistory = new List<Chart>();
	public float mainProductivity { get { return itemProductivityHistory[(int)Item.Type.soldier].current; } }
	public List<Stock> stocks = new List<Stock>();
	public List<Item> items = new List<Item>();
	public int firstPossibleEmptyItemSlot = 0;
	public int[] surplus = new int[(int)Item.Type.total];
	public List<InputWeight> inputWeights;
	public InputWeight plankForConstructionWeight, stoneForConstructionWeight;

	[Obsolete( "Compatibility with old files", true )]
	float totalEfficiency { set {} }
	[Obsolete( "Compatibility with old files", true )]
	World.Timer efficiencyTimer { set { productivityTimer = value; } }
	[Obsolete( "Compatibility with old files", true )]
	Chart averageEfficiencyHistory { set {} }
	[Obsolete( "Compatibility with old files", true )]
	List<Chart> itemEfficiencyHistory { set { itemProductivityHistory = value; } }
	[Obsolete( "Compatibility with old files", true )]
	Item.Type worseItemType { set {} }
	[Obsolete( "Compatibility with old files", true )]
	float averageEfficiency { set {} }
	[Obsolete( "Compatibility with old files", true )]
	int soldiersProduced { set { soldierCount = value; } }
	[Obsolete( "Compatibility with old files", true )]
	int bowmansProduced;
	[Obsolete( "Compatibility with old files", true )]
	int coinsProduced;

	public int soldierCount 
	{ 
		set { mainBuilding.content[(int)Item.Type.soldier] = value; }
		get { return mainBuilding.content[(int)Item.Type.soldier]; } 
	}

	[System.Serializable]
	public class InputWeight
	{
		public Workshop.Type workshopType;
		public Item.Type itemType;
		public float weight;
	}

	public class Chart : ScriptableObject
	{
		public List<float> data;
		public float record;
		public float current;
		public int recordIndex;
		public Item.Type itemType;
		public int production;
		
		[Obsolete( "Compatibility with old files", true )]
		float factor { set {} }
		[Obsolete( "Compatibility with old files", true )]
		float weight { set {} }
		[Obsolete( "Compatibility with old files", true )]
		float weighted { set {} }

		public static Chart Create()
		{
			return CreateInstance<Chart>();
		}

		public Chart Setup( Item.Type itemType )
		{
			this.itemType = itemType;
			data = new List<float>();
			record = current = 0;
			recordIndex = production = 0;
			return this;
		}

		public void Advance()
		{
			if ( data == null )
				data = new List<float>();

			current = current * ( 1 - Constants.Player.productionUpdateFactor ) + production * Constants.Player.productionUpdateFactor;

			if ( current > record )
			{
				record = current;
				recordIndex = data.Count;
			}

			data.Add( current );
			production = 0;
		}
	}

	public static Player Create()
	{
		return ScriptableObject.CreateInstance<Player>();
	}

	public Player Setup()
	{
		for ( int i = 0; i < (int)Item.Type.total; i++ )
		{
			if ( i == (int)Item.Type.plank || i == (int)Item.Type.stone )
				itemHaulPriorities.Add( 1.1f );
			else
				itemHaulPriorities.Add( 1 );
		}

		itemDispatcher = ItemDispatcher.Create();
		itemDispatcher.Setup( this );
		if ( !CreateMainBuilding() )
		{
			Destroy( this );
			return null;
		}
		productivityTimer.Start( Constants.Player.productivityUpdateTime );
		CreateInputWeights();

		return this;
	}

	void CreateInputWeights()
	{
		inputWeights = new List<InputWeight>();
		inputWeights.Add( new InputWeight
		{
			workshopType = Workshop.Type.unknown,
			itemType = Item.Type.plank,
			weight = 1
		} );
		inputWeights.Add( new InputWeight
		{
			workshopType = Workshop.Type.unknown,
			itemType = Item.Type.stone,
			weight = 1
		} );
		foreach ( var c in Workshop.configurations )
		{
			if ( c.inputs == null )
				continue;
			foreach ( var b in c.inputs )
			{
				inputWeights.Add( new InputWeight
				{
					workshopType = c.type,
					itemType = b.itemType,
					weight = Constants.Player.defaultInputWeight
				} );
			}
		}
		plankForConstructionWeight = FindInputWeight( Workshop.Type.unknown, Item.Type.plank );
		stoneForConstructionWeight = FindInputWeight( Workshop.Type.unknown, Item.Type.stone );
	}

	public InputWeight FindInputWeight( Workshop.Type workshopType, Item.Type itemType )
	{
		foreach ( var w in inputWeights )
			if ( workshopType == w.workshopType && itemType == w.itemType )
				return w;

		return null;
	}

	public void Start()
	{
		while ( itemHaulPriorities.Count < (int)Item.Type.total )
			itemHaulPriorities.Add( 1 );
		while ( itemProductivityHistory.Count < (int)Item.Type.total )
			itemProductivityHistory.Add( Chart.Create().Setup( (Item.Type)itemProductivityHistory.Count ) );

		if ( inputWeights == null )
			CreateInputWeights();	// For compatibility with old files

		if ( surplus.Length != (int)Item.Type.total )
			surplus = new int[(int)Item.Type.total];
	}

	public void FixedUpdate()
	{
		if ( !productivityTimer.done )
			return;
		productivityTimer.Start( Constants.Player.productivityUpdateTime );

		foreach ( var chart in itemProductivityHistory )
			chart.Advance();
	}

	bool CreateMainBuilding()
	{
		const int flagDirection = 1;
		Node center = World.instance.ground.GetCenter(), best = null;
		float heightdDif = float.MaxValue;
		var area = Building.GetFoundation( true, flagDirection );
		List<Ground.Offset> extendedArea = new List<Ground.Offset>();
		foreach ( var p in area )
		{
			foreach ( var o in Ground.areas[1] )
			{
				var n = p + o;
				if ( !extendedArea.Contains( n ) )
					extendedArea.Add( n );
			}
		}

		foreach ( var o in Ground.areas[8] )
		{
			Node node = center + o;

			bool invalidNode = false;
			foreach ( var n in area )
			{
				var localNode = node + n;
				if ( !localNode.CheckType( Node.Type.land ) || localNode.owner != null || localNode.IsBlocking() )
					invalidNode = true;
			}
			if ( invalidNode || node.Neighbour( flagDirection ).IsBlocking() )
				continue;

			float min, max;
			min = max = node.height;
			foreach ( var e in extendedArea )
			{
				var localNode = node + e;
				if ( !localNode.CheckType( Node.Type.land ) )
				{
					max = float.MaxValue;
					break;
				}
				float height = localNode.height;
				if ( height < min )
					min = height;
				if ( height > max )
					max = height;
			}
			if ( max - min < heightdDif )
			{
				best = node;
				heightdDif = max - min;
			}
		}

		if ( best == null )
			return false;

		foreach ( var j in extendedArea )
		{
			best.Add( j ).SetHeight( best.height );
			best.Add( j ).owner = this;
		}

		Assert.global.IsNull( mainBuilding );
		mainBuilding = Stock.Create();
		mainBuilding.SetupMain( best, this, flagDirection );
		World.instance.eye.FocusOn( mainBuilding.node );
		return true;
	}

	public void RegisterInfluence( Building building )
	{
		influencers.Add( building );
		mainBuilding.ground.RecalculateOwnership();
	}

	public void UnregisterInfuence( Building building )
	{
		influencers.Remove( building );
		mainBuilding.ground.RecalculateOwnership();
	}

	public void RegisterStock( Stock stock )
	{
		stock.assert.AreEqual( stock.owner, this );
		stocks.Add( stock );
	}

	public void UnregisterStock( Stock stock )
	{
		stock.assert.AreEqual( stock.owner, this );
		stocks.Remove( stock );
	}

	public void RegisterItem( Item item )
	{
		item.assert.AreEqual( item.owner, this );
		int slotIndex = firstPossibleEmptyItemSlot;
		while ( slotIndex < items.Count && items[slotIndex] != null )
			slotIndex++;

		if ( slotIndex < items.Count )
			items[slotIndex] = item;
		else
			items.Add( item );
		item.index = slotIndex;
		firstPossibleEmptyItemSlot = slotIndex + 1;
	}

	public void UnregisterItem( Item item )
	{
		item.assert.AreEqual( item.owner, this );
		item.assert.AreEqual( items[item.index], item );
		items[item.index] = null;
		if ( item.index < firstPossibleEmptyItemSlot )
			firstPossibleEmptyItemSlot = item.index;
		item.index = -2;
	}

	public void ItemProduced( Item.Type itemType, int quantity = 1 )
	{
		Assert.global.IsTrue( quantity > 0 );
		itemProductivityHistory[(int)itemType].production += quantity;
	}

	public void Validate()
	{
		Assert.global.IsNotNull( mainBuilding );
		foreach ( var building in influencers )
			Assert.global.IsNotNull( building );
		foreach ( var stock in stocks )
			Assert.global.IsNotNull( stock );
		Assert.global.AreEqual( itemHaulPriorities.Count, (int)Item.Type.total );
	}
}

[System.Serializable]
public class Versioned
{
	public int version;

	public void Trigger()
	{
		version++;
	}
}

[System.Serializable]
public class Watch
{
	public Versioned source;
	public int localVersion;

	public void Attach( Versioned source )
	{
		this.source = source;
		if ( source != null )
			localVersion = source.version;
	}
	public bool isAttached { get { return source != null; } }
	public bool Check()
	{
		if ( isAttached && localVersion != source.version )
		{
			localVersion = source.version;
			return true;
		}
		return false;
	}
}

