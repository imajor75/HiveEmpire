using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
[RequireComponent( typeof( CanvasGroup ) )]
public class Map : Interface.Panel
{
	public MapImage content;
	static public float zoom = Constants.Map.defaultZoom;
	public bool fullScreen;

	static public Map Create()
	{
		return new GameObject( "Map" ).AddComponent<Map>();
	}

	public void Open( bool fullScreen = false )
	{
		this.fullScreen = fullScreen;
		noCloseButton = fullScreen;
		borderWidth = fullScreen ? 0 : 20;
		base.Open( null, 0, 0, 316, 316 );

		content = MapImage.Create();
		content.Setup( fullScreen );
		content.Stretch( 20, 20, -20, -20 ).Link( this );

		if ( fullScreen )
		{
			var c = gameObject.GetComponent<CanvasGroup>();
			c.alpha = 0;
			c.blocksRaycasts = false;
		}
	}

	static public Interface.Hotkey toggleFullscreenHotkey = new Interface.Hotkey( "Toggle map fullscreen", KeyCode.Return );

	new void Update()
	{
		base.Update();

		if ( Interface.mapZoomInHotkey.IsDown() )
			zoom /= 1 + Constants.Map.zoomSpeed;
		if ( Interface.mapZoomOutHotkey.IsDown() )
			zoom *= 1 + Constants.Map.zoomSpeed;
		if ( Input.GetAxis( "Mouse ScrollWheel" ) > 0 && fullScreen )
			zoom /= 1 + Constants.Map.zoomSpeedWithMouseWheel;
		if ( Input.GetAxis( "Mouse ScrollWheel" ) < 0 && fullScreen )
			zoom *= 1 + Constants.Map.zoomSpeedWithMouseWheel;
		if ( zoom < Constants.Map.zoomMin )
			zoom = Constants.Map.zoomMin;
		if ( zoom > Constants.Map.zoomMax )
			zoom = Constants.Map.zoomMax;
		if ( toggleFullscreenHotkey.IsDown() )
		{
			fullScreen = !fullScreen;
			content.Setup( fullScreen );
			var c = gameObject.GetComponent<CanvasGroup>();
			c.alpha = fullScreen ? 0 : 1;
			c.blocksRaycasts = !fullScreen;
		}
		if ( fullScreen )
			eye.moveSensitivity = zoom / 3;

		float rotation = eye.direction / (float)Math.PI * 180f;
		content.SetTarget( new Vector2( eye.x, eye.y ), zoom, rotation );
	}

	[RequireComponent( typeof( RawImage ) )]
	public class MapImage : UIBehaviour
	{
		public RenderTexture renderTexture;
		new public Camera camera;
		public RawImage rawImage;
		public bool fullScreen;

		public static MapImage Create()
		{
			return new GameObject( "Map image" ).AddComponent<MapImage>();
		}

		public void Setup( bool fullScreen )
		{
			this.fullScreen = fullScreen;
			var r = (transform as RectTransform).rect;
			if ( !fullScreen )
				renderTexture = new RenderTexture( (int)r.width, (int)r.height, 24 );
			else
				renderTexture = null;

			rawImage = gameObject.GetComponent<RawImage>();
			rawImage.texture = renderTexture;

			if ( !camera )
				camera = new GameObject( "Map camera").AddComponent<Camera>();
			camera.transform.SetParent( ground.transform );
			camera.targetTexture = renderTexture;
			camera.cullingMask &= int.MaxValue - ( 1 << World.layerIndexNotOnMap );
			camera.gameObject.AddComponent<CameraHighlight>();
			if ( fullScreen )
				root.viewport.SetCamera( camera );
			else
				root.viewport.SetCamera( null );
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
			if ( !fullScreen )
			{
				var r = (transform as RectTransform).rect;
				if ( r.width > 0 && r.height > 0 )
					rawImage.texture = camera.targetTexture = renderTexture = new RenderTexture( (int)r.width, (int)r.height, 24 );
			}
			base.OnRectTransformDimensionsChange();
		}
	}
}
