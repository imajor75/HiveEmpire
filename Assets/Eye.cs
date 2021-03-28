using Newtonsoft.Json;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.SocialPlatforms.GameCenter;

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
	public bool rotateAround;
	float storedX, storedY, storedDirection;
	public new Camera camera;
	[JsonIgnore]
	public IDirector director;
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
		name = "Eye";
		camera = GetComponent<Camera>();
		camera.cullingMask &= int.MaxValue - ( 1 << World.layerIndexMapOnly );
		camera.farClipPlane = 50;
		camera.nearClipPlane = 0.001f;
		transform.SetParent( World.instance.transform );
		gameObject.AddComponent<CameraHighlight>();

		ear = new GameObject().transform;
		ear.gameObject.AddComponent<AudioListener>();
		ear.name = "Ear";
		ear.transform.SetParent( World.instance.transform );
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
	}

	public void GrabFocus( IDirector director )
	{
		storedX = x;
		storedY = y;
		storedDirection = direction;
		this.director = director;
	}

	public void ReleaseFocus( IDirector director, bool restore = false )
	{
		if ( this.director == director )
			this.director = null;

		if ( restore )
		{
			x = storedX;
			y = storedY;
			direction = storedDirection;
			rotateAround = false;
		}
	}

	public void FocusOn( GroundNode node, bool rotateAround = false )
	{
		storedX = x;
		storedY = y;
		storedDirection = direction;

		var p = node.Position;
		x = p.x;
		y = p.z;
		director = null;
		this.rotateAround = rotateAround;
	}

	public void FocusOn( Component component, bool rotateAround = false )
	{
		storedX = x;
		storedY = y;
		storedDirection = direction;

		x = component.transform.position.x;
		y = component.transform.position.z;
		director = null;
		this.rotateAround = rotateAround;
	}

	void FixedUpdate()
	{
		Vector3 movement = new Vector3();
		if ( Interface.GetKey( KeyCode.A ) )
		{
			movement += transform.right * -0.1f * altitude / 6;
			director = null;
		}
		if ( Interface.GetKey( KeyCode.D ) )
		{
			movement += transform.right * 0.1f * altitude / 6;
			director = null;
		}
		if ( Interface.GetKey( KeyCode.W ) )
		{
			movement += transform.forward * 0.13f * altitude / 6;
			director = null;
		}
		if ( Interface.GetKey( KeyCode.S ) )
		{
			movement += transform.forward * -0.13f * altitude / 6;
			director = null;
		}
		x += movement.x;
		y += movement.z;

		if ( Interface.GetKey( KeyCode.Q ) )
			direction += 0.03f;
		if ( Interface.GetKey( KeyCode.E ) )
			direction -= 0.03f;
		if ( rotateAround )
			direction += 0.001f;
		if ( direction >= Math.PI * 2 )
			direction -= (float)Math.PI * 2;
		if ( direction < 0 )
			direction += (float)Math.PI * 2;

		if ( Interface.GetKey( KeyCode.Z ) )
			targetAltitude *= 1.01f;
		if ( Interface.GetKey( KeyCode.X ) )
			targetAltitude *= 0.99f;
		if ( camera == camera.enabled )
		{
			if ( Input.GetAxis( "Mouse ScrollWheel" ) < 0 )     // TODO Use something else instead of strings here
				targetAltitude += 1;
			if ( Input.GetAxis( "Mouse ScrollWheel" ) > 0 )
				targetAltitude -= 1;
		}
		if ( targetAltitude < minAltitude )
			targetAltitude = minAltitude;
		if ( targetAltitude > maxAltitude )
			targetAltitude = maxAltitude;


		altitude += ( targetAltitude - altitude ) * 0.1f;
	}

	public interface IDirector
	{
		void SetCameraTarget( Eye eye );
	}
}

public class CameraHighlight : HiveObject
{
	public static Material highlightMaterial;
	static Material blurMaterial;
	static int highLightStencilRef;

	public static void Initialize()
	{
		highlightMaterial = new Material( Resources.Load<Shader>( "Highlight" ) );
		blurMaterial = new Material( Resources.Load<Shader>( "Blur" ) );
		highLightStencilRef = Shader.PropertyToID( "_StencilRef" );
	}

	void OnRenderImage( RenderTexture src, RenderTexture dst )
	{
		var volume = Interface.root.highlightVolume;
		if ( volume )
		{
			var collider = volume.GetComponent<MeshCollider>();
			collider.sharedMesh = volume.GetComponent<MeshFilter>().mesh;
			var eye = transform.position;
			var center = volume.transform.position;
			var ray = new Ray( eye, center - eye );
			var outside = collider.Raycast( ray, out _, 100 );
			highlightMaterial.SetInt( highLightStencilRef, outside ? 0 : 1 );
		}
		else
			highlightMaterial.SetInt( highLightStencilRef, 0 );

		// TODO Do the postprocess with less blit calls
		// This should be possible theoretically with a single blit from src
		// to dst using the stencil from src. But since the stencil values are
		// from the destination, a mixed rendertarget is needed, where the color
		// buffer is from dst, but the depth/stencil is from src. Theoretically
		// Graphics.SetRenderTarget can use RenderBuffers from two different
		// render textures, but Graphics.Blit will ruin this, so a manual blit
		// needs to be used (using GL.Begin(GL.QUADS) etc..). Unfortunately the 
		// practic shows that it is not working for unknown reasons. This needs 
		// to be tested withmap future versions of unity.
		if ( Interface.root.highlightType == Interface.HighlightType.none )
		{
			Graphics.Blit( src, dst );
			return;
		}

		var tempRT = RenderTexture.GetTemporary( src.width, src.height, 24 );

		Graphics.Blit( src, tempRT, blurMaterial );
		Graphics.Blit( tempRT, src, highlightMaterial );
		Graphics.Blit( src, dst );

		RenderTexture.ReleaseTemporary( tempRT );
	}

	public override GroundNode Node { get { return null; } }
}

