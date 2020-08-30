using System.Collections;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
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

	public class Buffer
	{
		public int size = 8;
		public int stored;
		public int onTheWay;
		public ItemDispatcher.Priority priority = ItemDispatcher.Priority.high;
		public Item.Type itemType;
	}

	public new enum Type
	{
		woodcutter,
		sawmill,
		total,
		unknown = -1
	}

	void Update()
	{
		foreach ( Buffer b in buffers )
		{
			int missing = b.size-b.stored-b.onTheWay;
			if ( missing > 0 )
				ItemDispatcher.instance.RegisterRequest( this, b.itemType, missing, b.priority );
		}
		if ( output > 0 )
			ItemDispatcher.instance.RegisterOffer( this, outputType, output, ItemDispatcher.Priority.high );
	}

	public override bool SendItem( Item.Type itemType, Building destination )
	{
		Assert.AreEqual( outputType, itemType );
		Assert.IsTrue( output > 0 );
		if ( Item.CreateNew( itemType, ground, flag, destination ) == null )
			return false;
		output--;
		return true;
	}

	public override void ItemOnTheWay( Item item )
	{
		foreach ( var b in buffers )
		{
			if ( b.itemType == item.type )
			{
				Assert.IsTrue( b.stored + b.onTheWay < b.size );
				b.onTheWay++;
				return;
			}
		}
		Assert.IsTrue( false );
	}

	public override void ItemArrived( Item item )
	{
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

	public void SetType( Type type )
	{
		this.type = type;
		buffers.Clear();
		switch ( type )
		{
			case Type.woodcutter:
			{
				outputType = Item.Type.wood;
				working = true;
				break;
			}
			case Type.sawmill:
			{
				Buffer b = new Buffer();
				b.itemType = Item.Type.wood;
				buffers.Add( b );
				outputType = Item.Type.plank;
				working = false;
				break;
			}
		}
	}

	void FixedUpdate()
	{
		switch ( type )
		{
			case Type.woodcutter:
			{
				if ( !working && output < outputMax )
				{
					working = true;
					progress = 0;
				}
				if ( working )
				{
					progress += 0.02f;  // TODO Speed needs to be defined somehow
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
				if ( working )
				{
					progress += 0.0015f;
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
