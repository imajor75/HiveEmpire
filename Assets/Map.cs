using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
[RequireComponent( typeof( CanvasGroup ) )]
public class Map : Interface.Panel
{
	public MapImage content;
	static public float zoom = 6f;
	const float zoomMin = 1;
	const float zoomMax = 20;
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

	new void Update()
	{
		base.Update();

		if ( Interface.GetKey( KeyCode.KeypadPlus ) )
			zoom *= 0.97f;
		if ( Interface.GetKey( KeyCode.KeypadMinus ) )
			zoom *= 1.03f;
		if ( Input.GetAxis( "Mouse ScrollWheel" ) > 0 && fullScreen )
			zoom *= 0.82f;
		if ( Input.GetAxis( "Mouse ScrollWheel" ) < 0 && fullScreen )
			zoom *= 1.2f;
		if ( zoom < zoomMin )
			zoom = zoomMin;
		if ( zoom > zoomMax )
			zoom = zoomMax;
		if ( Interface.GetKeyDown( KeyCode.Return ) )
		{
			fullScreen = !fullScreen;
			content.Setup( fullScreen );
			var c = gameObject.GetComponent<CanvasGroup>();
			c.alpha = fullScreen ? 0 : 1;
			c.blocksRaycasts = !fullScreen;
		}
		if ( fullScreen )
			World.instance.eye.moveSensitivity = zoom / 3;

		float rotation = World.instance.eye.direction / (float)Math.PI * 180f;
		content.SetTarget( new Vector2( World.instance.eye.x, World.instance.eye.y ), zoom, rotation );
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
			camera.transform.SetParent( World.instance.ground.transform );
			camera.targetTexture = renderTexture;
			camera.cullingMask &= int.MaxValue - ( 1 << World.layerIndexNotOnMap );
			camera.gameObject.AddComponent<CameraHighlight>();
			if ( fullScreen )
				Interface.root.viewport.SetCamera( camera );
			else
				Interface.root.viewport.SetCamera( null );
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
			Interface.root.viewport.SetCamera( null );
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
