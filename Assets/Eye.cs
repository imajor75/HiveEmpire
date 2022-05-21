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
	public float altitudeDirection = Constants.Eye.defaultAltitudeDirection, altitudeDirectionTarget = Constants.Eye.defaultAltitudeDirection;
	public float x, y, height;
	public float absoluteX, absoluteY;
	public float direction;
	public List<StoredPosition> oldPositions = new List<StoredPosition>();
	public float autoStorePositionTimer;
	public bool currentPositionStored;
	public float autoRotate;
	public Vector2 autoMove = Vector2.zero;
	public new Camera camera;
	public float moveSensitivity;
	DepthOfField depthOfField;
	[JsonIgnore]
	PostProcessLayer ppl;
	public Node target;
	public float targetApproachSpeed;

	[JsonIgnore]
	public IDirector director;
	
	new World world 
	{ 
		get { return HiveCommon.world; } 
		[Obsolete( "Compatibility with old files", true )]
		set {} 
	}

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
	[Obsolete( "Compatibility with old files", true )]
	int autoStorePositionCounter { set {} }
	[Obsolete( "Compatibility with old files", true )]
	float targetX { set {} }
	[Obsolete( "Compatibility with old files", true )]
	float targetY { set {} }
	[Obsolete( "Compatibility with old files", true )]
	bool hasTarget { set {} }
	[Obsolete( "Compatibility with old files", true )]
	bool rotateAround { set {} }

	public static Eye Create()
	{
		var o = Instantiate( Resources.Load<GameObject>( "eye" ) );
		o.name = "Eye";
		Eye eye = o.GetComponent<Eye>();
		eye.destroyed = false;
		return eye;
	}

	public Eye Setup( World world )
	{
		base.Setup();
		return this;
	}

	public void StopAutoChange()
	{
		autoRotate = 0;
		autoMove = Vector2.zero;
	}


	public void Awake()
	{
		transform.SetParent( world.transform );
	}

	new public void Start()
	{
		gameObject.AddComponent<CameraHighlight>();
		camera = GetComponent<Camera>();
		var ppv = world.light.GetComponent<PostProcessVolume>();
		if ( ppv && ppv.profile )
			depthOfField = ppv.profile.settings[0] as DepthOfField;
		ppl = GetComponent<PostProcessLayer>();
		bool depthOfField = Constants.Eye.depthOfField;
		if ( depthOfField )
		{
			var ppv = world.light.GetComponent<PostProcessVolume>();
			if ( ppv && ppv.profile )
				depthOfField = ppv.profile.settings[0] as DepthOfField;
			ppl = GetComponent<PostProcessLayer>();
		}
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

	public Vector3 visibleAreaCenter
	{
		get
		{
			Vector3 near = camera.ScreenToWorldPoint( new Vector3( Screen.width / 2, 0, camera.farClipPlane ) );
			Vector3 far = camera.ScreenToWorldPoint( new Vector3( Screen.width / 2, Screen.height, camera.farClipPlane ) );
			Vector3 cameraPos = camera.transform.position;
			float nearFactor = ( cameraPos.y - Constants.Eye.groundHeightDefault ) / ( cameraPos.y - near.y );
			Vector3 nearGround = Vector3.Lerp( cameraPos, near, nearFactor );
			if ( far.y < Constants.Eye.groundHeightDefault )
			{
				float farFactor = ( cameraPos.y - Constants.Eye.groundHeightDefault ) / ( cameraPos.y - far.y );
				Vector3 farGround = Vector3.Lerp( cameraPos, far, farFactor );
				float dist = ( farGround - nearGround ).magnitude;
				float center = dist / 2;
				if ( center > ground.dimension * Constants.Node.size * Constants.Eye.maxDistance )
					center = ground.dimension * Constants.Node.size * Constants.Eye.maxDistance;
				return Vector3.Lerp( nearGround, farGround, center / dist );
			}
			return position + ( position - nearGround );
		}
	}

    public override Node location
	{
		get
		{	
			return world.ground.GetNode( (int)x, (int)y );
		}
	}

    new private void Update()
	{
		if ( ground == null )
			return;
			
		var deltaTime = Time.unscaledDeltaTime;
		if ( deltaTime > 0.5f )
			deltaTime = 0.5f;

		if ( ppl )
			ppl.enabled = root.highlightType == Interface.HighlightType.none;

		while ( oldPositions.Count > Constants.Eye.maxNumberOfSavedPositions )
			oldPositions.RemoveAt( 0 );

		autoStorePositionTimer += Time.unscaledDeltaTime;
		if ( autoStorePositionTimer >= Constants.Eye.autoStorePositionAfter && !currentPositionStored )
		{
			oldPositions.Add( new StoredPosition() { x = x, y = y, direction = direction } );
			currentPositionStored = true;
		}

		var h = ground.GetHeightAt( x, y );
		if ( h < world.waterLevel )
			h = world.waterLevel;
		if ( height > 0 )
			height += ( h - height ) * Constants.Eye.heightFollowSpeed * deltaTime;
		else
			height = h;
		UpdateTransformation();
		camera.cullingMask = ~( 1 << World.layerIndexMapOnly );
		camera.clearFlags = CameraClearFlags.Skybox;

		if ( director == null )
			director = null;
		else
		{
			IDirector director = this.director;
			director.SetCameraTarget( this );
			this.director = director;
		}

		if ( depthOfField )
			depthOfField.focusDistance.value = altitude;

		Vector3 movement = Vector3.zero;
		if ( Interface.cameraLeftHotkey.IsDown() )
			movement += Move( -Constants.Eye.moveSpeed * Time.unscaledDeltaTime, 0 );
		if ( Interface.cameraRightHotkey.IsDown() )
			movement += Move( Constants.Eye.moveSpeed * Time.unscaledDeltaTime, 0 );
		if ( Interface.cameraUpHotkey.IsDown() )
			movement += Move( 0, Constants.Eye.moveSpeed * Time.unscaledDeltaTime * 1.3f );
		if ( Interface.cameraDownHotkey.IsDown() )
			movement += Move( 0, -Constants.Eye.moveSpeed * Time.unscaledDeltaTime * 1.3f );
		if ( target )
		{
			targetApproachSpeed += Time.unscaledDeltaTime;
			if ( targetApproachSpeed > 1 )
				targetApproachSpeed = 1;
			movement += ( target.GetPositionRelativeTo( position ) - position ) * targetApproachSpeed * Time.unscaledDeltaTime;
		}
		x += movement.x + autoMove.x * Time.unscaledDeltaTime;
		y += movement.z + autoMove.y * Time.unscaledDeltaTime;

		while ( y < -ground.dimension * Constants.Node.size / 2 )
		{
			y += ground.dimension * Constants.Node.size;
			x += ground.dimension * Constants.Node.size / 2;
			absoluteY -= ground.dimension * Constants.Node.size;
			absoluteX -= ground.dimension * Constants.Node.size / 2;
		}
		while ( y > ground.dimension * Constants.Node.size / 2 )
		{
			y -= ground.dimension * Constants.Node.size;
			x -= ground.dimension * Constants.Node.size / 2;
			absoluteY += ground.dimension * Constants.Node.size;
			absoluteX += ground.dimension * Constants.Node.size / 2;
		}
		while ( x < -ground.dimension * Constants.Node.size / 2 + y / 2 )
		{
			x += ground.dimension * Constants.Node.size;
			absoluteX -= ground.dimension * Constants.Node.size;
		}
		while ( x > ground.dimension * Constants.Node.size / 2 + y / 2 )
		{
			x -= ground.dimension * Constants.Node.size;
			absoluteX += ground.dimension * Constants.Node.size;
		}

		if ( Interface.cameraRotateCCWHotkey.IsDown() )
		{
			StopAutoChange();
			direction -= Constants.Eye.rotateSpeed * Time.unscaledDeltaTime;
		}
		if ( Interface.cameraRotateCWHotkey.IsDown() )
		{
			StopAutoChange();
			direction += Constants.Eye.rotateSpeed * Time.unscaledDeltaTime;
		}
		direction += autoRotate * Time.unscaledDeltaTime;
		if ( direction >= Math.PI * 2 )
			direction -= (float)Math.PI * 2;
		if ( direction < 0 )
			direction += (float)Math.PI * 2;

		if ( Interface.cameraZoomOutHotkey.IsDown() )
			targetAltitude += Constants.Eye.altitudeChangeSpeed * Time.unscaledDeltaTime;
		if ( Interface.cameraZoomInHotkey.IsDown() )
			targetAltitude -= Constants.Eye.altitudeChangeSpeed * Time.unscaledDeltaTime;
		if ( Interface.cameraRaiseHotkey.IsDown() )
			altitudeDirectionTarget -= Constants.Eye.altitudeDirectopmChangeSpeed * Time.unscaledDeltaTime;
		if ( Interface.cameraLowerHotkey.IsDown() )
			altitudeDirectionTarget += Constants.Eye.altitudeDirectopmChangeSpeed * Time.unscaledDeltaTime;
		if ( altitudeDirectionTarget < Constants.Eye.minAltitudeDirection )
			altitudeDirectionTarget = Constants.Eye.minAltitudeDirection;
		if ( altitudeDirectionTarget > Constants.Eye.maxAltitudeDirection )
			altitudeDirectionTarget = Constants.Eye.maxAltitudeDirection;
		
		if ( camera.enabled && root.viewport.mouseOver )
		{
			if ( Input.GetAxis( "Mouse ScrollWheel" ) < 0 )     // TODO Use something else instead of strings here
				targetAltitude += Constants.Eye.altitudeChangeSpeedWithMouseWheel * Time.unscaledDeltaTime;
			if ( Input.GetAxis( "Mouse ScrollWheel" ) > 0 )
				targetAltitude -= Constants.Eye.altitudeChangeSpeedWithMouseWheel * Time.unscaledDeltaTime;
			moveSensitivity = targetAltitude / 6;
		}
		if ( targetAltitude < Constants.Eye.minAltitude )
			targetAltitude = Constants.Eye.minAltitude;
		if ( targetAltitude > Constants.Eye.maxAltitude )
			targetAltitude = Constants.Eye.maxAltitude;

		var f = Constants.Eye.altitudeSmoothness * deltaTime;
		altitude = altitude * ( 1 - f ) + targetAltitude * f;
		altitudeDirection = altitudeDirection * ( 1 - f ) + altitudeDirectionTarget * f;
		base.Update();
	}

	public void GrabFocus( IDirector director )
	{
		oldPositions.Add( new StoredPosition() { x = x, y = y, direction = direction } );
		this.director = director;
	}

	public bool RestoreOldPosition()
	{
		// When currentPositionStored is true, the camera is in a position which was already saved. So the last saved position needs to be ignored.
		if ( currentPositionStored )
			oldPositions.RemoveAt( oldPositions.Count-1 );
		
		if ( oldPositions.Count == 0 )
			return false;

		currentPositionStored = true;
		var last = oldPositions[oldPositions.Count-1];
		x = last.x;
		y = last.y;
		direction = last.direction;
		height = -1;
		StopAutoChange();
		oldPositions.RemoveAt( oldPositions.Count-1 );
		return true;
	}

	public void UpdateTransformation()
	{
		var position = new Vector3( x, height, y );
		float horizontal = (float)Math.Sin(altitudeDirection) * altitude;
		float vertical = (float)Math.Cos(altitudeDirection) * altitude;
		Vector3 viewer = new Vector3( (float)( horizontal*Math.Sin(direction) ), -vertical, (float)( horizontal*Math.Cos(direction) ) );
		transform.position = position - viewer;
		transform.LookAt( position );
	}

	public void ReleaseFocus( IDirector director, bool restore = false )
	{
		if ( this.director != director )
			return;

		this.director = null;
		RestoreOldPosition();
		StopAutoChange();
	}

	public void FocusOn( HiveObject target, bool rotateAround = false, bool mark = false, bool useLogicalPosition = false, bool approach = true )
	{
		if ( target == null )
			return;
		if ( approach && target.location != this.target )
			oldPositions.Add( new StoredPosition() { x = x, y = y, direction = direction } );

		if ( approach )
		{
			this.target = target.location;
			height = target.location.height;
		}
		else
		{
			this.target = null;
			if ( useLogicalPosition )
			{
				x = target.location.positionInViewport.x;
				y = target.location.positionInViewport.z;
				height = target.location.positionInViewport.y;
			}
			else
			{
				x = target.transform.position.x;
				y = target.transform.position.z;
				height = target.location.position.y;
			}
		}
		director = null;
		this.autoRotate = rotateAround ? Constants.Eye.autoRotateSpeed : 0;
		root.viewport.markEyePosition = mark;
		autoStorePositionTimer = 0;
		currentPositionStored = false;
		UpdateTransformation();
	}

	void OnPositionChanged()
	{
		StopAutoChange();
		autoStorePositionTimer = 0;
		currentPositionStored = false;
	}

	public Vector3 Move( float side, float forward )
	{
		OnPositionChanged();
		root.viewport.markEyePosition = false;
		director = null;
		target = null;
		return transform.right * side * moveSensitivity + transform.forward * forward * moveSensitivity;
	}

	public interface IDirector
	{
		void SetCameraTarget( Eye eye );
	}
}

public class CameraHighlight : HiveCommon
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
		var volume = root.highlightVolume;
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
		if ( root.highlightType == Interface.HighlightType.none )
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

	void Update()
	{
		blurMaterial.SetFloat( "_OffsetX", 1f / Screen.width );
		blurMaterial.SetFloat( "_OffsetY", 1f / Screen.height );
		highlightMaterial.SetFloat( "_OffsetX", 2f / Screen.width );
		highlightMaterial.SetFloat( "_OffsetY", 2f / Screen.height );
	}
}

