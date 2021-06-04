using System;
using UnityEngine;
using UnityEngine.UI;

public class Map : Interface.Panel
{
	public MapImage content;
	static public float zoom = 6f;
	const float zoomMin = 1;
	const float zoomMax = 20;
	public bool fullScreen;

	static public Map Create()
	{
		return new GameObject().AddComponent<Map>();
	}

	public void Open( bool fullScreen = false )
	{
		Transform dialog = new GameObject( "Map dialog" ).transform;
		dialog.transform.SetParent( transform );
		this.fullScreen = fullScreen;
		base.Open( null, 0, 0, 316, 316 );
		name = "Map";
		content = MapImage.Create();
		content.Setup( fullScreen );
		Init( content.rawImage.rectTransform, 30, -30, 256, 256, dialog );
		dialog.gameObject.SetActive( !fullScreen );
	}

	new void Update()
	{
		base.Update();

		if ( Interface.GetKey( KeyCode.Equals ) )
			zoom *= 0.97f;
		if ( Interface.GetKey( KeyCode.Minus ) )
			zoom *= 1.03f;
		if ( Input.GetAxis( "Mouse ScrollWheel" ) > 0 )
			zoom *= 0.82f;
		if ( Input.GetAxis( "Mouse ScrollWheel" ) < 0 )
			zoom *= 1.2f;
		if ( zoom < zoomMin )
			zoom = zoomMin;
		if ( zoom > zoomMax )
			zoom = zoomMax;
		if ( Interface.GetKeyDown( KeyCode.Return ) )
		{
			fullScreen = !fullScreen;
			content.Setup( fullScreen );
		}

		float rotation = World.instance.eye.direction / (float)Math.PI * 180f;
		content.SetTarget( new Vector2( World.instance.eye.x, World.instance.eye.y ), zoom, rotation );
	}

	public class MapImage : MonoBehaviour
	{
		public RenderTexture renderTexture;
		new public Camera camera;
		public RawImage rawImage;

		public static MapImage Create()
		{
			return new GameObject().AddComponent<MapImage>();
		}

		public void Setup( bool fullScreen )
		{
			if ( !fullScreen )
				renderTexture = new RenderTexture( 256, 256, 24 );
			else
				renderTexture = null;

			rawImage = gameObject.AddComponent<RawImage>();
			name = "MapImage";
			rawImage.texture = renderTexture;

			camera = new GameObject().AddComponent<Camera>();
			camera.name = "MapCamera";
			camera.transform.SetParent( World.instance.ground.transform );
			camera.targetTexture = renderTexture;
			camera.cullingMask &= int.MaxValue - ( 1 << World.layerIndexNotOnMap );
			camera.gameObject.AddComponent<CameraHighlight>();
			if ( fullScreen )
				Interface.root.viewport.SetCamera( camera );
		}

		public void SetTarget( Vector2 position, float zoom, float rotation )
		{
			camera.orthographic = true;
			camera.transform.position = new Vector3( position.x, 50, position.y );
			camera.orthographicSize = zoom;
			camera.transform.rotation = Quaternion.Euler( 90, rotation, 0 );
		}

		void OnDestroy()
		{
			Destroy( camera.gameObject );
			Interface.root.viewport.SetCamera( null );
		}
	}
}
