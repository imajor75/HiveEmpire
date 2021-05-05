using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BorderEdge : HiveObject
{
    public GroundNode node;
    public int direction;

	public static BorderEdge Create()
	{
		GameObject body = GameObject.CreatePrimitive( PrimitiveType.Cube );
		body.name = "Border buoy";
		Destroy( body.GetComponent<BoxCollider>() );
		return body.AddComponent<BorderEdge>();
	}

    public BorderEdge Setup( GroundNode node, int direction )
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

	void Start()
	{
		transform.localScale = Vector3.one * 0.2f;
		node.ground.Link( this );
		UpdateBody();
	}

	public void UpdateBody()
	{
		Vector3 position = Vector3.Lerp( node.position, node.Neighbour( direction ).position, 0.4f );
		if ( position.y < World.instance.settings.waterLevel * World.instance.settings.maxHeight )
			position.y = World.instance.settings.waterLevel * World.instance.settings.maxHeight;
		transform.localPosition = position;
	}

	public override GroundNode Node { get { return node; } }

}
