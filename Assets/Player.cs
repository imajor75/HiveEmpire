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
	const float efficiencyUpdateFactor = 0.3f;
	public float totalEfficiency;
	public float averageEfficiency;
	public World.Timer efficiencyTimer;

	public int soldiersProduced = 0;
	public int bowmansProduced = 0;
	public int coinsProduced = 0;

	public List<Stock> stocks = new List<Stock>();
	public List<Item> items = new List<Item>();
	public int firstPossibleEmptyItemSlot = 0;
	public int[] production = new int[(int)Item.Type.total];
	public float[] efficiency = new float[(int)Item.Type.total];
	public int[] surplus = new int[(int)Item.Type.total];
	public Item.Type worseItemType;
	public static readonly float[] efficiencyFactors = {
		/*log*/1,
		/*stone*/0,
		/*plank*/1,
		/*fish*/1,
		/*grain*/0.4f,
		/*flour*/2,
		/*salt*/2,
		/*pretzel*/1,
		/*hide*/1,
		/*iron*/1,
		/*coal*/0.33f,
		/*gold*/1,
		/*bow*/1, 
		/*steel*/1,
		/*weapon*/1, 
		/*water*/1,
		/*beer*/0.4f,
		/*pork*/1,
		/*coin*/0.5f
	};


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
		Assert.global.AreEqual( efficiencyFactors.Length, (int)Item.Type.total );
		while ( itemHaulPriorities.Count < (int)Item.Type.total )
			itemHaulPriorities.Add( 1 );
		itemDispatcher.Start();
	}

	public void FixedUpdate()
	{
		if ( !efficiencyTimer.Done )
			return;
		efficiencyTimer.Start( efficiencyUpdateTime );

		totalEfficiency = float.MaxValue;
		averageEfficiency = 0;
		int count = 0;
		for ( int i = 0; i < efficiency.Length; i++ )
		{
			Assert.global.IsTrue( production[i] >= 0 );
			efficiency[i] = ( 1 - efficiencyUpdateFactor ) * efficiency[i] + efficiencyUpdateFactor * production[i];
			production[i] = 0;
			float efficiencyScore = efficiency[i] * efficiencyFactors[i];
			averageEfficiency += efficiencyScore;
			count++;
			if ( efficiencyFactors[i] > 0 && efficiencyScore < totalEfficiency )
			{
				worseItemType = (Item.Type)i;
				totalEfficiency = efficiencyScore;
			}
		}
		averageEfficiency /= count;
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
	}

	public void ItemProduced( Item.Type itemType, int quantity = 1 )
	{
		Assert.global.IsTrue( quantity > 0 );
		production[(int)itemType] += quantity;
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

