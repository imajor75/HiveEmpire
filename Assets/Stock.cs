using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

public class Stock : Building
{
	public bool main = false;
	public int[] content = new int[(int)Item.Type.total];

	public static Building SetupMain( Ground ground, GroundNode node )
	{
		if ( !CreateNew( ground, node, Type.stock ) )
			return null;

		var mainBuilding = (Stock)node.building;
		mainBuilding.main = true;
		return mainBuilding;
	}

	void Update()
    {
		for ( int itemType = 0; itemType < (int)Item.Type.total; itemType++ )
		{
			if ( content[itemType] > 0 )
				ItemDispatcher.instance.RegisterOffer( this, (Item.Type)itemType, content[itemType], ItemDispatcher.Priority.low );
			ItemDispatcher.instance.RegisterRequest( this, (Item.Type)itemType, int.MaxValue, ItemDispatcher.Priority.low );
		}
    }

	public override void OnClicked()
	{
		StockPanel.Open( this );
	}

	public override bool SendItem( Item.Type itemType, Building destination )
	{
		Assert.IsTrue( content[(int)itemType] > 0 );
		content[(int)itemType]--;
		return ( Item.CreateNew( itemType, ground, flag, destination ) != null );
	}

	public override void ItemOnTheWay( Item item )
	{
	}

	public override void ItemArrived( Item item )
	{
		content[(int)item.type]++;
	}

	public override void Validate()
	{
		base.Validate();
	}
}
