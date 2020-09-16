using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Assertions;

public class Panel : Image, IPointerClickHandler
{
	public static int iconSize = 24;
	static public Canvas canvasCached;
	public Component location;
	public static Font font;
	public static Panel last;
	public static Sprite templateFrame;
	public static Sprite templateProgress;

	public new Canvas canvas
    {
		get 
		{
			if ( canvasCached == null)
				canvasCached = Object.FindObjectOfType<Canvas>();
			return canvasCached;
		}
    }
	static public void Initialize()
	{
		font = (Font)Resources.GetBuiltinResource(typeof(Font), "Arial.ttf");
		Assert.IsNotNull( font );
		Texture2D tex = Resources.Load<Texture2D>( "simple UI & icons/box/box_event1" );
		templateFrame = Sprite.Create( tex, new Rect( 0.0f, 0.0f, tex.width, tex.height ), new Vector2( 0.0f, 0.0f ) );
		tex = Resources.Load<Texture2D>( "simple UI & icons/button/board" );
		templateProgress = Sprite.Create( tex, new Rect( 0.0f, 0.0f, tex.width, tex.height ), new Vector2( 0.0f, 0.0f ) );
	}

	public T CreateElement<T>( Component parent, int x, int y, int width = 0, int height = 0, string name = "" ) where T : Graphic
	{
		GameObject gameObject = new GameObject();
		if ( name.Length > 0 )
			gameObject.name = name;
		T mainObject = gameObject.AddComponent<T>();
		gameObject.transform.SetParent( parent.transform );
		mainObject.rectTransform.anchorMin = mainObject.rectTransform.anchorMax = Vector2.up;
		mainObject.rectTransform.pivot = Vector2.up;
		mainObject.rectTransform.anchoredPosition = new Vector2( x, y );
		if ( width > 0 && height > 0 )
			mainObject.rectTransform.sizeDelta = new Vector2( width, height );
		return mainObject;
	}

	protected override void Start()
	{
		last = this;
		base.Start();
		sprite = templateFrame;
		rectTransform.anchorMin = rectTransform.anchorMax = Vector2.zero;
	}

    // Update is called once per frame
    public virtual void Update()
    {
		if ( location == null )
			return;

		Vector3 screenPosition = Camera.main.WorldToScreenPoint( location.transform.position + Vector3.up * 2 * GroundNode.size );
		rectTransform.anchoredPosition = new Vector3( screenPosition.x, screenPosition.y, 0 );
	}

	public void OnPointerClick( PointerEventData data )
	{
		Destroy( gameObject );
	}
}
