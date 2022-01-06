using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

public class ItemDispatcher : HiveObject
{
	public enum Priority
	{
		zero,
		stock,
		low,
		high
	};

	public enum Result
	{
		match,
		flagJam,
		noDispatcher,
		notInArea,
		tooLowPriority,
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
		public Priority priority;
		public int quantity;
		public Type type;
		public Ground.Area area;
		public float weight = 0.5f;
		public bool flagJammed;
		public bool noDispenser;
		public int id { get { return building ? building.id : item.id; } }
	}

	public List<Market> markets = new List<Market>();
	public Building queryBuilding;
	public Item.Type queryItemType;
	public Potential.Type queryType;
	public bool fullTracking;

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
		public Priority priority;
	}

	public List<LogisticResult> results, resultsInThisFrame;

    public override Node location => throw new System.NotImplementedException();

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
		base.Setup();
	}

	new public void Start()
	{
		name = "Item displatcher";
		transform.SetParent( world.transform );

		foreach ( var market in markets )
			market.Start();
			
		base.Start();
	}

	public void RegisterRequest( Building building, Item.Type itemType, int quantity, Priority priority, Ground.Area area, float weight = 0.5f )
	{
		Assert.global.IsNotNull( area );
		if ( quantity < 0 )
			quantity = 0;
		if ( priority == Priority.zero )
			return;
		if ( quantity == 0 && !fullTracking )
			return;

		markets[(int)itemType].RegisterRequest( building, quantity, priority, area, weight );
	}

	public void RegisterOffer( Building building, Item.Type itemType, int quantity, Priority priority, Ground.Area area, float weight = 0.5f, bool flagJammed = false, bool noDispenser = false )
	{
		Assert.global.IsNotNull( area );
		if ( priority == Priority.zero )
			return;
		if ( quantity == 0 && !fullTracking )
			return;

		markets[(int)itemType].RegisterOffer( building, quantity, priority, area, weight, flagJammed, noDispenser );
	}

	public void RegisterOffer( Item item, Priority priority, Ground.Area area )
	{
		if ( priority == Priority.zero )
			return;

		Assert.global.IsNotNull( area );
		markets[(int)item.type].RegisterOffer( item, priority, area );
	}

	public void FixedUpdate()
	{
        if ( oh && oh.frameFinishPending )
            return;
		foreach ( var market in markets )
			market.FixedUpdate();

		results = resultsInThisFrame;
		if ( queryBuilding )
			resultsInThisFrame = new List<LogisticResult>();
		else
			resultsInThisFrame = null;
	}

	public class Market : ScriptableObject
	{
		public Item.Type itemType;
		public List<Potential> offers = new List<Potential>();
		public List<Potential> requests = new List<Potential>();
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

		public void RegisterRequest( Building building, int quantity, Priority priority, Ground.Area area, float weight = 0.5f )
		{
			var r = new Potential
			{
				building = building,
				quantity = quantity,
				priority = priority,
				location = building.node,
				type = Potential.Type.request,
				area = area,
				weight = weight
			};
			requests.Add( r );
		}

		public void RegisterOffer( Building building, int quantity, Priority priority, Ground.Area area, float weight = 0.5f, bool flagJammed = false, bool noDispenser = false )
		{
			var o = new Potential
			{
				building = building,
				quantity = quantity,
				priority = priority,
				location = building.node,
				type = Potential.Type.offer,
				area = area,
				flagJammed = flagJammed,
				noDispenser = noDispenser,
				weight = weight
			};
			offers.Add( o );
		}

		public void RegisterOffer( Item item, Priority priority, Ground.Area area, float weight = 0.5f )
		{
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
			o.weight = weight;

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

		public void FixedUpdate()
		{
			offers.Sort( ComparePotentials );
			requests.Sort( ComparePotentials );

			Priority[] priorities = { Priority.high, Priority.low };
			int nextOffer = 0, nextRequest = 0;
			foreach ( var priority in priorities )
			{
				do
				{
					int offerItemCount = 0, requestItemCount = 0;
					foreach ( var offer in offers )
					{
						if ( offer.priority >= priority )
							offerItemCount += offer.quantity;
					}
					foreach ( var request in requests )
					{
						if ( request.priority >= priority )
							requestItemCount += request.quantity;
					}

					// TODO what if one of these is zero? if priority is high, it is still possible that there are potentials which can be paired then, but we should not start with the one which has zero items
					// For example when lots of woodcutters offer log with low priority, so the high priority offers are zero, but there is a request with high priority somewhere? In that case are we finding
					// pair for a random offer?
					if ( offerItemCount < requestItemCount && offers.Count > nextOffer )
						FulfillPotentialFrom( offers[nextOffer++], requests, nextRequest );
					else if ( requests.Count > nextRequest )
						FulfillPotentialFrom( requests[nextRequest++], offers, nextOffer );
					else
						break;
				}
				while ( ( offers.Count > nextOffer && offers[nextOffer].priority > Priority.stock ) || ( requests.Count > nextRequest && requests[nextRequest].priority > Priority.stock ) );
			}
			int surplus = 0;
			foreach ( var offer in offers )
			{
				// If an item is not needed, it is on its way to a stock, it is not treated as surplus
				if ( offer.item && offer.priority < Priority.high )
					continue;
				int quantity = offer.quantity;
				Stock stock = offer.building as Stock;
				if ( stock && stock.itemData[(int)itemType].outputRoutes.Count > 0 )
					quantity -= Constants.Stock.cartCapacity;
				if ( quantity >= 0 )  
					surplus += quantity;
			}
			boss.team.surplus[(int)itemType] = surplus;			

			oldRequests = requests;
			oldOffers = offers;
			requests = new List<Potential>();
			offers = new List<Potential>();
		}

		// This function is trying to fulfill a request or offer from a list of offers/requests. Returns true, if the request/offer
		// was fully fulfilled.
		public bool FulfillPotentialFrom( Potential potential, List<Potential> list, int startIndex = 0 )
		{
			float maxScore = float.MaxValue;

			// Let empty potentials have a loop, just to have some statistics show to the user.
			do
			{
				float bestScore = 0;
				Potential best = null;
				for ( int i = startIndex; i < list.Count; i++ )
				{
					var other = list[i];
					if ( potential.building == other.building )
						continue;
					if ( !IsGoodFor( other, potential ) )
						continue;
					if ( !IsGoodFor( potential, other ) )
						continue;
					if ( potential.priority <= Priority.stock && other.priority <= Priority.stock )
					{
						ConsiderResult( potential, other, Result.tooLowPriority );
						continue;
					}
					ConsiderResult( potential, other, Result.match );
					int distance = other.location.DistanceFrom( potential.location );
					float weightedDistance = distance * ( 1 - potential.weight ) * ( 1 - other.weight ) + 1;
					float score = (int)other.priority * 1000 + 1f / weightedDistance;   // TODO Priorities?
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

			boss.resultsInThisFrame.Add( new LogisticResult
			{
				building = potential.building,
				incoming = potential.type == Potential.Type.offer,
				priority = potential.priority,
				quantity = potential.quantity,
				result = result,
				remote = remote
			} );
		}

		void ConsiderResult( Potential first, Potential second, Result result )
		{
			if ( boss.resultsInThisFrame == null || boss.queryItemType != itemType )
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
