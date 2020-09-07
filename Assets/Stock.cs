using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

public class Stock : Building
{
	public bool main = false;
	public int[] content = new int[(int)Item.Type.total];

	public static Stock Create()
	{
		var buildingObject = (GameObject)GameObject.Instantiate( prefab2 );
		return buildingObject.AddComponent<Stock>();
	}

	public static Building SetupMain( Ground ground, GroundNode node )
	{
		if ( !CreateNew( ground, node, Type.stock ) )
			return null;

		var mainBuilding = (Stock)node.building;
		mainBuilding.main = true;
		mainBuilding.construction.done = true;
		mainBuilding.content[(int)Item.Type.plank] = 10;
		return mainBuilding;
	}

	new void Start()
	{
		base.Start();
		gameObject.name = "Stock " + node.x + ", " + node.y;
	}

	new public void Update()
    {
		base.Update();

		if ( !construction.done )
			return;

		for ( int itemType = 0; itemType < (int)Item.Type.total; itemType++ )
		{
			if ( content[itemType] > 0 )
				ItemDispatcher.lastInstance.RegisterOffer( this, (Item.Type)itemType, content[itemType], ItemDispatcher.Priority.low );
			ItemDispatcher.lastInstance.RegisterRequest( this, (Item.Type)itemType, int.MaxValue, ItemDispatcher.Priority.low );
		}
    }

	public override void OnClicked()
	{
		StockPanel.Open( this );
	}

	public override bool SendItem( Item.Type itemType, Building destination )
	{
		Assert.IsTrue( content[(int)itemType] > 0 );
		Item item = Item.CreateNew( itemType, ground, flag, destination );
		if ( item == null )
			return false;

		content[(int)itemType]--;
		return true;
	}

	public override void ItemOnTheWay( Item item )
	{
		construction.ItemOnTheWay( item );
	}

	public override void ItemArrived( Item item )
	{
		if ( construction.ItemArrived( item ) )
			return;

		content[(int)item.type]++;
	}

	public override void Validate()
	{
		base.Validate();
	}
}
