using Newtonsoft.Json;
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
	const int efficiencyUpdateTime = 3000;
	public float totalEfficiency;
	public World.Timer efficiencyTimer;
	public Chart averageEfficiencyHistory;
	public List<Chart> itemEfficiencyHistory = new List<Chart>();

	public int soldiersProduced = 0;
	public int bowmansProduced = 0;
	public int coinsProduced = 0;

	public List<Stock> stocks = new List<Stock>();
	public List<Item> items = new List<Item>();
	public int firstPossibleEmptyItemSlot = 0;
	public int[] surplus = new int[(int)Item.Type.total];
	public Item.Type worseItemType;

	[JsonIgnore]
	public float averageEfficiency;
	[JsonIgnore]
	public List<float> production;
	[JsonIgnore]
	public List<float> efficiency;

	public class Chart : ScriptableObject
	{
		public List<float> data;
		public float record;
		public float current;
		public int recordIndex;
		public Item.Type itemType;
		public float factor;
		public int production;
		public float weighted;
		const float efficiencyUpdateFactor = 0.2f;

		public static Chart Create()
		{
			return CreateInstance<Chart>();
		}

		public Chart Setup( Item.Type itemType )
		{
			this.itemType = itemType;
			data = new List<float>();
			switch ( itemType )
			{
				case Item.Type.stone:
					factor = 0;
					break;
				case Item.Type.grain:
				case Item.Type.beer:
					factor = 0.4f;
					break;
				case Item.Type.flour:
				case Item.Type.salt:
					factor = 2;
					break;
				case Item.Type.coin:
					factor = 0.5f;
					break;
				case Item.Type.coal:
					factor = 0.33f;
					break;
				default:
					factor = 1;
					break;
			}
			record = current = weighted = 0;
			recordIndex = production = 0;
			return this;
		}

		public float Advance( float efficiency = 0 )
		{
			if ( data == null )
				data = new List<float>();

			if ( efficiency == 0 )
				current = current * ( 1 - efficiencyUpdateFactor ) + production * efficiencyUpdateFactor;
			else
				current = efficiency;

			if ( current > record )
			{
				record = current;
				recordIndex = data.Count;
			}

			data.Add( current );
			production = 0;
			return weighted = factor * current;
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

		itemDispatcher = ScriptableObject.CreateInstance<ItemDispatcher>();
		itemDispatcher.Setup( this );
		if ( !CreateMainBuilding() )
		{
			Destroy( this );
			return null;
		}
		efficiencyTimer.Start( efficiencyUpdateTime );
		return this;
	}

	public void Start()
	{
		while ( itemHaulPriorities.Count < (int)Item.Type.total )
			itemHaulPriorities.Add( 1 );
		if ( averageEfficiencyHistory == null )
			averageEfficiencyHistory = Chart.Create().Setup( Item.Type.total );
		while ( itemEfficiencyHistory.Count < (int)Item.Type.total )
			itemEfficiencyHistory.Add( Chart.Create().Setup( (Item.Type)itemEfficiencyHistory.Count ) );
		itemDispatcher.Start();
	}

	public void FixedUpdate()
	{
		if ( !efficiencyTimer.Done )
			return;
		efficiencyTimer.Start( efficiencyUpdateTime );

		totalEfficiency = float.MaxValue;
		float averageEfficiency = 0;
		int count = 0;
		for ( int i = 0; i < itemEfficiencyHistory.Count; i++ )
		{
			var current = itemEfficiencyHistory[i].Advance();

			averageEfficiency += current;
			count++;

			if ( itemEfficiencyHistory[i].factor > 0 && current < totalEfficiency )
			{
				worseItemType = (Item.Type)i;
				totalEfficiency = current;
			}
		}
		averageEfficiency /= count;
		averageEfficiencyHistory.Advance( averageEfficiency );
	}

	public void LateUpdate()
	{
		itemDispatcher.LateUpdate();
	}

	bool CreateMainBuilding()
	{
		GroundNode center = World.instance.ground.GetCenter(), best = null;
		float heightdDif = float.MaxValue;
		foreach ( var o in Ground.areas[8] )
		{
			GroundNode node = center.Add( o );
			if ( !node.CheckType( GroundNode.Type.land ) || node.owner != null )
				continue;
			if ( node.IsBlocking() || node.Add( Building.flagOffset ).IsBlocking() )
				continue;
			float min, max;
			min = max = node.height;
			for ( int i = 0; i < GroundNode.neighbourCount; i++ )
			{
				if ( !node.Neighbour( i ).CheckType( GroundNode.Type.land ) )
				{
					max = float.MaxValue;
					break;
				}
				float height = node.Neighbour( i ).height;
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

		Assert.global.IsNull( mainBuilding );
		mainBuilding = Stock.Create();
		mainBuilding.SetupMain( best, this );
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
		item.index = -1;
	}

	public void ItemProduced( Item.Type itemType, int quantity = 1 )
	{
		Assert.global.IsTrue( quantity > 0 );
		itemEfficiencyHistory[(int)itemType].production += quantity;
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
		localVersion = source.version;
	}
	public bool Check()
	{
		if ( localVersion != source.version )
		{
			localVersion = source.version;
			return true;
		}
		return false;
	}
}

