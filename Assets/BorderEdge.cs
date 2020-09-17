﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BorderEdge : MonoBehaviour
{
    public GroundNode node;
    public int direction;

	public static BorderEdge Create()
	{
		GameObject body = GameObject.CreatePrimitive( PrimitiveType.Cube );
		Destroy( body.GetComponent<BoxCollider>() );
		return body.AddComponent<BorderEdge>();
	}

    public BorderEdge Setup( GroundNode node, int direction )
    {
        this.node = node;
        this.direction = direction;
		Assert.AreNotEqual( node.owner, node.Neighbour( direction ).owner );
		return this;
    }

    void Start()
    {
		Vector3 position = Vector3.Lerp( node.Position(), node.Neighbour( direction ).Position(), 0.4f );
		if ( position.y < Ground.waterLevel * Ground.maxHeight )
			position.y = Ground.waterLevel * Ground.maxHeight;
		transform.SetParent( node.ground.transform );
		transform.localPosition = position;
		transform.localScale = Vector3.one * 0.2f;
	}
}
