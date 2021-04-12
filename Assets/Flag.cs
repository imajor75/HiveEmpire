using Newtonsoft.Json;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;

[SelectionBase]
public class Flag : HiveObject
{
	public Player owner;
	public const int maxItems = 6;
	public GroundNode node;
	public Item[] items = new Item[maxItems];
	[JsonIgnore]
	public GameObject[] frames = new GameObject[maxItems];
	public Worker user;
	public Road[] roadsStartingHere = new Road[GroundNode.neighbourCount];
	public Building building;
	static GameObject template;
	static GameObject baseTemplate;
	public Versioned itemsStored = new Versioned();
	[JsonIgnore]
	public bool debugSpawnPlank;
	public const float itemSpread = 0.25f;
	GameObject tiles;

	static public void Initialize()
	{
		template = (GameObject)Resources.Load( "prefabs/others/pathPointers" );
		Assert.global.IsNotNull( template );
		baseTemplate = (GameObject)Resources.Load( "prefabs/others/flagBase" );
		Assert.global.IsNotNull( baseTemplate );
	}

	public static Flag Create()
	{
		return new GameObject().AddComponent<Flag>();
	}

	public Flag Setup( GroundNode node, Player owner, bool blueprintOnly = false )
    {
		if ( !IsNodeSuitable( node, owner ) )
		{
			Destroy( this );
			return null;
		}

		node.flag = this;
        this.node = node;
		this.owner = owner;
		this.blueprintOnly = blueprintOnly;
		if ( node.road && !blueprintOnly )
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

	public override void Materialize()
	{
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
		base.Materialize();
	}

	void Start()
	{
		gameObject.name = "Flag " + node.x + ", " + node.y;
		transform.SetParent( node.ground.transform );
		Instantiate( template ).transform.SetParent( transform, false );

		tiles = Instantiate( baseTemplate );
		tiles.transform.SetParent( transform, false );

		UpdateBody();
		for ( int i = 0; i < maxItems; i++ )
		{
			frames[i] = new GameObject
			{
				name = "Item Frame " + i
			};
			var t = frames[i].transform;
			t.SetParent( transform, false );
			Vector3 pos;
			pos.x = Mathf.Sin( Mathf.PI * 2 / maxItems * i ) * itemSpread * GroundNode.size;
			pos.z = Mathf.Cos( Mathf.PI * 2 / maxItems * i ) * itemSpread * GroundNode.size;
			pos.y = node.ground.GetHeightAt( node.Position.x + pos.x, node.Position.z + pos.z ) - node.height;
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
		transform.localPosition = node.Position;

		var tileMesh = tiles.GetComponent<MeshFilter>().mesh;
		var gt = node.ground.transform;
		var vertices = tileMesh.vertices;
		for ( int i = 0; i < vertices.Length; i++ )
		{
			var groundPosition = gt.InverseTransformPoint( tiles.transform.TransformPoint( vertices[i] ) );
			groundPosition.y = node.ground.GetHeightAt( groundPosition.x, groundPosition.z ) + 0.03f;
			vertices[i] = tiles.transform.InverseTransformPoint( gt.TransformPoint( groundPosition ) );
		}
		tileMesh.vertices = vertices;
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
		item.assert.Fail( "Item not found at flag" );
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
		assert.Fail();
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

	public override void OnClicked()
	{
		Interface.FlagPanel.Create().Open( this );
	}

	public override bool Remove( bool takeYourTime = false )
	{
		if ( building && !building.Remove( takeYourTime ) )
			return false;
		foreach ( var road in roadsStartingHere )
			road?.Remove( takeYourTime );
		foreach ( var item in items )
			item?.Remove( takeYourTime );

		node.flag = null;
		Destroy( gameObject );
		return true;
	}

	// Returns the number of available slots at the flag
	public int FreeSpace()
	{
		int free = 0;
		for ( int i = 0; i < maxItems; i++ )
			if ( items[i] == null )
				free++;
		return free;
	}

	public override void Reset()
	{
		for ( int i = 0; i < maxItems; i++ )
		{
			if ( items[i] == null || items[i].flag != this )
				continue;
			items[i].Remove( false );
			items[i] = null;
		}
		foreach ( var road in roadsStartingHere )
			road?.Reset();
		assert.IsNull( user );
	}

	override public void Validate()
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

	public override GroundNode Node
	{
		get
		{
			return node;
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
 
