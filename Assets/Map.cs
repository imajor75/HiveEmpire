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
		Frame( 0, 0, 316, 316 );
		content = new GameObject().AddComponent<MapImage>();
		Init( content.rectTransform, 30, -30, 256, 256 );
	}

	public class MapImage : RawImage
	{
		public RenderTexture renderTexture;
		new public Camera camera;

		new void Start()
		{
			base.Start();
			name = "MapImage";
			texture = renderTexture;

			camera = new GameObject().AddComponent<Camera>();
			camera.targetTexture = renderTexture;
		}

		void Update()
		{
		}
	}
}
