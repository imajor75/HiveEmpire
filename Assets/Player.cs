using UnityEngine;

public class Player : ScriptableObject
{
	public float[] itemHaulPriorities = new float[(int)Item.Type.total];

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
		return this;
	}
}
