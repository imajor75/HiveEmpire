using System.Collections;
using System.Collections.Generic;
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
			rawImage = gameObject.AddComponent<RawImage>();
			name = "MapImage";
			rawImage.texture = renderTexture;

			renderTexture = new RenderTexture( 256, 256, 24 );

			camera = gameObject.AddComponent<Camera>();
			camera.targetTexture = renderTexture;
		}

		void Update()
		{
		}
	}
}
