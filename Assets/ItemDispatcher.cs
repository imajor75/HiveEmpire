using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

public class ItemDispatcher : ScriptableObject
{
	public enum Priority
	{
		stop,
		stock,
		low,
		high
	};

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
	}

	public void RegisterRequest( Building building, Item.Type itemType, int quantity, Priority priority, Ground.Area area )
	{
		Assert.global.IsNotNull( area );
		if ( quantity == 0 )
			return;

		Assert.global.IsTrue( quantity > 0 );
		markets[(int)itemType].RegisterRequest( building, quantity, priority, area );
	}

	public void RegisterOffer( Building building, Item.Type itemType, int quantity, Priority priority, Ground.Area area )
	{
		Assert.global.IsNotNull( area );
		if ( quantity == 0 )
			return;

		Assert.global.IsTrue( quantity > 0 );
		markets[(int)itemType].RegisterOffer( building, quantity, priority, area );
	}

	public void RegisterOffer( Item item, Priority priority, Ground.Area area )
	{
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
		public ItemDispatcher boss;

		public void Setup( ItemDispatcher boss,  Item.Type itemType )
		{
			this.boss = boss;
			this.itemType = itemType;
		}

		public void RegisterRequest( Building building, int quantity, Priority priority, Ground.Area area )
		{
			var r = new Potential();
			r.building = building;
			r.quantity = quantity;
			r.priority = priority;
			r.location = building.flag.node;
			r.type = Potential.Type.request;
			r.area = area;
			requests.Add( r );
		}

		public void RegisterOffer( Building building, int quantity, Priority priority, Ground.Area area )
		{
			var o = new Potential();
			o.building = building;
			o.quantity = quantity;
			o.priority = priority;
			o.location = building.flag.node;
			o.type = Potential.Type.offer;
			o.area = area;
			offers.Add( o );
		}

		public void RegisterOffer( Item item, Priority priority, Ground.Area area )
		{
			var o = new Potential();
			o.item = item;
			o.quantity = 1;
			o.priority = priority;
			if ( item.flag )
				o.location = item.flag.node;
			else
				o.location = item.nextFlag.node;
			o.type = Potential.Type.offer;
			o.area = area;

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
				bool success;
				do
				{
					while ( offers.Count > 0 && offers[0].quantity == 0 )
						offers.RemoveAt( 0 );
					while ( requests.Count > 0 && requests[0].quantity == 0 )
						requests.RemoveAt( 0 );

					int offerCount = 0, requestCount = 0;
					foreach ( var offer in offers )
					{
						if ( offer.priority == priority && offer.quantity > 0 )
							offerCount++;
					}
					foreach ( var request in requests )
					{
						if ( request.priority != priority && request.quantity > 0 )
							requestCount++;
					}

					success = false;
					if ( offerCount < requestCount && offerCount > 0 )
						success = FindPotentialFor( offers[0], requests );
					else if ( requestCount > 0 )
						success = FindPotentialFor( requests[0], offers );
				}
				while ( success );
			}
			int surplus = 0;
			foreach ( var offer in offers )
				surplus += offer.quantity;
			boss.player.surplus[(int)itemType] = surplus;			

			requests.Clear();
			offers.Clear();

			Profiler.EndSample();
		}

		bool FindPotentialFor( Potential potential, List<Potential> list )
		{
			Potential best = null;
			float bestScore = 0;

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
				float score = (int)other.priority * 1000 + 1f / other.location.DistanceFrom( potential.location );
				if ( score > bestScore )
				{
					bestScore = score;
					best = other;
				}
			}
			if ( best != null )
				return Attach( best, potential );    // TODO If this fails, the second best should be used

			return false;
		}

		public bool Attach( Potential first, Potential second )
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
				success = first.building.SendItem( (Item.Type)itemType, second.building, second.priority );
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
