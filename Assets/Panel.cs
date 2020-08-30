using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;
using UnityEngine.Assertions;

public class Panel : Image, IPointerClickHandler
{
	public static int iconSize = 24;
	static new public Canvas canvas;
	public Component location;
	public static Font font;

	static public void Initialize()
	{
		var o = GameObject.Find( "Canvas" );
		canvas = o.GetComponent<Canvas>();
		font = (Font)Resources.GetBuiltinResource(typeof(Font), "Arial.ttf");
		Assert.IsNotNull( font );
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
		base.Start();
		rectTransform.anchorMin = rectTransform.anchorMax = Vector2.zero;
	}

    // Update is called once per frame
    public virtual void Update()
    {
		Vector3 screenPosition = Camera.main.WorldToScreenPoint( location.transform.position + Vector3.up * 2 * GroundNode.size );

		rectTransform.anchoredPosition = new Vector3( screenPosition.x, screenPosition.y, 0 );
	}

	public void OnPointerClick( PointerEventData data )
	{
		Destroy( gameObject );
	}
}
