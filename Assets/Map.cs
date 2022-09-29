using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
[RequireComponent( typeof( CanvasGroup ) )]
public class Map : Interface.Panel
{
	public MapImage content;
	static public float zoom = Constants.Map.defaultZoom;

	static public Map Create()
	{
		return new GameObject( "Map" ).AddComponent<Map>();
	}

	public void Open()
	{
		allowInSpectateMode = true;
		base.Open( null, 0, 0, 316, 316 );

		content = MapImage.Create();
		content.Stretch( 20, 20, -20, -20 ).Link( this );
		content.Setup();
	}

	void ShowGround( bool on )
	{
		if ( on )
 			content.camera.cullingMask |= 1 << World.layerIndexGround | 1 << World.layerIndexWater;
		else
 			content.camera.cullingMask &= int.MaxValue - (1 << World.layerIndexGround) - (1 << World.layerIndexWater);
	}

	static public Interface.Hotkey toggleFullscreenHotkey = new Interface.Hotkey( "Toggle map fullscreen", KeyCode.Return );

	new void Update()
	{
		base.Update();
		if ( Interface.mapZoomInHotkey.IsDown() )
			zoom /= 1 + Constants.Map.zoomSpeed;
		if ( Interface.mapZoomOutHotkey.IsDown() )
			zoom *= 1 + Constants.Map.zoomSpeed;
		if ( zoom < Constants.Map.zoomMin )
			zoom = Constants.Map.zoomMin;
		if ( zoom > Constants.Map.zoomMax )
			zoom = Constants.Map.zoomMax;

		float rotation = eye.direction / (float)Math.PI * 180f;
		content.SetTarget( new Vector2( eye.x, eye.y ), zoom, rotation );
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
		new public Eye.CameraGrid camera;
		public static MapImage instance = null;
		public RawImage rawImage;

		public static MapImage Create()
		{
			return new GameObject( "Map Image" ).AddComponent<MapImage>();
		}

		public void Setup()
		{
			instance = this;
			var r = (transform as RectTransform).rect;
			renderTexture = new RenderTexture( (int)r.width, (int)r.height, 24 );

			rawImage = gameObject.GetComponent<RawImage>();
			rawImage.texture = renderTexture;

			if ( !camera )
			{
				camera = new GameObject( "Map Camera").AddComponent<Eye.CameraGrid>();
				camera.Setup( 10 );
			}
			camera.transform.SetParent( ground.transform );
			camera.targetTexture = renderTexture;
			camera.cullingMask = int.MaxValue - ( 1 << Ground.Grass.layerIndex ) - ( 1 << World.layerIndexBuildings ) - ( 1 << World.layerIndexUnits ) - ( 1 << World.layerIndexRoads ) - ( 1 << LayerMask.NameToLayer( "Trees" ) );
			if ( !Interface.Viewport.showGround )
				camera.cullingMask -= ( 1 << World.layerIndexGround ) + ( 1 << World.layerIndexWater );
				
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
			instance = null;
			base.OnDestroy();
			Destroy( camera.gameObject );
		}

		new void OnRectTransformDimensionsChange()
		{
			if ( camera )
			{
				var r = (transform as RectTransform).rect;
				if ( r.width > 0 && r.height > 0 )
					rawImage.texture = camera.targetTexture = renderTexture = new RenderTexture( (int)r.width, (int)r.height, 24 );
			}
			base.OnRectTransformDimensionsChange();
		}
	}
}
