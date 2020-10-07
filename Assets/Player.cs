using System.Collections.Generic;
using UnityEngine;

public class Player : ScriptableObject
{
	public List<float> itemHaulPriorities = new List<float>();
	public ItemDispatcher itemDispatcher;
	public Versioned versionedRoadDelete = new Versioned();

	public int soldiersProduced = 0;
	public int bowmansProduced = 0;
	public int coinsProduced = 0;

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
		itemDispatcher.Setup();

		return this;
	}

	public void Start()
	{
		while ( itemHaulPriorities.Count < (int)Item.Type.total )
			itemHaulPriorities.Add( 1 );
		itemDispatcher.Start();
	}

	public void LateUpdate()
	{
		itemDispatcher.LateUpdate();
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
}
