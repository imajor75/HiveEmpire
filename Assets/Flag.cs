using Newtonsoft.Json;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;

[SelectionBase]
public class Flag : Assert.Base
{
	public Player owner;
	public const int maxItems = 8;
	public GroundNode node;
	public Item[] items = new Item[maxItems];
	public GameObject[] frames = new GameObject[maxItems];
	public Worker user;
	public Road[] roadsStartingHere = new Road[GroundNode.neighbourCount];
	public Building building;
	static GameObject template;
	public Versioned itemsStored = new Versioned();
	[JsonIgnore]
	public bool debugSpawnPlank;
	public const float itemSpread = 0.3f;

	static public void Initialize()
	{
		template = (GameObject)Resources.Load( "Bizulka/Witchs_house/Prefabs/Pointer" );
		Assert.global.IsNotNull( template );
	}

	public static Flag Create()
	{
		return new GameObject().AddComponent<Flag>();
	}

	public Flag Setup( GroundNode node, Player owner )
    {
		if ( !IsNodeSuitable( node, owner ) )
		{
			Destroy( this );
			return null;
		}

		node.flag = this;
        this.node = node;
		this.owner = owner;
		if ( node.road )
		{
			if ( node.road.ready )
				node.road.Split( this );
			else
			{
				assert.IsTrue( node == node.road.LastNode );
				node.road = null;
			}
		}
		return this;
    }

	void Start()
	{
		gameObject.name = "Flag " + node.x + ", " + node.y;
		transform.SetParent( node.ground.transform );
		transform.localPosition = node.Position;
		var body = Instantiate( template );
		body.transform.SetParent( transform, false );
		UpdateBody();
		for ( int i = 0; i < maxItems; i++ )
		{
			frames[i] = new GameObject();
			frames[i].name = "Item Frame " + i;
			var t = frames[i].transform;
			t.SetParent( transform, false );
			Vector3 pos;
			pos.x = Mathf.Sin( Mathf.PI * 2 / maxItems * i ) * itemSpread * GroundNode.size;
			pos.y = 0;
			pos.z = Mathf.Cos( Mathf.PI * 2 / maxItems * i ) * itemSpread * GroundNode.size;
			t.localPosition = pos;
			t.localScale = 0.1f * Vector3.one;
			t.LookAt( transform );
			if ( items[i] != null )
				items[i].transform.SetParent( frames[i].transform, false );
		}
	}

	void Update()
	{
		if ( debugSpawnPlank )
		{
			if ( FreeSpace() > 0 )
			{
				Item item = Item.Create().Setup( Item.Type.plank, owner.mainBuilding );
				ReserveItem( item );
				FinalizeItem( item );
			}
			debugSpawnPlank = false;
		}
	}

	public void UpdateBody()
	{
		transform.localPosition = node.Position + Vector3.up * GroundNode.size * Road.height;
	}

	public bool ReleaseItem( Item item )
	{
		assert.AreEqual( item.flag, this );
		RemoveItem( item, item.buddy );

		item.flag = null;
		if ( item.buddy )
			item.buddy.buddy = null;
		itemsStored.Trigger();
		return true;
	}

	public bool CancelItem( Item item )
	{
		assert.AreEqual( item.nextFlag, this );
		item.nextFlag = null;
		if ( item.buddy )
		{
			item.buddy = null;
			return true;
		}
		return RemoveItem( item );
	}

	bool RemoveItem( Item item, Item replace = null )
	{
		for ( int i = 0; i < items.Length; i++ )
		{
			if ( items[i] == item )
			{
				items[i] = replace;
				UpdateBody();
				return true;
			}
		}
		item.assert.IsTrue( false, "Item not found at flag" );
		return false;
	}

	public bool ReserveItem( Item item, Item replace = null )
	{
		assert.IsNull( item.nextFlag, "Item already has a flag" );
		if ( replace )
			assert.AreEqual( replace.flag, this );
		for ( int i = 0; i < items.Length; i++ )
		{
			if ( items[i] == replace )
			{
				if ( items[i] )
					items[i].buddy = item;
				else
					items[i] = item;
				item.nextFlag = this;
				UpdateBody();
				return true;
			}
		}
		assert.IsTrue( false );
		return false;
	}

	public Item FinalizeItem( Item item )
	{
		assert.AreEqual( item.nextFlag, this );
		itemsStored.Trigger();

		item.flag = this;
		item.nextFlag = null;

		if ( item.buddy )
		{
			for ( int i = 0; i < maxItems; i++ )
			{
				if ( items[i] == item.buddy )
				{
					Item oldItem = items[i];
					items[i] = item;
					assert.IsNull( oldItem.buddy );
					oldItem.flag = null;
					item.buddy = null;
					return oldItem;
				}
			}
		}
		for ( int i = 0; i < maxItems; i++ )
		{
			if ( items[i] == item )
			{
				item.transform.SetParent( frames[i].transform, false );
				break;
			}
		}
		assert.IsTrue( items.Contains( item ) );
		return null;
	}

	public void OnClicked()
	{
		Interface.FlagPanel.Create().Open( this );
	}

	public bool Remove()
	{
		if ( building && !building.Remove() )
			return false;
		foreach ( var road in roadsStartingHere )
			road?.Remove();
		foreach ( var item in items )
			item?.Remove();

		node.flag = null;
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
        for ( int i = 0; i < GroundNode.neighbourCount; i++ )
            assert.IsNull( node.Neighbour( i ).flag );
		assert.IsTrue( FreeSpace() >= 0 );
		for ( int j = 0; j < maxItems; j++ )
		{
			Item i = items[j];
			if ( i )
			{
				assert.IsTrue( i.flag == this || i.nextFlag == this, "Item is here, but both flag and nextFlag pointers are referencing elsewhere (index: " + j + ")" );
				i.Validate();
			}
		}
		for ( int j = 0; j < GroundNode.neighbourCount; j++ )
			if ( roadsStartingHere[j] && roadsStartingHere[j].nodes[0] == node )
				roadsStartingHere[j].Validate();
		if ( user )
		{
			assert.IsTrue( user.type == Worker.Type.hauler || user.type == Worker.Type.cart );
			if ( user.type == Worker.Type.hauler )
				assert.IsNotNull( user.road );
			assert.IsTrue( user.onRoad );
			assert.AreEqual( user.exclusiveFlag, this );
		}
	}

	static public bool IsNodeSuitable( GroundNode placeToBuildOn, Player owner )
	{
		if ( placeToBuildOn.type == GroundNode.Type.underWater )
			return false;

		if ( placeToBuildOn.IsBlocking( false ) || placeToBuildOn.flag )
			return false;

		foreach ( var o in Ground.areas[1] )
			if ( placeToBuildOn.Add( o ).flag )
				return false;

		if ( placeToBuildOn.owner != owner )
			return false;

		return true;
	}
}
 
