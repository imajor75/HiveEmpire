using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Profiling;
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
	static public MediaTable<Sprite, Icon> iconTable;
	public GameObject debug;
	public static Interface instance;
	public bool heightStrips;
	public Player mainPlayer;
	public Tooltip tooltip;

	public enum Icon
	{
		exit,
		button,
		progress,
		frame,
		smallFrame,
		hauler,
		box,
		destroy,
		path,
		magnet,
		dynamite
	}

	public Interface()
	{
		instance = this;
	}

	public void Clear()
	{
		foreach ( Transform d in debug.transform )
			Destroy( d.gameObject );
		foreach ( var panel in panels )
		{
			if ( panel != tooltip )
				panel.Close();
		}
	}

	void LateUpdate()
	{
		Validate();
	}

	void FixedUpdate()
	{
		var o = ItemPanel.materialUIPath.mainTextureOffset;
		o.y -= 0.015f;
		if ( o.y < 0 )
			o.y += 1;
		ItemPanel.materialUIPath.mainTextureOffset = o;
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
		object[] table = {
		"simple UI & icons/box/box_event1", Icon.frame,
		"simple UI & icons/button/board", Icon.progress,
		"simple UI & icons/button/button_exit", Icon.exit,
		"simple UI & icons/button/button_login", Icon.button,
		"simple UI & icons/box/smallFrame", Icon.smallFrame };
		iconTable.Fill( table );
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
		ItemPanel.Initialize();

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
				Load( myFiles.First().FullName );
		}
		if ( !world.gameInProgress )
			NewGame( 1299783286 );
	}

	void NewGame( int seed )
	{
		world.NewGame( seed );
		if ( world.players.Count > 0 )
			mainPlayer = world.players[0];
		else
			mainPlayer = null;
	}

	void Load( string file )
	{
		world.Load( file );
		mainPlayer = world.players[0];
		print( file + " is loaded" );
	}

	void Update()
	{
		GroundNode node = world.eye.FindNodeAt( Input.mousePosition );

		if ( world.timeFactor != 0 )
		{
			if ( Input.GetKey( KeyCode.Space ) )
				world.SetTimeFactor( 5 );
			else
				world.SetTimeFactor( 1 ); 
		}
		if ( Input.GetKeyDown( KeyCode.Pause ) )
		{
			if ( world.timeFactor > 0 )
				world.SetTimeFactor( 0 );
			else
				world.SetTimeFactor( 1 );
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
			Load( myFile.FullName );
		}
		if ( Input.GetKeyDown( KeyCode.N ) )
		{
			NewGame( new System.Random().Next() );
			print( "New game created" );
		}
		if ( Input.GetKeyDown( KeyCode.I ) )
		{
			ItemList.Create().Open( mainPlayer );
		}
		if ( Input.GetKeyDown( KeyCode.J ) )
		{
			ItemStats.Create().Open( mainPlayer );
		}
		if ( Input.GetKeyDown( KeyCode.Escape ) )
		{
			for ( int i = panels.Count - 1; i >= 0; i-- )
			{
				if ( !panels[i].escCloses )
					continue;
				panels[panels.Count - 1].Close();
				break;
			}
		}
		if ( Input.GetKeyDown( KeyCode.M ) )
			Map.Create().Open( Input.GetKey( KeyCode.LeftShift ) || Input.GetKey( KeyCode.RightShift ) );
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
		Profiler.BeginSample( "Validate" );
		world.Validate();
		Profiler.EndSample();
	}

	public class Tooltip : Panel
	{
		GameObject objectToShow;
		Component origin;
		Text text;

		public static Tooltip Create()
		{
			return new GameObject().AddComponent<Tooltip>();
		}

		public void Open()
		{
			base.Open();
			escCloses = false;
			name = "Tooltip";
			( transform as RectTransform ).pivot = new Vector2( 0, 0.5f );

			Frame( 0, 0, 200, 40, 10 );
			text = Text( 20, -10, 150, 20 );
			gameObject.SetActive( false );
			FollowMouse();
		}

		public void SetText( Component origin, string text = "", GameObject objectToShow = null )
		{
			this.origin = origin;
			this.text.text = text;
			if ( this.objectToShow != null && this.objectToShow != objectToShow )
				Destroy( this.objectToShow );
			this.objectToShow = objectToShow;
			gameObject.SetActive( text != "" );
			FollowMouse();
		}

		public override void Update()
		{
			if ( origin == null )
			{
				gameObject.SetActive( false );
				return;
			}
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
		public bool escCloses = true;

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
			if ( target == null && x == 0 && y == 0 )
			{
				x = Screen.width / 2 - 50;
				y = -Screen.height / 2 - 50;
			}
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

		public ScrollRect ScrollRect( int x, int y, int xs, int ys, bool vertical = true, bool horizontal = false, Component parent = null )
		{
			var scroll = new GameObject().AddComponent<ScrollRect>();
			scroll.name = "Scroll view";
			var mask = scroll.gameObject.AddComponent<RectMask2D>();
			Init( mask.rectTransform, x, y, xs, ys, parent );
			scroll.vertical = vertical;
			scroll.horizontal = horizontal;

			if ( horizontal )
			{
				var scrollBarObject = GameObject.Instantiate( Resources.Load<GameObject>( "scrollbar" ) );
				var scrollBar = scrollBarObject.GetComponent<Scrollbar>();	// TODO Would be better to do this from script
				scrollBar.name = "Horizontal scroll bar";
				scrollBar.direction = Scrollbar.Direction.LeftToRight;
				scroll.horizontalScrollbar = scrollBar;
				scrollBar.transform.SetParent( scroll.transform );
				var t = scrollBar.transform as RectTransform;
				t.anchorMin = new Vector2( 1, 0 );
				t.anchorMax = Vector2.one;
				t.offsetMin = new Vector2( -20, 0 );
				t.offsetMax = new Vector2( 0, vertical ? -20 : 0 );
			}

			if ( vertical )
			{
				var scrollBarObject = GameObject.Instantiate( Resources.Load<GameObject>( "scrollbar" ) );
				var scrollBar = scrollBarObject.GetComponent<Scrollbar>();
				scrollBar.name = "Vertical scroll bar";
				scrollBar.direction = Scrollbar.Direction.BottomToTop;
				scroll.verticalScrollbar = scrollBar;
				scrollBar.transform.SetParent( scroll.transform );
				var t = scrollBar.transform as RectTransform;
				t.anchorMin = new Vector2( 1, 0 );
				t.anchorMax = Vector2.one;
				t.offsetMin = new Vector2( -20, 0 );
				t.offsetMax = new Vector2( 0, horizontal ? -20 : 0 );
			}
			var content = new GameObject().AddComponent<Image>();
			content.name = "Content";
			content.transform.SetParent( scroll.transform, false );
			scroll.content = content.rectTransform;
			content.enabled = false;
			content.rectTransform.sizeDelta = new Vector2( xs - ( vertical ? 20 : 0 ), ys - ( horizontal ? 20 : 0 ) );
			return scroll;
		}

		public ItemImage ItemIcon( int x, int y, int xs = 0, int ys = 0, Item.Type type = Item.Type.unknown, Component parent = null )
		{
			if ( xs == 0 )
				xs = iconSize;
			if ( ys == 0 )
				ys = iconSize;
			var frame = Image( x - itemIconBorderSize, y + itemIconBorderSize, xs + 2 * itemIconBorderSize, ys + 2 * itemIconBorderSize, iconTable.GetMediaData( Icon.smallFrame ) );
			ItemImage i = new GameObject().AddComponent<ItemImage>();
			i.name = "ItemImage";
			if ( type != Item.Type.unknown )
				i.sprite = Item.sprites[(int)type];
			else
				i.enabled = false;
			i.itemType = type;
			i.transform.SetParent( frame.transform );
			i.rectTransform.anchorMin = Vector2.zero;
			i.rectTransform.anchorMax = Vector2.one;
			i.rectTransform.offsetMax = -( i.rectTransform.offsetMin = new Vector2( itemIconBorderSize, itemIconBorderSize ) );
			Init( frame.rectTransform, x - itemIconBorderSize, y + itemIconBorderSize, xs + 2 * itemIconBorderSize, ys + 2 * itemIconBorderSize, parent );
			i.gameObject.AddComponent<Button>().onClick.AddListener( i.Track );
			return i;
		}

		public Component BuildingIcon( int x, int y, Building building, Component parent = null )
		{
			if ( building == null )
				return null;

			var text = Text( x, y, 150, 20, building.title, parent );
			text.gameObject.AddComponent<Button>().onClick.AddListener( delegate { SelectBuilding( building ); } );
			return text;
		}

		void SelectBuilding( Building building )
		{
			var workshop = building as Workshop;
			if ( workshop )
				WorkshopPanel.Create().Open( workshop, true );
			var stock = building as Stock;
			if ( stock )
				StockPanel.Create().Open( stock, true );
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

		public virtual void Close()
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
			GameObject path;

			public void Track()
			{
				if ( item == null )
					return;

				ItemPanel.Create().Open( item );
			}

			public void SetType( Item.Type itemType )
			{
				if ( this.itemType == itemType )
					return;

				Destroy( path );
				path = null;

				this.itemType = itemType;
				if ( itemType == Item.Type.unknown )
				{
					enabled = false;
					return;
				}
				enabled = true;
				sprite = Item.sprites[(int)itemType];
			}

			new void OnDestroy()
			{
				base.OnDestroy();
				Destroy( path );
				path = null;
			}

			public void SetItem( Item item )
			{
				this.item = item;
				if ( item )
					SetType( item.type );
				else
					SetType( Item.Type.unknown );
			}

			public void OnPointerEnter( PointerEventData eventData )
			{
				if ( item != null )
					Interface.instance.tooltip.SetText( this, item.type.ToString(), path = ItemPanel.CreateUIPath( item.path ) );
				else
					Interface.instance.tooltip.SetText( this, itemType.ToString() );
			}

			public void OnPointerExit( PointerEventData eventData )
			{
				Interface.instance.tooltip.SetText( this, "" );
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

		new public void Start()
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
#if DEBUG
			Selection.activeGameObject = building.gameObject;
#endif
			base.Open( building.node );
			this.building = building;
		}
	}

	public class WorkshopPanel : BuildingPanel
	{
		public Workshop workshop;
		public Image progressBar;
		public Image overdriveImage;
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
			Button( 210, -10, 20, 20, iconTable.GetMediaData( Icon.exit ) ).onClick.AddListener( Close );
			Button( 190, 30 - height, 20, 20, iconTable.GetMediaData( Icon.destroy ) ).onClick.AddListener( Remove );
			Button( 170, 30 - height, 20, 20, iconTable.GetMediaData( Icon.hauler ) ).onClick.AddListener( ShowWorker );
			var overdriveButton = Button( 150, 30 - height, 20, 20, iconTable.GetMediaData( Icon.dynamite ) );
			overdriveButton.onClick.AddListener( Overdrive );
			overdriveImage = overdriveButton.gameObject.GetComponent<Image>();

			Text( 20, -20, 160, 20, workshop.type.ToString() );

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

			if ( workshop.configuration.outputType != Item.Type.unknown || workshop.type == Workshop.Type.forester )
			{
				if ( workshop.configuration.gatheredResource == Resource.Type.unknown )
				{
					row -= iconSize / 2;
					outputs = new Buffer();
					outputs.Setup( this, workshop.configuration.outputType, workshop.configuration.outputMax, 20, row, iconSize + 5 );
				}
				row -= (int)( (float)iconSize * 1.5f );
				progressBar = Image( 20, row, ( iconSize + 5 ) * 8, iconSize, iconTable.GetMediaData( Icon.progress ) );

				itemsProduced = Text( 20, row - 24, 200, 20 );
				productivity = Text( 180, -20, 30, 20 );
			}
			if ( show )
				Root.world.eye.FocusOn( workshop );
		}

		void Remove()
		{
			if ( workshop && workshop.Remove() )
				Close();
		}

		void ShowWorker()
		{
			WorkerPanel.Create().Open( workshop.worker );
		}

		public override void Update()
		{
			base.Update();
			foreach ( var buffer in buffers )
				buffer.Update();

			outputs?.Update( workshop.output, 0 );

			if ( progressBar )
			{
				if ( workshop.working )
				{
					progressBar.rectTransform.sizeDelta = new Vector2( iconSize * 8 * workshop.progress, iconSize );
					progressBar.color = Color.white;
				}
				else
				{
					if ( workshop.configuration.gatheredResource != Resource.Type.unknown && !workshop.worker.IsIdle() )
						progressBar.color = Color.green;
					else
						progressBar.color = Color.red;
				}
				productivity.text = ( (int)( workshop.productivity.current * 100 ) ).ToString() + "%";
				itemsProduced.text = "Items produced: " + workshop.itemsProduced;
			}
			overdriveImage.color = new Color( 1, 1, 1, workshop.outputPriority == ItemDispatcher.Priority.high ? 1 : 0.4f );
		}

		void Overdrive()
		{
			workshop.outputPriority = workshop.outputPriority == ItemDispatcher.Priority.high ? ItemDispatcher.Priority.low : ItemDispatcher.Priority.high;
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
					items[i].SetType( itemType );
					if ( i < inStock )
					{
						items[i].color = Color.white;
						items[i].item = null;
					}
					else
					{
						if ( i < inStock + onTheWay )
						{
							items[i].color = new Color( 1, 1, 1, 0.4f );
							while ( itemsOnTheWay[k].type != itemType )
								k++;
							items[i].item = itemsOnTheWay[k++];
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
			int height = 290;
			Frame( 0, 0, 200, height );
			Button( 170, -10, 20, 20, iconTable.GetMediaData( Icon.exit ) ).onClick.AddListener( Close );
			Button( 150, 40 - height, 20, 20, iconTable.GetMediaData( Icon.destroy ) ).onClick.AddListener( Remove );

			int row = -25;
			for ( int j = 0; j < (int)Item.Type.total; j += 2 )
			{
				ItemIcon( 20, row, iconSize, iconSize, (Item.Type)j );
				counts[j] = Text( 44, row, 100, 20, "" );
				if ( j + 1 < Item.sprites.Length )
				{
					ItemIcon( 110, row, iconSize, iconSize, (Item.Type)j + 1 );
					counts[j + 1] = Text( 134, row, 100, 20, "" );
				};
				row -= iconSize + 5;
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
			name = "Node panel";

			Frame( 0, 0, 400, 550, 30 );
			Button( 360, -20, 20, 20, iconTable.GetMediaData( Icon.exit ) ).onClick.AddListener( Close );

			int row = -20;
			for ( int i = 0; i < (int)Workshop.Type.total; i++ )
			{
				var type = (Workshop.Type)i;
				BuildButton( 160, row, type.ToString(), Workshop.IsItGood( node, Root.mainPlayer, Workshop.GetConfiguration( type ) ), delegate { BuildWorkshop( type ); } );
				row -= 20;
			}
			BuildButton( 20, -220, "Flag", Flag.IsItGood( node, Root.mainPlayer ), AddFlag );
			BuildButton( 20, -240, "Stock", Stock.IsItGood( node, Root.mainPlayer ), AddStock );
			BuildButton( 20, -260, "Guardhouse", GuardHouse.IsItGood( node, Root.mainPlayer ), AddGuardHouse );
#if DEBUG
			BuildButton( 20, -280, "Tree", !node.IsBlocking( true ), AddTree );
			BuildButton( 20, -300, "Remove", node.IsBlocking( true ), Remove );
			BuildButton( 20, -320, "Raise", true, delegate { AlignHeight( 0.1f ); } );
			BuildButton( 20, -340, "Lower", true, delegate { AlignHeight( -0.1f ); } );
#endif
			if ( node.resource && ( !node.resource.underGround || !node.resource.exposed.Done ) )
				Text( 20, -40, 160, 20, "Resource: " + node.resource.type );
		}

		void BuildButton( int x, int y, string title, bool enabled, UnityEngine.Events.UnityAction action )
		{
			Button button = Button( x, y, 160, 20, title );
			button.onClick.AddListener( action );
			if ( !enabled )
			{
				Text text = button.gameObject.GetComponentInChildren<Text>();
				if ( text )
					text.color = Color.red;
			}
		}

		void AddFlag()
		{
			if ( Flag.Create().Setup( node, Root.mainPlayer ) != null )
				Close();
		}

		void AddStock()
		{
			if ( Stock.Create().Setup( node, Root.mainPlayer ) != null )
				Close();
		}

		void AddGuardHouse()
		{
			if ( GuardHouse.Create().Setup( node, Root.mainPlayer ) != null )
				Close();
		}

		void AddTree()
		{
			Resource.Create().Setup( node, Resource.Type.tree ).life.Start( -2 * Resource.treeGrowthMax );
		}

		void Remove()
		{
			node.resource?.Remove();
		}

		void AlignHeight( float change )
		{
			node.SetHeight( node.height + change );
		}

		public void BuildWorkshop( Workshop.Type type )
		{
			if ( Workshop.Create().Setup( node, Root.mainPlayer, type ) != null )
				Close();
		}
	}

	public class RoadPanel : Panel
	{
		public Road road;
		public List<ItemImage> leftItems = new List<ItemImage>(), rightItems = new List<ItemImage>(), centerItems = new List<ItemImage>();
		public List<Text> leftNumbers = new List<Text>(), rightNumbers = new List<Text>(), centerDirections = new List<Text>();
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
			Button( 190, 0, 20, 20, iconTable.GetMediaData( Icon.exit ) ).onClick.AddListener( Close );
			Button( 170, -10, 20, 20, iconTable.GetMediaData( Icon.hauler ) ).onClick.AddListener( Hauler );
			Button( 150, -10, 20, 20, iconTable.GetMediaData( Icon.destroy ) ).onClick.AddListener( Remove );
			Button( 130, -10, 20, 20, iconTable.GetMediaData( Icon.box ) ).onClick.AddListener( Split );
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
				centerDirections.Add( Text( 80, row, 60, 20, "" ) );
				centerItems.Add( ItemIcon( 90, row ) );
			}
#if DEBUG
			Selection.activeGameObject = road.gameObject;
#endif
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
			if ( Flag.Create().Setup( node, node.owner ) != null )
				Close();
			World.instance.Validate();
		}

		public override void Update()
		{
			base.Update();
			jam.text = "Items waiting: " + road.Jam;
			workers.text = "Worker count: " + road.workers.Count;

			bool reversed = false;
			var camera = World.instance.eye.camera;
			float x0 = camera.WorldToScreenPoint( road.GetEnd( 0 ).node.Position() ).x;
			float x1 = camera.WorldToScreenPoint( road.GetEnd( 1 ).node.Position() ).x;
			if ( x1 < x0 )
				reversed = true;

			for ( int j = 0; j < itemsDisplayed; j++ )
			{
				int i = itemsDisplayed - 1 - j;
				Worker worker;
				if ( j < road.workers.Count && (worker = road.workers[j]) && worker.taskQueue.Count > 0 )
				{
					Item item = worker.itemInHands;
					centerItems[i].SetItem( item );
					if ( item )
					{
						Flag flag = item.nextFlag;
						if ( flag == null )
							flag = item.destination.flag;
						if ( flag == road.GetEnd( reversed ? 1 : 0 ) )
							centerDirections[i].text = "<";
						else
							centerDirections[i].text = "        >";
					}
					else
						centerDirections[i].text = "";
				}
			}

			for ( int i = 0; i < 2; i++ )
			{
				bool side = reversed ? i == 1 : i == 0;
				var itemImages = side ? leftItems : rightItems;
				var itemTexts = side ? leftNumbers : rightNumbers;
				int[] counts = new int[(int)Item.Type.total];
				var flag = road.GetEnd( i );
				var items = flag.items;
				foreach ( var item in items )
				{
					if ( item != null && item.Road == road && item.flag == flag )
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
		public Image[] itemTimers = new Image[Flag.maxItems];

		public static FlagPanel Create()
		{
			return new GameObject().AddComponent<FlagPanel>();
		}

		public void Open( Flag flag, bool show = false )
		{
#if DEBUG
			Selection.activeGameObject = flag.gameObject;
#endif
			base.Open( flag.node );
			this.flag = flag;
			int col = 16;
			Frame( 0, 0, 250, 75, 10 );
			Button( 230, 0, 20, 20, iconTable.GetMediaData( Icon.exit ) ).onClick.AddListener( Close );
			Button( 210, -45, 20, 20, iconTable.GetMediaData( Icon.destroy ) ).onClick.AddListener( Remove );
			Button( 20, -45, 20, 20, iconTable.GetMediaData( Icon.path ) ).onClick.AddListener( StartRoad );
			Button( 45, -45, 20, 20, iconTable.GetMediaData( Icon.magnet ) ).onClick.AddListener( CaptureRoads );

			for ( int i = 0; i < Flag.maxItems; i++ )
			{
				itemTimers[i] = Image( col, -8, iconSize, 3 );
				items[i] = ItemIcon( col, -13, iconSize, iconSize, Item.Type.unknown );
				int j = i;
				items[i].name = "item " + i;
				col += iconSize+5;
			}
			name = "Flag panel";
			if ( show )
				Root.world.eye.FocusOn( flag );
			Update();
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

		void CaptureRoads()
		{
			for ( int i = 0; i < GroundNode.neighbourCount; i++ )
			{
				GroundNode A = flag.node.Neighbour( i ), B = flag.node.Neighbour( ( i + 1 ) % GroundNode.neighbourCount );
				if ( A.road && A.road == B.road )
				{
					if ( A.road.GetEnd( 0 ) == flag || A.road.GetEnd( 1 ) == flag )
						continue;
					A.road.Split( flag );
					return;
				}
			}
		}

		public override void Update()
		{
			base.Update();

			// TODO Skip empty slots
			for ( int i = 0; i < Flag.maxItems; i++ )
			{
				items[i].SetItem( flag.items[i] );
				itemTimers[i].enabled = false;
				if ( flag.items[i] )
				{
					if ( flag.items[i].flag && flag.items[i].flag == flag )
					{
						itemTimers[i].enabled = true;
						items[i].color = new Color( 1, 1, 1, 1 );
						int timeAtFlag = World.instance.time - flag.items[i].flagTime;
						itemTimers[i].rectTransform.sizeDelta = new Vector2( Math.Min( iconSize, timeAtFlag / 3000 ), 3 );
						itemTimers[i].color = Color.Lerp( Color.green, Color.red, timeAtFlag / 30000f );
					}
					else
						items[i].color = new Color( 1, 1, 1, 0.4f );
				}
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
			Button( 170, 0, 20, 20, iconTable.GetMediaData( Icon.exit ) ).onClick.AddListener( Close );
			item = ItemIcon( 20, -20 );
			itemCount = Text( 20, -44, 160, 20, "Items" );
			World.instance.eye.GrabFocus( this );
#if DEBUG
			Selection.activeGameObject = worker.gameObject;
#endif
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
			item.SetItem( worker.itemInHands );
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
			Button( 200, -10, 20, 20, iconTable.GetMediaData( Icon.exit ) ).onClick.AddListener( Close );
			Button( 190, -150, 20, 20, iconTable.GetMediaData( Icon.destroy ) ).onClick.AddListener( Remove );

			Workshop workshop = construction.boss as Workshop;
			if ( workshop )
				Text( 20, -20, 160, 20, workshop.type.ToString() );

			planks = new WorkshopPanel.Buffer();
			planks.Setup( this, Item.Type.plank, construction.plankNeeded, 20, -40, iconSize + 5 );
			stones = new WorkshopPanel.Buffer();
			stones.Setup( this, Item.Type.stone, construction.stoneNeeded, 20, -64, iconSize + 5 );

			progressBar = Image( 20, -90, ( iconSize + 5 ) * 8, iconSize, iconTable.GetMediaData( Icon.progress ) );

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
		public GameObject route;
		public Text stats;
		GameObject mapIcon;
		public static Material materialUIPath;

		public static void Initialize()
		{
			// TODO Why isn't it transparent?
			materialUIPath = new Material( World.defaultTextureShader );
			materialUIPath.mainTexture = Resources.Load<Texture2D>( "uipath" );
			World.SetRenderMode( materialUIPath, World.BlendMode.Transparent );
		}

		static public ItemPanel Create()
		{
			return new GameObject().AddComponent<ItemPanel>();
		}

		public void Open( Item item )
		{
			this.item = item;
			
			base.Open();
			name = "Item panel";

			Frame( 0, 0, 300, 150, 20 );
			Button( 270, -10, 20, 20, iconTable.GetMediaData( Icon.exit ) ).onClick.AddListener( Close );
			Text( 15, -15, 100, 20, item.type.ToString() );
			stats = Text( 15, -35, 250, 20 );
			Text( 15, -55, 170, 20, "Origin:" );
			BuildingIcon( 100, -55, item.origin );
			Text( 15, -75, 170, 20, "Destination:" );
			BuildingIcon( 100, -75, item.destination );

			mapIcon = new GameObject();
			World.SetLayerRecursive( mapIcon, World.layerIndexMapOnly );
			mapIcon.AddComponent<SpriteRenderer>().sprite = Item.sprites[(int)item.type];
			mapIcon.transform.SetParent( transform );
			mapIcon.name = "Map icon";
			mapIcon.transform.Rotate( 90, 0, 0 );
			mapIcon.transform.localScale = Vector3.one * 0.5f;
#if DEBUG
			Selection.activeGameObject = item.gameObject;
#endif
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

			stats.text = "Age: " + ( World.instance.time - item.born ) / 50 + " secs, at flag for " + ( World.instance.time - item.flagTime ) / 50 + " secs";

			if ( item.destination && route == null )
			{
				route = CreateUIPath( item.path );
				route.transform.SetParent( Root.transform );
			}
			if ( item.flag )
				mapIcon.transform.position = item.flag.node.Position() + Vector3.up * 4;
			else
				mapIcon.transform.position = item.worker.transform.position + Vector3.up * 4;
		}

		public override void Close()
		{
			base.Close();
			Destroy( route );
		}

		public static GameObject CreateUIPath( Path path )
		{
			if ( path == null )
				return null;

			GameObject routeOnMap = new GameObject();
			routeOnMap.name = "Path on map";
			routeOnMap.AddComponent<MeshRenderer>().material = materialUIPath;
			var route = routeOnMap.AddComponent<MeshFilter>().mesh = new Mesh();

			List<Vector3> vertices = new List<Vector3>();
			List<Vector2> uvs = new List<Vector2>();
			List<int> triangles = new List<int>();
			for ( int j = 0; j < path.roadPath.Count; j++ )
			{
				Road road = path.roadPath[j];
				float uvDir = path.roadPathReversed[j] ? 1f : 0f;
				for ( int i = 0; i < road.nodes.Count - 1; i++ )
				{
					Vector3 start = road.nodes[i].Position() + Vector3.up * 0.1f;
					Vector3 end = road.nodes[i + 1].Position() + Vector3.up * 0.1f;
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

					uvs.Add( new Vector2( 0, 6 * uvDir ) );
					uvs.Add( new Vector2( 1, 6 * uvDir ) );
					uvs.Add( new Vector2( 0, 6 * (1 - uvDir) ) );
					uvs.Add( new Vector2( 1, 6 * (1 - uvDir) ) );
				}
			}

			route.vertices = vertices.ToArray();
			route.triangles = triangles.ToArray();
			route.uv = uvs.ToArray();
			return routeOnMap;
		}

		public void SetCameraTarget( Eye eye )
		{
			if ( item.flag )
				World.instance.eye.FocusOn( item.flag.node );
			else
				World.instance.eye.FocusOn( item.worker );
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
		public enum CursorType
		{
			nothing,
			remove,
			road,
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
			if ( cursor && node )
				cursor.transform.localPosition = node.Position();
			if ( !inputHandler.OnMovingOverNode( node ) )
				inputHandler = this;
#if DEBUG
			if ( Input.GetKeyDown( KeyCode.PageUp ) && node )
				node.SetHeight( node.height + 0.05f );
			if ( Input.GetKeyDown( KeyCode.PageDown ) && node )
				node.SetHeight( node.height - 0.05f );
#endif
		}

		public bool OnMovingOverNode( GroundNode node )
		{
			if ( node != null )
			{
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
				SetCursorType( t );
			}
			return true;
		}

		public void SetCursorType( CursorType cursortype )
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
			
			for ( int i = 0; i < cursorTypes.Length; i++ )
				cursorTypes[i].SetActive( i == (int)cursortype );
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

	public class ItemList : Panel
	{
		ScrollRect scroll;
		Player player;

		public static ItemList Create()
		{
			return new GameObject().AddComponent<ItemList>();
		}

		public void Open( Player player )
		{
			base.Open();
			name = "Item list panel";
			this.player = player;
			World.instance.SetTimeFactor( 0 );

			Frame( 0, 0, 400, 320 );
			Button( 370, -10, 20, 20, iconTable.GetMediaData( Icon.exit ) ).onClick.AddListener( Close );
			Text( 50, -20, 100, 20, "Origin" ).gameObject.AddComponent<Button>().onClick.AddListener( delegate { Fill( CompareByOrigin ); } );
			Text( 150, -20, 100, 20, "Destination" ).gameObject.AddComponent<Button>().onClick.AddListener( delegate { Fill( CompareByDestination ); } );
			Text( 250, -20, 100, 20, "Age" ).gameObject.AddComponent<Button>().onClick.AddListener( delegate { Fill( CompareByAge ); } );
			Text( 300, -20, 100, 20, "Route" ).gameObject.AddComponent<Button>().onClick.AddListener( delegate { Fill( CompareByPathLength ); } );

			scroll = ScrollRect( 20, -40, 360, 260 );
			Fill( CompareByAge );
		}

		new void OnDestroy()
		{
			base.OnDestroy();
			World.instance.SetTimeFactor( 1 );
		}

		void Fill( Comparison<Item> comparison )
		{
			int row = 0;
			foreach ( Transform child in scroll.content )
				Destroy( child.gameObject );

			List<Item> sortedItems = new List<Item>();
			foreach ( var item in player.items )
			{
				if ( item )
					sortedItems.Add( item );
			}
			sortedItems.Sort( comparison );

			foreach ( var item in sortedItems )
			{
				var i = ItemIcon( 0, row, 0, 0, Item.Type.unknown, scroll.content );
				i.SetItem( item );

				BuildingIcon( 30, row, item.origin, scroll.content );
				BuildingIcon( 130, row, item.destination, scroll.content );
				Text( 230, row, 50, 20, ( ( World.instance.time - item.born ) / 50 ).ToString(), scroll.content );
				if ( item.path )
					Text( 280, row, 30, 20, item.path.roadPath.Count.ToString(), scroll.content );
				row -= iconSize + 5;
			}
			var t = scroll.content.transform as RectTransform;
			t.sizeDelta = new Vector2( t.sizeDelta.x, sortedItems.Count * ( iconSize + 5 ) );
			scroll.verticalNormalizedPosition = 1;
		}

		static public int CompareByAge( Item itemA, Item itemB )
		{
			if ( itemA.born == itemB.born )
				return 0;
			if ( itemA.born < itemB.born )
				return -1;
			return 1;
		}
		static public int CompareByPathLength( Item itemA, Item itemB )
		{
			int lA = 0, lB = 0;
			if ( itemA.path )
				lA = itemA.path.roadPath.Count;
			if ( itemB.path )
				lB = itemB.path.roadPath.Count;

			if ( lA == lB )
				return 0;
			if ( lA > lB )
				return -1;
			return 1;
		}
		static public int CompareByOrigin( Item itemA, Item itemB )
		{
			return CompareBuildings( itemA.origin, itemB.origin );
		}
		static public int CompareByDestination( Item itemA, Item itemB )
		{
			return CompareBuildings( itemA.destination, itemB.destination );
		}
		static int CompareBuildings( Building A, Building B )
		{
			if ( A == null && B == null )
				return 0;
			if ( A == null )
				return 1;
			if ( B == null )
				return -1;

			if ( A.node.Id == B.node.Id )
				return 0;
			if ( A.node.Id < B.node.Id )
				return 1;
			return -1;
		}
	}

	public class ItemStats : Panel
	{
		ScrollRect scroll;
		Player player;
		Text finalEfficiency;
		Text[] inStock = new Text[(int)Item.Type.total];
		Text[] onWay = new Text[(int)Item.Type.total];
		Text[] production = new Text[(int)Item.Type.total];
		Text[] efficiency = new Text[(int)Item.Type.total];

		public static ItemStats Create()
		{
			return new GameObject().AddComponent<ItemStats>();
		}

		public void Open( Player player )
		{
			base.Open();
			this.player = player;
			Frame( 0, 0, 320, 300 );
			Text( 70, -20, 50, 20, "In stock" ).fontSize = 10;
			Text( 120, -20, 50, 20, "On Road" ).fontSize = 10;
			Text( 170, -20, 50, 20, "Per minute" ).fontSize = 10;
			Text( 220, -20, 50, 20, "Efficiency" ).fontSize = 10;
			scroll = ScrollRect( 20, -45, 280, 205 );
			Button( 290, -10, 20, 20, iconTable.GetMediaData( Icon.exit ) ).onClick.AddListener( Close );
			finalEfficiency = Text( 100, -260, 100, 30 );
			finalEfficiency.fontSize = 16;

			for ( int i = 0; i < inStock.Length; i++ )
			{
				int row = i * - ( iconSize + 5 );
				ItemIcon( 0, row, 0, 0, (Item.Type)i, scroll.content );
				inStock[i] = Text( 30, row, 40, iconSize, "0", scroll.content );
				onWay[i] = Text( 80, row, 40, iconSize, "0", scroll.content );
				production[i] = Text( 130, row, 40, iconSize, "0", scroll.content );
				efficiency[i] = Text( 180, row, 40, iconSize, "0", scroll.content );
			}

			( scroll.content.transform as RectTransform).sizeDelta = new Vector2( 230, player.efficiency.Length * ( iconSize + 5 ) );
		}

		new void Update()
		{
			base.Update();
			int[] inStockCount = new int[(int)Item.Type.total];
			foreach ( var stock in player.stocks )
			{
				for ( int i = 0; i < inStock.Length; i++ )
					inStockCount[i] += stock.content[i];
			}

			int[] onWayCount = new int[(int)Item.Type.total];
			foreach ( var item in player.items )
			{
				if ( item == null )
					continue;
				onWayCount[(int)item.type]++;
			}

			for ( int i = 0; i < inStock.Length; i++ )
			{
				Color textColor = Color.yellow;
				if ( Player.efficiencyFactors[i] != 0 )
					textColor = Color.green;
				if ( (int)player.worseItemType == i )
					textColor = Color.red;
				inStock[i].color = onWay[i].color = production[i].color = efficiency[i].color = textColor;

				inStock[i].text = inStockCount[i].ToString();
				onWay[i].text = onWayCount[i].ToString();
				production[i].text = player.efficiency[i].ToString();
				float itemEfficiency = Player.efficiencyFactors[i] * player.efficiency[i];
				efficiency[i].text = itemEfficiency.ToString();
			};

			finalEfficiency.text = player.totalEfficiency.ToString();
		}
	}

	public interface InputHandler
	{
		bool OnMovingOverNode( GroundNode node );
		bool OnNodeClicked( GroundNode node );
	}
}

