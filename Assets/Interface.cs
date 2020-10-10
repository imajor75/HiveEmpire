using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Interface : Assert.Base
{
	public List<Panel> panels = new List<Panel>();
	public static int iconSize = 20;
	public static Font font;
	public World world;
	Canvas canvas;
	public GroundNode selectedNode;
	public Workshop.Type selectedWorkshopType = Workshop.Type.unknown;
	public Viewport viewport;
	public static Sprite templateFrame;
	public static Sprite templateProgress;
	public static Sprite iconExit;
	public static Sprite iconDestroy;
	public static Sprite iconHauler;
	public static Sprite iconPath;
	public static Sprite iconButton;
	public static Sprite iconBox;
	public static Sprite templateSmallFrame;
	public GameObject debug;
	public static Interface instance;
	public bool heightStrips;
	public Player mainPlayer;
	public Tooltip tooltip;

	public Interface()
	{
		instance = this;
	}

	public void Clear()
	{
		foreach ( Transform d in debug.transform )
			Destroy( d.gameObject );
	}

	void LateUpdate()
	{
		world.LateUpdate();
		Validate();
	}

	static Sprite LoadSprite( string fileName )
	{
		Texture2D tex = Resources.Load<Texture2D>( fileName );
		return Sprite.Create( tex, new Rect( 0.0f, 0.0f, tex.width, tex.height ), new Vector2( 0.0f, 0.0f ) );
	}

	static void Initialize()
	{
		font = (Font)Resources.GetBuiltinResource( typeof( Font ), "Arial.ttf" );
		Assert.global.IsNotNull( font );
		templateFrame = LoadSprite( "simple UI & icons/box/box_event1" );
		templateProgress = LoadSprite( "simple UI & icons/button/board" );
		iconExit = LoadSprite( "simple UI & icons/button/button_exit" );
		iconDestroy = LoadSprite( "destroy" );
		iconHauler = LoadSprite( "hauler" );
		iconPath = LoadSprite( "road" );
		iconButton = LoadSprite( "simple UI & icons/button/button_login" );
		iconBox = LoadSprite( "box" );
		templateSmallFrame = LoadSprite( "simple UI & icons/box/smallFrame" );
		Frame.Initialize();
		print( "Runtime debug: " + UnityEngine.Debug.isDebugBuild );
#if DEVELOPMENT_BUILD
		print( "DEVELOPMENT_BUILD" );
#endif
#if DEBUG
		print( "DEBUG" );
#endif

	}

	void Start()
	{
		World.Initialize();
		Ground.Initialize();
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

		Directory.CreateDirectory( Application.persistentDataPath + "/Saves" );
			
		canvas = gameObject.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		gameObject.AddComponent<GraphicRaycaster>();

		var viewport = new GameObject();
		this.viewport = viewport.AddComponent<Viewport>();
		viewport.transform.SetParent( transform );
		viewport.name = "Viewport";

		var esObject = new GameObject();
		esObject.name = "Event System";
		esObject.AddComponent<EventSystem>();
		esObject.AddComponent<StandaloneInputModule>();

		debug = new GameObject();
		debug.name = "Debug";
		debug.transform.SetParent( transform );

		tooltip = Tooltip.Create();
		tooltip.Open();

		world = World.Create().Setup();
		var directory = new DirectoryInfo( Application.persistentDataPath+"/Saves" );
		if ( directory.Exists )
		{
			var myFiles = directory.GetFiles().OrderByDescending( f => f.LastWriteTime );
			if ( myFiles.Count() > 0 )
				world.Load( myFiles.First().FullName );
		}
		if ( !world.gameInProgress )
			world.NewGame( 117274283 );
	}

	void Update()
	{
		GroundNode node = world.eye.FindNodeAt( Input.mousePosition );

		if ( world.speedModifier != 0 )
		{
			if ( Input.GetKey( KeyCode.Space ) )
				world.speedModifier = 5;
			else
				world.speedModifier = 1;
		}
		if ( Input.GetKeyDown( KeyCode.Pause ) )
		{
			if ( world.speedModifier > 0 )
				world.speedModifier = 0;
			else
				world.speedModifier = 1;
		}
		if ( Input.GetKeyDown( KeyCode.P ) )
		{

			string fileName = Application.persistentDataPath+"/Saves/"+World.rnd.Next()+".json";
			world.Save( fileName );
			print( fileName + " is saved" );
		}
		if ( Input.GetKeyDown( KeyCode.L ) )
		{
			var panels = this.panels.GetRange( 0, this.panels.Count );
			foreach ( var panel in panels )
				panel.Close();
			var directory = new DirectoryInfo( Application.persistentDataPath+"/Saves" );
			var myFile = directory.GetFiles().OrderByDescending( f => f.LastWriteTime ).First();
			world.Load( myFile.FullName );
			mainPlayer = world.players[0];
			print( myFile.FullName + " is loaded" );
		}
		if ( Input.GetKeyDown( KeyCode.N ) )
		{
			world.NewGame( new System.Random().Next() );
			mainPlayer = world.players[0];
			print( "New game created" );
		}
		if ( Input.GetKeyDown( KeyCode.Escape ) )
		{
			if ( panels.Count > 0 )
				panels[panels.Count - 1].Close();
		}
		if ( Input.GetKeyDown( KeyCode.M ) )
		{
			if ( Input.GetKey( KeyCode.LeftShift ) || Input.GetKey( KeyCode.RightShift ) )
			{
				world.eye.SetActive( false );
			}
			else
				Map.Create().Open();
		}
		if ( Input.GetKeyDown( KeyCode.Alpha9 ) )
			SetHeightStrips( !heightStrips );
	}

	void SetHeightStrips( bool value )
	{
		this.heightStrips = value;
		world.ground.material.SetInt( "_HeightStrips", value ? 1 : 0 ); 
	}

	[Conditional( "DEBUG" )]
	void Validate()
	{
		world.Validate();
	}

	public class Tooltip : Panel
	{
		new Frame frame;
		Text text;

		public static Tooltip Create()
		{
			return new GameObject().AddComponent<Tooltip>();
		}

		public void Open()
		{
			base.Open();
			name = "Tooltip";
			( transform as RectTransform ).pivot = new Vector2( 0, 0.5f );

			frame = Frame( 0, 0, 200, 40, 10 );
			text = Text( 20, -10, 150, 20 );
			gameObject.SetActive( false );
			FollowMouse();
		}

		public void SetText( string text = "" )
		{
			this.text.text = text;
			gameObject.SetActive( text != "" );
			FollowMouse();
		}

		public override void Update()
		{
			base.Update();
			FollowMouse();
			transform.SetAsLastSibling();
		}

		void FollowMouse()
		{
			var t = transform as RectTransform;
			t.anchoredPosition = new Vector2( Input.mousePosition.x + 20, Input.mousePosition.y - Screen.height );
		}
	}
	public class Panel : MonoBehaviour, IDragHandler, IPointerClickHandler
	{
		public GroundNode target;
		public bool followTarget = true;
		public Image frame;
		public Interface cachedRoot;

		public static int itemIconBorderSize = 2;

		public Interface Root
		{
			get
			{
				if ( cachedRoot == null )
					cachedRoot = GameObject.FindObjectOfType<Interface>();
				return cachedRoot;
			}
		}

		public void Open( GroundNode target = null, int x = 0, int y = 0 )
		{
			Root.panels.Add( this );
			name = "Panel";
			frame = gameObject.AddComponent<Image>();
			Init( frame.rectTransform, x, y, 100, 100, Root );
			frame.enabled = false;
			this.target = target;
			UpdatePosition();
		}

		public void OnDestroy()
		{
			Assert.global.IsTrue( Root.panels.Contains( this ) );
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

		public ItemImage ItemIcon( int x, int y, int xs = 0, int ys = 0, Item.Type type = Item.Type.unknown, Component parent = null )
		{
			if ( xs == 0 )
				xs = iconSize;
			if ( ys == 0 )
				ys = iconSize;
			Image( x - itemIconBorderSize, y + itemIconBorderSize, xs + 2 * itemIconBorderSize, ys + 2 * itemIconBorderSize, templateSmallFrame );
			ItemImage i = new GameObject().AddComponent<ItemImage>();
			i.name = "ItemImage";
			if ( type != Item.Type.unknown )
				i.sprite = Item.sprites[(int)type];
			else
				i.enabled = false;
			Init( i.rectTransform, x, y, xs, ys, parent );
			i.gameObject.AddComponent<Button>().onClick.AddListener( i.Track );
			return i;
		}

		public Button Button( int x, int y, int xs, int ys, Sprite picture, Component parent = null )
		{
			Image i = Image( x, y, xs, ys, picture, parent );
			return i.gameObject.AddComponent<Button>();
		}

		public Button Button( int x, int y, int xs, int ys, string text, Component parent = null )
		{
			Image i = Image( x, y, xs, ys, null, parent );
			i.enabled = false;
			Text( 0, 0, xs, ys, text, i );
			return i.gameObject.AddComponent<Button>();
		}

		public Text Text( int x, int y, int xs, int ys, string text = "", Component parent = null )
		{
			Text t = new GameObject().AddComponent<Text>();
			t.name = "Text";
			Init( t.rectTransform, x, y, xs, ys, parent );
			t.font = Interface.font;
			t.text = text;
			t.color = Color.yellow;
			return t;
		}

		public void Close()
		{
			Destroy( gameObject );
		}

		public void Init( RectTransform t, int x, int y, int xs, int ys, Component parent = null )
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
			if ( target == null || !followTarget )
				return;

			MoveTo( target.Position() + Vector3.up * GroundNode.size );
		}

		public void MoveTo( Vector3 position )
		{
			Vector3 screenPosition = World.instance.eye.camera.WorldToScreenPoint( position );
			if ( screenPosition.y > Screen.height )
				screenPosition = World.instance.eye.camera.WorldToScreenPoint( target.Position() - Vector3.up * GroundNode.size );
			screenPosition.y -= Screen.height;
			Rect size = new Rect();
			foreach ( RectTransform t in frame.rectTransform )
			{
				size.xMin = Math.Min( size.xMin, t.rect.xMin );
				size.yMin = Math.Min( size.yMin, t.rect.yMin );
				size.xMax = Math.Max( size.xMax, t.rect.xMax );
				size.yMax = Math.Max( size.yMax, t.rect.yMax );
			}
			if ( screenPosition.x + size.width > Screen.width )
				screenPosition.x -= size.width;
			frame.rectTransform.anchoredPosition = screenPosition;
		}

		public void OnDrag( PointerEventData eventData )
		{
			followTarget = false;
			frame.rectTransform.anchoredPosition += eventData.delta;
		}

		public void OnPointerClick( PointerEventData eventData )
		{
			if ( eventData.clickCount == 2 && target != null )
			{
				World.instance.eye.FocusOn( target );
				followTarget = true;
			};

		}

		public class ItemImage : Image, IPointerEnterHandler, IPointerExitHandler
		{
			public Item item;
			public Item.Type itemType = Item.Type.unknown;

			public void Track()
			{
				if ( item == null )
					return;

				ItemPanel.Create().Open( item );
			}
			public void SetType( Item.Type itemType )
			{
				this.itemType = itemType;
				if ( itemType == Item.Type.unknown )
				{
					enabled = false;
					return;
				}
				enabled = true;
				sprite = Item.sprites[(int)itemType];
			}

			public void OnPointerEnter( PointerEventData eventData )
			{
				if ( item != null )
					Interface.instance.tooltip.SetText( item.type.ToString() );
				else
					Interface.instance.tooltip.SetText( itemType.ToString() );
			}

			public void OnPointerExit( PointerEventData eventData )
			{
				Interface.instance.tooltip.SetText( "" );
			}
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

	public class BuildingPanel : Panel
	{
		public Building building;
		public void Open( Building building )
		{
			Selection.activeGameObject = building.gameObject;
			base.Open( building.node );
			this.building = building;
		}
	}

	public class WorkshopPanel : BuildingPanel
	{
		public Workshop workshop;
		public Image progressBar;
		public Text productivity;
		public Text itemsProduced;

		public List<Buffer> buffers;
		public Buffer outputs;

		public static WorkshopPanel Create()
		{
			return new GameObject().AddComponent<WorkshopPanel>();
		}
		
		public void Open( Workshop workshop, bool show = false )
		{
			base.Open( workshop );
			this.workshop = workshop;
			int height = 150+workshop.buffers.Count * iconSize * 3 / 2;
			Frame( 0, 0, 240, height );
			Button( 210, -10, 20, 20, iconExit ).onClick.AddListener( Close );
			Button( 190, 30 - height, 20, 20, iconDestroy ).onClick.AddListener( Remove );

			Text( 20, -20, 160, 20, workshop.type.ToString() );
			productivity = Text( 180, -20, 30, 20 );

			int row = -40;
			int col = 20;
			buffers = new List<Buffer>();
			foreach ( var b in workshop.buffers )
			{
				var bui = new Buffer();
				bui.Setup( this, b, col, row, iconSize + 5 );
				buffers.Add( bui );
				if ( !workshop.configuration.commonInputs )
					row -= iconSize * 3 / 2;
				else
					col += b.size * ( iconSize + 5 ) + 10;
			}
			if ( workshop.configuration.commonInputs )
				row -= iconSize * 3 / 2;

			row -= iconSize / 2;
			outputs = new Buffer();
			outputs.Setup( this, workshop.configuration.outputType, workshop.configuration.outputMax, 20, row, iconSize + 5 );

			row -= (int)((float)iconSize * 1.5f);
			progressBar = Image( 20, row, ( iconSize + 5 ) * 8, iconSize, templateProgress );

			itemsProduced = Text( 20, row - 24, 200, 20 );

			if ( show )
				Root.world.eye.FocusOn( workshop );
		}

		void Remove()
		{
			if ( workshop && workshop.Remove() )
				Close();
		}

		public override void Update()
		{
			base.Update();
			foreach ( var buffer in buffers )
				buffer.Update();

			outputs.Update( workshop.output, 0 );

			if ( workshop.working )
			{
				progressBar.rectTransform.sizeDelta = new Vector2( iconSize * 8 * workshop.progress, iconSize );
				progressBar.color = Color.white;
			}
			else
				progressBar.color = Color.red;
			productivity.text = ( (int)(workshop.productivity.current * 100) ).ToString() + "%";
			itemsProduced.text = "Items produced: " + workshop.itemsProduced;
		}

		public class Buffer
		{
			public ItemImage[] items;
			public BuildingPanel boss;
			public Item.Type itemType;
			Workshop.Buffer buffer;

			public void Setup( BuildingPanel boss, Item.Type itemType, int itemCount, int x, int y, int xi )
			{
				items = new ItemImage[itemCount];
				this.boss = boss;
				this.itemType = itemType;
				for ( int i = 0; i < itemCount; i++ )
				{
					items[i] = boss.ItemIcon( x, y, iconSize, iconSize, itemType );
					x += xi;
				}
			}

			public void Setup( BuildingPanel boss, Workshop.Buffer buffer, int x, int y, int xi )
			{
				this.buffer = buffer;
				Setup( boss, buffer.itemType, buffer.size, x, y, xi );
			}

			public void Update( int inStock, int onTheWay )
			{
				var itemsOnTheWay = boss.building.itemsOnTheWay;
				int k = 0;
				for ( int i = 0; i < items.Length; i++ )
				{
					if ( i < inStock )
					{
						items[i].color = Color.white;
						items[i].item = null;
					}
					else
					{
						if ( i < inStock + onTheWay )
						{
							items[i].color = new Color( 1, 1, 1, 0.25f );
							while ( itemsOnTheWay[k].type != itemType )
								k++;
							items[i].item = itemsOnTheWay[k];
						}
						else
							items[i].color = new Color( 1, 1, 1, 0 );
					}
				}
			}

			public void Update()
			{
				Assert.global.IsNotNull( buffer );
				Update( buffer.stored, buffer.onTheWay );
			}
		}

	}

	public class StockPanel : BuildingPanel
	{
		public Stock stock;
		public Text[] counts = new Text[(int)Item.Type.total];

		public static StockPanel Create()
		{
			return new GameObject().AddComponent<StockPanel>();
		}

		public void Open( Stock stock, bool show = false )
		{
			base.Open( stock );
			this.stock = stock;
			int height = 260;
			Frame( 0, 0, 200, height );

			int row = -25;
			for ( int j = 0; j < (int)Item.Type.total; j += 2 )
			{
				Image( 16, row, iconSize, iconSize, Item.sprites[j] );
				Button( 170, -10, 20, 20, iconExit ).onClick.AddListener( Close );
				Button( 150, 40 - height, 20, 20, iconDestroy ).onClick.AddListener( Remove );
				counts[j] = Text( 40, row, 100, 20, "" );
				if ( j + 1 < Item.sprites.Length )
				{
					Image( 100, row, iconSize, iconSize, Item.sprites[j + 1] );
					counts[j + 1] = Text( 124, row, 100, 20, "" );
				};
				row -= iconSize;
			}

			if ( show )
				Root.world.eye.FocusOn( stock );
		}

		void Remove()
		{
			if ( stock && stock.Remove() )
				Close();
		}

		public override void Update()
		{
			base.Update();
			for ( int i = 0; i < (int)Item.Type.total; i++ )
				counts[i].text = stock.content[i].ToString();
		}
	}

	public class NodePanel : Panel
	{
		public GroundNode node;

		public static NodePanel Create()
		{
			return new GameObject().AddComponent<NodePanel>();
		}

		public void Open( GroundNode node )
		{
			base.Open( node );
			this.node = node;

			Frame( 0, 0, 400, 550, 30 );
			Button( 360, -20, 20, 20, iconExit ).onClick.AddListener( Close );

			int row = -20;
			for ( int i = 0; i < (int)Workshop.Type.total; i++ )
			{
				var type = (Workshop.Type)i;
				Button( 160, row, 200, 20, ( type.ToString() ) ).onClick.AddListener( delegate { BuildWorkshop( type ); } );
				row -= 20;
			}
			Button( 20, -220, 140, 20, "Flag" ).onClick.AddListener( AddFlag );
			Button( 20, -240, 140, 20, "Stock" ).onClick.AddListener( AddStock );
			Button( 20, -260, 140, 20, "Guardhouse" ).onClick.AddListener( AddGuardHouse );
			if ( node.resource && ( !node.resource.underGround || node.resource.exposed > 0 ) )
				Text( 20, -40, 160, 20, "Resource: " + node.resource.type );
		}

		void AddFlag()
		{
			if ( Flag.Create().Setup( node.ground, node, node.owner ) != null )
				Close();
		}

		void AddStock()
		{
			if ( Stock.Create().Setup( node.ground, node, node.owner ) != null )
				Close();
		}

		void AddGuardHouse()
		{
			if ( GuardHouse.Create().Setup( node.ground, node, node.owner ) != null )
				Close();
		}


		public void BuildWorkshop( Workshop.Type type )
		{
			if ( Workshop.Create().Setup( node.ground, node, node.owner, type ) != null )
				Close();
		}
	}
	public class RoadPanel : Panel
	{
		public Road road;
		public List<ItemImage> leftItems = new List<ItemImage>(), rightItems = new List<ItemImage>();
		public List<Text> leftNumbers = new List<Text>(), rightNumbers = new List<Text>();
		public GroundNode node;
		public Text jam;
		public Text workers;

		const int itemsDisplayed = 3;

		public static RoadPanel Create()
		{
			return new GameObject().AddComponent<RoadPanel>();
		}

		public void Open( Road road, GroundNode node )
		{
			base.Open( node );
			this.road = road;
			this.node = node;
			Frame( 0, 0, 210, 140, 10 );
			Button( 190, 0, 20, 20, iconExit ).onClick.AddListener( Close );
			Button( 170, -10, 20, 20, iconHauler ).onClick.AddListener( Hauler );
			Button( 150, -10, 20, 20, iconDestroy ).onClick.AddListener( Remove );
			Button( 130, -10, 20, 20, iconBox ).onClick.AddListener( Split );
			jam = Text( 12, -4, 120, 20, "Jam" );
			workers = Text( 12, -24, 120, 20, "Worker count" );
			name = "Road panel";

			for ( int i = 0; i < itemsDisplayed; i++ )
			{
				int row = i * (iconSize + 5 ) - 100;
				leftItems.Add( ItemIcon( 15, row ) );
				leftNumbers.Add( Text( 40, row, 30, 20, "0" ) );
				rightNumbers.Add( Text( 150, row, 20, 20, "0" ) );
				rightItems.Add( ItemIcon( 170, row ) );
			}
		}

		void Remove()
		{
			if ( road && road.Remove() )
				Close();
		}

		void Hauler()
		{
			WorkerPanel.Create().Open( road.workers[0] ); // TODO Make it possibe to view additional workers
		}

		void Split()
		{
			if ( Flag.Create().Setup( node.ground, node, node.owner ) != null )
				Close();
			World.instance.Validate();
		}

		public override void Update()
		{
			base.Update();
			jam.text = "Items waiting: " + road.Jam();
			workers.text = "Worker count: " + road.workers.Count;

			for ( int i = 0; i < 2; i++ )
			{
				var itemImages = i == 0 ? leftItems : rightItems;
				var itemTexts = i == 0 ? leftNumbers : rightNumbers;
				int[] counts = new int[(int)Item.Type.total];
				var items = road.GetEnd( i ).items;
				foreach ( var item in items )
				{
					if ( item != null && item.path && item.path.Road() == road )
						counts[(int)item.type]++;
				}
				for ( int j = itemsDisplayed - 1; j >= 0; j-- )
				{
					int best = 0, bestIndex = 0;
					for ( int t = 0; t < counts.Length; t++ )
					{
						if ( counts[t] > best )
						{
							best = counts[t];
							bestIndex = t;
						}
					}
					if ( best > 0 )
					{
						itemImages[j].SetType( (Item.Type)bestIndex );
						itemTexts[j].text = counts[bestIndex].ToString();
						counts[bestIndex] = 0;
					}
					else
					{
						itemImages[j].SetType( Item.Type.unknown );
						itemTexts[j].text = "-";
					}
				}

			}
		}
	}
	public class FlagPanel : Panel
	{
		public Flag flag;
		public ItemImage[] items = new ItemImage[Flag.maxItems];

		public static FlagPanel Create()
		{
			return new GameObject().AddComponent<FlagPanel>();
		}

		public void Open( Flag flag, bool show = false )
		{
			Selection.activeGameObject = flag.gameObject;
			base.Open( flag.node );
			this.flag = flag;
			int col = 16;
			Frame( 0, 0, 250, 70, 10 );
			Button( 230, 0, 20, 20, iconExit ).onClick.AddListener( Close );
			Button( 210, -40, 20, 20, iconDestroy ).onClick.AddListener( Remove );
			Button( 20, -40, 20, 20, iconPath ).onClick.AddListener( StartRoad );

			for ( int i = 0; i < Flag.maxItems; i++ )
			{
				items[i] = ItemIcon( col, -8, iconSize, iconSize, Item.Type.unknown );
				int j = i;
				items[i].name = "item " + i;
				col += iconSize+5;
			}
			name = "Flag Panel";
			if ( show )
				Root.world.eye.FocusOn( flag );
		}

		void Remove()
		{
			if ( flag && flag.Remove() )
				Close();
		}

		void StartRoad()
		{
			if ( flag )
			{
				Road.AddNodeToNew( flag.node.ground, flag.node, flag.owner );
				if ( Road.newRoad )
					Root.viewport.inputHandler = Road.newRoad;
			}
			Close();
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
					if ( flag.items[i].flag && flag.items[i].flag == flag )
						items[i].color = new Color( 1, 1, 1, 1 );
					else
						items[i].color = new Color( 1, 1, 1, 0.25f );
				}
				items[i].item = flag.items[i];
			}
		}
	}

	public class WorkerPanel : Panel, Eye.IDirector
	{
		public Worker worker;
		Text itemCount;
		ItemImage item;

		public static WorkerPanel Create()
		{
			return new GameObject().AddComponent<WorkerPanel>();
		}

		public void Open( Worker worker )
		{
			base.Open( worker.node );
			this.worker = worker;
			Frame( 0, 0, 200, 80 );
			Button( 170, 0, 20, 20, iconExit ).onClick.AddListener( Close );
			item = ItemIcon( 20, -20 );
			itemCount = Text( 20, -44, 120, 20, "Items" );
			World.instance.eye.GrabFocus( this );
		}

		public void SetCameraTarget( Eye eye )
		{
			eye.FocusOn( worker );
		}

		public override void Update()
		{
			if ( worker == null )
			{
				Close();
				return;
			}
			base.Update();
			itemCount.text = "Items delivered: " + worker.itemsDelivered;
			if ( worker.itemInHands != null )
			{
				item.sprite = Item.sprites[(int)worker.itemInHands.type];
				item.enabled = true;
			}
			else
				item.enabled = false;
			item.item = worker.itemInHands;
			MoveTo( worker.transform.position + Vector3.up * GroundNode.size );
		}

		new void OnDestroy()
		{
			base.OnDestroy();
			World.instance.eye.ReleaseFocus( this );
		}
	}

	public class ConstructionPanel : BuildingPanel
	{
		public Image progressBar;
		public Building.Construction construction;
		public WorkshopPanel.Buffer planks;
		public WorkshopPanel.Buffer stones;

		public static ConstructionPanel Create()
		{
			return new GameObject().AddComponent<ConstructionPanel>();
		}

		public void Open( Building.Construction construction, bool show = false )
		{
			base.Open( construction.boss );
			this.construction = construction;
			Frame( 0, 0, 240, 200 );
			Button( 200, -10, 20, 20, iconExit ).onClick.AddListener( Close );
			Button( 190, -150, 20, 20, iconDestroy ).onClick.AddListener( Remove );

			Workshop workshop = construction.boss as Workshop;
			if ( workshop )
				Text( 20, -20, 160, 20, workshop.type.ToString() );

			planks = new WorkshopPanel.Buffer();
			planks.Setup( this, Item.Type.plank, construction.plankNeeded, 20, -40, iconSize + 5 );
			stones = new WorkshopPanel.Buffer();
			stones.Setup( this, Item.Type.stone, construction.stoneNeeded, 20, -64, iconSize + 5 );

			progressBar = Image( 20, -90, ( iconSize + 5 ) * 8, iconSize, templateProgress );

			if ( show )
				Root.world.eye.FocusOn( workshop );
		}

		new void Update()
		{
			if ( construction.done )
			{
				Workshop workshop = construction.boss as Workshop;
				if ( workshop )
					WorkshopPanel.Create().Open( workshop );
				Stock stock = construction.boss as Stock;
				if ( stock )
					StockPanel.Create().Open( stock );
				Close();
				return;
			}
			base.Update();
			planks.Update( construction.plankArrived, construction.plankOnTheWay );
			stones.Update( construction.stoneArrived, construction.stoneOnTheWay );
			progressBar.rectTransform.sizeDelta = new Vector2( construction.progress * ( iconSize + 5 ) * 8, iconSize );
		}

		void Remove()
		{
			if ( construction != null && construction.boss != null && construction.boss.Remove() )
				Close();
		}
	}

	public class ItemPanel : Panel, Eye.IDirector
	{
		public Item item;
		public List<Button> path = new List<Button>();
		public Mesh route;

		static public ItemPanel Create()
		{
			return new GameObject().AddComponent<ItemPanel>();
		}

		public void Open( Item item )
		{
			this.item = item;
			
			base.Open();
			name = "Item panel";

			Frame( 0, 0, 200, 300, 20 );
			Button( 170, -10, 20, 20, iconExit ).onClick.AddListener( Close );
			Text( 15, -15, 100, 20, item.type.ToString() );
			if ( item.origin )
				Text( 15, -35, 170, 20, "Origin: " + item.origin.name );
			Selection.activeGameObject = item.gameObject;
			World.instance.eye.GrabFocus( this );
		}

		override public void Update()
		{
			base.Update();
			if ( item == null )
			{
				Close();
				return;
			}

			if ( item.path && item.path.roadPath.Count != path.Count )
			{
				foreach ( var o in path )
					Destroy( o.gameObject );
				path.Clear();

				int row = -60;
				for ( int i = 0; i < item.path.roadPath.Count; i++ )
				{
					Road r = item.path.roadPath[i];
					int e = 1;
					if ( i == item.path.roadPath.Count - 1 )
					{
						if ( r.GetEnd( 1 ) == item.destination.flag )
							e = 0;
					}
					else
					{
						Road nr = item.path.roadPath[i + 1];
						if ( r.GetEnd( 1 ) == nr.GetEnd( 0 ) || r.GetEnd( 1 ) == nr.GetEnd( 1 ) )
							e = 0;
					}
					Flag flag = r.GetEnd( e );

					Button b = Button( 15, row, 100, 20, "flag" );
					b.onClick.AddListener( delegate { ShowFlag( flag ); } );
					path.Add( b );
					row -= 20;
				}
				Button( 15, row, 100, 20, item.destination.name ).onClick.AddListener( delegate { ShowFlag( item.destination.flag ); } );

				Destroy( route );
				GameObject routeOnMap = new GameObject();
				World.SetLayerRecursive( routeOnMap, World.layerIndexMapOnly );
				routeOnMap.transform.SetParent( transform );
				routeOnMap.name = "Route on map";
				routeOnMap.AddComponent<MeshRenderer>().material = new Material( World.defaultShader );
				route = routeOnMap.AddComponent<MeshFilter>().mesh = new Mesh();

				List<Vector3> vertices = new List<Vector3>();
				List<int> triangles = new List<int>();
				foreach ( var road in item.path.roadPath )
				{
					for ( int i = 0; i < road.nodes.Count - 1; i++ )
					{
						Vector3 start = road.nodes[i].Position() + Vector3.up * 3;
						Vector3 end = road.nodes[i + 1].Position() + Vector3.up * 3;
						Vector3 side = (end - start) * 0.1f;
						side = new Vector3( -side.z, side.y, side.x );

						triangles.Add( vertices.Count + 0 );
						triangles.Add( vertices.Count + 1 );
						triangles.Add( vertices.Count + 2 );
						triangles.Add( vertices.Count + 1 );
						triangles.Add( vertices.Count + 3 );
						triangles.Add( vertices.Count + 2 );

						vertices.Add( start - side );
						vertices.Add( start + side );
						vertices.Add( end - side );
						vertices.Add( end + side );
					}
				}
				route.vertices = vertices.ToArray();
				route.triangles = triangles.ToArray();
			}
		}

		public void SetCameraTarget( Eye eye )
		{
			if ( item.flag )
				World.instance.eye.FocusOn( item.flag.node );
			else
				World.instance.eye.FocusOn( item.worker );
		}

		void ShowFlag( Flag flag )
		{
			World.instance.eye.ReleaseFocus( this );
			FlagPanel.Create().Open( flag, true );
		}

		new void OnDestroy()
		{
			base.OnDestroy();
			World.instance.eye.ReleaseFocus( this );
		}
	}

	public class Viewport : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, InputHandler
	{
		public bool mouseOver;
		public GameObject cursor;
		public InputHandler inputHandler;
		GameObject[] cursorTypes = new GameObject[(int)CursorType.total];
		GameObject cursorFlag;
		GameObject cursorBuilding;
		enum CursorType
		{
			nothing,
			flag,
			building,
			total
		}

		public void OnPointerClick( PointerEventData eventData )
		{
			GroundNode node = World.instance.eye.FindNodeAt( Input.mousePosition );
			if ( !inputHandler.OnNodeClicked( node ) )
				inputHandler = this;
		}

		public void OnPointerEnter( PointerEventData eventData )
		{
			mouseOver = true;
		}

		public void OnPointerExit( PointerEventData eventData )
		{
			mouseOver = false;
		}

		void Start()
		{
			Image image = gameObject.AddComponent<Image>();
			image.rectTransform.anchorMin = Vector2.zero;
			image.rectTransform.anchorMax = Vector2.one;
			image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
			image.color = new Color( 1, 1, 1, 0 );

			inputHandler = this;
		}

		void Update()
		{
			if ( !mouseOver )
				return;
			GroundNode node = World.instance.eye.FindNodeAt( Input.mousePosition );
			if ( !inputHandler.OnMovingOverNode( node ) )
				inputHandler = this;
		}

		public bool OnMovingOverNode( GroundNode node )
		{
			if ( node != null )
			{
				if ( cursor == null )
				{
					cursor = GameObject.Instantiate( Resources.Load<GameObject>( "cursor" ) );
					cursor.transform.SetParent( World.instance.ground.transform );
					for ( int i = 0; i < cursorTypes.Length; i++ )
					{
						cursorTypes[i] = World.FindChildRecursive( cursor.transform, ( (CursorType)i ).ToString() ).gameObject;
						Assert.global.IsNotNull( cursorTypes[i] );
					}
				}
				cursor.transform.localPosition = node.Position();

				CursorType t = CursorType.nothing;
				GroundNode flagNode = node.Add( Building.flagOffset );
				bool hasFlagAround = false, hasFlagAroundFlag = false;
				foreach ( var o in Ground.areas[1] )
					if ( node.Add( o ).flag != null )
						hasFlagAround = true;
				foreach ( var o in Ground.areas[1] )
					if ( flagNode.Add( o ).flag != null )
						hasFlagAroundFlag = true;
				if ( !node.IsBlocking( false ) && !hasFlagAround )
					t = CursorType.flag;
				if ( !node.IsBlocking() && !flagNode.IsBlocking( false ) && !hasFlagAroundFlag )
					t = CursorType.building;
				for ( int i = 0; i < cursorTypes.Length; i++ )
					cursorTypes[i].SetActive( i == (int)t );
			}
			return true;
		}

		public bool OnNodeClicked( GroundNode node )
		{
			if ( node.building )
			{
				node.building.OnClicked();
				return true;
			}
			if ( node.flag )
			{
				node.flag.OnClicked();
				return true;
			}
			if ( node.road )
			{
				node.road.OnClicked( node );
				return true;
			}
			node.OnClicked();
			return true;
		}
	}

	public interface InputHandler
	{
		bool OnMovingOverNode( GroundNode node );
		bool OnNodeClicked( GroundNode node );
	}
}

