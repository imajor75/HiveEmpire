using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Dialog : Panel
{
	public new Type type;

	public new enum Type
	{
		selectBuildingType
	}

	public static Dialog Open( Type type )
	{
		var g = new GameObject();
		g.name = "Dialog";
		Dialog dialog = g.AddComponent<Dialog>();
		dialog.type = type;
		dialog.transform.SetParent( Panel.canvas.transform );
		Vector2 position = Panel.canvas.pixelRect.center;
		dialog.transform.localPosition = new Vector3( position.x, position.y, 0 );
		Debug.Log( "startx: " + position.x );
		Debug.Log( "startx2: " + dialog.transform.localPosition.x );
		dialog.rectTransform.sizeDelta = new Vector2( 300, 300 );

		int row = -40;
		for ( int i = 0; i < (int)Workshop.Type.total; i++ )
		{
			Text t = dialog.CreateElement<Text>( dialog, 40, row, 150, 20 );
			t.color = Color.yellow;
			t.text = ( (Workshop.Type)i ).ToString();
			t.font = font;
			row -= 30;
		}
		dialog.CreateSelectableElement<Button>( dialog, 20, 20, "woorcutter" );
		return dialog;
	}

	public override void OnPointerClick( PointerEventData data )
	{
		int y = (int)(rectTransform.anchoredPosition.y + rectTransform.sizeDelta.y / 2 - data.position.y);
		int i = (y - 40) / 30;
		if ( i <= (int)Workshop.Type.total )
			Ground.selectedWorkshopType = (Workshop.Type)i;

		Debug.Log( "wtr" + y );
		base.OnPointerClick( data );
	}
}
