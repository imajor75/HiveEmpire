using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FlagPanel : Panel
{
	public Flag flag;
	public Image[] items = new Image[Flag.maxItems];

	public static void Open( Flag flag )
	{
		var g = new GameObject();
		g.name = "Flag panel";
		g.AddComponent<FlagPanel>().Attach( flag );
	}

	public void Attach( Flag flag )
	{
		location = this.flag = flag;
		transform.SetParent( canvas.transform );
		rectTransform.anchoredPosition = Vector2.zero;
		rectTransform.sizeDelta = new Vector2( 200, 50 );
		int col = 0;
		for ( int i = 0; i < Flag.maxItems; i++ )
		{
			items[i] = CreateElement<Image>( this, col, -8, iconSize, iconSize, "item " + i );
			col += iconSize;
		}
	}

	public override void Update()
	{
		base.Update();

		// TODO Skip empty slots
		for ( int i = 0; i < Flag.maxItems; i++ )
		{
			if ( flag.items[i] == null )
				items[i].enabled = false;
			else
			{
				items[i].enabled = true;
				items[i].sprite = Item.sprites[(int)flag.items[i].type];
			}
		}
	}
}
