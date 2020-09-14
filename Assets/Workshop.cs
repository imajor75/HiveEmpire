using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

public class Workshop : Building
{
	public int output;
	public Item.Type outputType = Item.Type.unknown;
	public int outputMax = 8;
	public float progress;
	public bool working;
	public Type type = Type.unknown;
	public List<Buffer> buffers = new List<Buffer>();
	GameObject body;

	[System.Serializable]
	public class Buffer
	{
		public int size = 8;
		public int stored;
		public int onTheWay;
		public ItemDispatcher.Priority priority = ItemDispatcher.Priority.high;
		public Item.Type itemType;
	}

	public enum Type
	{
		woodcutter,
		sawmill,
		total,
		unknown = -1
	}

	public static Workshop Create()
	{
		var buildingObject = new GameObject();
		return buildingObject.AddComponent<Workshop>();
	}

	public Workshop Setup( Ground ground, GroundNode node, Player owner, Type type )
	{
		if ( Setup( ground, node, owner ) == null )
			return null;

		this.type = type;
		buffers.Clear();
		switch ( type )
		{
			case Type.woodcutter:
			{
				outputType = Item.Type.wood;
				working = true;
				construction.plankNeeded = 2;
				body = (GameObject)GameObject.Instantiate( templates[1], transform );
				break;
			}
			case Type.sawmill:
			{
				Buffer b = new Buffer();
				b.itemType = Item.Type.wood;
				buffers.Add( b );
				outputType = Item.Type.plank;
				working = false;
				construction.plankNeeded = 2;
				body = (GameObject)GameObject.Instantiate( templates[2], transform );
				break;
			}
		}
		return this;
	}

	new void Update()
	{
		base.Update();

		if ( !construction.done )
			return;

		foreach ( Buffer b in buffers )
		{
			int missing = b.size-b.stored-b.onTheWay;
			if ( missing > 0 )
				ItemDispatcher.lastInstance.RegisterRequest( this, b.itemType, missing, b.priority );
		}
		if ( output > 0 )
			ItemDispatcher.lastInstance.RegisterOffer( this, outputType, output, ItemDispatcher.Priority.high );
	}

	public override Item SendItem( Item.Type itemType, Building destination )
	{
		Assert.AreEqual( outputType, itemType );
		Assert.IsTrue( output > 0 );
		Item item = base.SendItem( itemType, destination );
		if ( item != null )
			output--;
		return item;
	}

	public override void ItemOnTheWay( Item item, bool cancel = false )
	{
		if ( construction.ItemOnTheWay( item, cancel ) )
			return;

		foreach ( var b in buffers )
		{
			if ( b.itemType == item.type )
			{
				if ( cancel )
				{
					Assert.IsTrue( b.onTheWay > 0 );
					b.onTheWay--;
				}
				else
				{
					Assert.IsTrue( b.stored + b.onTheWay < b.size );
					b.onTheWay++;
				}
				return;
			}
		}
		Assert.IsTrue( false );
	}

	public override void ItemArrived( Item item )
	{
		if ( construction.ItemArrived( item ) )
			return;

		foreach ( var b in buffers )
		{
			if ( b.itemType == item.type )
			{
				Assert.IsTrue( b.onTheWay > 0 );
				b.onTheWay--;
				Assert.IsTrue( b.onTheWay + b.stored < b.size );
				b.stored++;
				return;
			}
		}
		Assert.IsTrue( false );
	}

	new void FixedUpdate()
	{
		if ( !construction.done )
		{
			base.FixedUpdate();
			return;
		}

		switch ( type )
		{
			case Type.woodcutter:
			{
				if ( !working && output < outputMax )
				{
					working = true;
					progress = 0;
				}
				if ( working && worker && worker.IsIdleInBuilding() )
				{
					progress += 0.02f * ground.speedModifier;  // TODO Speed needs to be defined somehow
					if ( progress > 1 )
					{
						output++;
						working = false;
					}
				}
				break;
			}
			case Type.sawmill:
			{
				if ( !working && output < outputMax && buffers[0].stored > 0 )
				{
					working = true;				
					progress = 0;
					buffers[0].stored--;
				}
				if ( working && worker && worker.IsIdleInBuilding() )
				{
					progress += 0.0015f * ground.speedModifier;
					if ( progress > 1 )
					{
						output++;
						working = false;
					}
				}
				break;
			}
		}
	}

	public override void OnClicked()
	{
		WorkshopPanel.Open( this );
	}

	public override void Validate()
	{
		base.Validate();
		foreach ( Buffer b in buffers )
			Assert.IsTrue( b.stored + b.onTheWay <= b.size );
	}
}
