﻿using Newtonsoft.Json;
using System;
using UnityEngine;

[RequireComponent( typeof( Camera ) )]
public class Eye : MonoBehaviour
{
	public float altitude = 4.0f;
	public float targetAltitude = 4.0f;
	static public float minAltitude = 2.0f;
	static public float maxAltitude = 15.0f;
	[JsonIgnore]
	public float viewDistance = 5.0f;
	public World world;
	public float x, y;
	public float direction;
	public new Camera camera;
	[JsonIgnore]
	public IDirector director;
	Transform ear;
	GameObject highlightPlane;

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
		name = "Eye";
		camera = GetComponent<Camera>();
		camera.cullingMask &= int.MaxValue - ( 1 << World.layerIndexMapOnly );
		transform.SetParent( World.instance.transform );

		ear = new GameObject().transform;
		ear.gameObject.AddComponent<AudioListener>();
		ear.name = "Ear";
		ear.transform.SetParent( World.instance.transform );

		var p = highlightPlane = GameObject.CreatePrimitive( PrimitiveType.Plane );
		p.transform.SetParent( transform );
		p.transform.localPosition = new Vector3( 0, 0, 1.0f );
		p.transform.rotation = Quaternion.Euler( -90, 0, 0 );
		p.GetComponent<MeshRenderer>().material = new Material( Resources.Load<Shader>( "highlight" ) );
		p.name = "Highlight Plane";
	}

	private void Update()
	{
		Ray ray = new Ray( new Vector3( x, GroundNode.size * 50, y ), Vector3.down );
		RaycastHit hit;
		if ( world.ground.collider.Raycast( ray, out hit, GroundNode.size * 100 ) )
		{
			Vector3 position = hit.point;
			if ( position.y < World.instance.waterLevel * World.instance.maxHeight )
				position.y = World.instance.waterLevel * World.instance.maxHeight;
			ear.position = position;
			Vector3 viewer = new Vector3( (float)( viewDistance*Math.Sin(direction) ), -altitude, (float)( viewDistance*Math.Cos(direction) ) );
			transform.position = position - viewer;
			transform.LookAt( ear );
		}
		if ( director == null )
		{
			director = null;
			viewDistance = 5;
		}
		else
		{
			viewDistance = 2;
			IDirector director = this.director;
			director.SetCameraTarget( this );
			this.director = director;
		}

		highlightPlane.SetActive( Interface.instance.highlightType != Interface.HighlightType.none );
	}

	public void GrabFocus( IDirector director )
	{
		this.director = director;
	}

	public void ReleaseFocus( IDirector director )
	{
		if ( this.director == director )
			this.director = null;
	}

	public void FocusOn( GroundNode node )
	{
		var p = node.Position();
		x = p.x;
		y = p.z;
		director = null;
	}

	public void FocusOn( Component component )
	{
		x = component.transform.position.x;
		y = component.transform.position.z;
		director = null;
	}

	public void SetRendering( bool on )
	{
		camera.enabled = on;
	}

	void FixedUpdate()
	{
		Vector3 movement = new Vector3();
		if ( Input.GetKey( KeyCode.A ) )
			movement += transform.right * -0.1f * altitude / 6;
		if ( Input.GetKey( KeyCode.D ) )
			movement += transform.right * 0.1f * altitude / 6;
		if ( Input.GetKey( KeyCode.W ) )
			movement += transform.forward * 0.13f * altitude / 6;
		if ( Input.GetKey( KeyCode.S ) )
			movement += transform.forward * -0.13f * altitude / 6;
		x += movement.x;
		y += movement.z;

		if ( Input.GetKey( KeyCode.Q ) )
			direction += 0.03f;
		if ( Input.GetKey( KeyCode.E ) )
			direction -= 0.03f;
		if ( direction >= Math.PI * 2 )
			direction -= (float)Math.PI * 2;
		if ( direction < 0 )
			direction += (float)Math.PI * 2;

		if ( Input.GetKey( KeyCode.Z ) )
			targetAltitude *= 1.01f;
		if ( Input.GetKey( KeyCode.X ) )
			targetAltitude *= 0.99f;
		if ( Input.GetAxis( "Mouse ScrollWheel" ) < 0 )		// TODO Use something else instead of strings here
			targetAltitude += 1;	
		if ( Input.GetAxis( "Mouse ScrollWheel" ) > 0 )		
			targetAltitude -= 1;
		if ( targetAltitude < minAltitude )
			targetAltitude = minAltitude;
		if ( targetAltitude > maxAltitude )
			targetAltitude = maxAltitude;


		altitude += ( targetAltitude - altitude ) * 0.1f;
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

	public interface IDirector
	{
		void SetCameraTarget( Eye eye );
	}
}
