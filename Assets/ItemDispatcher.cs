using System.Collections;
using System.Collections.Generic;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.Assertions;

public class ItemDispatcher : MonoBehaviour
{
	public enum Priority
	{
		stop,
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
		public int quantity;
		public Priority priority;
	}
	public List<Request>[] requests = new List<Request>[(int)Item.Type.total];
	public List<Offer>[] offers = new List<Offer>[(int)Item.Type.total];

	public static ItemDispatcher instance;

	public void RegisterRequest( Building building, Item.Type itemType, int quantity, Priority priority )
	{
		Assert.IsTrue( quantity > 0 );
		var r = new Request();
		r.building = building;
		r.quantity = quantity;
		r.priority = priority;
		if ( priority == Priority.low )
			requests[(int)itemType].Add( r );
		else
			requests[(int)itemType].Insert( 0, r );
	}

	public void RegisterOffer( Building building, Item.Type itemType, int quantity, Priority priority )
	{
		Assert.IsTrue( quantity > 0 );
		var o = new Offer();
		o.building = building;
		o.quantity = quantity;
		o.priority = priority;
		if ( priority == Priority.low )
			offers[(int)itemType].Add( o );
		else
			offers[(int)itemType].Insert( 0, o );
	}

	void Start()
    {
		for ( int i = 0; i < requests.Length; i++ )
			requests[i] = new List<Request>();
		for ( int i = 0; i < requests.Length; i++ )
			offers[i] = new List<Offer>();
		Assert.IsNull( instance );
		instance = this;
	}

	void LateUpdate()
    {
		// TODO Take into account the distance between buildings
		for ( int itemType = 0; itemType < (int)Item.Type.total; itemType++ )
		{
			var r = requests[itemType];
			var o = offers[itemType];

			while ( true )
			{
				while ( r.Count > 0 && r[0].quantity == 0 )
					r.RemoveAt( 0 );
				while ( o.Count > 0 && o[0].quantity == 0 )
					o.RemoveAt( 0 );
				if ( r.Count == 0 || o.Count == 0 || (int)r[0].priority + (int)o[0].priority < 3 )
					break;
				if ( o[0].building.SendItem( (Item.Type)itemType, r[0].building ) )
				{
					r[0].quantity--;
					o[0].quantity--;
				}
				else
				{
					// TODO Figure out if the request or the offer is wrong
					o.RemoveAt( 0 );
				}
			}
		}

		foreach ( var list in requests )
			list.Clear();
		foreach ( var list in offers )
			list.Clear();
	}
}
