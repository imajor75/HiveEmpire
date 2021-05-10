using Newtonsoft.Json;
using System;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

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
	public float absoluteX, absoluteY;
	public float forwardForGroundBlocks = 10;
	public float direction;
	public Vector3 autoMovement;
	public bool rotateAround;
	float storedX, storedY, storedDirection;
	bool hasStoredValues;
	public new Camera camera;
	[JsonIgnore]
	public IDirector director;
	Transform ear;

	public static Eye Create()
	{
		return new GameObject().AddComponent<Eye>();
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
		var ppl = gameObject.AddComponent<PostProcessLayer>();
		ppl.Init( Interface.root.postProcessResources );
		ppl.volumeLayer = 1 << World.layerIndexPPVolume;

		// Disable temporarily, as unity is crashing at the moment with it.
		ppl.enabled = false;

		ear = new GameObject( "Ear" ).transform;
		ear.gameObject.AddComponent<AudioListener>();
		ear.transform.SetParent( World.instance.transform );
	}

	public Vector3 position
	{
		get
		{
			return new Vector3( x, 0, y );
		}
	}

	public Vector3 absolutePosition
	{
		get
		{
			return new Vector3( absoluteX, 0, absoluteY );
		}
	}

	private void Update()
	{
		var h = World.instance.ground.GetHeightAt( x, y );
		if ( h < World.instance.waterLevel )
			h = World.instance.waterLevel;
		h += ( ear.position.y - h ) * 0.96f;
		var p = new Vector3( x, h, y );
		ear.position = p;
		Vector3 viewer = new Vector3( (float)( viewDistance*Math.Sin(direction) ), -altitude, (float)( viewDistance*Math.Cos(direction) ) );
		transform.position = p - viewer;
		transform.LookAt( ear );
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
		World.instance.ground.UpdateBlockOffsets( transform.position + transform.forward * forwardForGroundBlocks );
	}

	public void GrabFocus( IDirector director )
	{
		storedX = x;
		storedY = y;
		storedDirection = direction;
		hasStoredValues = true;
		this.director = director;
	}

	public void ReleaseFocus( IDirector director, bool restore = false )
	{
		if ( this.director == director )
			this.director = null;

		if ( restore && hasStoredValues )
		{
			x = storedX;
			y = storedY;
			direction = storedDirection;
			hasStoredValues = false;
		}
		rotateAround = false;
	}

	public void FocusOn( GroundNode node, bool rotateAround = false )
	{
		if ( node == null )
			return;

		storedX = x;
		storedY = y;
		storedDirection = direction;
		hasStoredValues = true;

		var p = node.position;
		x = p.x;
		y = p.z;
		director = null;
		this.rotateAround = rotateAround;
	}

	public void FocusOn( Component component, bool rotateAround = false )
	{
		if ( component == null )
			return;
		storedX = x;
		storedY = y;
		storedDirection = direction;
		hasStoredValues = true;

		x = component.transform.position.x;
		y = component.transform.position.z;
		director = null;
		this.rotateAround = rotateAround;
	}

	void FixedUpdate()
	{
		Vector3 movement = autoMovement;
		if ( Interface.GetKey( KeyCode.A ) )
		{
			movement += transform.right * -0.1f * altitude / 6;
			rotateAround = false;
			director = null;
		}
		if ( Interface.GetKey( KeyCode.D ) )
		{
			movement += transform.right * 0.1f * altitude / 6;
			rotateAround = false;
			director = null;
		}
		if ( Interface.GetKey( KeyCode.W ) )
		{
			movement += transform.forward * 0.13f * altitude / 6;
			rotateAround = false;
			director = null;
		}
		if ( Interface.GetKey( KeyCode.S ) )
		{
			movement += transform.forward * -0.13f * altitude / 6;
			rotateAround = false;
			director = null;
		}
		x += movement.x;
		y += movement.z;

		if ( y < -World.instance.ground.dimension * GroundNode.size / 2 )
		{
			y += World.instance.ground.dimension * GroundNode.size;
			x += World.instance.ground.dimension * GroundNode.size / 2;
			absoluteY -= World.instance.ground.dimension * GroundNode.size;
			absoluteX -= World.instance.ground.dimension * GroundNode.size / 2;
		}
		if ( y > World.instance.ground.dimension * GroundNode.size / 2 )
		{
			y -= World.instance.ground.dimension * GroundNode.size;
			x -= World.instance.ground.dimension * GroundNode.size / 2;
			absoluteY += World.instance.ground.dimension * GroundNode.size;
			absoluteX += World.instance.ground.dimension * GroundNode.size / 2;
		}
		if ( x < -World.instance.ground.dimension * GroundNode.size / 2 + y / 2 )
		{
			x += World.instance.ground.dimension * GroundNode.size;
			absoluteX -= World.instance.ground.dimension * GroundNode.size;
		}
		if ( x > World.instance.ground.dimension * GroundNode.size / 2 + y / 2 )
		{
			x -= World.instance.ground.dimension * GroundNode.size;
			absoluteX += World.instance.ground.dimension * GroundNode.size;
		}

		if ( Interface.GetKey( KeyCode.Q ) )
		{
			rotateAround = false;
			direction += 0.03f;
		}
		if ( Interface.GetKey( KeyCode.E ) )
		{
			rotateAround = false;
			direction -= 0.03f;
		}
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
				targetAltitude += 0.5f;
			if ( Input.GetAxis( "Mouse ScrollWheel" ) > 0 )
				targetAltitude -= 0.5f;
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

	public override GroundNode location { get { return null; } }
}

