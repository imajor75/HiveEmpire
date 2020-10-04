using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;

[SelectionBase]
public class Item : Assert.Base
{
	public Flag flag;
	public Worker worker;
	public Type type;
	public Ground ground;
	public Path path;
	public Building origin;
	public Building destination;
	static public Sprite[] sprites = new Sprite[(int)Type.total];
	static public Material[] materials = new Material[(int)Type.total];

	public enum Type
    {
        log,
        stone,
        plank,
		fish,
		grain,
		flour,
		salt,
		pretzel,
		hide,
		iron,
		coal,
		gold,
        total,
		unknown = -1
    }

	public static void Initialize()
	{
		string[] filenames = {
			"log",
			"rock",
			"plank",
			"fish",
			"wheat",
			"flour",
			"salt",
			"pretzel",
			"hide",
			"iron",
			"coal",
			"gold"
		};
		var shader = Shader.Find( "Standard" );
		for ( int i = 0; i < (int)Type.total; i++ )
		{
			Texture2D tex = Resources.Load<Texture2D>( filenames[i] );
			sprites[i] = Sprite.Create( tex, new Rect( 0.0f, 0.0f, tex.width, tex.height ), new Vector2( 0.5f, 0.5f ) );
			Assert.global.IsNotNull( sprites[i] );
			materials[i] = new Material( shader );
			materials[i].SetTexture( "_MainTex", tex );
			materials[i].SetInt( "_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One );
			materials[i].SetInt( "_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero );
			materials[i].SetInt( "_ZWrite", 1 );
			materials[i].EnableKeyword( "_ALPHATEST_ON" );
			materials[i].DisableKeyword( "_ALPHABLEND_ON" );
			materials[i].DisableKeyword( "_ALPHAPREMULTIPLY_ON" );
			materials[i].renderQueue = 2450;
		}
	}

	public static Item Create()
	{
		GameObject itemBody = new GameObject();
		return itemBody.AddComponent<Item>();
	}

	public Item Setup( Type type, Building origin, Building destination = null )
	{
		ground = origin.ground;
		this.type = type;
		this.origin = origin;
		if ( destination )
		{
			if ( !SetTarget( destination ) )
			{
				Destroy( gameObject );
				return null;
			}
		}
		UpdateLook();
		return this;
	}

	void Start()
	{
		transform.SetParent( ground.transform );
		transform.localScale *= 0.05f;
		name = type.ToString();
		UpdateLook();
	}

	void Update()
	{
		transform.LookAt( World.instance.eye.transform.position, -Vector3.up );
		if ( path && !path.IsFinished() && path.Road() == null )
			CancelTrip();
		if ( destination == null && worker == null )
			ItemDispatcher.lastInstance.RegisterOffer( this, ItemDispatcher.Priority.high );
	}

	public bool SetTarget( Building building )
	{
		GroundNode current = origin.flag.node;
		if ( flag )
			current = flag.node;

		if ( current == building.flag.node )
		{
			destination = building;
			building.ItemOnTheWay( this );
			Arrived();
			return true;
		}

		path = Path.Between( current, building.flag.node, PathFinder.Mode.onRoad );
		if ( path != null )
		{
			destination = building;
			building.ItemOnTheWay( this );
			return true;
		}
		return false;
	}

	public void CancelTrip()
	{
		path = null;
		destination?.ItemOnTheWay( this, true );
		destination = null;
	}

	public void ArrivedAt( Flag flag )
	{
		if ( destination )
			assert.IsTrue( flag == path.Road().GetEnd( 0 ) || flag == path.Road().GetEnd( 1 ) );

		assert.IsNotNull( worker.reservation );
		assert.IsTrue( flag.reserved > 0 );
		worker.reservation = null;
		flag.reserved--;
		worker = null;
		if ( destination != null && path.IsFinished() )
		{
			assert.AreEqual( destination.flag, flag );
			Arrived();
			return;
		}

		if ( destination == null )
			CancelTrip();	

		if ( flag.StoreItem( this ) )
			UpdateLook();
		else
			Remove();
	}

	public void Arrived()
	{
		if ( flag != null )
			assert.AreEqual( destination.flag, flag );
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
					transform.localPosition = flag.node.Position() + Vector3.up * GroundNode.size / 2 + Vector3.right * i * GroundNode.size / 10;
					return;
				}
			}
			assert.IsTrue( false );
		}
		if ( worker )
		{
			// TODO Put the item in the hand of the worker
			transform.localPosition = worker.transform.localPosition + Vector3.up * GroundNode.size / 2.5f;			;
		}
	}

	public bool Remove()
	{
		CancelTrip();
		Destroy( gameObject );
		return true;
	}

	public void Validate()
	{
		assert.IsTrue( flag || worker );
		if ( flag )
		{
			int s = 0;
			foreach ( var i in flag.items )
				if ( i == this )
					s++;
			assert.AreEqual( s, 1 );
		}
		if ( worker && worker.itemInHands != null )
			assert.AreEqual( this, worker.itemInHands );
		if ( path != null )
			path.Validate();
		if ( destination )
			assert.IsTrue( destination.itemsOnTheWay.Contains( this ) );
	}
}
