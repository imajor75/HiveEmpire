using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class Player : HiveObject
{
	public new string name;
	public LinkedList<Message> messages = new LinkedList<Message>();

	[Obsolete( "Compatibility with old files", true )]
	float totalEfficiency { set {} }
	[Obsolete( "Compatibility with old files", true )]
	World.Timer efficiencyTimer { set { chartAdvanceTimer = value; } }
	[Obsolete( "Compatibility with old files", true )]
	Team.Chart averageEfficiencyHistory { set {} }
	[Obsolete( "Compatibility with old files", true )]
	List<Team.Chart> itemEfficiencyHistory { set { itemProductivityHistory = value; } }
	[Obsolete( "Compatibility with old files", true )]
	Item.Type worseItemType { set {} }
	[Obsolete( "Compatibility with old files", true )]
	float averageEfficiency { set {} }
	[Obsolete( "Compatibility with old files", true )]
	int soldiersProduced { set { safeTeam.soldierCount = value; } }
	[Obsolete( "Compatibility with old files", true )]
	int bowmansProduced { set {} }
	[Obsolete( "Compatibility with old files", true )]
	int coinsProduced { set {} }
	[Obsolete( "Compatibility with old files", true )]
	World.Timer productivityTimer { set { chartAdvanceTimer = value; } }
	[Obsolete( "Compatibility with old files", true )]
	Stock mainBuilding { set { safeTeam.mainBuilding = value; } }
	[Obsolete( "Compatibility with old files", true )]
	List<float> itemHaulPriorities { set { safeTeam.itemHaulPriorities = value; } }
	[Obsolete( "Compatibility with old files", true )]
	ItemDispatcher itemDispatcher { set { safeTeam.itemDispatcher = value; } }
	[Obsolete( "Compatibility with old files", true )]
	Versioned versionedRoadDelete { set { safeTeam.versionedRoadDelete = value; } }
	[Obsolete( "Compatibility with old files", true )]
	Versioned versionedRoadNetworkChanged { set { safeTeam.versionedRoadNetworkChanged = value; } }
	[Obsolete( "Compatibility with old files", true )]
	Versioned versionedBuildingDelete { set { safeTeam.versionedBuildingDelete = value; } }
	[Obsolete( "Compatibility with old files", true )]
	List<Building> influencers { set { safeTeam.influencers = value; } }
	[Obsolete( "Compatibility with old files", true )]
	World.Timer chartAdvanceTimer { set { safeTeam.chartAdvanceTimer = value; } }
	[Obsolete( "Compatibility with old files", true )]
	World.Timer productivityUpdateTimer { set { safeTeam.productivityUpdateTimer = value; } }
	[Obsolete( "Compatibility with old files", true )]
	List<Team.Chart> itemProductivityHistory { set { safeTeam.itemProductivityHistory = value; } }
	[Obsolete( "Compatibility with old files", true )]
	List<Stock> stocks { set { safeTeam.stocks = value; } }
	[Obsolete( "Compatibility with old files", true )]
	List<bool> stocksHaveNeed { set { safeTeam.stocksHaveNeed = value; } }
	[Obsolete( "Compatibility with old files", true )]
	List<int> buildingCounts { set { safeTeam.buildingCounts = value; } }
	[Obsolete( "Compatibility with old files", true )]
	List<Item> items { set { safeTeam.items = value; } }
	[Obsolete( "Compatibility with old files", true )]
	int firstPossibleEmptyItemSlot { set { safeTeam.firstPossibleEmptyItemSlot = value; } }
	[Obsolete( "Compatibility with old files", true )]
	int[] surplus { set { safeTeam.surplus = value; } }
	[Obsolete( "Compatibility with old files", true )]
	List<Team.InputWeight> inputWeights { set { safeTeam.inputWeights = value; } }
	[Obsolete( "Compatibility with old files", true )]
	Team.InputWeight plankForConstructionWeight { set { safeTeam.plankForConstructionWeight = value; } }
	[Obsolete( "Compatibility with old files", true )]
	Team.InputWeight stoneForConstructionWeight { set { safeTeam.stoneForConstructionWeight = value; } }

	[Obsolete( "Compatibility with old files", true )]
	Team safeTeam
	{
		get
		{
			if ( team == null )
			{
				team = Team.Create();
				team.players.Add( this );
			}
			return team;
		}
	}

	new void Start()
	{
		base.name = "Player " + name;
		transform.SetParent( World.playersAndTeams.transform );
		base.Start();
	}

	public Player Setup( string name, Team team )
	{
		this.name = name;
		this.team = team;
		team.players.Add( this );
		base.Setup();
		return this;
	}

	public override void Remove()
	{
		world.players.Remove( this );
		Destroy( gameObject );
	}

	public class Message
	{
		public string text;
		public HiveObject location;
	}
}

public class Team : HiveObject
{
	public List<Player> players = new List<Player>();
	public Color color;
	public new string name;

	public Stock mainBuilding;
	public List<float> itemHaulPriorities = new List<float>();
	public ItemDispatcher itemDispatcher;
	public Versioned versionedRoadDelete = new Versioned();
	public Versioned versionedRoadNetworkChanged = new Versioned();
	public Versioned versionedBuildingDelete = new Versioned();
	public List<Building> influencers = new List<Building>();
	public World.Timer chartAdvanceTimer = new World.Timer(), productivityUpdateTimer = new World.Timer();
	public List<Chart> itemProductivityHistory = new List<Chart>();
	public List<Stock> stocks = new List<Stock>();
	public List<Flag> flags = new List<Flag>();
	public List<Road> roads = new List<Road>();
	public List<Workshop> workshops = new List<Workshop>();
	public List<GuardHouse> guardHouses = new List<GuardHouse>();

	public List<bool> stocksHaveNeed = new List<bool>();
	public List<int> buildingCounts = new List<int>();
	public List<Item> items = new List<Item>();
	public int firstPossibleEmptyItemSlot = 0;
	public int[] surplus = new int[(int)Item.Type.total];
	public List<InputWeight> inputWeights;
	public InputWeight plankForConstructionWeight, stoneForConstructionWeight;
	public List<float> constructionFactors = new List<float>();
	public int soldierCount 
	{ 
		set { mainBuilding.itemData[(int)Item.Type.soldier].content = value; }
		get { return mainBuilding.itemData[(int)Item.Type.soldier].content; } 
	}

    public override int checksum
	{
		get
		{
			int checksum = base.checksum;
			foreach ( var workshop in workshops )
				checksum += workshop.checksum;
			foreach ( var stock in stocks )
				checksum += stock.checksum;
			foreach ( var road in roads )
				checksum += road.checksum;
			foreach ( var flag in flags )
				checksum += flag.checksum;
			return checksum;
		}
	}

    public int lastTimeAttack;

	public Material buoyMaterial;
	public Material standard01AMaterial;

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

		public void UpdateCurrent()
		{
			current = current * ( Constants.Player.productionUpdateFactor ) + production * ( Constants.World.normalSpeedPerSecond * 60 / Constants.Player.productivityUpdateTime ) * ( 1 - Constants.Player.productionUpdateFactor );
			production = 0;
		}

		public void Advance()
		{
			if ( data == null )
				data = new List<float>();

			if ( current > record )
			{
				record = current;
				recordIndex = data.Count;
			}

			data.Add( current );
		}
	}

	public static Team Create()
	{
		return new GameObject( "Team" ).AddComponent<Team>();
	}

	public Team Setup( string name, Color color )
	{
		this.name = name;
		this.color = color;
		for ( int i = 0; i < (int)Item.Type.total; i++ )
		{
			if ( i == (int)Item.Type.plank || i == (int)Item.Type.stone )
				itemHaulPriorities.Add( 1.1f );
			else
				itemHaulPriorities.Add( 1 );
		}

		itemDispatcher = ItemDispatcher.Create();
		itemDispatcher.Setup( this );

		while ( buildingCounts.Count < (int)Building.Type.total )
			buildingCounts.Add( 0 );
		while ( stocksHaveNeed.Count < (int)Item.Type.total )
			stocksHaveNeed.Add( false );

		if ( !CreateMainBuilding() )
		{
			Destroy( this );
			return null;
		}
		chartAdvanceTimer.Start( Constants.Player.productivityAdvanceTime );
		productivityUpdateTimer.Start( Constants.Player.productivityUpdateTime );
		CreateInputWeights();
		base.Setup();

		return this;
	}

	public void SendMessage( string text, HiveObject location )
	{
		var message = new Player.Message{ text = text, location = location };
		foreach ( var player in players )
			player.messages.AddLast( message );
	}

	public Material GetBuoyMaterial()
	{
		if ( buoyMaterial == null )
		{
			buoyMaterial = new Material( World.defaultShader );
			buoyMaterial.color = color;
		}
		return buoyMaterial;
	}

	public Material Get01AMaterial()
	{
		if ( standard01AMaterial == null )
		{
			standard01AMaterial = Instantiate( Resources.Load<Material>( "PolygonFantasyKingdom_Mat_01_A_teamHighlight" ) );
			standard01AMaterial.color = color;
			standard01AMaterial.name = $"Team {name} material";
		}
		return standard01AMaterial;
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
		
		for ( int i = 0; i < (int)Building.Type.total; i++ )
			constructionFactors.Add( i == (int)Workshop.Type.woodcutter || i == (int)Workshop.Type.stonemason ? Constants.Building.importantBuildingConstructionWeight : 1 );
	}

	public InputWeight FindInputWeight( Workshop.Type workshopType, Item.Type itemType )
	{
		foreach ( var w in inputWeights )
			if ( workshopType == w.workshopType && itemType == w.itemType )
				return w;

		return null;
	}

	public bool Attack( Attackable target, int attackerCount, bool checkOnly = false )
	{
		if ( soldierCount < attackerCount || ( target.attackerTeam && target.attackerTeam != this ) )
			return false;

		int sourceCount = 0;
		foreach ( var offset in Ground.areas[Constants.GuardHouse.attackMaxDistance] )
		{
			if ( target.node.Add( offset ).team == this )
				sourceCount++;
		}
		if ( sourceCount == 0 )
			return false;

		if ( checkOnly )
			return true;

		List<Node> gather = new List<Node>();
		for ( int i = 0; i < Constants.Node.neighbourCount; i++ )
		{
			Node n = target.flag.node.Neighbour( i );
			if ( !n.block.IsBlocking( Node.Block.Type.units ) )
				gather.Add( n );
		}

		for ( int i = 0; i < attackerCount; i++ )
		{
			var attacker = Unit.Create().SetupAsAttacker( this, target );
			int attackTime = Math.Max( world.time, lastTimeAttack+Constants.Player.attackPeriod );
			lastTimeAttack = attackTime;
			attacker.ScheduleWait( attackTime - world.time );
			attacker.ScheduleWalkToNeighbour( mainBuilding.flag.node );
			attacker.ScheduleWalkToNode( gather[(i+target.lastSpot)%gather.Count] );
			attacker.building = target;
			target.attackers.Add( attacker );
			soldierCount--;
		}
		target.lastSpot += attackerCount;

		target.team.SendMessage( $"Military building under attack!", target );
		return true;
	}

	public void UpdateStockRoutes( Item.Type itemType )
	{
		var i = (int)itemType;
		bool hasInput = false;
		foreach ( var stock in stocks )
		{
			stock.itemData[i].UpdateRoutes();
			if ( stock.itemData[i].cartInput > 0 )
				hasInput = true;
		}
		stocksHaveNeed[i] = hasInput;
	}

	public void RebuildStockRoutes()
	{
		foreach ( var stock in stocks )
		{
			foreach ( var itemType in stock.itemData )
				itemType.outputRoutes.Clear();
		}

		for ( int i = 0; i < (int)Item.Type.total; i++ )
		UpdateStockRoutes( (Item.Type)i );
	}

	public new void Start()
	{
		transform.SetParent( World.playersAndTeams.transform );
		while ( itemHaulPriorities.Count < (int)Item.Type.total )
			itemHaulPriorities.Add( 1 );
		while ( itemProductivityHistory.Count < (int)Item.Type.total )
			itemProductivityHistory.Add( Chart.Create().Setup( (Item.Type)itemProductivityHistory.Count ) );

		if ( inputWeights == null )
			CreateInputWeights();	// For compatibility with old files
		if ( constructionFactors.Count == 0 )
		{
			for ( int i = 0; i < (int)Building.Type.total; i++ )
				constructionFactors.Add( i == (int)Workshop.Type.woodcutter || i == (int)Workshop.Type.stonemason ? Constants.Building.importantBuildingConstructionWeight : 1 );
		}

		if ( surplus.Length != (int)Item.Type.total )
			surplus = new int[(int)Item.Type.total];
		base.name = "Team " + name;
		base.Start();
	}

	public override void GameLogicUpdate()
	{
		if ( chartAdvanceTimer.done )
		{
			chartAdvanceTimer.Start( Constants.Player.productivityAdvanceTime );

			foreach ( var chart in itemProductivityHistory )
				chart.Advance();
		}
		if ( productivityUpdateTimer.done || productivityUpdateTimer.empty )
		{
			productivityUpdateTimer.Start( Constants.Player.productivityUpdateTime );

			foreach ( var chart in itemProductivityHistory )
				chart.UpdateCurrent();
		}
	}

	bool CreateMainBuilding()
	{
		const int flagDirection = 1;
		Node best = null;
		int closestEnemy = 0;
		float heightdDif = float.MaxValue;
		var area = Building.GetFoundation( true, flagDirection );
		List<Ground.Offset> extendedArea = new List<Ground.Offset>();
		foreach ( var p in area )
		{
			foreach ( var o in Ground.areas[1] )
			{
				var n = p + o;
				if ( o && !extendedArea.Contains( n ) )
					extendedArea.Add( n );
			}
		}

		foreach ( var node in HiveCommon.ground.nodes )
		{
			bool invalidNode = false;
			foreach ( var n in area )
			{
				var localNode = node + n;
				if ( !localNode.CheckType( Node.Type.land ) || localNode.team != null || localNode.block )
					invalidNode = true;
			}
			if ( invalidNode || node.Neighbour( flagDirection ).block )
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
			int localClosestEnemy = int.MaxValue;
			foreach ( var team in world.teams )
			{
				int distance = node.DistanceFrom( team.mainBuilding.node );
				if ( distance < localClosestEnemy )
					localClosestEnemy = distance;

			}
			if ( localClosestEnemy > closestEnemy )
			{
				best = node;
				heightdDif = max - min;
				closestEnemy = localClosestEnemy;
			}
		}

		if ( best == null )
			return false;

		foreach ( var j in extendedArea )
		{
			best.Add( j ).SetHeight( best.height );
			best.Add( j ).team = this;
		}

		Assert.global.IsNull( mainBuilding );
		mainBuilding = Stock.Create().SetupAsMain( best, this, flagDirection );
		return mainBuilding;
	}

	public override void Remove()
	{
		destroyed = true;
		world.teams.Remove( this );
		foreach ( var player in players )
			player.Remove();

		List<Workshop> tmpWorkshops = new List<Workshop>( workshops );
		foreach ( var workshop in tmpWorkshops )
			if ( workshop )
				workshop.Remove();
		List<Stock> tmpStocks = new List<Stock>( stocks );
		foreach ( var stock in tmpStocks )
			if ( stock )
				stock.Remove();
		List<GuardHouse> tmpGuardHouses = new List<GuardHouse>( guardHouses );
		foreach ( var guardHouse in tmpGuardHouses )
			if ( guardHouse )
				guardHouse.Remove();
		List<Flag> tmpFlags = new List<Flag>( flags );
		foreach ( var flag in tmpFlags )
			if ( flag )
				flag.Remove();
		List<Item> tmpItems = new List<Item>( items );
		foreach ( var item in tmpItems )
			if ( item )
				item.Remove();
		itemDispatcher.Remove();
		world.ground.dirtyOwnership = true;
		Destroy( gameObject );
	}

	public void RegisterInfluence( Building building )
	{
		influencers.Add( building );
		world.lastAreaInfluencer = building;
		HiveCommon.ground.dirtyOwnership = true;
	}

	public void UnregisterInfuence( Building building )
	{
		if ( !influencers.Contains( building ) )
			return;

		influencers.Remove( building );
		HiveCommon.ground.dirtyOwnership = true;
	}

	public void RegisterStock( Stock stock )
	{
		stock.assert.AreEqual( stock.team, this );
		stocks.Add( stock );
	}

	public void UnregisterStock( Stock stock )
	{
		stock.assert.AreEqual( stock.team, this );
		stocks.Remove( stock );
	}

	public void RegisterItem( Item item )
	{
		item.assert.AreEqual( item.team, this );
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
		item.assert.AreEqual( item.team, this );
		item.assert.IsTrue( items.Count > item.index && item.index >= 0 );	// Triggered on app quit
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
		int[] bc = new int[(int)Building.Type.total];
		void ProcessList<Type>( List<Type> list ) where Type : Building
		{
			foreach ( var building in list )
			{
				if ( !building.blueprintOnly )
					bc[(int)building.type]++;
			}
		}
		ProcessList( stocks );
		ProcessList( guardHouses );
		ProcessList( workshops );
		for ( int i = 0; i < (int)Building.Type.total; i++ )
			Assert.global.AreEqual( bc[i], buildingCounts[i] );
		foreach ( var flag in flags )
			flag.assert.IsFalse( flag.destroyed );
		foreach ( var road in roads )
			road.assert.IsFalse( road.destroyed );
		foreach ( var workshop in workshops )
			workshop.assert.IsFalse( workshop.destroyed );
		foreach ( var guardHouse in guardHouses )
			guardHouse.assert.IsFalse( guardHouse.destroyed );
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
	public int localVersion = -1;

	public void Attach( Versioned source, bool update = true )
	{
		this.source = source;
		if ( source != null )
			localVersion = source.version - ( update ? 0 : 1 );
	}
	public void Disconnect()
	{
		source = null;
	}
	public bool isAttached { get { return source != null; } }
	public bool status
	{
		get
		{
			if ( isAttached && localVersion != source.version )
			{
				localVersion = source.version;
				return true;
			}
			return false;
		}
	}
}

