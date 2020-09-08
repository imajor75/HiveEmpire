using UnityEngine;
using UnityEngine.UI;

public class StockPanel : Panel
{
	public Stock stock;
	public Text[] counts = new Text[(int)Item.Type.total];

	public static void Open( Stock stock )
	{
		var g = new GameObject();
		g.name = "Stock panel";
		g.AddComponent<StockPanel>().Attach( stock );
	}

	public void Attach( Stock stock )
	{
		location = this.stock = stock;
		transform.SetParent( canvas.transform );
		rectTransform.anchoredPosition = Vector2.zero;
		rectTransform.sizeDelta = new Vector2( 200, 200 );

		int row = 0;
		for ( int j = 0; j < (int)Item.Type.total; j++ )
		{
			Image i = CreateElement<Image>( this, 8, row, iconSize, iconSize, ( (Item.Type)j ).ToString() );
			i.sprite = Item.sprites[j];
			Text t = CreateElement<Text>( this, 32, row, 0, 0, ( (Item.Type)j ).ToString()+" count" );
			t.color = Color.black;
			t.font = font;
			counts[j] = t;
			row -= iconSize;
		}
	}

	public override void Update()
	{
		base.Update();
		for ( int i = 0; i < (int)Item.Type.total; i++ )
			counts[i].text = stock.content[i].ToString();
	}
}
