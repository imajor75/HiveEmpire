using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

[RequireComponent( typeof( Camera ), typeof( AudioListener ) )]
public class Eye : HiveObject
{
	public float altitude = Constants.Eye.defaultAltitude;
	public float targetAltitude = Constants.Eye.defaultAltitude;
	[JsonIgnore]
	public float viewDistance = Constants.Eye.defaultViewDistance;
	public World world;
	public float x, y, height;
	public float absoluteX, absoluteY;
	public float direction;
	public List<StoredPosition> oldPositions = new List<StoredPosition>();
	public int autoStorePositionCounter;
	public Vector3 autoMovement;
	public bool rotateAround;
	public new Camera camera;
	public float moveSensitivity;
	[JsonIgnore]
	public IDirector director;

	[Obsolete( "Compatibility with old files", true )]
	float forwardForGroundBlocks { set {} }
	[Obsolete( "Compatibility with old files", true )]
	public float storedX { set {} }
	[Obsolete( "Compatibility with old files", true )]
	public float storedY { set {} }
	[Obsolete( "Compatibility with old files", true )]
	public float storedDirection { set {} }
	[Obsolete( "Compatibility with old files", true )]
	bool hasStoredValues { set {} }

	public static Eye Create()
	{
		return new GameObject().AddComponent<Eye>();
	}

	public Eye Setup( World world )
	{
		this.world = world;
		return this;
	}

	new public void Start()
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
		base.Start();
	}

	[Serializable]
	public class StoredPosition
	{
		public float x, y, direction;		
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

	// Approximation only
	public Vector3 visibleAreaCenter
	{
		get
		{
			return transform.position + transform.forward * Constants.Eye.forwardForGroundBlocks;
		}
	}

    public override Node location => throw new NotImplementedException();

    private void Update()
	{
		var h = World.instance.ground.GetHeightAt( x, y );
		if ( h < World.instance.waterLevel )
			h = World.instance.waterLevel;
		if ( height > 0 )
			height += ( h - height ) * Constants.Eye.heightFollowSpeed;
		else
			height = h;
		var position = new Vector3( x, height, y );
		Vector3 viewer = new Vector3( (float)( viewDistance*Math.Sin(direction) ), -altitude, (float)( viewDistance*Math.Cos(direction) ) );
		transform.position = position - viewer;
		transform.LookAt( position );
		if ( director == null )
		{
			director = null;
			viewDistance = Constants.Eye.defaultViewDistance;
		}
		else
		{
			viewDistance = Constants.Eye.defaultViewDistanceWithDirector;
			IDirector director = this.director;
			director.SetCameraTarget( this );
			this.director = director;
		}
	}

	public void GrabFocus( IDirector director )
	{
		oldPositions.Add( new StoredPosition() { x = x, y = y, direction = direction } );
		this.director = director;
	}

	public bool RestoreOldPosition()
	{
		if ( autoStorePositionCounter > Constants.Eye.autoStorePositionAfter )
		{
			// When the condition above is true, the camera is in a position which was already saved. So the last saved position needs to be ignored.
			oldPositions.RemoveAt( oldPositions.Count-1 );
			autoStorePositionCounter = 0;
		}

		if ( oldPositions.Count == 0 )
			return false;

		var last = oldPositions[oldPositions.Count-1];
		x = last.x;
		y = last.y;
		direction = last.direction;
		height = -1;
		rotateAround = false;
		oldPositions.RemoveAt( oldPositions.Count-1 );
		return true;
	}

	public void ReleaseFocus( IDirector director, bool restore = false )
	{
		if ( this.director != director )
			return;

		this.director = null;
		RestoreOldPosition();
		rotateAround = false;
	}

	public void FocusOn( HiveObject target, bool rotateAround = false, bool mark = false )
	{
		if ( target == null )
			return;
		oldPositions.Add( new StoredPosition() { x = x, y = y, direction = direction } );

		x = target.transform.position.x;
		y = target.transform.position.z;
		height = -1;
		director = null;
		this.rotateAround = rotateAround;
		Interface.root.viewport.markEyePosition = mark;
	}

	void OnPositionChanged()
	{
		rotateAround = false;
		autoStorePositionCounter = 0;
	}

	Vector3 Move( float side, float forward )
	{
		OnPositionChanged();
		Interface.root.viewport.markEyePosition = false;
		director = null;
		return transform.right * side * moveSensitivity + transform.forward * forward * moveSensitivity;
	}

	void FixedUpdate()
	{
		while ( oldPositions.Count > Constants.Eye.maxNumberOfSavedPositions )
			oldPositions.RemoveAt( oldPositions.Count-1 );

		if ( autoStorePositionCounter++ == Constants.Eye.autoStorePositionAfter )
			oldPositions.Add( new StoredPosition() { x = x, y = y, direction = direction } );

		Vector3 movement = autoMovement;
		if ( Interface.cameraLeftHotkey.IsHold() )
			movement += Move( -Constants.Eye.moveSpeed, 0 );
		if ( Interface.cameraRightHotkey.IsHold() )
			movement += Move( Constants.Eye.moveSpeed, 0 );
		if ( Interface.cameraUpHotkey.IsHold() )
			movement += Move( 0, Constants.Eye.moveSpeed * 1.3f );
		if ( Interface.cameraDownHotkey.IsHold() )
			movement += Move( 0, -Constants.Eye.moveSpeed * 1.3f );
		x += movement.x;
		y += movement.z;

		if ( y < -World.instance.ground.dimension * Constants.Node.size / 2 )
		{
			y += World.instance.ground.dimension * Constants.Node.size;
			x += World.instance.ground.dimension * Constants.Node.size / 2;
			absoluteY -= World.instance.ground.dimension * Constants.Node.size;
			absoluteX -= World.instance.ground.dimension * Constants.Node.size / 2;
		}
		if ( y > World.instance.ground.dimension * Constants.Node.size / 2 )
		{
			y -= World.instance.ground.dimension * Constants.Node.size;
			x -= World.instance.ground.dimension * Constants.Node.size / 2;
			absoluteY += World.instance.ground.dimension * Constants.Node.size;
			absoluteX += World.instance.ground.dimension * Constants.Node.size / 2;
		}
		if ( x < -World.instance.ground.dimension * Constants.Node.size / 2 + y / 2 )
		{
			x += World.instance.ground.dimension * Constants.Node.size;
			absoluteX -= World.instance.ground.dimension * Constants.Node.size;
		}
		if ( x > World.instance.ground.dimension * Constants.Node.size / 2 + y / 2 )
		{
			x -= World.instance.ground.dimension * Constants.Node.size;
			absoluteX += World.instance.ground.dimension * Constants.Node.size;
		}

		if ( Interface.cameraRotateCCWHotkey.IsHold() )
		{
			rotateAround = false;
			direction += Constants.Eye.rotateSpeed;
		}
		if ( Interface.cameraRotateCWHotkey.IsHold() )
		{
			rotateAround = false;
			direction -= Constants.Eye.rotateSpeed;
		}
		if ( rotateAround )
			direction += Constants.Eye.autoRotateSpeed;
		if ( direction >= Math.PI * 2 )
			direction -= (float)Math.PI * 2;
		if ( direction < 0 )
			direction += (float)Math.PI * 2;

		if ( Interface.cameraZoomOutHotkey.IsHold() )
			targetAltitude *= Constants.Eye.altitudeChangeSpeed;
		if ( Interface.cameraZoomInHotkey.IsHold() )
			targetAltitude /= Constants.Eye.altitudeChangeSpeed;
		if ( camera.enabled && Interface.root.viewport.mouseOver )
		{
			if ( Input.GetAxis( "Mouse ScrollWheel" ) < 0 )     // TODO Use something else instead of strings here
				targetAltitude += Constants.Eye.altitudeChangeSpeedWithMouseWheel;
			if ( Input.GetAxis( "Mouse ScrollWheel" ) > 0 )
				targetAltitude -= Constants.Eye.altitudeChangeSpeedWithMouseWheel;
			moveSensitivity = targetAltitude / 6;
		}
		if ( targetAltitude < Constants.Eye.minAltitude )
			targetAltitude = Constants.Eye.minAltitude;
		if ( targetAltitude > Constants.Eye.maxAltitude )
			targetAltitude = Constants.Eye.maxAltitude;


		altitude += ( targetAltitude - altitude ) * Constants.Eye.altitudeSmoothness;
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
		highlightMaterial = new Material( Resources.Load<Shader>( "shaders/Highlight" ) );
		blurMaterial = new Material( Resources.Load<Shader>( "shaders/Blur" ) );
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

	public override Node location { get { return null; } }
}

