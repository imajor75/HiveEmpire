﻿using UnityEngine;

public class BorderEdge : HiveObject
{
    public Node node;
    public int direction;

	override public UpdateStage updateMode => UpdateStage.none;

	public static BorderEdge Create()
	{
		GameObject body = GameObject.CreatePrimitive( PrimitiveType.Cube );
		body.name = "Border buoy";
		body.layer = Constants.World.layerIndexGround;
		Eradicate( body.GetComponent<BoxCollider>() );
		return body.AddComponent<BorderEdge>();
	}

    public BorderEdge Setup( Node node, int direction )
    {
		if ( node.DistanceFrom( node.Neighbour( direction ) ) > 1 )
		{
			base.Remove();
			return null;
		}
        this.node = node;
        this.direction = direction;
		assert.AreNotEqual( node.team, node.Neighbour( direction ).team );
		base.Setup( node.world );
		return this;
    }

	new public void Start()
	{
		gameObject.GetComponent<MeshRenderer>().material = node.team.GetBuoyMaterial();
		transform.localScale = Vector3.one * 0.2f;
		ground.Link( this );
		UpdateBody();
		base.Start();
	}

	public void UpdateBody()
	{
		Vector3 position = Vector3.Lerp( node.position, node.Neighbour( direction ).GetPositionRelativeTo( node ), 0.4f );
		if ( position.y < game.waterLevel )
			position.y = game.waterLevel;
		transform.localPosition = position;
	}

	public override Node location { get { return node; } }

}
