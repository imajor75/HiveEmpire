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
	public const int efficiencyUpdateTime = 3000;
	public float totalEfficiency;
	public World.Timer efficiencyTimer;
	public Chart averageEfficiencyHistory;
	public List<Chart> itemEfficiencyHistory = new List<Chart>();

	public int soldiersProduced = 0;
	[Obsolete( "Compatibility with old files", true )]
	int bowmansProduced;
	[Obsolete( "Compatibility with old files", true )]
	int coinsProduced;

	public List<Stock> stocks = new List<Stock>();
	public List<Item> items = new List<Item>();
	public int firstPossibleEmptyItemSlot = 0;
	public int[] surplus = new int[(int)Item.Type.total];
	public Item.Type worseItemType;
	public float averageEfficiency;

	[JsonIgnore]
	public List<float> production;
	[JsonIgnore]
	public List<float> efficiency;

	[System.Serializable]
	public class InputWeight
	{
		public Workshop.Type workshopType;
		public Item.Type itemType;
		public float weight;
	}

	public List<InputWeight> inputWeights;
	public InputWeight plankForConstructionWeight, stoneForConstructionWeight;

	public class Chart : ScriptableObject
	{
		public List<float> data;
		public float record;
		public float current;
		public int recordIndex;
		public Item.Type itemType;
		[Obsolete( "Compatibility with old files", true )]
		float factor { set { weight = value * 2; } }
		public float weight;
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
			record = current = weighted = 0;
			recordIndex = production = 0;
			return this;
		}

		public void ResetWeight()
		{
			// to produce 1 soldier, the following items need to be produced:
			// log		=> 1(coin)+1(plank)
			// stone	=> 0	
			// plank	=> 1	
			// fish		=> 0.75 salt+1(iron+coal)=1.75
			// grain	=> 0.75(flour)+1.25(beer)=2
			// flour	=> 0.75	
			// salt		=> 0.75
			// pretzel	=> 0.5(gold)+1(iron+coal)=1.5	
			// hide		=> 1
			// iron		=> 1
			// coal		=> 1(weapon)+1(steel)=2
			// gold		=> 1
			// bow		=> 1
			// steel	=> 1	
			// weapon	=> 1	
			// water	=> 1.25
			// beer		=> 1(barrack)+1.5(pork)=2.5
			// pork		=> 0.5(gold)+1(iron+coal)=1.5
			// coin		=> 1
			weight = itemType switch
			{
				Item.Type.stone => 0,
				Item.Type.grain => 1,
				Item.Type.beer => 2/2.5f,
				Item.Type.flour => 2/0.75f,
				Item.Type.salt => 2/0.75f,
				Item.Type.coal => 1,
				Item.Type.fish => 2/1.75f,
				Item.Type.pretzel => 2/1.5f,
				Item.Type.water => 2/1.25f,
				Item.Type.pork => 2/1.5f,
				_ => 2
			};
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
			return weighted = weight * current;
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
		efficiencyTimer.Start( efficiencyUpdateTime );
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
					weight = 0.5f
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
		if ( averageEfficiencyHistory == null )
			averageEfficiencyHistory = Chart.Create().Setup( Item.Type.total );
		while ( itemEfficiencyHistory.Count < (int)Item.Type.total )
			itemEfficiencyHistory.Add( Chart.Create().Setup( (Item.Type)itemEfficiencyHistory.Count ) );
		foreach ( var h in itemEfficiencyHistory )
			h.ResetWeight();

		if ( inputWeights == null )
			CreateInputWeights();	// For compatibility with old files
	}

	public void FixedUpdate()
	{
		if ( !efficiencyTimer.done )
			return;
		efficiencyTimer.Start( efficiencyUpdateTime );

		totalEfficiency = float.MaxValue;
		averageEfficiency = 1;
		int count = 0;
		for ( int i = 0; i < itemEfficiencyHistory.Count; i++ )
		{
			var current = itemEfficiencyHistory[i].Advance();

			if ( itemEfficiencyHistory[i].weight != 0 )
			{
				averageEfficiency *= current;
				count++;
			}

			if ( itemEfficiencyHistory[i].weight > 0 && current < totalEfficiency )
			{
				worseItemType = (Item.Type)i;
				totalEfficiency = current;
			}
		}
		averageEfficiency = (float)Math.Pow( averageEfficiency, 1f / count );
		averageEfficiencyHistory.Advance( averageEfficiency );
	}

	bool CreateMainBuilding()
	{
		const int flagDirection = 1;
		GroundNode center = World.instance.ground.GetCenter(), best = null;
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
			GroundNode node = center + o;

			bool invalidNode = false;
			foreach ( var n in area )
			{
				var localNode = node + n;
				if ( !localNode.CheckType( GroundNode.Type.land ) || localNode.owner != null || localNode.IsBlocking() )
					invalidNode = true;
			}
			if ( invalidNode || node.Neighbour( flagDirection ).IsBlocking() )
				continue;

			float min, max;
			min = max = node.height;
			foreach ( var e in extendedArea )
			{
				var localNode = node + e;
				if ( !localNode.CheckType( GroundNode.Type.land ) )
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

