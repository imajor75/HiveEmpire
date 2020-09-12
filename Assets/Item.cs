using TMPro;
using UnityEngine;
using UnityEngine.Assertions;

public class Item : MonoBehaviour
{
	public Flag flag;
	public Worker worker;
	public Type type;
	public Ground ground;
	public Path path;
	public Building origin;
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

	public static Item Create()
	{
		GameObject itemBody = new GameObject();
		itemBody.name = "Item";
		itemBody.AddComponent<SpriteRenderer>();
		return itemBody.AddComponent<Item>();
	}

	public Item Setup( Type type, Building origin, Building destination )
	{
		ground = destination.ground;
		this.type = type;
		this.origin = origin;
		if ( destination )
		{
			if ( !SetTarget( destination ) )
			{
				Destroy( gameObject );
				return null;
			}
			destination.ItemOnTheWay( this );
		}
		UpdateLook();
		return this;
	}

	static void CancelNew()
	{
	}

	void Start()
	{
		transform.SetParent( ground.transform );
		transform.localScale *= 0.1f;
		gameObject.GetComponent<SpriteRenderer>().sprite = sprites[(int)type];
		name = type.ToString();
		UpdateLook();
	}

	void Update()
	{
		transform.LookAt( Camera.main.transform.position, -Vector3.up );
	}

	public bool SetTarget( Building building )
	{
		GroundNode current = origin.flag.node;
		if ( flag )
			current = flag.node;

		path = Path.Between( current, building.flag.node, PathFinder.Mode.onRoad );
		if ( path != null )
		{
			destination = building;
			return true;
		}
		return false;
	}

	public void CancelTrip()
	{
		path = null;
		destination?.ItemOnTheWay( this, true );
	}

	public void ArrivedAt( Flag flag )
	{
		Assert.IsTrue( flag == path.Road().GetEnd( 0 ) || flag == path.Road().GetEnd( 1 ) );

		worker = null;
		if ( path.IsFinished() && destination != null )
		{
			Assert.AreEqual( destination.flag, flag );
			destination.ItemArrived( this );
			GetComponent<SpriteRenderer>().enabled = false;
			Destroy( gameObject );
			return;
		}

		if ( destination == null )
			CancelTrip();

		flag.StoreItem( this );
		UpdateLook();
	}

	public void Arrived()
	{
		destination.ItemArrived( this );
		Destroy( gameObject );
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
					transform.localPosition = flag.node.Position() + Vector3.up * GroundNode.size / 2 + Vector3.right * i * GroundNode.size / 6;
					return;
				}
			}
			Assert.IsTrue( false );
		}
		if ( worker )
		{
			// TODO Put the item in the hand of the worker
			transform.localPosition = worker.transform.localPosition + Vector3.up * GroundNode.size / 1.5f;			;
		}
	}

	public void Remove()
	{
		CancelTrip();
		Destroy( gameObject );
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
		if ( worker && worker.itemInHands != null )
			Assert.AreEqual( this, worker.itemInHands );
		if ( path != null )
			path.Validate();
	}
}
