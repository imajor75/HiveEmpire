﻿using System.Collections.Generic;
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

	public enum Result
	{
		match,
		flagJam,
		noDispatcher,
		notInArea,
		otherAreaExcludes,
		tooLowPriority,
		outOfItems,
		full
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
	public Building queryBuilding;
	public Item.Type queryItemType;

	[System.Serializable]
	public class PotentialResult
	{
		public Building building;
		public Result result;
		public bool incoming;
		public Priority priority;
	}

	public List<PotentialResult> results, resultsInThisFrame;

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
		if ( priority == Priority.zero )
			return;

		markets[(int)itemType].RegisterRequest( building, quantity, priority, area, weight );
	}

	public void RegisterOffer( Building building, Item.Type itemType, int quantity, Priority priority, Ground.Area area, float weight = 0.5f )
	{
		Assert.global.IsNotNull( area );
		if ( priority == Priority.zero )
			return;

		markets[(int)itemType].RegisterOffer( building, quantity, priority, area, weight );
	}

	public void RegisterOffer( Item item, Priority priority, Ground.Area area )
	{
		if ( priority == Priority.zero )
			return;

		Assert.global.IsNotNull( area );
		markets[(int)item.type].RegisterOffer( item, priority, area );
	}

	public void RegisterResult( Building building, Item.Type itemType, Result result )
	{
		if ( resultsInThisFrame == null || building != queryBuilding || itemType != queryItemType )
			return;

		resultsInThisFrame.Add( new PotentialResult
		{
			result = result
		} );
	}

	public void RegisterResult( Item.Type itemType, Potential first, Potential second, Result result )
	{
		if ( resultsInThisFrame == null || itemType != queryItemType )
			return;

		if ( first.building == queryBuilding && second.building )
		{
			resultsInThisFrame.Add( new PotentialResult
			{
				building = second.building,
				priority = second.priority,
				incoming = second.type == Potential.Type.offer,
				result = result
			} ); ;
		}
		//if ( second.building == queryBuilding )
		//{
		//	if ( result == Result.notInArea )
		//		result = Result.otherAreaExcludes;
		//	else if ( result == Result.otherAreaExcludes )
		//		result = Result.notInArea;

		//	resultsInThisFrame.Add( new PotentialResult
		//	{
		//		building = first.building,
		//		priority = first.priority,
		//		incoming = first.type == Potential.Type.offer,
		//		result = result
		//	} );
		//}
	}

	public void LateUpdate()
	{
		foreach ( var market in markets )
			market.LateUpdate();

		results = resultsInThisFrame;
		if ( queryBuilding )
			resultsInThisFrame = new List<PotentialResult>();
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

		void RegisterResult( Potential first, Potential second, Result result )
		{
			boss.RegisterResult( itemType, first, second, result );
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

					if ( offerItemCount < requestItemCount && offers.Count > nextOffer )
						FulfillPotentialFrom( offers[nextOffer++], requests );
					else if ( requests.Count > nextRequest )
						FulfillPotentialFrom( requests[nextRequest++], offers );
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

			// Let empty potentials have a loop, just to have some statistics show to the user.
			do
			{
				float bestScore = 0;
				Potential best = null;
				foreach ( var other in list )
				{
					if ( other.quantity == 0 )
					{
						if ( other.type == Potential.Type.offer )
							RegisterResult( potential, other, Result.outOfItems );
						else
							RegisterResult( potential, other, Result.full );
						continue;
					}
					if ( potential.priority == Priority.stock && other.priority == Priority.stock )
					{
						RegisterResult( potential, other, Result.tooLowPriority );
						continue;
					}
					if ( potential.building == other.building )
						continue;
					if ( !potential.area.IsInside( other.location ) )
					{
						RegisterResult( potential, other, Result.notInArea );
						continue;
					}
					if ( !other.area.IsInside( potential.location ) )
					{
						RegisterResult( potential, other, Result.otherAreaExcludes );
						continue;
					}
					RegisterResult( potential, other, Result.match );
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
