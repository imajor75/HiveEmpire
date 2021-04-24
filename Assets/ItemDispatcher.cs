using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

public class ItemDispatcher : ScriptableObject
{
	public enum Priority
	{
		zero,
		stock,
		low,
		high
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
		public GroundNode location;
		public Building building;
		public Priority priority;
		public int quantity;
		public Type type;
		public Ground.Area area;
		public float weight = 0.5f;
	}

	public Market[] markets;
	public Player player;

	public void Setup( Player player )
	{
		this.player = player;
	}

	public void Start()
	{
		if ( markets == null || markets.Length != (int)Item.Type.total )
		{
			markets = new Market[(int)Item.Type.total];
			for ( int i = 0; i < (int)Item.Type.total; i++ )
			{
				markets[i] = ScriptableObject.CreateInstance<Market>();
				markets[i].Setup( this, (Item.Type)i );
			}
		}
		foreach ( var market in markets )
			market.Start();
	}

	public void RegisterRequest( Building building, Item.Type itemType, int quantity, Priority priority, Ground.Area area, float weight = 0.5f )
	{
		Assert.global.IsNotNull( area );
		if ( quantity == 0 || priority == Priority.zero )
			return;

		Assert.global.IsTrue( quantity > 0 );
		markets[(int)itemType].RegisterRequest( building, quantity, priority, area, weight );
	}

	public void RegisterOffer( Building building, Item.Type itemType, int quantity, Priority priority, Ground.Area area, float weight = 0.5f )
	{
		Assert.global.IsNotNull( area );
		if ( quantity == 0 || priority == Priority.zero )
			return;

		Assert.global.IsTrue( quantity > 0 );
		markets[(int)itemType].RegisterOffer( building, quantity, priority, area, weight );
	}

	public void RegisterOffer( Item item, Priority priority, Ground.Area area )
	{
		if ( priority == Priority.zero )
			return;
		Assert.global.IsNotNull( area );
		markets[(int)item.type].RegisterOffer( item, priority, area );
	}

	public void LateUpdate()
	{
		foreach ( var market in markets )
			market.LateUpdate();
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

		public void RegisterOffer( Building building, int quantity, Priority priority, Ground.Area area, float weight = 0.5f )
		{
			var o = new Potential
			{
				building = building,
				quantity = quantity,
				priority = priority,
				location = building.node,
				type = Potential.Type.offer,
				area = area,
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
				return 0;
			if ( first.priority > second.priority )
				return -1;

			return 1;
		}

		public void LateUpdate()
		{
			offers.Sort( ComparePotentials );
			requests.Sort( ComparePotentials );

			Profiler.BeginSample( "Market" );
			Priority[] priorities = { Priority.high, Priority.low };
			foreach ( var priority in priorities )
			{
				do
				{
					while ( offers.Count > 0 && offers[0].quantity == 0 )
						offers.RemoveAt( 0 );
					while ( requests.Count > 0 && requests[0].quantity == 0 )
						requests.RemoveAt( 0 );

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

					if ( offerItemCount < requestItemCount && offers.Count > 0 )
					{
						FulfillPotentialFrom( offers[0], requests );
						offers.RemoveAt( 0 );
					}
					else if ( requests.Count > 0 )
					{
						FulfillPotentialFrom( requests[0], offers );
						requests.RemoveAt( 0 );
					}
					else
						break;
				}
				while ( ( offers.Count > 0 && offers[0].priority > Priority.stock ) || ( requests.Count > 0 && requests[0].priority > Priority.stock ) );
			}
			int surplus = 0;
			foreach ( var offer in offers )
			{
				// If an item is not needed, it is on its way to a stock, it is not treated as surplus
				if ( offer.item && offer.priority < Priority.high )
					continue;
				int quantity = offer.quantity;
				Stock stock = offer.building as Stock;
				if ( stock && stock.destinations[(int)itemType] )
					quantity -= Stock.Cart.capacity;
				if ( quantity >= 0 )
					surplus += quantity;
			}
			boss.player.surplus[(int)itemType] = surplus;			

			oldRequests = requests;
			oldOffers = offers;
			requests = new List<Potential>();
			offers = new List<Potential>();

			Profiler.EndSample();
		}

		// This function is trying to fulfill a request or offer from a list of offers/requests. Returns true, if the request/offer
		// was fully fulfilled.
		public bool FulfillPotentialFrom( Potential potential, List<Potential> list )
		{
			float maxScore = float.MaxValue;

			while ( potential.quantity != 0 )
			{
				float bestScore = 0;
				Potential best = null;
				foreach ( var other in list )
				{
					if ( other.quantity == 0 )
						continue;
					if ( potential.priority == Priority.stock && other.priority == Priority.stock )
						continue;
					if ( potential.building == other.building )
						continue;
					if ( !potential.area.IsInside( other.location ) )
						continue;
					if ( !other.area.IsInside( potential.location ) )
						continue;
					int distance = other.location.DistanceFrom( potential.location );
					float weightedDistance = distance * ( 1 - potential.weight ) * ( 1 - other.weight ) + 1;
					float score = (int)other.priority * 1000 + 1f / weightedDistance;	// TODO Priorities?
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
					if ( Attach( best, potential ) )
						continue;
				}
				else
					return false;
			}
			return true;
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
			Assert.global.AreEqual( first.type, Potential.Type.offer, "Potential types: " + first.type + ", " + second.type );
			Assert.global.AreEqual( second.type, Potential.Type.request, "Potential types: " + first.type + ", " + second.type );

			bool success = false;
			if ( first.building != null )
				success = first.building.SendItem( itemType, second.building, second.priority );
			else if ( first.item != null )
			{
				Assert.global.AreEqual( first.item.type, itemType );
				success = first.item.SetTarget( second.building, second.priority );
			}

			if ( !success )
				return false;

			first.quantity--;
			second.quantity--;

			return true;
		}
	}
}
