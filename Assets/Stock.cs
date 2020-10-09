using System.Collections.Generic;
using UnityEngine;

public class Stock : Building
{
	public bool main = false;
	public List<int> content = new List<int>();
	public static int influenceRange = 10;
	public static int mainBuildingInfluence = 10;
	public static GameObject template;

	public static new void Initialize()
	{
		template = (GameObject)Resources.Load( "Medieval fantasy house/Medieva_fantasy_house" );
	}

	public static Stock Create()
	{
		var buildingObject = (GameObject)GameObject.Instantiate( template );
		buildingObject.transform.localScale = new Vector3( 0.12f, 0.12f, 0.12f );
		buildingObject.transform.Rotate( Vector3.up * -55 );	
		return buildingObject.AddComponent<Stock>();
	}

	new public Stock Setup( Ground ground, GroundNode node, Player owner )
	{
		construction.plankNeeded = 3;
		construction.stoneNeeded = 3;
		construction.flatteningNeeded = true;
		height = 2;

		while ( content.Count < (int)Item.Type.total )
			content.Add( 0 );
		if ( base.Setup( ground, node, owner ) == null )
			return null;

		return this;
	}

	public Stock SetupMain( Ground ground, GroundNode node, Player owner )
	{
		node.owner = owner;
		foreach ( var o in Ground.areas[1] )
			node.Add( o ).owner = owner;
		if ( !Setup( ground, node, owner ) )
			return null;

		main = true;
		construction = new Construction();
		construction.boss = this;
		construction.done = true;
		if ( construction.flatteningNeeded )
		{
			foreach ( var o in Ground.areas[1] )
				node.Add( o ).SetHeight( node.height );
			construction.flatteningNeeded = false;
		}
		content[(int)Item.Type.plank] = 10;
		content[(int)Item.Type.fish] = 10;
		worker = Worker.Create();
		worker.SetupForBuilding( this );
		owner.RegisterInfluence( this );
		return this;
	}

	public override bool Remove()
	{
		if ( main )
			return false;
		return base.Remove();
	}

	new void Start()
	{
		base.Start();
		if ( main )
			name = "Headquarters";
		else
			name = "Stock " + node.x + ", " + node.y;
		while ( content.Count < (int)Item.Type.total )
			content.Add( 0 );
	}

	new public void Update()
    {
		base.Update();

		if ( !construction.done )
			return;

		for ( int itemType = 0; itemType < (int)Item.Type.total; itemType++ )
		{
			if ( content.Count > itemType && content[itemType] > 0 && flag.FreeSpace() > 0 )
				owner.itemDispatcher.RegisterOffer( this, (Item.Type)itemType, content[itemType], ItemDispatcher.Priority.low );
			owner.itemDispatcher.RegisterRequest( this, (Item.Type)itemType, int.MaxValue, ItemDispatcher.Priority.low );
		}
    }

	new void FixedUpdate()
	{
		base.FixedUpdate();
		if ( worker == null && construction.done )
		{
			worker = Worker.Create();
			worker.SetupForBuilding( this );
		}
	}

	public override int Influence( GroundNode node )
	{
		if ( !main )
			base.Influence( node );

		return Stock.mainBuildingInfluence - node.DistanceFrom( this.node );
	}

	public override void OnClicked()
	{
		if ( construction.done )
			Interface.StockPanel.Create().Open( this );
		else
			Interface.ConstructionPanel.Create().Open( construction );
	}

	public override Item SendItem( Item.Type itemType, Building destination )
	{
		assert.IsTrue( content[(int)itemType] > 0 );
		Item item = base.SendItem( itemType, destination );
		if ( item != null )
			content[(int)itemType]--;

		return item;
	}

	public override void ItemOnTheWay( Item item, bool cancel = false )
	{
		base.ItemOnTheWay( item, cancel );
	}

	public override void ItemArrived( Item item )
	{
		base.ItemArrived( item );

		if ( !construction.done )
			return;

		while ( content.Count <= (int)item.type )
			content.Add( 0 );
		content[(int)item.type]++;
	}

	public override void Validate()
	{
		base.Validate();
	}
}
