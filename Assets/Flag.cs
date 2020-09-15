using UnityEngine;
using UnityEngine.Assertions;

public class Flag : MonoBehaviour
{
	public Player owner;
	public static int maxItems = 8;
	public GroundNode node;
	public Item[] items = new Item[maxItems];
	public Worker user;
	public Road[] roadsStartingHere = new Road[GroundNode.	neighbourCount];
	public Building building;
	static GameObject template;
	public int reserved;

	static public void Initialize()
	{
		template = (GameObject)Resources.Load( "Tresure_box/tresure_box" );
		Assert.IsNotNull( template );
	}

	public static Flag Create()
	{
		GameObject flagObject = GameObject.Instantiate( template );
		flagObject.transform.localScale *= 0.1f;
		return flagObject.AddComponent<Flag>();
	}

	public Flag Setup( Ground ground, GroundNode node, Player owner )
    {
        if ( node.flag )
        {
            Debug.Log( "There is a flag there already" );
			Destroy( gameObject );
			return null;
        }
		if ( node.building )
		{
			Debug.Log( "Cannot create a flag at a building" );
			Destroy( gameObject );
			return null;
		}
		if ( node.resource )
		{
			Debug.Log( "Cannot create a flag on a resource" );
			Destroy( gameObject );
			return null;
		}
		if ( node.road )
		{
			// TODO Make it possible to create a flag at a road
			Debug.Log( "Cannot create a flag at a road" );
			Destroy( gameObject );
			return null;
		}
		if ( node.owner != owner )
		{
			Debug.Log( "Node is outside of border" );
			Destroy( gameObject );
			return null;
		}
        bool hasAdjacentFlag = false;
		for ( int i = 0; i < GroundNode.neighbourCount; i++ )
		{
			if ( node.Neighbour( i ).flag )
				hasAdjacentFlag = true;
		}
        if (hasAdjacentFlag)
        {
            Debug.Log("Another flag is too close");
			Destroy( gameObject );
			return null;
        }
        node.flag = this;
        this.node = node;
		this.owner = owner;
        return this;
    }

	void Start()
	{
		gameObject.name = "Flag " + node.x + ", " + node.y;
		transform.SetParent( node.ground.transform );
		UpdateBody();
	}

	public void UpdateBody()
	{
		transform.localPosition = node.Position() + Vector3.up * GroundNode.size * Road.height;
	}

	public bool ReleaseItem( Item item )
	{
		for ( int i = 0; i < items.Length; i++ )
		{
			if ( items[i] == item )
			{
				items[i] = null;
				item.flag = null;
				return true;
			}
		}
		Assert.IsTrue( false );
		return false;
	}

	public bool StoreItem( Item item )
	{
		int reserved = this.reserved;
		Assert.IsNull( item.flag, "Item already has a flag" );
		for ( int i = 0; i < items.Length; i++ )
		{
			if ( items[i] == null && --reserved <= 0 )
			{
				items[i] = item;
				item.flag = this;
				return true;
			}
		}
		Assert.IsTrue( false );
		return false;
	}

	public void OnClicked()
	{
		FlagPanel.Open( this );
	}

	public void Remove()
	{
		foreach ( var road in roadsStartingHere )
			road?.Remove();
		foreach ( var item in items )
			item?.Remove();
		node.Neighbour( 4 ).building?.Remove();

		Destroy( gameObject );
	}

	public int FreeSpace()
	{
		int free = 0;
		for ( int i = 0; i < maxItems; i++ )
			if ( items[i] == null )
				free++;
		return free - reserved;
	}

	public void Validate()
    {
		if ( building )
			Assert.AreEqual( building.flag, this );
        Assert.AreEqual( this, node.flag );
        for ( int i = 0; i < 6; i++ )
            Assert.IsNull( node.Neighbour( i ).flag );
		foreach ( var i in items )
		{
			if ( i )
			{
				Assert.AreEqual( i.flag, this );
				i.Validate();
			}
		}
		for ( int j = 0; j < 6; j++ )
			if ( roadsStartingHere[j] )
				roadsStartingHere[j].Validate();
	}
}
 
