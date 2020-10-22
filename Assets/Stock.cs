using System.Collections.Generic;
using UnityEngine;

public class Stock : Building
{
	public bool main = false;
	public List<int> content = new List<int>();
	public List<int> target = new List<int>();
	public List<int> onWay = new List<int>();
	public static int influenceRange = 10;
	public static int mainBuildingInfluence = 10;
	public static GameObject template;
	static Configuration configuration = new Configuration();

	public static new void Initialize()
	{
		template = (GameObject)Resources.Load( "Medieval fantasy house/Medieva_fantasy_house" );
		configuration.plankNeeded = 2;
		configuration.stoneNeeded = 2;
		configuration.flatteningNeeded = true;
	}

	public static Stock Create()
	{
		var buildingObject = (GameObject)GameObject.Instantiate( template );
		buildingObject.transform.localScale = new Vector3( 0.12f, 0.12f, 0.12f );
		buildingObject.transform.Rotate( Vector3.up * -55 );	
		return buildingObject.AddComponent<Stock>();
	}

	public static bool IsItGood( GroundNode placeToBuild, Player owner )
	{
		return Building.IsItGood( placeToBuild, owner, configuration );
	}

	public Stock Setup( GroundNode node, Player owner )
	{
		title = "stock";
		construction.plankNeeded = 3;
		construction.stoneNeeded = 3;
		construction.flatteningNeeded = true;
		height = 2;

		while ( content.Count < (int)Item.Type.total )
		{
			content.Add( 0 );
			target.Add( 0 );
			onWay.Add( 0 );
		}
		if ( base.Setup( node, owner, configuration ) == null )
			return null;

		owner.RegisterStock( this );
		return this;
	}

	public Stock SetupMain( GroundNode node, Player owner )
	{
		node.owner = owner;
		foreach ( var o in Ground.areas[1] )
			node.Add( o ).owner = owner;
		if ( !Setup( node, owner ) )
			return null;

		title = "headquarter";
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
		owner.UnregisterStock( this );
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
		while ( target.Count < (int)Item.Type.total )
			target.Add( 0 );
		while ( onWay.Count < (int)Item.Type.total )
			onWay.Add( 0 );
	}

	new public void Update()
    {
		base.Update();

		if ( !construction.done )
			return;

		for ( int itemType = 0; itemType < (int)Item.Type.total; itemType++ )
		{
			owner.itemDispatcher.RegisterRequest( this, (Item.Type)itemType, int.MaxValue, ItemDispatcher.Priority.stock );
			if ( content.Count > itemType && content[itemType] > 0 && flag.FreeSpace() > 3 )
				owner.itemDispatcher.RegisterOffer( this, (Item.Type)itemType, content[itemType], ItemDispatcher.Priority.stock );
			int missing = target[itemType] - content[itemType] + onWay[itemType];
			if ( missing > 0 )
				owner.itemDispatcher.RegisterRequest( this, (Item.Type)itemType, missing, ItemDispatcher.Priority.high );
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

	public override Item SendItem( Item.Type itemType, Building destination, ItemDispatcher.Priority priority )
	{
		assert.IsTrue( content[(int)itemType] > 0 );
		Item item = base.SendItem( itemType, destination, priority );
		if ( item != null )
			content[(int)itemType]--;

		return item;
	}

	public override void ItemOnTheWay( Item item, bool cancel = false )
	{
		if ( cancel )
			onWay[(int)item.type]--;
		else
			onWay[(int)item.type]++;
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
