using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class WorkshopPanel : Panel
{
	public Workshop workshop;
	public Image progressBar;

	public class BufferUI
	{
		public Image[] items;
	}

	public List<BufferUI> buffers;
	public Image[] outputs;

	public static void Open( Workshop workshop )
	{
		var g = new GameObject();
		g.name = "Workshop panel";
		g.AddComponent<WorkshopPanel>().Attach( workshop );
	}

	public void Attach( Workshop workshop )
	{
		location = this.workshop = workshop;
		transform.SetParent( canvas.transform );
		rectTransform.anchoredPosition = Vector2.zero;
		rectTransform.sizeDelta = new Vector2( 200, 100 );

		int row = 0;
		int col;
		buffers = new List<BufferUI>();
		foreach ( var b in workshop.buffers )
		{
			col = 0;
			var bui = new BufferUI();
			bui.items = new Image[b.size];
			for ( int i = 0; i < b.size; i++ )			
			{
				Image image = CreateElement<Image>( this, col, row, iconSize, iconSize, b.itemType.ToString() );
				image.sprite = Item.sprites[(int)b.itemType];
				col += iconSize;
				bui.items[i] = image;
			}
			row -= iconSize * 2;
			buffers.Add( bui );
		}

		row -= iconSize / 2;
		col = 0;
		outputs = new Image[workshop.outputMax];
		for ( int i = 0; i < workshop.outputMax; i++ )
		{
			Image image = CreateElement<Image>( this, col, row, iconSize, iconSize, workshop.outputType.ToString() );
			image.sprite = Item.sprites[(int)workshop.outputType];
			col += iconSize;
			outputs[i] = image;
		}

		progressBar = CreateElement<Image>( this, 0, row - iconSize - iconSize / 2, iconSize * 8, iconSize, "Progress" );
	}

	void UpdateIconRow( Image[] icons, int full, int half )
	{
		for ( int i = 0; i < icons.Length; i++ )
		{
			float a = 0;
			if ( i < half+full )
				a = 0.5f;
			if ( i < full )
				a = 1;
			icons[i].color = new Color( 1, 1, 1, a );
		}
	}

	// Update is called once per frame
	public override void Update()
	{
		base.Update();

		for ( int j = 0; j < buffers.Count; j++ )
			UpdateIconRow( buffers[j].items, workshop.buffers[j].stored, workshop.buffers[j].onTheWay );

		UpdateIconRow( outputs, workshop.output, 0 );
		if ( workshop.working )
		{
			progressBar.rectTransform.sizeDelta = new Vector2( iconSize * 8 * workshop.progress, iconSize );
			progressBar.color = Color.green;
		}
		else
			progressBar.color = Color.red;
    }
}
