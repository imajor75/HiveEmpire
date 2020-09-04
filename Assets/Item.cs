using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

public class Item : MonoBehaviour
{
	public Flag flag;
	public Worker worker;
	public Type type;
	public Ground ground;
	public PathFinder path;
	public int pathProgress;
	public Building destination;
	static public Sprite[] sprites = new Sprite[(int)Type.total];

    public enum Type
    {
        wood,
        stone,
        plank,
        total,
		unknown = -1
    }

	public static Item Create()
	{
		GameObject itemBody = new GameObject();
		itemBody.name = "Item";
		itemBody.AddComponent<SpriteRenderer>();
		return itemBody.AddComponent<Item>();
	}

	public static void Initialize()
	{
		string[] filenames = { "log", "log", "plank", "log","log","log","log","log","log","log","log","log","log","log","log","log","log","log","log","log", };
		for ( int i = 0; i < (int)Type.total; i++ )
		{
			Texture2D tex = Resources.Load<Texture2D>( filenames[i] );
			sprites[i] = Sprite.Create( tex, new Rect( 0.0f, 0.0f, tex.width, tex.height ), new Vector2( 0.5f, 0.5f ) );
			Assert.IsNotNull( sprites[i] );
		}
	}

	static public Item CreateNew( Type type, Ground ground, Flag flag, Building destination )
	{
		var item = Create();
		item.ground = ground;
		item.type = type;
		if ( flag && !flag.StoreItem( item ) )
		{
			Destroy( item.gameObject );
			return null;
		}
		if ( destination )
		{
			if ( !item.SetTarget( destination ) )
			{
				if ( flag )
					flag.ReleaseItem( item );
				Destroy( item.gameObject );
				return null;
			}
			destination.ItemOnTheWay( item );
		}
		item.UpdateLook();
		return item;
	}

	static void CancelNew()
	{
	}

	void Start()
	{
		transform.SetParent( ground.transform );
		transform.localScale *= 0.1f;
		gameObject.GetComponent<SpriteRenderer>().sprite = sprites[(int)type];
		UpdateLook();
	}

	void Update()
	{
		transform.LookAt( Camera.main.transform.position, -Vector3.up );
	}

	public bool SetTarget( Building building )
	{
		path = new PathFinder();
		pathProgress = 0;
		if ( path.FindPathBetween( this.flag.node, building.flag.node, PathFinder.Mode.onRoad ) )
		{
			destination = building;
			return true;
		}
		else
		{
			path = null;
			return false;
		}
	}

	public void UpdateLook()	
	{
		if ( flag )
		{
			for ( int i = 0; i < Flag.maxItems; i++ )
			{
				if ( flag.items[i] == this )
				{
					// TODO Arrange the items around the flag
					transform.localPosition = flag.node.Position() + Vector3.up * GroundNode.size + Vector3.right * i * GroundNode.size / 6;
					return;
				}
			}
			Assert.IsTrue( false );
		}
		if ( worker )
		{
			// TODO Put the item in the hand of the worker
			transform.localPosition = worker.transform.localPosition + Vector3.up * GroundNode.size;
		}
	}

	public void Validate()
	{
		Assert.IsTrue( flag || worker );
		if ( flag )
		{
			int s = 0;
			foreach ( var i in flag.items )
				if ( i == this )
					s++;
			Assert.AreEqual( s, 1 );
		}
		if ( worker )
			Assert.AreEqual( this, worker.item );
		if ( path != null )
			Assert.IsTrue( pathProgress < path.roadPath.Count );
	}
}
