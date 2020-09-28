using System;
using UnityEngine;

[RequireComponent( typeof( Camera ) )]
public class Eye : MonoBehaviour
{
	public float altitude = 4.0f;
	static public float minAltitude = 2.0f;
	static public float maxAltitude = 15.0f;
	static public float viewDistance = 5.0f;
	public World world;
	public float x, y;
	public float direction;
	new Camera camera;
	Transform ear;

	public static Eye Create()
	{
		var eyeObject = new GameObject();
		Eye eye = eyeObject.AddComponent<Eye>();
		return eye;
	}

	public Eye Setup( World world )
	{
		this.world = world;
		return this;
	}

	void Start()
	{
		tag = "MainCamera";
		name = "Eye";
		camera = GetComponent<Camera>();

		ear = new GameObject().transform;
		ear.gameObject.AddComponent<AudioListener>();
		ear.name = "Ear";
	}

	private void Update()
	{
		Ray ray = new Ray( new Vector3( x, GroundNode.size * 50, y ), Vector3.down );
		RaycastHit hit;
		var size = GroundNode.size;
		if ( world.ground.collider.Raycast( ray, out hit, GroundNode.size * 100 ) )
		{
			Vector3 position = hit.point;
			if ( position.y < Ground.waterLevel * Ground.maxHeight )
				position.y = Ground.waterLevel * Ground.maxHeight;
			ear.position = position;
			Vector3 viewer = new Vector3( (float)( viewDistance*Math.Sin(direction) ), -altitude, (float)( viewDistance*Math.Cos(direction) ) );
			transform.position = position - viewer;
			transform.LookAt( ear );
		}
	}

	public void FocusOn( GroundNode node )
	{
		var p = node.Position();
		x = p.x;
		y = p.z;
	}

	void FixedUpdate()
	{
		Vector3 movement = new Vector3();
		if ( Input.GetKey( KeyCode.A ) )
			movement += transform.right * -0.1f;
		if ( Input.GetKey( KeyCode.D ) )
			movement += transform.right * 0.1f;
		if ( Input.GetKey( KeyCode.W ) )
			movement += transform.forward * 0.13f;
		if ( Input.GetKey( KeyCode.S ) )
			movement += transform.forward * -0.13f;
		x += movement.x;
		y += movement.z;

		if ( Input.GetKey( KeyCode.Q ) )
			direction += 0.03f;
		if ( Input.GetKey( KeyCode.E ) )
			direction -= 0.03f;
		if ( Input.GetKey( KeyCode.Z ) && altitude < maxAltitude )
			altitude *= 1.01f;
		if ( Input.GetKey( KeyCode.X ) && altitude > minAltitude )
			altitude *= 0.99f;

		if ( direction >= Math.PI * 2 )
			direction -= (float)Math.PI * 2;
		if ( direction < 0 )
			direction += (float)Math.PI * 2;
	}

	public GroundNode FindNodeAt( Vector3 screenPosition )
	{
		Ray ray = camera.ScreenPointToRay( screenPosition );
		RaycastHit hit;
		if ( !world.ground.collider.Raycast( ray, out hit, 1000 ) )	// TODO How long the ray should really be?
			return null;

		Vector3 localPosition = world.ground.transform.InverseTransformPoint( hit.point );
		return GroundNode.FromPosition( localPosition, world.ground );
	}
}
