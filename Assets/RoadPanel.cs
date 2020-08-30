using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RoadPanel : Panel
{
	public Road road;
	public Text jam;

	public static void Open( Road road )
	{
		var g = new GameObject();
		g.name = "Road panel";
		g.AddComponent<RoadPanel>().Attach( road );
	}

	public void Attach( Road road )
	{
		location = this.road = road;
		transform.SetParent( canvas.transform );
		rectTransform.anchoredPosition = Vector2.zero;
		rectTransform.sizeDelta = new Vector2( 120, 40 );
		jam = CreateElement<Text>( this, 8, -8, 0, 0, "Jam text" );
		jam.color = Color.black;
		jam.font = font;
	}

	public override void Update()
	{
		base.Update();
		jam.text = "Items waiting: " + road.Jam();
	}
}
