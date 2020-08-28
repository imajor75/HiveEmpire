using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

public class Item : MonoBehaviour
{
	public Flag flag;
	public Worker worker;
	public Type type;
	public Ground ground;
	public PathFinder path;
	public int pathProgress;

    public enum Type
    {
        wood,
        stone,
        plank,
        total
    }

	static public Item CreateNew( Type type, Ground ground, Flag flag )
	{
		GameObject itemBody = GameObject.CreatePrimitive( PrimitiveType.Capsule );
		itemBody.name = "Item";
		itemBody.transform.SetParent( ground.transform );
		itemBody.transform.localScale *= 0.2f;
		var item = itemBody.AddComponent<Item>();
		item.ground = ground;
		item.type = type;
		if ( flag )
			flag.StoreItem( item );
		item.UpdateLook();
		return item;
	}

	public void SetTarget( Flag flag )
	{
		path = new PathFinder();
		pathProgress = 0;
		path.FindPathBetween( this.flag.node, flag.node, PathFinder.Mode.onRoad );
	}

	public void UpdateLook()
	{
		if ( flag )
		{
			// TODO Arrand the items around the flag
			transform.localPosition = flag.node.Position() + Vector3.up * GroundNode.size;
		}
		if ( worker )
		{
			// TODO Put the item in the hand of the worker
			transform.localPosition = worker.transform.localPosition + Vector3.up * GroundNode.size;
		}
	}

	public void Validate()
	{
		Assert.IsTrue( flag || worker );
		if ( flag )
		{
			int s = 0;
			foreach ( var i in flag.items )
				if ( i == this )
					s++;
			Assert.AreEqual( s, 1 );
		}
		if ( worker )
			Assert.AreEqual( this, worker.item );
	}
}
