using System.Collections.Generic;
using System.Data;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.Assertions;

public class Stock : Building
{
	public bool main = false;
	public List<int> content = new List<int>();
	public static int influenceRange = 10;
	public static int mainBuildingInfluence = 10;

	public static Stock Create()
	{
		var buildingObject = (GameObject)GameObject.Instantiate( templates[0] );
		buildingObject.transform.localScale = new Vector3( 0.12f, 0.12f, 0.12f );
		buildingObject.transform.Rotate( Vector3.up * -55 );	
		return buildingObject.AddComponent<Stock>();
	}

	new public Stock Setup( Ground ground, GroundNode node, Player owner )
	{
		while ( content.Count < (int)Item.Type.total )
			content.Add( 0 );
		if ( base.Setup( ground, node, owner ) == null )
			return null;

		return this;
	}

	public bool SetupMain( Ground ground, GroundNode node, Player owner )
	{
		height = 2;
		node.owner = owner;
		ground.GetNode( node.x + 1, node.y - 1 ).owner = owner;
		if ( !Setup( ground, node, owner ) )
			return false;

		main = true;
		construction = new Construction();
		construction.boss = this;
		construction.done = true;
		content[(int)Item.Type.plank] = 10;
		worker = Worker.Create();
		worker.SetupForBuilding( this );
		ground.RegisterInfluence( this );
		return true;
	}

	new void Start()
	{
		base.Start();
		if ( main )
			name = "Headquarters";
		else
			name = "Stock " + node.x + ", " + node.y;
	}

	new public void Update()
    {
		base.Update();

		if ( !construction.done )
			return;

		for ( int itemType = 0; itemType < (int)Item.Type.total; itemType++ )
		{
			if ( content.Count > itemType && content[itemType] > 0 && flag.FreeSpace() > 0 )
				ItemDispatcher.lastInstance.RegisterOffer( this, (Item.Type)itemType, content[itemType], ItemDispatcher.Priority.low );
			ItemDispatcher.lastInstance.RegisterRequest( this, (Item.Type)itemType, int.MaxValue, ItemDispatcher.Priority.low );
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
		StockPanel.Open( this );
	}

	public override Item SendItem( Item.Type itemType, Building destination )
	{
		Assert.IsTrue( content[(int)itemType] > 0 );
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
		if ( construction.ItemArrived( item ) )
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
