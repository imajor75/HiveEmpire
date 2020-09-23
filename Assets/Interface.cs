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

		canvas = gameObject.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		gameObject.AddComponent<GraphicRaycaster>();

		world = ScriptableObject.CreateInstance<World>();
		world.NewGame( 792469403 );
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
			Image( 0, 0, 240, 200, templateFrame );

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
			Image( 0, 0, 200, 300, templateFrame );

			int row = -25;
			for ( int j = 0; j < (int)Item.Type.total; j++ )
			{
				Image( 16, row, iconSize, iconSize, Item.sprites[j] );
				counts[j] = Text( 40, row, "" );
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

			int row = -20;
			Image( 0, 0, 200, 250, templateFrame );
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
			Image( 0, 0, 150, 50, templateFrame );
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
			Image( 0, 0, 250, 40, templateFrame );
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
