using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Interface : MonoBehaviour
{
	public List<Panel> panels = new List<Panel>();
	public static int iconSize = 24;
	public static Font font;
	public World world;
	public GameObject cursor;
	Canvas canvas;
	public GroundNode selectedNode;
	public Workshop.Type selectedWorkshopType = Workshop.Type.unknown;
	public static Sprite templateFrame;
	public static Sprite templateProgress;
	public GameObject debug;
	public static Interface instance;

	public Interface()
	{
		instance = this;
	}

	public void Clear()
	{
		foreach ( Transform d in debug.transform )
			Destroy( d.gameObject );
	}

	static void Initialize()
	{
		font = (Font)Resources.GetBuiltinResource( typeof( Font ), "Arial.ttf" );
		Assert.IsNotNull( font );
		Texture2D tex = Resources.Load<Texture2D>( "simple UI & icons/box/box_event1" );
		templateFrame = Sprite.Create( tex, new Rect( 0.0f, 0.0f, tex.width, tex.height ), new Vector2( 0.0f, 0.0f ) );
		tex = Resources.Load<Texture2D>( "simple UI & icons/button/board" );
		templateProgress = Sprite.Create( tex, new Rect( 0.0f, 0.0f, tex.width, tex.height ), new Vector2( 0.0f, 0.0f ) );
		Frame.Initialize();
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
		Workshop.Initialize();
		Stock.Initialize();
		GuardHouse.Initialize();

		canvas = gameObject.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		gameObject.AddComponent<GraphicRaycaster>();

		debug = new GameObject();
		debug.name = "Debug";
		debug.transform.SetParent( transform );

		world = ScriptableObject.CreateInstance<World>();
		world.NewGame( 117274283 );
	}

	void Update()
	{
		GroundNode node = world.eye.FindNodeAt( Input.mousePosition );
		if ( node != null )
		{
			if ( cursor == null )
			{
				cursor = GameObject.CreatePrimitive( PrimitiveType.Cube );
				cursor.name = "Cursor";
				cursor.GetComponent<MeshRenderer>().material = Resources.Load<Material>( "Cursor" );
				cursor.transform.localScale *= 0.25f;
				cursor.transform.SetParent( world.ground.transform );
			}
			cursor.transform.localPosition = node.Position();
			CheckNodeContext( node );
		};

		if ( Input.GetKeyDown( KeyCode.V ) )
			BuildingTypeSelector.Create().Open( (int)Input.mousePosition.x, (int)Input.mousePosition.y-Screen.height );

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
		if ( Input.GetKeyDown( KeyCode.J ) )
			GuardHouse.Create().Setup( world.ground, node, player );
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
		public Component target;
		public Image frame;
		public Interface cachedRoot;
		public Interface Root
		{
			get
			{
				if ( cachedRoot == null )
					cachedRoot = GameObject.FindObjectOfType<Interface>();
				return cachedRoot;
			}
		}

		public void Open( Component target = null, int x = 0, int y = 0 )
		{
			foreach ( var panel in Root.panels )
				Destroy( panel.gameObject );
			Root.panels.Add( this );
			name = "Panel";
			frame = gameObject.AddComponent<Image>();
			Init( frame.rectTransform, x, y, 100, 100, Root );
			frame.enabled = false;
			this.target = target;
			UpdatePosition();
		}

		void OnDestroy()
		{
			Assert.IsTrue( Root.panels.Contains( this ) );
			Root.panels.Remove( this );
		}

		public Image Image( int x, int y, int xs, int ys, Sprite picture = null, Component parent = null )
		{
			Image i = new GameObject().AddComponent<Image>();
			i.name = "Image";
			i.sprite = picture;
			Init( i.rectTransform, x, y, xs, ys, parent );
			return i;
		}

		public Frame Frame( int x, int y, int xs, int ys, int borderWidth = 30, Component parent = null )
		{
			Frame f = new GameObject().AddComponent<Frame>();
			f.name = "Image";
			f.borderWidth = borderWidth;
			Init( f.rectTransform, x, y, xs, ys, parent );
			return f;
		}

		public Text Text( int x, int y, string text, Component parent = null )
		{
			Text t = new GameObject().AddComponent<Text>();
			t.name = "Text";
			Init( t.rectTransform, x, y, 200, 20, parent );
			t.font = Interface.font;
			t.text = text;
			t.color = Color.yellow;
			return t;
		}

		void Init( RectTransform t, int x, int y, int xs, int ys, Component parent = null )
		{
			if ( parent == null )
				parent = this;
			t.SetParent( parent.transform );
			t.anchorMin = t.anchorMax = t.pivot = Vector2.up;
			t.anchoredPosition = new Vector2( x, y );
			if ( xs != 0 && ys != 0 )
				t.sizeDelta = new Vector2( xs, ys );
		}

		public virtual void Update()
		{
			UpdatePosition();
		}

		void UpdatePosition()
		{
			if ( target == null )
				return;

			Vector3 screenPosition = Camera.main.WorldToScreenPoint( target.transform.position + Vector3.up * GroundNode.size );
			if ( screenPosition.y > Screen.height )
				screenPosition = Camera.main.WorldToScreenPoint( target.transform.position - Vector3.up * GroundNode.size );
			screenPosition.y -= Screen.height;
			frame.rectTransform.anchoredPosition = screenPosition;
		}

		public virtual void OnPointerClick( PointerEventData data )
		{
			Destroy( gameObject );
		}
	}

	public class Frame : Image
	{
		public int borderWidth = 30;

		static Sprite[] pieces = new Sprite[9];
		public static void Initialize()
		{
			string[] files = {
				"simple UI & icons/box/frame11",
				"simple UI & icons/box/frame21",
				"simple UI & icons/box/frame31",
				"simple UI & icons/box/frame12",
				"simple UI & icons/box/frame22",
				"simple UI & icons/box/frame32",
				"simple UI & icons/box/frame13",
				"simple UI & icons/box/frame23",
				"simple UI & icons/box/frame33" };
			for ( int i = 0; i < files.Length; i++ )
			{
				var t = Resources.Load<Texture2D>( files[i] );
				pieces[i] = Sprite.Create( t, new Rect( 0, 0, t.width, t.height ), Vector2.zero );
			}
		}

		new void Start()
		{
			int w = borderWidth;
			int a = 0;
			base.Start();
			Image i11 = new GameObject().AddComponent<Image>();
			i11.rectTransform.SetParent( transform );
			i11.rectTransform.anchorMin = new Vector2( 0, 1 );
			i11.rectTransform.anchorMax = new Vector2( 0, 1 );
			i11.rectTransform.offsetMin = new Vector2( 0, -w-a );
			i11.rectTransform.offsetMax = new Vector2( w+a, 0 );
			i11.sprite = pieces[0];
			i11.name = "Frame piece";

			Image i21 = new GameObject().AddComponent<Image>();
			i21.rectTransform.SetParent( transform );
			i21.rectTransform.anchorMin = new Vector2( 0, 1 );
			i21.rectTransform.anchorMax = new Vector2( 1, 1 );
			i21.rectTransform.offsetMin = new Vector2( w-a, -w-a );
			i21.rectTransform.offsetMax = new Vector2( -w+a, 0 );
			i21.sprite = pieces[1];
			i21.name = "Frame piece";

			Image i31 = new GameObject().AddComponent<Image>();
			i31.rectTransform.SetParent( transform );
			i31.rectTransform.anchorMin = new Vector2( 1, 1 );
			i31.rectTransform.anchorMax = new Vector2( 1, 1 );
			i31.rectTransform.offsetMin = new Vector2( -w-a, -w-a );
			i31.rectTransform.offsetMax = new Vector2( 0, 0 );
			i31.sprite = pieces[2];
			i31.name = "Frame piece";

			Image i12 = new GameObject().AddComponent<Image>();
			i12.rectTransform.SetParent( transform );
			i12.rectTransform.anchorMin = new Vector2( 0, 0 );
			i12.rectTransform.anchorMax = new Vector2( 0, 1 );
			i12.rectTransform.offsetMin = new Vector2( 0, w-a );
			i12.rectTransform.offsetMax = new Vector2( w+a, -w+a );
			i12.sprite = pieces[3];
			i12.name = "Frame piece";

			Image i22 = new GameObject().AddComponent<Image>();
			i22.rectTransform.SetParent( transform );
			i22.rectTransform.anchorMin = new Vector2( 0, 0 );
			i22.rectTransform.anchorMax = new Vector2( 1, 1 );
			i22.rectTransform.offsetMin = new Vector2( w-a, w-a );
			i22.rectTransform.offsetMax = new Vector2( -w+a, -w+a );
			i22.sprite = pieces[4];
			i22.name = "Frame piece";

			Image i32 = new GameObject().AddComponent<Image>();
			i32.rectTransform.SetParent( transform );
			i32.rectTransform.anchorMin = new Vector2( 1, 0 );
			i32.rectTransform.anchorMax = new Vector2( 1, 1 );
			i32.rectTransform.offsetMin = new Vector2( -w-a, w-a );
			i32.rectTransform.offsetMax = new Vector2( 0, -w+a );
			i32.sprite = pieces[5];
			i32.name = "Frame piece";

			Image i13 = new GameObject().AddComponent<Image>();
			i13.rectTransform.SetParent( transform );
			i13.rectTransform.anchorMin = new Vector2( 0, 0 );
			i13.rectTransform.anchorMax = new Vector2( 0, 0 );
			i13.rectTransform.offsetMin = new Vector2( 0, 0 );
			i13.rectTransform.offsetMax = new Vector2( w+a, w+a );
			i13.sprite = pieces[6];
			i13.name = "Frame piece";

			Image i23 = new GameObject().AddComponent<Image>();
			i23.rectTransform.SetParent( transform );
			i23.rectTransform.anchorMin = new Vector2( 0, 0 );
			i23.rectTransform.anchorMax = new Vector2( 1, 0 );
			i23.rectTransform.offsetMin = new Vector2( w-a, 0 );
			i23.rectTransform.offsetMax = new Vector2( -w+a, w+a );
			i23.sprite = pieces[7];
			i23.name = "Frame piece";

			Image i33 = new GameObject().AddComponent<Image>();
			i33.rectTransform.SetParent( transform );
			i33.rectTransform.anchorMin = new Vector2( 1, 0 );
			i33.rectTransform.anchorMax = new Vector2( 1, 0 );
			i33.rectTransform.offsetMin = new Vector2( -w-a, 0 );
			i33.rectTransform.offsetMax = new Vector2( 0, w+a );
			i33.sprite = pieces[8];
			i33.name = "Frame piece";

			enabled = false;
		}
	}

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

		public static WorkshopPanel Create()
		{
			return new GameObject().AddComponent<WorkshopPanel>();
		}
		
		public void Open( Workshop workshop )
		{
			base.Open( workshop );
			this.workshop = workshop;
			Frame( 0, 0, 240, 200 );

			Text( 20, -20, workshop.type.ToString() );

			int row = -40;
			int col;
			buffers = new List<BufferUI>();
			foreach ( var b in workshop.buffers )
			{
				col = 16;
				var bui = new BufferUI();
				bui.items = new Image[b.size];
				for ( int i = 0; i < b.size; i++ )
				{
					bui.items[i] = Image( col, row, iconSize, iconSize, Item.sprites[(int)b.itemType] );
					col += iconSize;
				}
				row -= iconSize * 2;
				buffers.Add( bui );
			}

			row -= iconSize / 2;
			col = 16;
			outputs = new Image[workshop.outputMax];
			for ( int i = 0; i < workshop.outputMax; i++ )
			{
				outputs[i] = Image( col, row, iconSize, iconSize, Item.sprites[(int)workshop.outputType] );
				col += iconSize;
			}

			progressBar = Image( 20, row - iconSize - iconSize / 2, iconSize * 8, iconSize, templateProgress );
		}

		void UpdateIconRow( Image[] icons, int full, int half )
		{
			for ( int i = 0; i < icons.Length; i++ )
			{
				float a = 0;
				if ( i < half + full )
					a = 0.5f;
				if ( i < full )
					a = 1;
				icons[i].color = new Color( 1, 1, 1, a );
			}
		}

		public override void Update()
		{
			base.Update();

			for ( int j = 0; j < buffers.Count; j++ )
				UpdateIconRow( buffers[j].items, workshop.buffers[j].stored, workshop.buffers[j].onTheWay );

			UpdateIconRow( outputs, workshop.output, 0 );
			if ( workshop.working )
			{
				progressBar.rectTransform.sizeDelta = new Vector2( iconSize * 8 * workshop.progress, iconSize );
				progressBar.color = Color.white;
			}
			else
				progressBar.color = Color.red;
		}
	}

	public class StockPanel : Panel
	{
		public Stock stock;
		public Text[] counts = new Text[(int)Item.Type.total];

		public static StockPanel Create()
		{
			return new GameObject().AddComponent<StockPanel>();
		}

		public void Open( Stock stock )
		{
			base.Open( stock );
			this.stock = stock;
			Frame( 0, 0, 200, 200 );

			int row = -25;
			for ( int j = 0; j < (int)Item.Type.total; j += 2 )
			{
				Image( 16, row, iconSize, iconSize, Item.sprites[j] );
				counts[j] = Text( 40, row, "" );
				if ( j + 1 < Item.sprites.Length )
				{
					Image( 100, row, iconSize, iconSize, Item.sprites[j + 1] );
					counts[j + 1] = Text( 124, row, "" );
				};
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

	public class BuildingTypeSelector : Panel
	{
		public static BuildingTypeSelector Create()
		{
			return new GameObject().AddComponent<BuildingTypeSelector>();
		}

		public void Open( int x = 0, int y = 0 )
		{
			base.Open( null, x, y );

			int row = -30;
			Frame( 0, 0, 200, 400, 30 );
			for ( int i = 0; i < (int)Workshop.Type.total; i++ )
			{
				Text( 16, row, ((Workshop.Type)i).ToString() );
				row -= 25;
			}
		}

		public override void OnPointerClick( PointerEventData data )
		{
			Vector2 localPos;
			RectTransformUtility.ScreenPointToLocalPointInRectangle( frame.rectTransform, data.position, data.pressEventCamera, out localPos );
			int i = -((int)localPos.y+20)/25;
			if ( i <= (int)Workshop.Type.total )
				Root.selectedWorkshopType = (Workshop.Type)i;

			base.OnPointerClick( data );
		}
	}
	public class RoadPanel : Panel
	{
		public Road road;
		public Text jam;
		public Text workers;

		public static RoadPanel Create()
		{
			return new GameObject().AddComponent<RoadPanel>();
		}

		public void Open( Road road )
		{
			base.Open( road );
			this.road = road;
			Frame( 0, 0, 150, 50, 10 );
			jam = Text( 12, -4, "Jam" );
			workers = Text( 12, -24, "Worker count" );
			name = "Road panel";
		}

		public override void Update()
		{
			base.Update();
			jam.text = "Items waiting: " + road.Jam();
			workers.text = "Worker count: " + road.workers.Count;
		}
	}
	public class FlagPanel : Panel
	{
		public Flag flag;
		public Image[] items = new Image[Flag.maxItems];

		public static FlagPanel Create()
		{
			return new GameObject().AddComponent<FlagPanel>();
		}

		public void Open( Flag flag )
		{
			base.Open( flag );
			this.flag = flag;
			int col = 16;
			Frame( 0, 0, 250, 40, 10 );
			for ( int i = 0; i < Flag.maxItems; i++ )
			{
				items[i] = Image( col, -8, iconSize, iconSize, null );
				items[i].name = "item " + i;
				col += iconSize;
			}
			name = "Flag Panel";
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
