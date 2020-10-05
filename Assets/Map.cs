using System;
using UnityEngine;
using UnityEngine.UI;

public class Map : Interface.Panel
{
	public MapImage content;
	static public float zoom = 6f;
	const float zoomMin = 1;
	const float zoomMax = 20;


	static public Map Create()
	{
		return new GameObject().AddComponent<Map>();
	}

	public void Open()
	{
		base.Open();
		name = "Map";
		Frame( 0, 0, 316, 316 );
		content = MapImage.Create();
		content.Setup();
		Init( content.rawImage.rectTransform, 30, -30, 256, 256 );
		Button( 290, -10, 20, 20, Interface.iconExit ).onClick.AddListener( Close );
	}

	new void Update()
	{
		base.Update();

		if ( Input.GetKey( KeyCode.Equals ) )
			zoom *= 0.99f;
		if ( Input.GetKey( KeyCode.Minus ) )
			zoom *= 1.01f;
		if ( zoom < zoomMin )
			zoom = zoomMin;
		if ( zoom > zoomMax )
			zoom = zoomMax;

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

		public void Setup()
		{
			renderTexture = new RenderTexture( 256, 256, 24 );

			rawImage = gameObject.AddComponent<RawImage>();
			name = "MapImage";
			rawImage.texture = renderTexture;

			camera = new GameObject().AddComponent<Camera>();
			camera.name = "MapCamera";
			camera.transform.SetParent( World.instance.ground.transform );
			camera.targetTexture = renderTexture;
			camera.cullingMask &= int.MaxValue - ( 1 << World.layerIndexNotOnMap );
		}

		public void SetTarget( Vector2 position, float zoom, float rotation )
		{
			camera.orthographic = true;
			camera.transform.position = new Vector3( position.x, 20, position.y );
			camera.orthographicSize = zoom;
			camera.transform.rotation = Quaternion.Euler( 90, rotation, 0 );
		}

		void OnDestroy()
		{
			Destroy( camera.gameObject );
		}
	}
}
