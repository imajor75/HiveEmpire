﻿using System.Collections.Generic;
using UnityEngine;

public class ItemDispatcher : ScriptableObject
{
	public enum Priority
	{
		stop,
		stock,
		low,
		high
	};

	public class Request
	{
		public Building building;
		public int quantity;
		public Priority priority;
	}
	public class Offer
	{
		public Building building;
		public Item item;
		public GroundNode location;
		public int quantity;
		public Priority priority;
	}

	public Market[] markets;

	public void Setup()
	{
	}

	public void Start()
	{
		if ( markets == null || markets.Length != (int)Item.Type.total )
		{
			markets = new Market[(int)Item.Type.total];
			for ( int i = 0; i < (int)Item.Type.total; i++ )
			{
				markets[i] = ScriptableObject.CreateInstance<Market>();
				markets[i].Setup( (Item.Type)i );
			}
		}
	}

	public void RegisterRequest( Building building, Item.Type itemType, int quantity, Priority priority )
	{
		if ( quantity == 0 )
			return;

		Assert.global.IsTrue( quantity > 0 );
		markets[(int)itemType].RegisterRequest( building, quantity, priority );
	}

	public void RegisterOffer( Building building, Item.Type itemType, int quantity, Priority priority )
	{
		if ( quantity == 0 )
			return;

		Assert.global.IsTrue( quantity > 0 );
		markets[(int)itemType].RegisterOffer( building, quantity, priority );
	}

	public void RegisterOffer( Item item, Priority priority )
	{
		markets[(int)item.type].RegisterOffer( item, priority );
	}

	public void LateUpdate()
	{
		foreach ( var market in markets )
			market.LateUpdate();
	}

	public class Market : ScriptableObject
	{
		public Item.Type itemType;
		public List<Offer> offers = new List<Offer>();
		public List<Request> requests = new List<Request>();

		public void Setup( Item.Type itemType )
		{
			this.itemType = itemType;
		}

		public void RegisterRequest( Building building, int quantity, Priority priority )
		{
			var r = new Request();
			r.building = building;
			r.quantity = quantity;
			r.priority = priority;
			requests.Add( r );
		}

		public void RegisterOffer( Building building, int quantity, Priority priority )
		{
			var o = new Offer();
			o.building = building;
			o.quantity = quantity;
			o.priority = priority;
			o.location = building.node;
			offers.Add( o );
		}

		public void RegisterOffer( Item item, Priority priority )
		{
			var o = new Offer();
			o.item = item;
			o.quantity = 1;
			o.priority = priority;
			if ( item.flag )
				o.location = item.flag.node;
			else
				o.location = item.nextFlag.node;

			offers.Add( o );
		}

		public void LateUpdate()
		{
			Priority[] priorities = { Priority.high, Priority.low };
			foreach ( var priority in priorities )
			{
				foreach ( var request in requests )
				{
					if ( request.priority != priority )
						continue;
					while ( request.quantity > 0 && FindOfferFor( request ) )
						;
				}

				foreach ( var offer in offers )
				{
					if ( offer.priority != priority )
						continue;
					while ( offer.quantity > 0 && FindRequestFor( offer ) )
						;
				}
			}

			requests.Clear();
			offers.Clear();
		}

		bool FindOfferFor( Request request )
		{
			Offer bestOffer = null;
			float bestScore = 0;

			foreach ( var offer in offers )
			{
				if ( offer.quantity == 0 )
					continue;
				float score = (int)offer.priority * 1000 + 1f / offer.location.DistanceFrom( request.building.node );
				if ( score > bestScore )
				{
					bestScore = score;
					bestOffer = offer;
				}
			}
			if ( bestOffer != null )
				return Attach( bestOffer, request );    // TODO If this fails, the second best should be used

			return false;
		}

		bool FindRequestFor( Offer offer )
		{
			Request bestRequest = null;
			float bestScore = 0;

			foreach ( var request in requests )
			{
				if ( request.quantity == 0 )
					continue;
				float score = (int)offer.priority * 1f / offer.location.DistanceFrom( request.building.node );
				if ( score > bestScore )
				{
					bestScore = score;
					bestRequest = request;
				}
			}
			if ( bestRequest != null )
				return Attach( offer, bestRequest );    // TODO If this fails, the second best should be used

			return false;
		}

		public bool Attach( Offer offer, Request request )
		{
			bool success = false;
			if ( offer.building != null )
				success = offer.building.SendItem( (Item.Type)itemType, request.building );
			else if ( offer.item != null )
				success = offer.item.SetTarget( request.building );

			if ( !success )
				return false;

			request.quantity--;
			offer.quantity--;

			return true;
		}
	}
}
