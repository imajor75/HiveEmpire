using UnityEngine;

public class Player : ScriptableObject
{
	public float[] itemHaulPriorities = new float[(int)Item.Type.total];
	public ItemDispatcher itemDispatcher;
	public Versioned versionedRoadDelete;

	public static Player Create()
	{
		return ScriptableObject.CreateInstance<Player>();
	}

	public Player Setup()
	{
		for ( int i = 0; i < (int)Item.Type.total; i++ )
		{
			if ( i == (int)Item.Type.plank || i == (int)Item.Type.stone )
				itemHaulPriorities[i] = 1.1f;
			else
				itemHaulPriorities[i] = 1;
		}

		itemDispatcher = ScriptableObject.CreateInstance<ItemDispatcher>();
		itemDispatcher.Setup();

		return this;
	}

	public void LateUpdate()
	{
		itemDispatcher.LateUpdate();
	}

	public struct Versioned
	{
		public int version;

		public void Trigger()
		{
			version++;
		}
	}

	public struct Watch
	{
		Versioned source;
		int localVersion;

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
