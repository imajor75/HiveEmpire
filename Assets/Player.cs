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
		CreateMainBuilding();

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

	void CreateMainBuilding()
	{
		GroundNode center = World.instance.ground.GetCenter(), best = null;
		float heightdDif = float.MaxValue;
		foreach ( var o in Ground.areas[8] )
		{
			GroundNode node = center.Add( o );
			if ( node.type != GroundNode.Type.grass || node.owner != null )
				continue;
			float min, max;
			min = max = node.height;
			for ( int i = 0; i < GroundNode.neighbourCount; i++ )
			{
				if ( node.Neighbour( i ).type != GroundNode.Type.grass )
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

		Assert.global.IsNull( mainBuilding );
		mainBuilding = Stock.Create();
		mainBuilding.SetupMain( World.instance.ground, best, this );
		World.instance.eye.FocusOn( mainBuilding.node );
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


}
