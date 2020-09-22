using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Interface : MonoBehaviour
{
	public static int iconSize = 24;
	public static Font font;
	public World world;
	public GameObject cursor;
	Canvas canvas;
	public GroundNode selectedNode;
	public Workshop.Type selectedWorkshopType = Workshop.Type.unknown;
	public static Sprite templateFrame;
	public static Sprite templateProgress;

	static void Initialize()
	{
		font = (Font)Resources.GetBuiltinResource( typeof( Font ), "Arial.ttf" );
		Assert.IsNotNull( font );
		Texture2D tex = Resources.Load<Texture2D>( "simple UI & icons/box/box_event1" );
		templateFrame = Sprite.Create( tex, new Rect( 0.0f, 0.0f, tex.width, tex.height ), new Vector2( 0.0f, 0.0f ) );
		tex = Resources.Load<Texture2D>( "simple UI & icons/button/board" );
		templateProgress = Sprite.Create( tex, new Rect( 0.0f, 0.0f, tex.width, tex.height ), new Vector2( 0.0f, 0.0f ) );
	}

	void Start()
	{
		Item.Initialize();
		Building.Initialize();
		Road.Initialize();
		Worker.Initialize();
		Flag.Initialize();
		Resource.Initialize();
		Interface.Initialize();

		cursor = GameObject.CreatePrimitive( PrimitiveType.Cube );
		cursor.name = "Cursor";
		cursor.GetComponent<MeshRenderer>().material = Resources.Load<Material>( "Cursor" );
		cursor.transform.localScale *= 0.25f;
		cursor.transform.SetParent( transform );

		world = ScriptableObject.CreateInstance<World>();
		world.NewGame( 792469403 );
	}

	void Update()
	{
		GroundNode node = world.eye.FindNodeAt( Input.mousePosition );
		if ( node != null )
		{
			cursor.transform.localPosition = node.Position();
			CheckNodeContext( node );
		};

		if ( Input.GetKey( KeyCode.Space ) )
			world.speedModifier = 5;
		else
			world.speedModifier = 1;
		if ( Input.GetKeyDown( KeyCode.P ) )
		{
			string fileName = "test.json";
			world.Save( fileName );
			Debug.Log( fileName + " is saved" );
		}
		if ( Input.GetKeyDown( KeyCode.L ) )
		{
			string fileName = "test.json";
			world.Load( "test.json" );
			Debug.Log( fileName + " is loaded" );
		}
		if ( Input.GetKeyDown( KeyCode.N ) )
		{
			world.NewGame( new System.Random().Next() );
			Debug.Log( "New game created" );
		}

	}

	void CheckNodeContext( GroundNode node )
	{
		Player player = world.mainPlayer;
		if ( Input.GetKeyDown( KeyCode.F ) )
		{
			Flag flag = Flag.Create();
			if ( !flag.Setup( world.ground, node, player ) )
				Destroy( flag );
		};
		if ( Input.GetKeyDown( KeyCode.R ) )
			Road.AddNodeToNew( world.ground, node, player );
		//if ( Input.GetKeyDown( KeyCode.V ) )
		//	Dialog.Open( Dialog.Type.selectBuildingType );
		if ( Input.GetKeyDown( KeyCode.B ) && selectedWorkshopType != Workshop.Type.unknown )
			Workshop.Create().Setup( world.ground, node, player, selectedWorkshopType );
		if ( Input.GetMouseButtonDown( 0 ) )
		{
			if ( node.building )
				node.building.OnClicked();
			if ( node.flag )
				node.flag.OnClicked();
			if ( node.road )
				node.road.OnClicked();
		}
		if ( Input.GetKeyDown( KeyCode.O ) )
		{
			selectedNode = node;
			Debug.Log( "Current pos: " + node.x + ", " + node.y );
			Debug.Log( "Distance from main building: " + node.DistanceFrom( world.mainBuilding.node ) );
		}
		if ( Input.GetKeyDown( KeyCode.K ) )
		{
			if ( node.road )
				node.road.Remove();
			if ( node.building )
				node.building.Remove();
			if ( node.flag )
				node.flag.Remove();
		}
	}
	public class Panel : MonoBehaviour, IPointerClickHandler
	{
		public Interface root;

		public Panel Setup()
		{
			//transform.SetParent( canvas.transform );
			//rectTransform.anchoredPosition = Vector2.zero;
			//rectTransform.sizeDelta = new Vector2( 120, 72 );
			return this;
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

		public T CreateSelectableElement<T>( Component parent, int x, int y, string name = "" ) where T : Selectable
		{
			GameObject gameObject = new GameObject();
			if ( name.Length > 0 )
				gameObject.name = name;
			T mainObject = gameObject.AddComponent<T>();
			gameObject.transform.SetParent( parent.transform );
			return mainObject;
		}

		public Image Image( int x, int y, int xs, int ys, Component parent = null )
		{
			return null;
		}

		//protected override void Start()
		//{
		//	last = this;
		//	base.Start();
		//	sprite = templateFrame;
		//	rectTransform.anchorMin = rectTransform.anchorMax = Vector2.zero;
		//}

		// Update is called once per frame
		public virtual void Update()
		{
			//if ( location == null )
			//	return;

			//Vector3 screenPosition = Camera.main.WorldToScreenPoint( location.transform.position + Vector3.up * 2 * GroundNode.size );
			//rectTransform.anchoredPosition = new Vector3( screenPosition.x, screenPosition.y, 0 );
		}

		public virtual void OnPointerClick( PointerEventData data )
		{
			Destroy( gameObject );
		}
	}
	//public class WorkshopPanel : Panel
	//{
	//	public Workshop workshop;
	//	public Image progressBar;

	//	public class BufferUI
	//	{
	//		public Image[] items;
	//	}

	//	public List<BufferUI> buffers;
	//	public Image[] outputs;

	//	public static void Open( Workshop workshop )
	//	{
	//		if ( Panel.last != null )
	//		{
	//			Destroy( Panel.last.gameObject );
	//			Panel.last = null;
	//		}
	//		var g = new GameObject();
	//		g.name = "Workshop panel";
	//		g.AddComponent<WorkshopPanel>().Attach( workshop );
	//	}

	//	public void Attach( Workshop workshop )
	//	{
	//		location = this.workshop = workshop;
	//		transform.SetParent( canvas.transform );
	//		rectTransform.anchoredPosition = Vector2.zero;
	//		rectTransform.sizeDelta = new Vector2( 240, 200 );

	//		Text title = CreateElement<Text>( this, 20, -20 );
	//		title.text = workshop.type.ToString();
	//		title.color = Color.yellow;
	//		title.font = font;

	//		int row = -40;
	//		int col;
	//		buffers = new List<BufferUI>();
	//		foreach ( var b in workshop.buffers )
	//		{
	//			col = 16;
	//			var bui = new BufferUI();
	//			bui.items = new Image[b.size];
	//			for ( int i = 0; i < b.size; i++ )
	//			{
	//				Image image = CreateElement<Image>( this, col, row, iconSize, iconSize, b.itemType.ToString() );
	//				image.sprite = Item.sprites[(int)b.itemType];
	//				col += iconSize;
	//				bui.items[i] = image;
	//			}
	//			row -= iconSize * 2;
	//			buffers.Add( bui );
	//		}

	//		row -= iconSize / 2;
	//		col = 16;
	//		outputs = new Image[workshop.outputMax];
	//		for ( int i = 0; i < workshop.outputMax; i++ )
	//		{
	//			Image image = CreateElement<Image>( this, col, row, iconSize, iconSize, workshop.outputType.ToString() );
	//			image.sprite = Item.sprites[(int)workshop.outputType];
	//			col += iconSize;
	//			outputs[i] = image;
	//		}

	//		progressBar = CreateElement<Image>( this, 20, row - iconSize - iconSize / 2, iconSize * 8, iconSize, "Progress" );
	//		progressBar.sprite = templateProgress;
	//	}

	//	void UpdateIconRow( Image[] icons, int full, int half )
	//	{
	//		for ( int i = 0; i < icons.Length; i++ )
	//		{
	//			float a = 0;
	//			if ( i < half + full )
	//				a = 0.5f;
	//			if ( i < full )
	//				a = 1;
	//			icons[i].color = new Color( 1, 1, 1, a );
	//		}
	//	}

	//	// Update is called once per frame
	//	public override void Update()
	//	{
	//		base.Update();

	//		for ( int j = 0; j < buffers.Count; j++ )
	//			UpdateIconRow( buffers[j].items, workshop.buffers[j].stored, workshop.buffers[j].onTheWay );

	//		UpdateIconRow( outputs, workshop.output, 0 );
	//		if ( workshop.working )
	//		{
	//			progressBar.rectTransform.sizeDelta = new Vector2( iconSize * 8 * workshop.progress, iconSize );
	//			progressBar.color = Color.white;
	//		}
	//		else
	//			progressBar.color = Color.red;
	//	}
	//}
	//public class StockPanel : Panel
	//{
	//	public Stock stock;
	//	public Text[] counts = new Text[(int)Item.Type.total];

	//	public static void Open( Stock stock )
	//	{
	//		if ( Panel.last != null )
	//		{
	//			Destroy( Panel.last.gameObject );
	//			Panel.last = null;
	//		}
	//		var g = new GameObject();
	//		g.name = "Stock panel";
	//		g.AddComponent<StockPanel>().Attach( stock );
	//	}

	//	public void Attach( Stock stock )
	//	{
	//		location = this.stock = stock;
	//		transform.SetParent( canvas.transform );
	//		rectTransform.anchoredPosition = Vector2.zero;
	//		rectTransform.sizeDelta = new Vector2( 200, 200 );

	//		int row = -20;
	//		for ( int j = 0; j < (int)Item.Type.total; j++ )
	//		{
	//			Image i = CreateElement<Image>( this, 16, row, iconSize, iconSize, ( (Item.Type)j ).ToString() );
	//			i.sprite = Item.sprites[j];
	//			Text t = CreateElement<Text>( this, 40, row, 0, 0, ( (Item.Type)j ).ToString()+" count" );
	//			t.color = Color.yellow;
	//			t.font = font;
	//			counts[j] = t;
	//			row -= iconSize;
	//		}
	//	}

	//	public override void Update()
	//	{
	//		base.Update();
	//		for ( int i = 0; i < (int)Item.Type.total; i++ )
	//			counts[i].text = stock.content[i].ToString();
	//	}
	//}

	//public class Dialog : Panel
	//{
	//	public new Type type;

	//	public new enum Type
	//	{
	//		selectBuildingType
	//	}

	//	public static Dialog Open( Type type )
	//	{
	//		var g = new GameObject();
	//		g.name = "Dialog";
	//		Dialog dialog = g.AddComponent<Dialog>();
	//		dialog.type = type;
	//		dialog.transform.SetParent( Panel.canvas.transform );
	//		Vector2 position = Panel.canvas.pixelRect.center;
	//		dialog.transform.localPosition = new Vector3( position.x, position.y, 0 );
	//		dialog.rectTransform.sizeDelta = new Vector2( 300, 300 );

	//		int row = -40;
	//		for ( int i = 0; i < (int)Workshop.Type.total; i++ )
	//		{
	//			Text t = dialog.CreateElement<Text>( dialog, 40, row, 150, 20 );
	//			t.color = Color.yellow;
	//			t.text = ( (Workshop.Type)i ).ToString();
	//			t.font = font;
	//			row -= 30;
	//		}
	//		dialog.CreateSelectableElement<Button>( dialog, 20, 20, "woorcutter" );
	//		return dialog;
	//	}

	//	public override void OnPointerClick( PointerEventData data )
	//	{
	//		Interface root = GameObject.FindObjectOfType<Interface>();

	//		int y = (int)(rectTransform.anchoredPosition.y + rectTransform.sizeDelta.y / 2 - data.position.y);
	//		int i = (y - 40) / 30;
	//		if ( i <= (int)Workshop.Type.total )
	//			root.selectedWorkshopType = (Workshop.Type)i;

	//		base.OnPointerClick( data );
	//	}
	//}
	//public class RoadPanel : Panel
	//{
	//	public Road road;
	//	public Text jam;
	//	public Text workers;

	//	public static RoadPanel Create()
	//	{
	//		return new GameObject().AddComponent<RoadPanel>();
	//	}

	//	public RoadPanel Setup( Road road )
	//	{
	//		base.Setup();
	//		this.road = road;
	//		jam = CreateElement<Text>( this, 8, -8, 108, iconSize, "Jam text" );
	//		jam.color = Color.black;
	//		jam.font = font;
	//		workers = CreateElement<Text>( this, 8, -40, 108, iconSize, "Worker count" );
	//		workers.color = Color.black;
	//		workers.font = font;
	//		return this;
	//	}

	//	public override void Update()
	//	{
	//		base.Update();
	//		jam.text = "Items waiting: " + road.Jam();
	//		workers.text = "Worker count: " + road.workers.Count;
	//	}
	//}
	public class FlagPanel : Panel
	{
		public Flag flag;
		public Image[] items = new Image[Flag.maxItems];


		public static FlagPanel Create()
		{
			return new GameObject().AddComponent<FlagPanel>();
		}

		public FlagPanel Setup( Flag flag )
		{
			if ( base.Setup() == null )
				return null;
			this.flag = flag;
			int col = 16;
			for ( int i = 0; i < Flag.maxItems; i++ )
			{
				items[i] = Image( col, 8, iconSize, iconSize );
				items[i].name = "item " + i;
				col += iconSize;
			}
			return this;
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
}
