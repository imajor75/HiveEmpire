using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Assertions;

[SelectionBase]
public class Flag : Assert.Base
{
	public Player owner;
	public static int maxItems = 8;
	public GroundNode node;
	public Item[] items = new Item[maxItems];
	public Worker user;
	public Road[] roadsStartingHere = new Road[GroundNode.	neighbourCount];
	public Building building;
	static GameObject template;
	MeshRenderer itemTable;

	static public void Initialize()
	{
		template = (GameObject)Resources.Load( "Tresure_box/tresure_box_flag" );
		template.transform.localScale = Vector3.one;
		template.transform.rotation = Quaternion.AngleAxis( 135, Vector3.up );
		Assert.global.IsNotNull( template );
	}

	public static Flag Create()
	{
		GameObject flagObject = GameObject.Instantiate( template );
		return flagObject.AddComponent<Flag>();
	}

	public Flag Setup( Ground ground, GroundNode node, Player owner )
    {
		if ( node.IsBlocking() && node.road == null )
		{
			Debug.Log( "That block is already occupied" );
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
        if ( hasAdjacentFlag )
        {
            Debug.Log("Another flag is too close");
			Destroy( gameObject );
			return null;
        }
		node.flag = this;
        this.node = node;
		this.owner = owner;
		if ( node.road )
			node.road.Split( this );
		return this;
    }

	void Start()
	{
		gameObject.name = "Flag " + node.x + ", " + node.y;
		transform.SetParent( node.ground.transform );
		itemTable = World.FindChildRecursive( transform, "ItemTable" ).gameObject.GetComponent<MeshRenderer>();
		assert.IsNotNull( itemTable );
		UpdateBody();
	}

	public void UpdateBody()
	{
		int[] l = new int[(int)Item.Type.total];
		if ( itemTable == null )
			return;
		foreach ( var i in items )
		{
			if ( i != null )
				l[(int)i.type]++;
		}
		int t = 0, b = 0;
		for ( int i = 0; i < l.Length; i++ )
		{
			if ( l[i] > b )
			{
				b = l[i];
				t = i;
			}
		}
		if ( b > 0 )
		{
			itemTable.material = Item.materials[t];
			itemTable.enabled = true;
		}
		else
			itemTable.enabled = false;
		transform.localPosition = node.Position() + Vector3.up * GroundNode.size * Road.height;
	}

	public bool ReleaseItem( Item item )
	{
		assert.AreEqual( item.flag, this );
		CancelItem( item );
		item.flag = null;
		return true;
	}

	public bool CancelItem( Item item )
	{
		for ( int i = 0; i < items.Length; i++ )
		{
			if ( items[i] == item )
			{
				items[i] = null;
				UpdateBody();
				return true;
			}
		}
		assert.IsTrue( false );
		return false;
	}

	public bool ReserveItem( Item item )
	{
		assert.IsNull( item.nextFlag, "Item already has a flag" );
		for ( int i = 0; i < items.Length; i++ )
		{
			if ( items[i] == null )
			{
				items[i] = item;
				item.nextFlag = this;
				UpdateBody();
				return true;
			}
		}
		assert.IsTrue( false );
		return false;
	}

	public void OnClicked()
	{
		Interface.FlagPanel.Create().Open( this );
	}

	public bool Remove()
	{
		if ( building && !building.Remove() )
			return false;
		node.flag = null;
		foreach ( var road in roadsStartingHere )
			road?.Remove();
		foreach ( var item in items )
			item?.Remove();

		Destroy( gameObject );
		return true;
	}

	public int FreeSpace()
	{
		int free = 0;
		for ( int i = 0; i < maxItems; i++ )
			if ( items[i] == null )
				free++;
		return free;
	}

	public void Validate()
    {
		if ( building )
			assert.AreEqual( building.flag, this );
        assert.AreEqual( this, node.flag );
        for ( int i = 0; i < 6; i++ )
            assert.IsNull( node.Neighbour( i ).flag );
		assert.IsTrue( FreeSpace() >= 0 );
		foreach ( var i in items )
		{
			if ( i )
			{
				assert.IsTrue( i.flag == this || i.nextFlag == this );
				i.Validate();
			}
		}
		for ( int j = 0; j < 6; j++ )
			if ( roadsStartingHere[j] && roadsStartingHere[j].nodes[0] == node )
				roadsStartingHere[j].Validate();
		if ( user )
		{
			assert.AreEqual( user.type, Worker.Type.haluer );
			assert.IsNotNull( user.road );
			assert.IsTrue( user.atRoad );
			assert.AreEqual( user.exclusiveFlag, this );
		}
	}
}
 
