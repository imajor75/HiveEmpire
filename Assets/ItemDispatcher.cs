using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using Newtonsoft.Json;

public class ItemDispatcher : HiveObject
{
	public enum Category
	{
		zero,
		reserve,		// when the item is only stored here because it is not needed elsewhere
		prepare,		// when a stock is about to collect items for a nearby workshop to use
		work,			// when the item would be used, like a workshop
	};

	public enum Result
	{
		match,
		flagJam,
		noDispatcher,
		notInArea,
		tooLowPriority,
		betweenStocksDisabled,
		outOfItems
	};

	[System.Serializable]
	public class Potential
	{
		public enum Type
		{
			offer,
			request
		}

		public Item item;
		public Node location;
		public Building building;
		public Category priority;
		public int quantity;
		public Type type;
		public Ground.Area area;
		public float importance = 0.5f;
		public bool flagJammed;
		public bool stock;
		public bool noDispenser;
		public int id 
		{ 
			get 
			{ 
				if ( building )
					return building.id;
					
				if ( item )
					return item.id; 

				return -1;
			} 
		}
		[Obsolete( "Compatibility with old files", true )]
		float weight { set { importance = value; } }
	}

	public List<Market> markets = new ();
	public Building queryBuilding;
	public Item.Type queryItemType;
	public Potential.Type queryType;
	public bool fullTracking;
	[JsonIgnore]
	public Item.Type dump = Item.Type.unknown;

	override public UpdateStage updateMode => UpdateStage.turtle;

	[Obsolete( "Compatibility with old files", true )]
	public Player player { set { team = value.team; } }

	[System.Serializable]
	public class LogisticResult
	{
		public Building building;
		public Result result;
		public bool incoming;
		public bool remote; // This is true if the result is determined by the remote building, not queryBuilding
		public int quantity;
		public float importance;
		public Category category;
	}

	public List<LogisticResult> results, resultsInThisCycle;
	[Obsolete( "Compatibility with old files", true )]
	List<LogisticResult> resultsInThisFrame { set { resultsInThisCycle = value; } }

    static public ItemDispatcher Create()
	{
		return new GameObject().AddComponent<ItemDispatcher>();
	}

	public void Setup( Team team )
	{
		this.team = team;
		for ( int i = 0; i < (int)Item.Type.total; i++ )
		{
			markets.Add( ScriptableObject.CreateInstance<Market>() );
			markets[i].Setup( this, (Item.Type)i );
		}
		base.Setup( team.world );
	}

	new public void Start()
	{
		name = "Item dispatcher";
		transform.SetParent( game.transform, false );

		foreach ( var market in markets )
			market.Start();
			
		base.Start();
	}

	public override void Remove()
	{
		foreach ( var market in markets )
			Eradicate( market );
		base.Remove();
	}

	public void RegisterRequest( Building building, Item.Type itemType, int quantity, Category priority, Ground.Area area, float weight = 0.5f )
	{
		Assert.global.IsNotNull( area );
		if ( quantity < 0 )
			quantity = 0;
		if ( priority == Category.zero )
			return;
		if ( quantity == 0 && !fullTracking )
			return;
		if ( weight == 0 )
			return;

		markets[(int)itemType].RegisterRequest( building, quantity, priority, area, weight );
	}

	public void RegisterOffer( Building building, Item.Type itemType, int quantity, Category priority, Ground.Area area, bool flagJammed = false, bool noDispenser = false )
	{
		Assert.global.IsNotNull( area );
		if ( priority == Category.zero )
			return;
		if ( quantity == 0 && !fullTracking )
			return;

		markets[(int)itemType].RegisterOffer( building, quantity, priority, area, flagJammed, noDispenser );
	}

	public void RegisterOffer( Item item, Category priority, Ground.Area area )
	{
		if ( priority == Category.zero )
			return;

		Assert.global.IsNotNull( area );
		markets[(int)item.type].RegisterOffer( item, priority, area );
	}

	public override void GameLogicUpdate( UpdateStage stage )
	{
		Assert.global.AreEqual( game.updateStage, UpdateStage.turtle );
		foreach ( var market in markets )
			market.GameLogicUpdate();

		results = resultsInThisCycle;
		if ( queryBuilding )
			resultsInThisCycle = new ();
		else
			resultsInThisCycle = null;
	}

	public class Market : ScriptableObject
	{
		public Item.Type itemType;
		public List<Potential> offers = new ();
		public List<Potential> requests = new ();
		public List<Potential> oldOffers;
		public List<Potential> oldRequests;
		public ItemDispatcher boss;

		public void Setup( ItemDispatcher boss,  Item.Type itemType )
		{
			this.boss = boss;
			this.itemType = itemType;
		}

		public void Start()
		{
			name = itemType + " market";
		}

		public void RegisterRequest( Building building, int quantity, Category priority, Ground.Area area, float weight = 0.5f )
		{
			building.assert.AreEqual( game.updateStage, UpdateStage.turtle );
			var r = new Potential
			{
				building = building,
				stock = building is Stock && building.construction.done,
				quantity = quantity,
				priority = priority,
				location = building.node,
				type = Potential.Type.request,
				area = area,
				importance = weight
			};
			requests.Add( r );
		}

		public void RegisterOffer( Building building, int quantity, Category priority, Ground.Area area, bool flagJammed = false, bool noDispenser = false )
		{
			building.assert.AreEqual( game.updateStage, UpdateStage.turtle );
			var o = new Potential
			{
				building = building,
				stock = building is Stock,
				quantity = quantity,
				priority = priority,
				location = building.node,
				type = Potential.Type.offer,
				area = area,
				flagJammed = flagJammed,
				noDispenser = noDispenser,
			};
			offers.Add( o );
		}

		public void RegisterOffer( Item item, Category priority, Ground.Area area, float weight = 0.5f )
		{
			item.assert.AreEqual( game.updateStage, UpdateStage.turtle );
			var o = new Potential
			{
				item = item,
				quantity = 1,
				priority = priority
			};
			if ( item.flag )
				o.location = item.flag.node;
			else
				o.location = item.nextFlag.node;
			o.type = Potential.Type.offer;
			o.area = area;
			o.importance = weight;

			offers.Add( o );
		}

		static int ComparePotentials( Potential first, Potential second )
		{
			if ( first.priority == second.priority )
				return first.id.CompareTo( second.id );
			if ( first.priority > second.priority )
				return -1;

			return 1;
		}

		public void GameLogicUpdate()
		{
			if ( boss.dump == itemType )
			{
				Log( "Offers" );
				for ( int i = 0; i < offers.Count; i++ )
					Log( $" {i} {offers[i].building} {offers[i].quantity} {offers[i].location} {offers[i].priority}" );

				Log( "Requests" );
				for ( int i = 0; i < requests.Count; i++ )
					Log( $" {i} {requests[i].building} {requests[i].quantity} {requests[i].location} {requests[i].priority} {requests[i].importance}" );

				boss.dump = Item.Type.unknown;
			}

			// TODO what if one of these is zero? if priority is high, it is still possible that there are potentials which can be paired then, but we should not start with the one which has zero items
			// For example when lots of woodcutters offer log with low priority, so the high priority offers are zero, but there is a request with high priority somewhere? In that case are we finding
			// pair for a random offer?
			if ( offers.Count < requests.Count )
			{
				foreach ( var offer in offers )
					FulfillPotentialFrom( offer, requests );
			}
			else
			{
				foreach ( var request in requests )
					FulfillPotentialFrom( request, offers );
			}
			int surplus = 0;
			foreach ( var offer in offers )
			{
				// If an item is not needed, it is on its way to a stock, it is not treated as surplus
				if ( offer.item && offer.priority < Category.prepare )
					continue;
				int quantity = offer.quantity;
				Stock stock = offer.building as Stock;
				if ( quantity >= 0 )  
					surplus += quantity;
			}
			boss.team.surplus[(int)itemType] = surplus;			

			oldRequests = requests;
			oldOffers = offers;
			requests = new ();
			offers = new ();
		}

		// This function is trying to fulfill a request or offer from a list of offers/requests. Returns true, if the request/offer
		// was fully fulfilled.
		public bool FulfillPotentialFrom( Potential potential, List<Potential> list )
		{
			float maxScore = float.MaxValue;

			// Let empty potentials have a loop, just to have some statistics to show to the user.
			do
			{
				float bestScore = 0;
				Potential best = null;
				foreach ( var other in list )
				{
					if ( potential.building == other.building )
						continue;
					if ( other.importance == 0 )
						continue;
					if ( !IsGoodFor( other, potential ) )
						continue;
					if ( !IsGoodFor( potential, other ) )
						continue;
					if ( potential.stock && other.stock )
					{
						ConsiderResult( potential, other, Result.betweenStocksDisabled );
						continue;
					}
					if ( potential.priority <= Category.reserve && other.priority <= Category.reserve )
					{
						ConsiderResult( potential, other, Result.tooLowPriority );
						continue;
					}
					ConsiderResult( potential, other, Result.match );
					int distance = other.location.DistanceFrom( potential.location );
					float score = 10 + 1f / distance * potential.importance * other.importance;
					if ( score >= maxScore )
						continue;
					if ( score > bestScore )
					{
						bestScore = score;
						best = other;
					}
				}
				if ( best != null )
				{
					maxScore = bestScore;
					if ( potential.quantity == 0 )
						return true;
					if ( Attach( best, potential ) )
						continue;
				}
				else
					return false;
			} while ( potential.quantity != 0 );
			return true;
		}

		bool IsGoodFor( Potential first, Potential second )
		{
			Result r = Result.match;
			if ( first.flagJammed )
				r = Result.flagJam;
			else if ( first.noDispenser || ( first.building && first.building.itemDispatchedThisFrame ) )
				r = Result.noDispatcher;
			else if ( first.quantity == 0 )
				r = Result.outOfItems;
			else if ( !first.area.IsInside( second.location ) )
				r = Result.notInArea;

			if ( r != Result.match )
				ConsiderResult( first, second, r );

			return r == Result.match;
		}

		void AddResult( Potential potential, Result result, bool remote )
		{
			if ( potential.building == null )
				return;

			boss.resultsInThisCycle.Add( new LogisticResult
			{
				building = potential.building,
				incoming = potential.type == Potential.Type.offer,
				category = potential.priority,
				quantity = potential.quantity,
				importance = potential.importance,
				result = result,
				remote = remote
			} );
		}

		void ConsiderResult( Potential first, Potential second, Result result )
		{
			if ( boss.resultsInThisCycle == null || boss.queryItemType != itemType )
				return;
			if ( boss.queryBuilding == first.building && boss.queryType == first.type )
				AddResult( second, result, false );
			if ( boss.queryBuilding == second.building && boss.queryType == second.type )
				AddResult( first, result, true );
		}

		// This function is trying to send an item from an offer to a request. Returns true if the item was sent, otherwise false.
		// When an item was sent, the quantity data member of both the offer and the request is decreased by one.
		bool Attach( Potential first, Potential second )
		{
			if ( first.type == Potential.Type.request )
			{
				var temp = first;
				first = second;
				second = temp;
			}
			Assert.global.AreEqual( first.type, Potential.Type.offer, $"Potential types: {first.type}, {second.type}" );
			Assert.global.AreEqual( second.type, Potential.Type.request, $"Potential types: {first.type}, {second.type}" );
			World.CRC( first.id + second.id, OperationHandler.Event.CodeLocation.itemDispatcherAttach );

			bool success = false;
			if ( first.building != null )
				success = first.building.SendItem( itemType, second.building, second.priority );
			else if ( first.item != null )
			{
				Assert.global.AreEqual( first.item.type, itemType );
				success = first.item.SetTarget( second.building, second.priority );
			}

			if ( !success )
			{
				first.building?.UpdateIsolated();
				if ( first.item )
					first.item.roadNetworkChangeListener.Attach( first.item.team.versionedRoadNetworkChanged );
				second.building.UpdateIsolated();
				return false;
			}

			first.quantity--;
			second.quantity--;

			return true;
		}
	}
}
