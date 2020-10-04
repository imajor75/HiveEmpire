using System.Collections;
using System.Collections.Generic;
using System.Xml.Serialization;
using UnityEngine;
using UnityEngine.UI;

public class Map : Interface.Panel
{
	public MapImage content;

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
		}

		public void SetTarget( Vector2 position, float zoom, float rotation )
		{
			camera.orthographic = true;
			camera.transform.position = new Vector3( position.x, zoom, position.y );
			camera.transform.rotation = Quaternion.Euler( 0, rotation, 0 );
		}

		void Update()
		{
			SetTarget( new Vector2( World.instance.eye.x, World.instance.eye.y ), 10, 0 );
		}
	}
}
