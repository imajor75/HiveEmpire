using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RoadPanel : Panel
{
	public Road road;
	public Text jam;
	public Text workers;

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
		rectTransform.sizeDelta = new Vector2( 120, 72 );
		jam = CreateElement<Text>( this, 8, -8, 108, iconSize, "Jam text" );
		jam.color = Color.black;
		jam.font = font;
		workers = CreateElement<Text>( this, 8, -40, 108, iconSize, "Worker count" );
		workers.color = Color.black;
		workers.font = font;
	}

	public override void Update()
	{
		base.Update();
		jam.text = "Items waiting: " + road.Jam();
		workers.text = "Worker count: " + road.workers.Count;
	}
}
