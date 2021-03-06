﻿using UnityEngine;

public class BorderEdge : HiveObject
{
    public Node node;
    public int direction;

	public static BorderEdge Create()
	{
		GameObject body = GameObject.CreatePrimitive( PrimitiveType.Cube );
		body.name = "Border buoy";
		Destroy( body.GetComponent<BoxCollider>() );
		return body.AddComponent<BorderEdge>();
	}

    public BorderEdge Setup( Node node, int direction )
    {
		if ( node.DistanceFrom( node.Neighbour( direction ) ) > 1 )
		{
			DestroyThis();
			return null;
		}
        this.node = node;
        this.direction = direction;
		assert.AreNotEqual( node.owner, node.Neighbour( direction ).owner );
		return this;
    }

	new public void Start()
	{
		transform.localScale = Vector3.one * 0.2f;
		node.ground.Link( this );
		UpdateBody();
		base.Start();
	}

	public void UpdateBody()
	{
		Vector3 position = Vector3.Lerp( node.position, node.Neighbour( direction ).GetPositionRelativeTo( node ), 0.4f );
		if ( position.y < World.instance.waterLevel )
			position.y = World.instance.waterLevel;
		transform.localPosition = position;
	}

	public override Node location { get { return node; } }

}
