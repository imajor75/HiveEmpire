﻿using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.Assertions;

public class Stock : Building
{
	public bool main = false;
	public int[] content = new int[(int)Item.Type.total];
	public static int influenceRange = 10;
	public static int mainBuildingInfluence = 10;

	public static Stock Create()
	{
		var buildingObject = (GameObject)GameObject.Instantiate( templateA );
		buildingObject.transform.localScale = new Vector3( 0.09f, 0.09f, 0.09f );
		buildingObject.transform.Rotate( Vector3.up * -55 );
		return buildingObject.AddComponent<Stock>();
	}

	public bool SetupMain( Ground ground, GroundNode node, Player owner )
	{
		node.owner = owner;
		ground.GetNode( node.x + 1, node.y - 1 ).owner = owner;
		if ( !Setup( ground, node, owner ) )
			return false;

		main = true;
		construction = new Construction();
		construction.boss = this;
		construction.done = true;
		content[(int)Item.Type.plank] = 10;
		worker = WorkerWoman.Create();
		worker.SetupForBuilding( this );
		ground.RegisterInfluence( this );
		return true;
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
		construction.ItemOnTheWay( item, cancel );
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
