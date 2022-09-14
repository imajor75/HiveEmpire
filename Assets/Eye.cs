using Newtonsoft.Json;
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.Rendering;

[RequireComponent( typeof( AudioListener ) )]
public class Eye : HiveObject
{
	public float altitude = Constants.Eye.defaultAltitude;
	public float targetAltitude = Constants.Eye.defaultAltitude;
	public float altitudeDirection = Constants.Eye.defaultAltitudeDirection, altitudeDirectionTarget = Constants.Eye.defaultAltitudeDirection;
	public float x, y, height;
	public float absoluteX, absoluteY;
	public float direction;
	public List<StoredPosition> oldPositions = new ();
	public float autoStorePositionTimer;
	public bool currentPositionStored;
	public float autoRotate;
	public Vector2 autoMove = Vector2.zero;
	public float moveSensitivity;
	[JsonIgnore]
	public bool mapMode;
	DepthOfField depthOfField;
	[JsonIgnore]
	PostProcessLayer ppl;
	public Node target;
	public float targetApproachSpeed;
	[JsonIgnore]
	public CameraGrid cameraGrid;
	[JsonIgnore]
	public Highlight highlight;

	public void SetMapMode( bool mapMode )
	{
		this.mapMode = mapMode;
		cameraGrid.orthographic = mapMode;
		if ( mapMode )
		{
			cameraGrid.cullingMask = int.MaxValue - (1 << Ground.Grass.layerIndex ) - (1 << World.layerIndexBuildings) - (1 << World.layerIndexUnits) - (1 << World.layerIndexRoads);
			if ( !Interface.Viewport.showGround )
				cameraGrid.cullingMask = cameraGrid.cullingMask - (1 << World.layerIndexWater) - (1 << World.layerIndexGround);
		}
		else
			cameraGrid.cullingMask = int.MaxValue - (1 << World.layerIndexMapOnly);
		RenderSettings.fog = !mapMode;
	}

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
	[Obsolete( "Compatibility with old files", true )]
	bool enableSideCameras { set {} }

	public static Eye Create()
	{
		return new GameObject( "eye" ).AddComponent<Eye>();
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
		cameraGrid = new GameObject( "Camera grid" ).AddComponent<CameraGrid>();
		cameraGrid.transform.SetParent( transform, false );
		cameraGrid.Setup();
		bool depthOfField = Constants.Eye.depthOfField;
		if ( depthOfField )
		{
			var ppv = world.light.GetComponent<PostProcessVolume>();
			if ( ppv && ppv.profile )
				depthOfField = ppv.profile.settings[0] as DepthOfField;
			ppl = GetComponent<PostProcessLayer>();
		}

		highlight = new GameObject( "Highlight" ).AddComponent<Highlight>();
		highlight.transform.SetParent( transform );

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
			Vector3 near = cameraGrid.center.ScreenToWorldPoint( new Vector3( Screen.width / 2, 0, cameraGrid.center.farClipPlane ) );
			Vector3 far = cameraGrid.center.ScreenToWorldPoint( new Vector3( Screen.width / 2, Screen.height, cameraGrid.center.farClipPlane ) );
			Vector3 cameraPos = cameraGrid.center.transform.position;
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
			ppl.enabled = eye.highlight.type == Eye.Highlight.Type.none;

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

		if ( Interface.cameraZoomOutHotkeyHold.IsDown() )
			targetAltitude += Constants.Eye.altitudeChangeSpeed * Time.unscaledDeltaTime;
		if ( Interface.cameraZoomInHotkeyHold.IsDown() )
			targetAltitude -= Constants.Eye.altitudeChangeSpeed * Time.unscaledDeltaTime;
		if ( Interface.cameraRaiseHotkey.IsDown() )
			altitudeDirectionTarget -= Constants.Eye.altitudeDirectopmChangeSpeed * Time.unscaledDeltaTime;
		if ( Interface.cameraLowerHotkey.IsDown() )
			altitudeDirectionTarget += Constants.Eye.altitudeDirectopmChangeSpeed * Time.unscaledDeltaTime;
		if ( altitudeDirectionTarget < Constants.Eye.minAltitudeDirection )
			altitudeDirectionTarget = Constants.Eye.minAltitudeDirection;
		if ( altitudeDirectionTarget > Constants.Eye.maxAltitudeDirection )
			altitudeDirectionTarget = Constants.Eye.maxAltitudeDirection;
		
		if ( root.viewport.mouseOver )
		{
			if ( Interface.cameraZoomOutHotkey.IsPressed() )
				targetAltitude += Constants.Eye.altitudeChangeStep * Time.unscaledDeltaTime;
			if ( Interface.cameraZoomInHotkey.IsPressed() )
				targetAltitude -= Constants.Eye.altitudeChangeStep * Time.unscaledDeltaTime;
			moveSensitivity = targetAltitude / 6;
		}
		if ( targetAltitude < Constants.Eye.minAltitude )
			targetAltitude = Constants.Eye.minAltitude;
		if ( targetAltitude > Constants.Eye.maxAltitude )
			targetAltitude = Constants.Eye.maxAltitude;

		var f = Constants.Eye.altitudeSmoothness * deltaTime;
		altitude = altitude * ( 1 - f ) + targetAltitude * f;
		altitudeDirection = altitudeDirection * ( 1 - f ) + altitudeDirectionTarget * f;

		UpdateTransformation();

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
		if ( mapMode )
		{
			transform.position = position + Vector3.up * 50;
			transform.LookAt( position, new Vector3( (float)Math.Sin(direction), 0, (float)Math.Cos(direction) ) );
			if ( cameraGrid )
				cameraGrid.orthographicSize = altitude;
			return;
		}
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
		if ( restore )
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
		var forwardDir = transform.forward;
		if ( mapMode )
			forwardDir = transform.up;
		return transform.right * side * moveSensitivity + forwardDir * forward * moveSensitivity;
	}

	public interface IDirector
	{
		void SetCameraTarget( Eye eye );
	}

	public class CameraGrid : HiveCommon
	{
		public List<Camera> cameras = new ();
		public Camera center { get { return cameras[4]; } }
		public Camera first;
		public Camera last;
		public bool enableSideCameras = true;

		public void Setup( float depth = 0 )
		{
			for ( int y = -1; y <= 1; y++ )
				for ( int x = -1; x <= 1; x++ )
					cameras.Add( new GameObject( $"Camera {x}:{y}" ).AddComponent<Camera>() );
			first = cameras.First();
			last = cameras.Last();
			foreach ( var camera in cameras )
			{
				camera.clearFlags = CameraClearFlags.Nothing;
				camera.cullingMask = ~( 1 << World.layerIndexMapOnly );
				camera.transform.SetParent( transform, false );
				camera.nearClipPlane = 0.03f;
				camera.farClipPlane = 100;
				camera.allowMSAA = false;
				camera.depth = depth;
				camera.gameObject.AddComponent<Highlight.Applier>().enabled = false;
			}
			first.depth = depth-1;
			first.clearFlags = CameraClearFlags.Skybox;
			last.gameObject.GetComponent<Highlight.Applier>().enabled = true;
			last.depth = depth+1;
		}

		void OnEnable()
		{
			foreach ( var camera in cameras )
				camera.enabled = true;
		}

		void OnDisable()
		{
			foreach ( var camera in cameras )
				camera.enabled = false;
		}

		void LateUpdate()
		{
			if ( ground == null )
			{
				first.clearFlags = CameraClearFlags.SolidColor;
				first.backgroundColor = Color.black;
				return;
			}

			float closest = float.MaxValue, furthest = float.MinValue;
			var forward = center.transform.forward;
			for ( int i = 0; i < cameras.Count; i++ )
			{
				int x = ( i % 3 ) - 1;
				int y = ( i / 3 ) - 1;
				var right = new Vector3( 1, 0, 0 ) * Constants.Node.size * ground.dimension;
				var up = new Vector3( 0.5f, 0, 1 ) * Constants.Node.size * ground.dimension;
				var newPosition = transform.position + x * right + y * up;
				cameras[i].transform.position = newPosition;
				if ( x != 0 || y != 0 )
					cameras[i].enabled = enableSideCameras;
				float depth = -Math.Abs( newPosition.z ) - Math.Abs( newPosition.x - newPosition.z / 2 );
				if ( depth < closest )
					closest = depth;
				if ( depth > furthest )
					furthest = depth;
				cameras[i].depth = depth;
			}
			foreach ( var camera in cameras )
			{
				if ( camera.depth == furthest )
				{
					if ( last != camera )
					{
						last.gameObject.GetComponent<Highlight.Applier>().enabled = false;
						camera.gameObject.GetComponent<Highlight.Applier>().enabled = true;
						camera.clearFlags = CameraClearFlags.Nothing;
						last = camera;
					}
				}
				else if ( camera.depth == closest )
				{
					if ( first != camera )
					{
						camera.clearFlags = CameraClearFlags.Skybox;
						first = camera;
					}
				}
				else
					camera.clearFlags = CameraClearFlags.Nothing;
			}
		}

		public int cullingMask
		{
			get { return center.cullingMask; }
			set { foreach ( var camera in cameras ) camera.cullingMask = value; }
		}

		public bool orthographic
		{
			get { return center.orthographic; }
			set { foreach ( var camera in cameras ) camera.orthographic = value; }
		}

		public float orthographicSize
		{
			get { return center.orthographicSize; }
			set { foreach ( var camera in cameras ) camera.orthographicSize = value; }
		}

		public RenderTexture targetTexture
		{
			set { foreach ( var camera in cameras ) camera.targetTexture = value; }
		}
	}

	public class Highlight : HiveCommon
	{
		public Type type;
		public List<Building.Type> buildingTypes = new (); 
		public Ground.Area area;
		public Mesh volume;
		Node volumeCenter;
		int volumeRadius;
		public RenderTexture mask, blur, smoothMask;
		public static Material markerMaterial, mainMaterial, volumeMaterial;
		public Smoother colorSmoother = new (), maskSmoother = new ();
		public static int maskValueOffsetID;
		public CommandBuffer maskCreator;
		public int maskCreatorCRC;
		public GameObject owner;
		public float strength, strengthChange;
		static int strengthID;

		public class Smoother
		{
			public RenderTexture temporary;
			public List<Material> materials = new ();

			public void Setup( int width, int height, int steps, RenderTextureFormat format = RenderTextureFormat.ARGB32 )
			{
				if ( temporary )
					Destroy( temporary );
				materials.Clear();

				if ( steps > 1 )
				{
					temporary = new RenderTexture( width, height, 0, format );
					temporary.name = "Smoother temporary";
				}

				for ( int i = 0; i < steps; i++ )
				{
					var material = new Material( Resources.Load<Shader>( "shaders/blur" ) );
					var factor = (float)Math.Pow( 4, i );
					material.SetFloat( "_XStart", -2.5f / width * factor );
					material.SetFloat( "_YStart", -2.5f / height * factor );
					material.SetFloat( "_XMove", 2f / width * factor );
					material.SetFloat( "_YMove", 2f / height * factor );
					materials.Add( material );
				}
			}
			public void Blur( RenderTexture source, RenderTexture destination )
			{
				if ( temporary )
				{
					Assert.global.AreEqual( source.width, temporary.width );
					Assert.global.AreEqual( source.height, temporary.height );
					Assert.global.AreEqual( destination.width, temporary.width );
					Assert.global.AreEqual( destination.height, temporary.height );
				}
				for ( int i = 0; i < materials.Count; i++ )
				{
					var blitSource = source;
					var left = materials.Count - i - 1;
					var blitDestination = left % 2 == 0 ? destination : temporary;
					if ( i > 0 )
						blitSource = blitDestination == temporary ? destination : temporary;
					Graphics.Blit( blitSource, blitDestination, materials[i] );
				}
			}
		}

		public int CRC
		{
			get
			{
				int CRC = 0;
				if ( type == Type.buildingType )
				{
					if ( eye.mapMode )
						CRC++;
					foreach ( var t in buildingTypes )
						CRC += (int)t;
					CRC += root.mainTeam.stocks.Count();
					CRC += root.mainTeam.workshops.Count();
					CRC += root.mainTeam.guardHouses.Count();
					if ( buildingTypes.Contains( (Building.Type)(Workshop.Type.mill) ) )
						CRC += time;	// TODO In case of mills the command buffer is always recreated
				}
				if ( type == Type.area )
				{
					CRC += area.center.x;
					CRC += area.center.y * 100;
					CRC += area.radius;
				}
				return CRC;
			}
		}

		public enum Type
		{
			none,
			buildingType,
			area
		}

		public static void Initialize()
		{
			markerMaterial = new Material( Resources.Load<Shader>( "shaders/highlightMarker" ) );
			mainMaterial = new Material( Resources.Load<Shader>( "shaders/Highlight" ) );
			volumeMaterial = new Material( Resources.Load<Shader>( "shaders/HighlightVolume" ) );
			mainMaterial.SetColor( "_GlowColor", Constants.Eye.highlightEffectGlowColor );
			strengthID = Shader.PropertyToID( "_Strength" );

			maskValueOffsetID = Shader.PropertyToID( "_MaskValueOffset" );
		}

		public void HighlightArea( Ground.Area area, GameObject owner )
		{
			type = Type.area;
			this.area = area;
			this.owner = owner;
			strength = 0;
			strengthChange = 1 / Constants.Eye.highlightSwitchTime;
		}

		public void HighlightBuildingTypes( Building.Type buildingType0, Building.Type buildingType1 = Building.Type.unknown, GameObject owner = null )
		{
			type = Type.buildingType;
			buildingTypes.Clear();
			buildingTypes.Add( buildingType0 );
			if ( buildingType1 != Building.Type.unknown )
				buildingTypes.Add( buildingType1 );
			this.owner = owner;
			strength = 0;
			strengthChange = 1 / Constants.Eye.highlightSwitchTime;
		}

		public void TurnOff()
		{
			strengthChange = -1 / Constants.Eye.highlightSwitchTime;
		}

		public void ApplyHighlight( RenderTexture source, RenderTexture target )
		{
			if ( maskCreator == null )
				Graphics.Blit( source, target );
			else
			{
				GL.modelview = eye.cameraGrid.center.worldToCameraMatrix;
				GL.LoadProjectionMatrix( eye.cameraGrid.center.projectionMatrix );
				Graphics.ExecuteCommandBuffer( maskCreator );
				colorSmoother.Blur( source, blur );
				maskSmoother.Blur( mask, smoothMask );
				Graphics.Blit( source, target, mainMaterial );
			}
		}

		void Update()
		{
			if ( owner == null )
				type = Type.none;
			if ( type == Type.area && area.center == null )
				type = Type.none;
			strength += strengthChange * Time.unscaledDeltaTime;
			if ( strength > 1 )
				strength = 1;
			if ( strength < 0 )
				type = Type.none;
			mainMaterial.SetFloat( strengthID, strength );

			if ( type == Type.none )
			{
				if ( mask )
				{
					Destroy( mask );
					mask = null;
					maskCreator = null;
				}
			}
			else
			{
				if ( mask == null || mask.width != Screen.width || mask.height != Screen.height )
				{
					Destroy( mask );

					mask = new RenderTexture( Screen.width, Screen.height, 0, RenderTextureFormat.RFloat );
					mask.name = "Eye highlight mask";
					smoothMask = new RenderTexture( Screen.width, Screen.height, 0, RenderTextureFormat.RFloat );
					smoothMask.name = "Eye highlight smooth mask";
					mainMaterial.SetTexture( "_Mask", mask );
					mainMaterial.SetTexture( "_SmoothMask", smoothMask );
					maskSmoother.Setup( Screen.width, Screen.height, Constants.Eye.highlightEffectGlowSize, RenderTextureFormat.RFloat );
					maskCreator = null;
				}

				if ( blur == null || blur.width != Screen.width || blur.height != Screen.height )
				{
					Destroy( blur );

					blur = new RenderTexture( Screen.width, Screen.height, 0 );
					blur.name = "Eye highlight blur";
					mainMaterial.SetTexture( "_Blur", blur );
					colorSmoother.Setup( Screen.width, Screen.height, Constants.Eye.highlightEffectLevels );
				}

				if ( maskCreator == null || maskCreatorCRC != CRC )
				{
					var maskId = new RenderTargetIdentifier( mask );
					var currentId = new RenderTargetIdentifier( BuiltinRenderTextureType.CurrentActive );

					maskCreator = new ();
					maskCreator.name = "Highlight mask creation";

					maskCreator.SetRenderTarget( maskId, currentId );
					maskCreator.ClearRenderTarget( false, true, Color.black );

					void DrawMeshRepeatedly( Mesh mesh, Matrix4x4 location, Material material )
					{
						var rightShift = Matrix4x4.Translate( new Vector3( ground.dimension * Constants.Node.size, 0, 0 ) );
						var leftShift = Matrix4x4.Translate( new Vector3( -ground.dimension * Constants.Node.size, 0, 0 ) );
						var upShift = Matrix4x4.Translate( new Vector3( ground.dimension * Constants.Node.size / 2, 0, ground.dimension * Constants.Node.size ) );
						var downShift = Matrix4x4.Translate( new Vector3( -ground.dimension * Constants.Node.size / 2, 0, -ground.dimension * Constants.Node.size ) );
						maskCreator.DrawMesh( mesh, location, material );
						maskCreator.DrawMesh( mesh, rightShift * location, material );
						maskCreator.DrawMesh( mesh, leftShift * location, material );
						maskCreator.DrawMesh( mesh, upShift * location, material );
						maskCreator.DrawMesh( mesh, downShift * location, material );
					}
					if ( type == Type.buildingType )
					{
						void ProcessBuilding( Building building )
						{
							void DrawMeshRecursively( GameObject o )
							{
								MeshFilter filter;
								o.TryGetComponent<MeshFilter>( out filter );
								if ( filter && filter.mesh )
									DrawMeshRepeatedly( filter.mesh, o.transform.localToWorldMatrix, markerMaterial );
								foreach ( Transform child in o.transform )
									DrawMeshRecursively( child.gameObject );
							}
							if ( !buildingTypes.Contains( building.type ) )
								return;
								
							if ( eye.mapMode )
							{
								var shiftUp = Matrix4x4.Translate( new Vector3( 0, 10, 0 ) );
								DrawMeshRepeatedly( building.mapIndicator.GetComponent<MeshCollider>().sharedMesh, shiftUp * building.mapIndicator.transform.localToWorldMatrix, markerMaterial );
							}
							else
								DrawMeshRecursively( building.body );
						}
						foreach ( var stock in root.mainTeam.stocks )
							ProcessBuilding( stock );
						foreach ( var workshop in root.mainTeam.workshops )
							ProcessBuilding( workshop );
						foreach ( var guardHouse in root.mainTeam.guardHouses )
							ProcessBuilding( guardHouse );
					}
					if ( type == Type.area )
					{
						float s = ( area.radius + 0.5f ) * Constants.Node.size;
						DrawMeshRepeatedly( volume, Matrix4x4.TRS( area.center.position, Quaternion.identity, new Vector3( s, 20, s ) ), volumeMaterial );
					}
					maskCreatorCRC = CRC;
				}
			}

			if ( type == Type.area )
			{
				float maskValueOffset = 0;
				var distance = area.center.DistanceFrom( eye.transform.position );
				if ( distance < ( area.radius + 0.5 ) * Constants.Node.size && !eye.mapMode )
					maskValueOffset = 1;

				mainMaterial.SetFloat( maskValueOffsetID, maskValueOffset );
			}
		}

		void Start()
		{
			volume = new ();
			var vertices = new Vector3[Constants.Node.neighbourCount * 2];
			var corners = new int[,] { { 1, 1 }, { 0, 1 }, { -1, 0 }, { -1, -1 }, { 0, -1 }, { 1, 0 } };
			for ( int i = 0; i < Constants.Node.neighbourCount; i++ )
			{
				float x = corners[i, 0] - corners[i, 1] / 2f;
				float y = corners[i, 1];
				vertices[i * 2 + 0] = new Vector3( x, -1, y );
				vertices[i * 2 + 1] = new Vector3( x, +1, y );
			}
			volume.vertices = vertices;

			var triangles = new int[Constants.Node.neighbourCount * 2 * 3 + 2 * 3 * (Constants.Node.neighbourCount - 2)];
			for ( int i = 0; i < Constants.Node.neighbourCount; i++ )
			{
				int a = i * 2;
				int b = i * 2 + 2;
				if ( b == Constants.Node.neighbourCount * 2 )
					b = 0;

				triangles[i * 2 * 3 + 0] = a + 0;
				triangles[i * 2 * 3 + 1] = a + 1;
				triangles[i * 2 * 3 + 2] = b + 0;

				triangles[i * 2 * 3 + 3] = a + 1;
				triangles[i * 2 * 3 + 4] = b + 1;
				triangles[i * 2 * 3 + 5] = b + 0;
			}
			Assert.global.AreEqual( Constants.Node.neighbourCount, 6 );
			int cap = Constants.Node.neighbourCount * 6;
			triangles[cap++] = 0;
			triangles[cap++] = 2;
			triangles[cap++] = 10;

			triangles[cap++] = 10;
			triangles[cap++] = 2;
			triangles[cap++] = 8;

			triangles[cap++] = 8;
			triangles[cap++] = 2;
			triangles[cap++] = 4;

			triangles[cap++] = 8;
			triangles[cap++] = 4;
			triangles[cap++] = 6;

			triangles[cap++] = 11;
			triangles[cap++] = 3;
			triangles[cap++] = 1;

			triangles[cap++] = 9;
			triangles[cap++] = 3;
			triangles[cap++] = 11;

			triangles[cap++] = 5;
			triangles[cap++] = 3;
			triangles[cap++] = 9;

			triangles[cap++] = 7;
			triangles[cap++] = 5;
			triangles[cap++] = 9;
			volume.triangles = triangles;
		}

		public class Applier : HiveCommon
		{
			void OnRenderImage( RenderTexture source, RenderTexture target )
			{
				eye.highlight.ApplyHighlight( source, target );
			}
		}
	}
}
