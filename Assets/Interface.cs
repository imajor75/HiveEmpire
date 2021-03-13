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

public class Interface : HiveObject
{
	public List<Panel> panels = new List<Panel>();
	public static int iconSize = 20;
	public static Font font;
	public World world;
	Canvas canvas;
	public Workshop.Type selectedWorkshopType = Workshop.Type.unknown;
	public Viewport viewport;
	static public MediaTable<Sprite, Icon> iconTable;
	public GameObject debug;
	public static Interface root;
	public bool heightStrips;
	public Player mainPlayer;
	static Tooltip tooltip;
	public int autoSave = autoSaveInterval;
	const int autoSaveInterval = 15000;
	public HighlightType highlightType;
	public Ground.Area highlightArea;
	public GameObject highlightVolume;
	GroundNode highlightVolumeCenter;
	int highlightVolumeRadius;
	static Material highlightMaterial;
	public GameObject highlightOwner;
	public static Material materialUIPath;

	public enum HighlightType
	{
		none,
		stocks,
		volume,
		area
	}

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
		dynamite,
		rightArrow,
		crosshair,
		summa,
		tinyFrame,
		reset,
		sleeping,
		clock,
		alarm
	}

	public Interface()
	{
		root = this;
	}

	static public GameObject GetUIElementUnderCursor()
	{
		PointerEventData p = new PointerEventData( EventSystem.current )
		{
			position = Input.mousePosition
		};
		List<RaycastResult> result = new List<RaycastResult>();
		EventSystem.current.RaycastAll( p, result );
		if ( result.Count == 0 )
			return null;

		return result[0].gameObject;
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

	void OnApplicationQuit()
	{
		if ( !Assert.error )
			Save();
	}

	void LateUpdate()
	{
		Validate();
	}

	void FixedUpdate()
	{
		autoSave--;
		if ( autoSave < 0 )
		{
			Save();
			autoSave = autoSaveInterval;
		}
		var o = materialUIPath.mainTextureOffset;
		o.y -= 0.015f;
		if ( o.y < 0 )
			o.y += 1;
		materialUIPath.mainTextureOffset = o;
	}

	static void Initialize()
	{
		//{
		//	Color o = Color.white;
		//	Color g = Color.black;
		//	var t = Resources.Load<Texture2D>( "arrow" );
		//	var d = new Texture2D( t.width, t.height );
		//	for ( int x = 0; x < t.width; x++ )
		//	{
		//		for ( int y = 0; y < t.height; y++ )
		//		{
		//			var c = t.GetPixel( x, y );
		//			float a = c.a;
		//			if ( a < 0.0001f )
		//			{
		//				o = c;
		//				c = g;
		//			}
		//			else
		//				c = Color.Lerp( g, c - ( o * ( a - 1 ) ), a );
		//			c.a = a;
		//			d.SetPixel( x, y, c );
		//		};
		//	}
		//	d.Apply();
		//	System.IO.File.WriteAllBytes( "target.png", d.EncodeToPNG() );
		//}
		var highlightShader = Resources.Load<Shader>( "HighlightVolume" );
		highlightMaterial = new Material( highlightShader );

		font = (Font)Resources.GetBuiltinResource( typeof( Font ), "Arial.ttf" );
		Assert.global.IsNotNull( font );
		object[] table = {
		"simple UI & icons/box/box_event1", Icon.frame,
		"simple UI & icons/button/button_triangle_right", Icon.rightArrow,
		"simple UI & icons/button/board", Icon.progress,
		"simple UI & icons/button/button_exit", Icon.exit,
		"simple UI & icons/button/button_login", Icon.button,
		"simple UI & icons/box/smallFrame", Icon.smallFrame };
		iconTable.Fill( table );
		Frame.Initialize();
		//			print( "Runtime debug: " + UnityEngine.Debug.isDebugBuild );
		//#if DEVELOPMENT_BUILD
		//		print( "DEVELOPMENT_BUILD" );
		//#endif
		//#if DEBUG
		//		print( "DEBUG" );
		//#endif

		materialUIPath = new Material( World.defaultMapShader )
		{
			mainTexture = Resources.Load<Texture2D>( "uipath" ),
			renderQueue = 4001
		};
	}

	void Start()
	{
		Assert.Initialize();
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
		CameraHighlight.Initialize();

		Directory.CreateDirectory( Application.persistentDataPath + "/Saves" );

		canvas = gameObject.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		gameObject.AddComponent<GraphicRaycaster>();

		var viewport = new GameObject();
		this.viewport = viewport.AddComponent<Viewport>();
		viewport.transform.SetParent( transform );
		viewport.name = "Viewport";

		var esObject = new GameObject
		{
			name = "Event System"
		};
		esObject.AddComponent<EventSystem>();
		esObject.AddComponent<StandaloneInputModule>();

		debug = new GameObject
		{
			name = "Debug"
		};
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

	void Load( string fileName )
	{
		print( "Loading " + fileName );
		world.Load( fileName );
		mainPlayer = world.players[0];
	}

	void Save( string fileName = "" )
	{
		viewport.ResetInputHandler();
		if ( fileName == "" )
			fileName = Application.persistentDataPath + "/Saves/" + World.rnd.Next() + ".json";
		world.Save( fileName );
		print( fileName + " is saved" );
	}

	void Update()
	{
		GroundNode node = viewport.FindNodeAt( Input.mousePosition );

		if ( world.timeFactor != 0 && world.timeFactor != 8 )
		{
			if ( Input.GetKey( KeyCode.Space ) )
				world.SetTimeFactor( 5 );
			else
				world.SetTimeFactor( 1 );
		}
		if ( Input.GetKey( KeyCode.R ) )
		{
			if ( panels.Count > 0 )
				foreach ( var panel in panels )
					panel.target?.Reset();
			else
				world.Reset();
		}
		if ( Input.GetKeyDown( KeyCode.Insert ) )
			world.SetTimeFactor( 8 );
		if ( Input.GetKeyDown( KeyCode.Delete ) )
			world.SetTimeFactor( 1 );
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
		if ( Input.GetKeyDown( KeyCode.H ) )
		{
			History.Create().Open( mainPlayer );
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
			if ( !viewport.ResetInputHandler() )
			{
				for ( int i = panels.Count - 1; i >= 0; i-- )
				{
					if ( !panels[i].escCloses )
						continue;
					panels[panels.Count - 1].Close();
					break;
				}
			}
		}
		if ( Input.GetKeyDown( KeyCode.M ) )
			Map.Create().Open( Input.GetKey( KeyCode.LeftShift ) || Input.GetKey( KeyCode.RightShift ) );
		if ( Input.GetKeyDown( KeyCode.Alpha9 ) )
			SetHeightStrips( !heightStrips );

		CheckHighlight();
	}

	void CheckHighlight()
	{
		if ( highlightOwner == null )
			highlightType = HighlightType.none;
		if ( highlightType != HighlightType.area || highlightArea.center == null )
		{
			Destroy( highlightVolume );
			highlightVolume = null;
			return;
		}

		UpdateHighlight();
	}

	void UpdateHighlight()
	{
		if ( highlightVolume && highlightVolumeCenter == highlightArea.center && highlightVolumeRadius == highlightArea.radius )
			return;

		highlightVolumeCenter = highlightArea.center;
		highlightVolumeRadius = highlightArea.radius;

		Mesh m;
		if ( highlightVolume == null )
		{
			highlightVolume = new GameObject
			{
				name = "Highlight Volume"
			};
			highlightVolume.transform.SetParent( World.instance.transform );
			var f = highlightVolume.AddComponent<MeshFilter>();
			var r = highlightVolume.AddComponent<MeshRenderer>();
			r.material = highlightMaterial;
			m = f.mesh = new Mesh();
			var c = highlightVolume.AddComponent<MeshCollider>();
			c.convex = true;
			c.sharedMesh = m;
			CreateHighLightVolumeMesh( m );
		}

		highlightVolume.transform.localPosition = highlightVolumeCenter.Position;
		float scale = ( highlightVolumeRadius + 0.5f ) * GroundNode.size;
		highlightVolume.transform.localScale = new Vector3( scale, 20, scale );
		Destroy( highlightVolume.GetComponent<MeshCollider>() );
		highlightVolume.AddComponent<MeshCollider>();
	}

	void CreateHighLightVolumeMesh( Mesh m )
	{
		var vertices = new Vector3[GroundNode.neighbourCount * 2];
		var corners = new int[,] { { 1, 1 }, { 0, 1 }, { -1, 0 }, { -1, -1 }, { 0, -1 }, { 1, 0 } };
		for ( int i = 0; i < GroundNode.neighbourCount; i++ )
		{
			float x = corners[i, 0] - corners[i, 1] / 2f;
			float y = corners[i, 1];
			vertices[i * 2 + 0] = new Vector3( x, -1, y );
			vertices[i * 2 + 1] = new Vector3( x, +1, y );
		}
		m.vertices = vertices;

		var triangles = new int[GroundNode.neighbourCount * 2 * 3 + 2 * 3 * (GroundNode.neighbourCount - 2)];
		for ( int i = 0; i < GroundNode.neighbourCount; i++ )
		{
			int a = i * 2;
			int b = i * 2 + 2;
			if ( b == GroundNode.neighbourCount * 2 )
				b = 0;

			triangles[i * 2 * 3 + 0] = a + 0;
			triangles[i * 2 * 3 + 1] = a + 1;
			triangles[i * 2 * 3 + 2] = b + 0;

			triangles[i * 2 * 3 + 3] = a + 1;
			triangles[i * 2 * 3 + 4] = b + 1;
			triangles[i * 2 * 3 + 5] = b + 0;
		}
		assert.AreEqual( GroundNode.neighbourCount, 6 );
		int cap = GroundNode.neighbourCount * 6;
		triangles[cap++] = 0;
		triangles[cap++] = 2;
		triangles[cap++] = 10;

		triangles[cap++] = 10;
		triangles[cap++] = 2;
		triangles[cap++] = 8;

		triangles[cap++] = 8;
		triangles[cap++] = 2;
		triangles[cap++] = 4;

		triangles[cap++] = 8;
		triangles[cap++] = 4;
		triangles[cap++] = 6;

		triangles[cap++] = 11;
		triangles[cap++] = 3;
		triangles[cap++] = 1;

		triangles[cap++] = 9;
		triangles[cap++] = 3;
		triangles[cap++] = 11;

		triangles[cap++] = 5;
		triangles[cap++] = 3;
		triangles[cap++] = 9;

		triangles[cap++] = 7;
		triangles[cap++] = 5;
		triangles[cap++] = 9;
		m.triangles = triangles;

		highlightVolume.GetComponent<MeshCollider>().sharedMesh = m;
	}

	public static GameObject CreateUIPath( Path path )
	{
		if ( path == null )
			return null;

		GameObject routeOnMap = new GameObject
		{
			name = "Path on map"
		};
		var renderer = routeOnMap.AddComponent<MeshRenderer>();
		renderer.material = materialUIPath;
		renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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
				Vector3 start = road.nodes[i].Position + Vector3.up * 0.1f;
				Vector3 end = road.nodes[i + 1].Position + Vector3.up * 0.1f;
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
				uvs.Add( new Vector2( 0, 6 * ( 1 - uvDir ) ) );
				uvs.Add( new Vector2( 1, 6 * ( 1 - uvDir ) ) );
			}
		}

		route.vertices = vertices.ToArray();
		route.triangles = triangles.ToArray();
		route.uv = uvs.ToArray();
		return routeOnMap;
	}

	void SetHeightStrips( bool value )
	{
		this.heightStrips = value;
		world.ground.material.SetInt( "_HeightStrips", value ? 1 : 0 );
	}

	public override void Validate()
	{
#if DEBUG
		Profiler.BeginSample( "Validate" );
		world.Validate();
		if ( highlightType == HighlightType.volume )
			Assert.global.IsNotNull( highlightVolume );
		if ( highlightType == HighlightType.area )
		{
			Assert.global.IsNotNull( highlightArea );
			Assert.global.IsNotNull( highlightArea.center );
		}
		Profiler.EndSample();
#endif
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
		public bool disableDrag;

		public static int itemIconBorderSize = 2;

		public enum CompareResult
		{
			different,
			sameButDifferentTarget,
			same
		}

		public Interface Root
		{
			get
			{
				if ( cachedRoot == null )
					cachedRoot = GameObject.FindObjectOfType<Interface>();
				return cachedRoot;
			}
		}

		// Summary:
		// Return true if the caller should give
		public bool Open( GroundNode target = null, int x = 0, int y = 0 )
		{
			foreach ( var panel in Root.panels )
			{
				var r = IsTheSame( panel );
				if ( r != CompareResult.different )
					panel.Close();
				if ( r == CompareResult.same )
				{
					Destroy( gameObject );
					return true;
				}
			}

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
			return false;
		}

		public virtual CompareResult IsTheSame( Panel other )
		{
			if ( other.GetType() == GetType() )
				return CompareResult.same;

			return CompareResult.different;
		}

		public void OnDestroy()
		{
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

		public AreaControl AreaIcon( int x, int y, Ground.Area area, Component parent = null )
		{
			Image( x - itemIconBorderSize, y + itemIconBorderSize, iconSize + 2 * itemIconBorderSize, iconSize + 2 * itemIconBorderSize, iconTable.GetMediaData( Icon.smallFrame ), parent );
			var i = Image( x, y, iconSize, iconSize, iconTable.GetMediaData( Icon.crosshair ), parent );
			var a= i.gameObject.AddComponent<AreaControl>();
			a.Setup( area );
			return a;
		}

		public Component BuildingIcon( int x, int y, Building building, Component parent = null )
		{
			if ( building == null )
				return null;

			var text = Text( x, y, 150, 20, building.title, parent );
			text.gameObject.AddComponent<Button>().onClick.AddListener( delegate { SelectBuilding( building ); } );
			return text;
		}

		public static void SelectBuilding( Building building )
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

			MoveTo( target.Position + Vector3.up * GroundNode.size );
		}

		public void MoveTo( Vector3 position )
		{
			Vector3 screenPosition = root.viewport.camera.WorldToScreenPoint( position );
			if ( screenPosition.y > Screen.height )
				screenPosition = World.instance.eye.camera.WorldToScreenPoint( target.Position - Vector3.up * GroundNode.size );
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
			if ( disableDrag )
				return;

			followTarget = false;
			frame.rectTransform.anchoredPosition += eventData.delta;
		}

		public void OnPointerClick( PointerEventData eventData )
		{
			if ( eventData.clickCount == 2 )
				OnDoubleClick();
		}

		public virtual void OnDoubleClick()
		{
			if ( target == null )
				return;

			World.instance.eye.FocusOn( target );
			followTarget = true;
		}

		public class AreaControl : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IInputHandler
		{
			public Ground.Area area;
			public Image image;

			public void Setup( Ground.Area area )
			{
				this.area = area;
				image = gameObject.GetComponent<Image>();
			}

			public bool OnMovingOverNode( GroundNode node )
			{
				if ( node )
					area.center = node;
				return true;
			}

			public bool OnNodeClicked( GroundNode node )
			{
				if ( root.highlightArea == area )
					root.highlightType = HighlightType.none;
				return false;
			}

			public void OnPointerClick( PointerEventData eventData )
			{
				if ( Input.GetKey( KeyCode.LeftShift ) || Input.GetKey( KeyCode.RightShift ) )
				{
					area.center = null;
					if ( root.highlightArea == area )
						root.highlightType = HighlightType.none;
					return;
				}
				area.center = World.instance.ground.nodes[0];
				area.radius = 4;
				root.highlightType = HighlightType.area;
				root.highlightArea = area;
				root.highlightOwner = gameObject;
				root.viewport.InputHandler = this;
			}

			public void OnPointerEnter( PointerEventData eventData )
			{
				if ( area.center == null )
					return;

				root.highlightType = HighlightType.area;
				root.highlightOwner = gameObject;
				root.highlightArea = area;
			}

			public void OnPointerExit( PointerEventData eventData )
			{
				if ( root.viewport.InputHandler != this as IInputHandler && root.highlightArea == area )
					root.highlightType = HighlightType.none;
			}

			void Update()
			{
				image.color = area.center != null ? Color.green : Color.grey;
				if ( Input.GetKeyDown( KeyCode.Comma ) )
				{
					if ( area.radius > 1 )
						area.radius--;
				}
				if ( Input.GetKeyDown( KeyCode.Period ) )
				{
					if ( area.radius < 8 )
						area.radius++;
				}
			}

			public void OnLostInput()
			{
				area.center = null;
				root.highlightType = HighlightType.none;
			}
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
					tooltip.SetText( this, item.type.ToString(), path = CreateUIPath( item.path ) );
				else
					tooltip.SetText( this, itemType.ToString() );
			}

			public void OnPointerExit( PointerEventData eventData )
			{
				tooltip.SetText( this, "" );
			}
		}
	}

	public class Frame : Image
	{
		public int borderWidth = 30;

		static readonly Sprite[] pieces = new Sprite[9];
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
		public bool Open( Building building )
		{
#if DEBUG
			Selection.activeGameObject = building.gameObject;
#endif
			this.building = building;
			return base.Open( building.node );
		}
		public override CompareResult IsTheSame( Panel other )
		{
			var p = other as BuildingPanel;
			if ( p == null )
				return CompareResult.different;

			if ( p.building == this.building )
				return CompareResult.same;

			return CompareResult.sameButDifferentTarget;
		}


	}

	public class WorkshopPanel : BuildingPanel
	{
		public Workshop workshop;
		public Image progressBar;
		public Image changeModeImage;
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
			if ( base.Open( workshop ) )
				return;

			name = "Workshop panel";
			this.workshop = workshop;
			bool showOutputBuffer = false, showProgressBar = false;
			if ( workshop.configuration.outputType != Item.Type.unknown || workshop.type == Workshop.Type.forester )
			{
				showProgressBar = true;
				showOutputBuffer = workshop.configuration.outputType != Item.Type.unknown;
			}
			int displayedBufferCount = workshop.buffers.Count + ( showOutputBuffer ? 1 : 0 );
			int height = 100 + displayedBufferCount * iconSize * 3 / 2 + ( showProgressBar ? iconSize : 0 );
			Frame( 0, 0, 240, height );
			Button( 210, -10, 20, 20, iconTable.GetMediaData( Icon.exit ) ).onClick.AddListener( Close );
			Button( 190, 40 - height, 20, 20, iconTable.GetMediaData( Icon.destroy ) ).onClick.AddListener( Remove );
			Button( 170, 40 - height, 20, 20, iconTable.GetMediaData( Icon.hauler ) ).onClick.AddListener( ShowWorker );
			var changeModeButton = Button( 150, 40 - height, 20, 20, GetModeIcon() );
			changeModeButton.onClick.AddListener( ChangeMode );
			changeModeImage = changeModeButton.gameObject.GetComponent<Image>();

			Text( 20, -20, 160, 20, workshop.type.ToString() );

			int row = -40;
			int col = 20;
			buffers = new List<Buffer>();
			foreach ( var b in workshop.buffers )
			{
				var bui = new Buffer();
				bui.Setup( this, b, col, row, iconSize + 5 );
				buffers.Add( bui );
				//if ( !workshop.configuration.commonInputs )
					row -= iconSize * 3 / 2;
				//else
				//	col += b.size * ( iconSize + 5 ) + 20;
			}
			//if ( workshop.configuration.commonInputs )
			//	row -= iconSize * 3 / 2;

			if ( showProgressBar )
			{
				if ( showOutputBuffer )
				{
					outputs = new Buffer();
					outputs.Setup( this, workshop.configuration.outputType, workshop.configuration.outputMax, 20, row, iconSize + 5 );
					row -= iconSize * 3 / 2;
				}
				progressBar = Image( 20, row, ( iconSize + 5 ) * 7, iconSize, iconTable.GetMediaData( Icon.progress ) );
				AreaIcon( 200, row, workshop.outputArea );

				itemsProduced = Text( 20, row - 24, 200, 20 );
				productivity = Text( 180, -20, 30, 20 );
			}
			if ( show )
				Root.world.eye.FocusOn( workshop );
		}

		void Remove()
		{
			if ( workshop && workshop.Remove( false ) )
				Close();
		}

		void ShowWorker()
		{
			WorkerPanel.Create().Open( workshop.worker, true );
		}

		Sprite GetModeIcon()
		{
			if ( workshop.mode == Workshop.Mode.sleeping )
				return iconTable.GetMediaData( Icon.sleeping );
			if ( workshop.mode == Workshop.Mode.whenNeeded )
				return iconTable.GetMediaData( Icon.clock );
			if ( workshop.mode == Workshop.Mode.always )
				return iconTable.GetMediaData( Icon.alarm );

			workshop.assert.IsTrue( false );
			return null;
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
					progressBar.rectTransform.sizeDelta = new Vector2( iconSize * 7 * workshop.progress, iconSize );
					progressBar.color = Color.white;
				}
				else
				{
					if ( workshop.Gatherer && !workshop.working )
						progressBar.color = Color.green;
					else
						progressBar.color = Color.red;
				}
				productivity.text = ( (int)( workshop.productivity.current * 100 ) ).ToString() + "%";
				itemsProduced.text = "Items produced: " + workshop.itemsProduced;
			}
			changeModeImage.sprite = GetModeIcon();
		}

		void ChangeMode()
		{
			if ( workshop.mode == Workshop.Mode.sleeping )
				workshop.mode = Workshop.Mode.whenNeeded;
			else if ( workshop.mode == Workshop.Mode.whenNeeded )
				workshop.mode = Workshop.Mode.always;
			else
				workshop.mode = Workshop.Mode.sleeping;
		}

		public class Buffer
		{
			public ItemImage[] items;
			public BuildingPanel boss;
			public Item.Type itemType;
			Workshop.Buffer buffer;

			public void Setup( BuildingPanel boss, Item.Type itemType, int itemCount, int x, int y, int xi, Ground.Area area = null )
			{
				items = new ItemImage[itemCount];
				this.boss = boss;
				this.itemType = itemType;
				for ( int i = 0; i < itemCount; i++ )
				{
					items[i] = boss.ItemIcon( x, y, iconSize, iconSize, itemType );
					x += xi;
				}
				if ( area != null )
					boss.AreaIcon( x, y, area );
			}

			public void Setup( BuildingPanel boss, Workshop.Buffer buffer, int x, int y, int xi )
			{
				this.buffer = buffer;
				Setup( boss, buffer.itemType, buffer.size, x, y, xi, buffer.area );
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

	public class StockPanel : BuildingPanel, IInputHandler
	{
		public Stock stock;
		public Text[] counts = new Text[(int)Item.Type.total];
		public Text total;
		public Item.Type itemTypeForRetarget;
		public Item.Type selectedItemType = Item.Type.log;
		public ItemImage selected;
		public Text inputMin, inputMax, outputMin, outputMax;

		float lastMouseXPosition;
		List<int>listToChange;
		int min, max;

		public static StockPanel Create()
		{
			return new GameObject().AddComponent<StockPanel>();
		}

		public void Open( Stock stock, bool show = false )
		{
			this.stock = stock;
			if ( base.Open( stock ) )
				return;
			name = "Stock panel";
			RecreateControls();
			if ( show )
				Root.world.eye.FocusOn( stock );
		}

		void SelectItemType( Item.Type itemType )
		{
			if ( Input.GetKey( KeyCode.LeftAlt ) && Input.GetKey( KeyCode.LeftShift ) && Input.GetKey( KeyCode.LeftControl ) )
			{
				stock.content[(int)itemType] = 0;
				return;
			}
			selectedItemType = itemType;
		}

		void RecreateControls()
		{
			foreach ( Transform t in transform )
				Destroy( t.gameObject );

			int height = 340;
			Frame( 0, 0, 300, height );
			Button( 270, -10, 20, 20, iconTable.GetMediaData( Icon.exit ) ).onClick.AddListener( Close );
			Button( 270, 40 - height, 20, 20, iconTable.GetMediaData( Icon.destroy ) ).onClick.AddListener( Remove );
			AreaIcon( 30, -30, stock.inputArea );
			AreaIcon( 250, -30, stock.outputArea );
			Button( 140, -30, iconSize, iconSize, iconTable.GetMediaData( Icon.reset ) ).onClick.AddListener( stock.ClearSettings );
			total = Text( 35, 35 - height, 100, 20 );
			total.fontSize = 16;

			int row = -55;
			for ( int j = 0; j < (int)Item.Type.total; j++ )
			{
				int offset = j % 2 > 0 ? 140 : 0;
				var t = (Item.Type)j;
				var i = ItemIcon( 20 + offset, row, iconSize, iconSize, (Item.Type)j );
				if ( stock.destinations[j] )
				{
					Image( 35 + offset, row, 20, 20, iconTable.GetMediaData( Icon.rightArrow ) );
					offset += 10;
				}
				i.gameObject.GetComponent<Button>().onClick.AddListener( delegate { SelectItemType( t ); } );
				counts[j] = Text( 44 + offset, row, 100, 20, "" );
				if ( j % 2 > 0 )
					row -= iconSize + 5;
			}

			for ( int i = 0; i < counts.Length; i++ )
			{
				Item.Type j = (Item.Type)i;
				counts[i].gameObject.AddComponent<Button>().onClick.AddListener( delegate { SelectItemType( j ); } );
			}

			int ipx = 165, ipy = -280;
			selected = ItemIcon( ipx, ipy, 2 * iconSize, 2 * iconSize, selectedItemType );
			selected.GetComponent<Button>().onClick.RemoveAllListeners();
			selected.GetComponent<Button>().onClick.AddListener( SetTarget );
			inputMin = Text( ipx - 40, ipy, 40, 20 );
			inputMax = Text( ipx + 50, ipy, 40, 20 );
			outputMin = Text( ipx - 40, ipy - 20, 40, 20 );
			outputMax = Text( ipx + 50, ipy - 20, 40, 20 );
		}

		void SetTarget()
		{
			if ( Input.GetKey( KeyCode.LeftShift ) || Input.GetKey( KeyCode.RightShift ) )
			{
				SelectBuilding( stock.destinations[(int)selectedItemType] );
				return;
			}
			if ( Input.GetKey( KeyCode.LeftControl ) || Input.GetKey( KeyCode.RightControl ) )
			{
				stock.destinations[(int)selectedItemType] = null;
				RecreateControls();
				return;
			}

			itemTypeForRetarget = selectedItemType;
			Root.highlightOwner = gameObject;
			Root.viewport.InputHandler = this;
			Root.highlightType = HighlightType.stocks;
		}

		void Remove()
		{
			if ( stock && stock.Remove( false ) )
				Close();
		}

		public override void Update()
		{
			base.Update();
			for ( int i = 0; i < (int)Item.Type.total; i++ )
			{
				Color c = Color.yellow;
				if ( stock.content[i] < stock.inputMin[i] )
					c = Color.red;
				if ( stock.content[i] > stock.inputMax[i] )
					c = Color.green;
				counts[i].color = c;
				counts[i].text = stock.content[i] + " (+" + stock.onWay[i] + ")";
			}
			total.text = stock.total + " => " + stock.totalTarget;
			selected.SetType( selectedItemType );
			int t = (int)selectedItemType;
			inputMin.text = stock.inputMin[t] + "<";
			outputMin.text = stock.outputMin[t] + "<";
			inputMax.text = "<" + stock.inputMax[t];
			outputMax.text = "<" + stock.outputMax[t];

			if ( Input.GetKeyDown( KeyCode.Mouse0 ) )
			{
				lastMouseXPosition = Input.mousePosition.x;
				var g = GetUIElementUnderCursor();
				if ( g == inputMin.gameObject )
				{
					listToChange = stock.inputMin;
					min = 0;
					max = stock.inputMax[t];
					disableDrag = true;
				}
				if ( g == inputMax.gameObject )
				{
					listToChange = stock.inputMax;
					min = stock.inputMin[t];
					max = Stock.maxItems;
					disableDrag = true;
				}
				if ( g == outputMin.gameObject )
				{
					listToChange = stock.outputMin;
					min = 0;
					max = stock.outputMax[t];
					disableDrag = true;
				}
				if ( g == outputMax.gameObject )
				{
					listToChange = stock.outputMax;
					min = stock.outputMin[t];
					max = Stock.maxItems;
					disableDrag = true;
				}
			}

			if ( listToChange != null )
			{
				if ( Input.GetKey( KeyCode.Mouse0 ) )
				{
					int newValue = listToChange[(int)selectedItemType] + (int)( ( Input.mousePosition.x - lastMouseXPosition ) * 0.2f );
					if ( newValue < min )
						newValue = min;
					if ( newValue > max )
						newValue = max;
					listToChange[(int)selectedItemType] = newValue;
					lastMouseXPosition = Input.mousePosition.x;
				}
				else
				{
					disableDrag = false;
					listToChange = null;
				}
			}
		}

		public bool OnMovingOverNode( GroundNode node )
		{
			return true;
		}

		public bool OnNodeClicked( GroundNode node )
		{
			var target = node.building as Stock;
			if ( target == null )
				return true;

			stock.destinations[(int)itemTypeForRetarget] = target;
			Root.highlightType = HighlightType.none;
			RecreateControls();
			return false;
		}

		public void OnLostInput()
		{
			Root.highlightType = HighlightType.none;
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
				BuildButton( 180, row, type.ToString(), Workshop.IsNodeSuitable( node, Root.mainPlayer, Workshop.GetConfiguration( type ) ), delegate { BuildWorkshop( type ); } );
				row -= 20;
			}
			BuildButton( 20, -220, "Flag", Flag.IsNodeSuitable( node, Root.mainPlayer ), AddFlag );
			BuildButton( 20, -240, "Stock", Stock.IsNodeSuitable( node, Root.mainPlayer ), AddStock );
			BuildButton( 20, -260, "Guardhouse", GuardHouse.IsNodeSuitable( node, Root.mainPlayer ), AddGuardHouse );
#if DEBUG
			BuildButton( 20, -280, "Tree", !node.IsBlocking( true ), AddTree );
			BuildButton( 20, -300, "Remove", node.IsBlocking( true ), Remove );
			BuildButton( 20, -320, "Raise", true, delegate { AlignHeight( 0.1f ); } );
			BuildButton( 20, -340, "Lower", true, delegate { AlignHeight( -0.1f ); } );
			BuildButton( 20, -360, "Cave", !node.IsBlocking( true ), AddCave );

			BuildButton( 20, -380, "Gold patch", true, delegate { AddResourcePatch( Resource.Type.gold ); } );
			BuildButton( 20, -400, "Coal patch", true, delegate { AddResourcePatch( Resource.Type.coal ); } );
			BuildButton( 20, -420, "Iron patch", true, delegate { AddResourcePatch( Resource.Type.iron ); } );
			BuildButton( 20, -440, "Stone patch", true, delegate { AddResourcePatch( Resource.Type.stone ); } );
			BuildButton( 20, -460, "Salt patch", true, delegate { AddResourcePatch( Resource.Type.salt ); } );
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

		void AddResourcePatch( Resource.Type resourceType )
		{
			node.AddResourcePatch( resourceType, 3, 10, true, true );
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

		void AddCave()
		{
			Resource.Create().Setup( node, Resource.Type.animalSpawner );
		}

		void Remove()
		{
			node.resource?.Remove( false );
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
			if ( road && road.Remove( false ) )
				Close();
		}

		void Hauler()
		{
			WorkerPanel.Create().Open( road.workers[0], true ); // TODO Make it possibe to view additional workers
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
			float x0 = camera.WorldToScreenPoint( road.GetEnd( 0 ).node.Position ).x;
			float x1 = camera.WorldToScreenPoint( road.GetEnd( 1 ).node.Position ).x;
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
			if ( base.Open( flag.node ) )
				return;

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
			if ( flag && flag.Remove( false ) )
				Close();
		}

		void StartRoad()
		{
			if ( flag )
			{
				Road road = Road.Create().Setup( flag );
				Root.viewport.InputHandler = road;
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
						int timeAtFlag = flag.items[i].atFlag.Age;
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
		Text itemsInCart;
		public Stock cartDestination;
		Component destinationBuilding;
		GameObject cartPath;

		public static WorkerPanel Create()
		{
			return new GameObject().AddComponent<WorkerPanel>();
		}

		public void Open( Worker worker, bool show )
		{
			if ( base.Open( worker.node ) )
				return;
			name = "Worker panel";
			this.worker = worker;
			var cart = worker as Stock.Cart;
			Frame( 0, 0, 200, cart ? 140 : 80 );
			Button( 170, 0, 20, 20, iconTable.GetMediaData( Icon.exit ) ).onClick.AddListener( Close );
			item = ItemIcon( 20, -20 );
			itemsInCart = Text( 45, -20, 150, 20 );
			itemCount = Text( 20, -44, 160, 20, "Items" );
			if ( cart )
			{
				Text( 20, -70, 50, 20, "From:" );
				BuildingIcon( 70, -70, cart.building );
				Text( 20, -95, 50, 20, "To:" );
			}

			if ( show )
				World.instance.eye.GrabFocus( this );
#if DEBUG
			Selection.activeGameObject = worker.gameObject;
#endif
		}

		public override void OnDoubleClick()
		{
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
			var cart = worker as Stock.Cart;
			if ( cart )
			{
				item.SetType( cart.itemType );
				itemsInCart.text = "x" + cart.itemQuantity;
				if ( cart.destination != cartDestination )
				{
					cartDestination = cart.destination;
					Destroy( destinationBuilding );
					if ( cart.destination )
						destinationBuilding = BuildingIcon( 70, -95, cart.destination );
					var path = cart.FindTaskInQueue<Worker.WalkToFlag>()?.path;
					Destroy( cartPath );
					cartPath = CreateUIPath( path );
				}
			}
			else
				item.SetItem( worker.itemInHands );
			itemCount.text = "Items delivered: " + worker.itemsDelivered;
			if ( followTarget )
				MoveTo( worker.transform.position + Vector3.up * GroundNode.size );
		}

		new void OnDestroy()
		{
			base.OnDestroy();
			World.instance.eye.ReleaseFocus( this );
			Destroy( cartPath );
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
			if ( construction != null && construction.boss != null && construction.boss.Remove( true ) )
				Close();
		}
	}

	public class ItemPanel : Panel, Eye.IDirector
	{
		public Item item;
		public GameObject route;
		public Text stats;
		GameObject mapIcon;

		static public ItemPanel Create()
		{
			return new GameObject().AddComponent<ItemPanel>();
		}

		public void Open( Item item )
		{
			this.item = item;

			if ( base.Open() )
				return;

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
		}

		public override void OnDoubleClick()
		{
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

			stats.text = "Age: " + item.life.Age / 50 + " secs, at flag for " + item.atFlag.Age / 50 + " secs";

			if ( item.destination && route == null )
			{
				route = CreateUIPath( item.path );
				route.transform.SetParent( Root.transform );
			}
			if ( item.flag )
				mapIcon.transform.position = item.flag.node.Position + Vector3.up * 4;
			else
			{
				item.assert.IsNotNull( item.worker );
				mapIcon.transform.position = item.worker.transform.position + Vector3.up * 4;
			}
		}

		public override void Close()
		{
			base.Close();
			Destroy( route );
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

	public class Viewport : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IInputHandler
	{
		public bool mouseOver;
		public GameObject cursor;
		IInputHandler inputHandler;
		readonly GameObject[] cursorTypes = new GameObject[(int)CursorType.total];
		//readonly GameObject cursorFlag;
		//readonly GameObject cursorBuilding;
		public new Camera camera;

		public IInputHandler InputHandler
		{
			get { return inputHandler; }
			set
			{
				if ( inputHandler == value )
					return;
				inputHandler?.OnLostInput();
				inputHandler = value;
			}

		}

		public bool ResetInputHandler()
		{
			if ( inputHandler == this as IInputHandler )
				return false;
			InputHandler = this;
			return true;
		}

		public enum CursorType
		{
			nothing,
			remove,
			road,
			flag,
			building,
			total
		}

		public void SetCamera( Camera camera )
		{
			this.camera = camera;
			if ( camera )
				World.instance.eye.camera.enabled = false;
			else
			{
				camera = World.instance.eye.camera;
				camera.enabled = true;
			}
		}

		public GroundNode FindNodeAt( Vector3 screenPosition )
		{
			if ( camera == null )
				camera = World.instance.eye.camera;
			Ray ray = camera.ScreenPointToRay( screenPosition );
			if ( !World.instance.ground.collider.Raycast( ray, out RaycastHit hit, 1000 ) ) // TODO How long the ray should really be?
				return null;

			var ground = World.instance.ground;
			Vector3 localPosition = ground.transform.InverseTransformPoint( hit.point );
			return GroundNode.FromPosition( localPosition, ground );
		}

		public void OnPointerClick( PointerEventData eventData )
		{
			GroundNode node = FindNodeAt( Input.mousePosition );
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
			if ( inputHandler == null || inputHandler.Equals( null ) )
				inputHandler = this;
			if ( !mouseOver )
				return;
			GroundNode node = FindNodeAt( Input.mousePosition );
			if ( cursor && node )
				cursor.transform.localPosition = node.Position;
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
			if ( node.road && node.road.ready )
			{
				var worker = node.road.workerAtNodes[node.road.NodeIndex( node )];
				if ( worker && worker.type == Worker.Type.cart )
					worker.OnClicked();
				else
					node.road.OnClicked( node );
				return true;
			}
			node.OnClicked();
			return true;
		}

		public void OnLostInput()
		{
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
			if ( base.Open() )
				return;
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
				Text( 230, row, 50, 20, ( item.life.Age / 50 ).ToString(), scroll.content );
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
			if ( itemA.life.Age == itemB.life.Age )
				return 0;
			if ( itemA.life.Age < itemB.life.Age )
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
		readonly Text[] inStock = new Text[(int)Item.Type.total];
		readonly Text[] onWay = new Text[(int)Item.Type.total];
		readonly Text[] surplus = new Text[(int)Item.Type.total];
		readonly Text[] production = new Text[(int)Item.Type.total];
		readonly Text[] efficiency = new Text[(int)Item.Type.total];
		readonly Button[] stockButtons = new Button[(int)Item.Type.total];

		public static ItemStats Create()
		{
			return new GameObject().AddComponent<ItemStats>();
		}

		public void Open( Player player )
		{
			if ( base.Open() )
				return;

			name = "Item stats panel";
			this.player = player;
			Frame( 0, 0, 370, 300 );
			Text( 70, -20, 50, 20, "In stock" ).fontSize = 10;
			Text( 120, -20, 50, 20, "On Road" ).fontSize = 10;
			Text( 170, -20, 50, 20, "Surplus" ).fontSize = 10;
			Text( 220, -20, 50, 20, "Per minute" ).fontSize = 10;
			Text( 270, -20, 50, 20, "Efficiency" ).fontSize = 10;
			scroll = ScrollRect( 20, -45, 330, 205 );
			Button( 340, -10, 20, 20, iconTable.GetMediaData( Icon.exit ) ).onClick.AddListener( Close );
			finalEfficiency = Text( 100, -260, 100, 30 );
			finalEfficiency.fontSize = 16;

			for ( int i = 0; i < inStock.Length; i++ )
			{
				int row = i * - ( iconSize + 5 );
				ItemIcon( 0, row, 0, 0, (Item.Type)i, scroll.content );
				inStock[i] = Text( 30, row, 40, iconSize, "0", scroll.content );
				stockButtons[i] = inStock[i].gameObject.AddComponent<Button>();
				onWay[i] = Text( 80, row, 40, iconSize, "0", scroll.content );
				surplus[i] = Text( 130, row, 40, iconSize, "0", scroll.content );
				production[i] = Text( 180, row, 40, iconSize, "0", scroll.content );
				efficiency[i] = Text( 230, row, 40, iconSize, "0", scroll.content );
			}

			( scroll.content.transform as RectTransform).sizeDelta = new Vector2( 280, (int)Item.Type.total * ( iconSize + 5 ) );
		}

		new void Update()
		{
			base.Update();
			int[] inStockCount = new int[(int)Item.Type.total];
			int[] maxStockCount = new int[(int)Item.Type.total];
			Stock[] richestStock = new Stock[(int)Item.Type.total];
			foreach ( var stock in player.stocks )
			{
				for ( int i = 0; i < inStock.Length; i++ )
				{
					if ( stock.content[i] > maxStockCount[i] )
					{
						maxStockCount[i] = stock.content[i];
						richestStock[i] = stock;
					}
					inStockCount[i] += stock.content[i];
				}
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
				if ( player.itemEfficiencyHistory[i].factor != 0 )
					textColor = Color.green;
				if ( (int)player.worseItemType == i )
					textColor = Color.red;
				inStock[i].color = onWay[i].color = production[i].color = efficiency[i].color = textColor;

				inStock[i].text = inStockCount[i].ToString();
				stockButtons[i].onClick.RemoveAllListeners();
				Stock stock = richestStock[i];
				stockButtons[i].onClick.AddListener( delegate { SelectBuilding( stock ); } );
				onWay[i].text = onWayCount[i].ToString();
				surplus[i].text = player.surplus[i].ToString();
				production[i].text = player.itemEfficiencyHistory[i].production.ToString( "n2" );
				float itemEfficiency = player.itemEfficiencyHistory[i].weighted;
				efficiency[i].text = itemEfficiency.ToString( "n2" );
			};

			finalEfficiency.text = player.averageEfficiencyHistory.current.ToString( "n2" );
		}
	}

	public class History : Panel
	{
		Item.Type selected;
		Player player;
		float lastAverageEfficiency;
		Image chart, itemFrame;
		Text record;

		public static History Create()
		{
			return new GameObject().AddComponent<History>();
		}

		public void Open( Player player )
		{
			this.player = player;

			if ( base.Open() )
				return;

			name = "History panel";
			Frame( 0, 0, 450, 300 );
			Button( 420, -10, 20, 20, iconTable.GetMediaData( Icon.exit ) ).onClick.AddListener( Close );
			for ( int i = 0; i < (int)Item.Type.total; i++ )
			{
				var t = (Item.Type)i;
				var j = ItemIcon( 20 + i * iconSize, -20, 0, 0, (Item.Type)i );
				j.GetComponent<Button>().onClick.AddListener( delegate { selected = t; lastAverageEfficiency = -1; } );
			}
			Button( 400, -20, 20, 20, iconTable.GetMediaData( Icon.summa ) ).onClick.AddListener( delegate { selected = Item.Type.total; lastAverageEfficiency = -1; } );
			itemFrame = Image( 17, -17, 26, 26, iconTable.GetMediaData( Icon.tinyFrame ) );
			chart = Image( 20, -40, 410, 240 );
			record = Text( 25, -45, 150, 20 );
			selected = Item.Type.total;
			lastAverageEfficiency = -1;
		}

		new public void Update()
		{
			base.Update();
			if ( lastAverageEfficiency == player.averageEfficiencyHistory.current )
				return;

			var t = new Texture2D( 400, 260 );
			var a = player.averageEfficiencyHistory;
			if ( selected < Item.Type.total )
				a = player.itemEfficiencyHistory[(int)selected];
			int row = -1;
			for ( int x = t.width - 1; x >= 0; x-- )
			{
				int index = a.data.Count - t.width + x;
				int recordRow = (int)(100 * a.record);
				for ( int y = 0; y < t.height; y++ )
					t.SetPixel( x, y, index == a.recordIndex ? Color.yellow : y == recordRow ? Color.grey : Color.black );
				if ( 0 <= index )
				{
					int newRow = (int)Math.Min( (float)t.height - 1, 100 * a.data[index] );
					if ( row < 0 )
						row = newRow;
					var step = row < newRow ? 1 : -1;
					if ( row == newRow )
						step = 0;
					while ( row != newRow )
					{
						t.SetPixel( x, row, new Color( 0, 128, 255 ) );
						row += step;
					}
				}
			}
			t.Apply();
			chart.sprite = Sprite.Create( t, new Rect( 0, 0, t.width, t.height ), Vector2.zero );
			Assert.global.IsNotNull( chart.sprite );
			record.text = "Record: " + a.record;
			itemFrame.rectTransform.anchoredPosition = new Vector2( 17 + iconSize * (int)selected, -17 );
			lastAverageEfficiency = player.averageEfficiencyHistory.current;
		}
	}

	public interface IInputHandler
	{
		bool OnMovingOverNode( GroundNode node );
		bool OnNodeClicked( GroundNode node );
		void OnLostInput();
	}
}

