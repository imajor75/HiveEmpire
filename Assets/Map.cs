using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
[RequireComponent( typeof( CanvasGroup ) )]
public class Map : Interface.Panel
{
	public MapImage content;
	static public float zoom = Constants.Map.defaultZoom;
	public bool fullscreen;
	UIHelpers.Button toggleGround;

	static public Map Create()
	{
		return new GameObject( "Map" ).AddComponent<Map>();
	}

	public void Open( bool fullscreen = false )
	{
		this.fullscreen = fullscreen;
		noCloseButton = fullscreen;
		borderWidth = fullscreen ? 0 : 20;
		allowInSpectateMode = true;
		base.Open( null, 0, 0, 316, 316 );

		content = MapImage.Create();
		content.Stretch( 20, 20, -20, -20 ).Link( this );

		toggleGround = Image( Interface.Icon.map ).Pin( 0, 20, 20, 20, 0, 0 ).AddToggleHandler( ShowGround ).Link( content ).gameObject.GetComponent<UIHelpers.Button>();
		SetFullscreen( fullscreen );
	}

	void ShowGround( bool on )
	{
		if ( on )
 			content.camera.cullingMask |= 1 << World.layerIndexGround;
		else
 			content.camera.cullingMask &= int.MaxValue - ( 1 << World.layerIndexGround );
	}

	static public Interface.Hotkey toggleFullscreenHotkey = new Interface.Hotkey( "Toggle map fullscreen", KeyCode.Return );
	static public Interface.Hotkey toggleGroundHotkey = new Interface.Hotkey( "Toggle ground on minimap", KeyCode.G );

	new void Update()
	{
		base.Update();
		if ( toggleGroundHotkey.IsPressed() )
			toggleGround.Toggle();


		if ( Interface.mapZoomInHotkey.IsDown() )
			zoom /= 1 + Constants.Map.zoomSpeed;
		if ( Interface.mapZoomOutHotkey.IsDown() )
			zoom *= 1 + Constants.Map.zoomSpeed;
		if ( Input.GetAxis( "Mouse ScrollWheel" ) > 0 && fullscreen )
			zoom /= 1 + Constants.Map.zoomSpeedWithMouseWheel;
		if ( Input.GetAxis( "Mouse ScrollWheel" ) < 0 && fullscreen )
			zoom *= 1 + Constants.Map.zoomSpeedWithMouseWheel;
		if ( zoom < Constants.Map.zoomMin )
			zoom = Constants.Map.zoomMin;
		if ( zoom > Constants.Map.zoomMax )
			zoom = Constants.Map.zoomMax;
		if ( toggleFullscreenHotkey.IsPressed() )
			SetFullscreen( !fullscreen );
		if ( fullscreen )
			eye.moveSensitivity = zoom / 3;

		float rotation = eye.direction / (float)Math.PI * 180f;
		content.SetTarget( new Vector2( eye.x, eye.y ), zoom, rotation );
	}

	public void SetFullscreen( bool fullscreen )
	{
		content.Setup( fullscreen );
		content.Link( fullscreen ? root : content );
		content.rawImage.enabled = !fullscreen;
		this.fullscreen = fullscreen;
		var c = gameObject.GetComponent<CanvasGroup>();
		c.alpha = fullscreen ? 0 : 1;
		c.blocksRaycasts = !fullscreen;
	}

	new void OnDestroy()
	{
		if ( content )
			Destroy( content.gameObject );

		base.OnDestroy();
	}

	[RequireComponent( typeof( RawImage ) )]
	public class MapImage : UIBehaviour
	{
		public RenderTexture renderTexture;
		new public Camera camera;
		public RawImage rawImage;
		public bool fullscreen;

		public static MapImage Create()
		{
			return new GameObject( "Map image" ).AddComponent<MapImage>();
		}

		public void Setup( bool fullscreen )
		{
			this.fullscreen = fullscreen;
			var r = (transform as RectTransform).rect;
			if ( !fullscreen )
				renderTexture = new RenderTexture( (int)r.width, (int)r.height, 24 );
			else
				renderTexture = null;

			rawImage = gameObject.GetComponent<RawImage>();
			rawImage.texture = renderTexture;

			if ( !camera )
				camera = new GameObject( "Map camera").AddComponent<Camera>();
			camera.transform.SetParent( ground.transform );
			camera.targetTexture = renderTexture;
			camera.cullingMask &= int.MaxValue - ( 1 << World.layerIndexNotOnMap ) - ( 1 << World.layerIndexGround ) - ( 1 << Ground.grassLayerIndex ) - ( 1 << World.layerIndexBuilding );
			camera.gameObject.AddComponent<CameraHighlight>();
			if ( fullscreen )
				root.viewport.SetCamera( camera );
			else
				root.viewport.SetCamera( null );
				
			float rotation = eye.direction / (float)Math.PI * 180f;
			SetTarget( new Vector2( eye.x, eye.y ), zoom, rotation );
		}

		public void SetTarget( Vector2 position, float zoom, float rotation )
		{
			camera.orthographic = true;
			camera.transform.position = new Vector3( position.x, 50, position.y );
			camera.orthographicSize = zoom;
			camera.transform.rotation = Quaternion.Euler( 90, rotation, 0 );
		}

		new void OnDestroy()
		{
			base.OnDestroy();
			Destroy( camera.gameObject );
			root.viewport.SetCamera( null );
		}

		new void OnRectTransformDimensionsChange()
		{
			if ( !fullscreen && camera )
			{
				var r = (transform as RectTransform).rect;
				if ( r.width > 0 && r.height > 0 )
					rawImage.texture = camera.targetTexture = renderTexture = new RenderTexture( (int)r.width, (int)r.height, 24 );
			}
			base.OnRectTransformDimensionsChange();
		}
	}
}
