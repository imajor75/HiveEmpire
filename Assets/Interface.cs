using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Profiling;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.UI;

public class Interface : HiveObject
{
	public List<Panel> panels = new List<Panel>();
	public PostProcessResources postProcessResources;
	public const int iconSize = 20;
	public static float uiScale = 1.5f;
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
	public int fullValidate = fullValidateInterval;
	const int fullValidateInterval = 500;
	public HighlightType highlightType;
	public Ground.Area highlightArea;
	public GameObject highlightVolume;
	GroundNode highlightVolumeCenter;
	int highlightVolumeRadius;
	static Material highlightMaterial;
	public GameObject highlightOwner;
	public static Material materialUIPath;
	static bool focusOnInputField;

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
		newRoad,
		magnet,
		rightArrow,
		crosshair,
		summa,
		tinyFrame,
		reset,
		sleeping,
		clock,
		alarm,
		shovel,
		crossing
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

	public void OnApplicationQuit()
	{
		if ( !Assert.error )
			Save();

		foreach ( var item in Resources.FindObjectsOfTypeAll<Item>() )
			item.destination = null;    // HACK to silence the assert in Item.OnDestroy
	}

	public void LateUpdate()
	{
		Validate( true );
	}

	public static bool GetKey( KeyCode key )
	{
		if ( focusOnInputField )
			return false;

		return Input.GetKey( key );
	}

	public static bool GetKeyDown( KeyCode key )
	{
		if ( focusOnInputField )
			return false;

		return Input.GetKeyDown( key );
	}

	public void FixedUpdate()
	{
		if ( EventSystem.current.currentSelectedGameObject != null && EventSystem.current.currentSelectedGameObject.GetComponent<InputField>() != null )
			focusOnInputField = true;
		else
			focusOnInputField = false;

#if DEBUG
		if ( --fullValidate < 0 )
		{
			ValidateAll();
			fullValidate = fullValidateInterval;
		}
#endif

		if ( --autoSave < 0 )
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

	public void Start()
	{
		GroundNode.Initialize();
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
		Viewport.Initialize();

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

		Main.Create().Open( true );
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
		if ( world.players.Count > 0 )
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

	public void Update()
	{
		if ( world.timeFactor != 0 && world.timeFactor != 8 )
		{
			if ( GetKey( KeyCode.Space ) )
				world.SetTimeFactor( 5 );
			else
				world.SetTimeFactor( 1 );
		}
		if ( GetKey( KeyCode.R ) )
		{
			bool localReset = false;
			foreach ( var panel in panels )
			{
				if ( panel.target )
				{
					panel.target?.Reset();
					localReset = true;
				}
			}
			if ( !localReset )
				world.Reset();
		}
		if ( GetKeyDown( KeyCode.Insert ) )
			world.SetTimeFactor( 8 );
		if ( GetKeyDown( KeyCode.Delete ) )
			world.SetTimeFactor( 1 );
		if ( GetKeyDown( KeyCode.Pause ) )
		{
			if ( world.timeFactor > 0 )
				world.SetTimeFactor( 0 );
			else
				world.SetTimeFactor( 1 );
		}
		if ( GetKeyDown( KeyCode.H ) )
		{
			History.Create().Open( mainPlayer );
		}
		if ( GetKeyDown( KeyCode.I ) )
		{
			ItemList.Create().Open( mainPlayer );
		}
		if ( GetKeyDown( KeyCode.J ) )
		{
			ItemStats.Create().Open( mainPlayer );
		}
		if ( GetKeyDown( KeyCode.K ) )
		{
			ResourceList.Create().Open();
		}
		if ( GetKeyDown( KeyCode.Escape ) )
		{
			if ( !viewport.ResetInputHandler() )
			{
				bool closedSomething = false;
				bool isMainOpen = false;
				for ( int i = panels.Count - 1; i >= 0; i-- )
				{
					if ( panels[i] as Main )
						isMainOpen = true;
					if ( !panels[i].escCloses )
						continue;
					panels[panels.Count - 1].Close();
					closedSomething = true;
					break;
				}
				if ( !closedSomething && !isMainOpen )
					Main.Create().Open();
			}
		}
		if ( GetKeyDown( KeyCode.M ) )
			Map.Create().Open( GetKey( KeyCode.LeftShift ) || GetKey( KeyCode.RightShift ) );
		if ( GetKeyDown( KeyCode.Alpha9 ) )
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

		highlightVolume.transform.localPosition = highlightVolumeCenter.position;
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
				Vector3 start = road.nodes[i].position + Vector3.up * 0.1f;
				Vector3 end = road.nodes[i + 1].position + Vector3.up * 0.1f;
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

	public static void ValidateAll()
	{
		foreach ( var ho in Resources.FindObjectsOfTypeAll<HiveObject>() )
			ho.Validate( false );
	}

	public override void Validate( bool chain )
	{
#if DEBUG
		Profiler.BeginSample( "Validate" );
		if ( chain )
			world.Validate( true );
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

	public override GroundNode Node { get { return null; } }

	public class Tooltip : Panel
	{
		GameObject objectToShow;
		Component origin;
		Text text, additionalText;
		Image image, backGround;

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

			backGround = Frame( 0, 0, 200, 40, 3 );
			text = Text( 15, -10, 270, 60 );
			additionalText = Text( 20, -30, 150, 60 );
			additionalText.fontSize = (int)( 10 * uiScale );
			image = Image( 20, -20, 100, 100 );
			gameObject.SetActive( false );
			FollowMouse();
		}

		public void SetText( Component origin, string text = "", Sprite imageToShow = null, GameObject objectToShow = null, string additionalText = "" )
		{
			this.origin = origin;
			this.text.text = text;
			this.additionalText.text = additionalText;
			if ( this.objectToShow != null && this.objectToShow != objectToShow )
				Destroy( this.objectToShow );
			this.objectToShow = objectToShow;
			if ( imageToShow )
			{
				image.sprite = imageToShow;
				image.enabled = true;
				backGround.rectTransform.sizeDelta = new Vector2( (int)( uiScale * 200 ), (int)( uiScale * 140 ) );
			}
			else
			{
				image.enabled = false;
				if ( text.Length > 20 ) // TODO Big fat hack
					backGround.rectTransform.sizeDelta = new Vector2( (int)(uiScale * 300 ), (int)( uiScale * 70 ) );
				else
					backGround.rectTransform.sizeDelta = new Vector2( (int)(uiScale * 200 ), (int)( uiScale * 40 ) );
			}
			gameObject.SetActive( true );
			FollowMouse();
		}

		public void Clear()
		{
			gameObject.SetActive( false );
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

	class TooltipSource : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
	{
		public string text;
		public string additionalText;
		public Sprite image;

		public void OnPointerEnter( PointerEventData eventData )
		{
			tooltip.SetText( this, text, image, null, additionalText );
		}

		public void OnPointerExit( PointerEventData eventData )
		{
			tooltip.Clear();
		}

		public static TooltipSource AddTo( Component widget, string text, Sprite image = null, string additionalText = "" )
		{
			var s = widget.gameObject.AddComponent<TooltipSource>();
			s.text = text;
			s.image = image;
			s.additionalText = additionalText;
			return s;
		}
	}

	public class Panel : MonoBehaviour, IDragHandler, IPointerClickHandler
	{
		public HiveObject target;
		public bool followTarget = true;
		public Image frame;
		public Interface cachedRoot;
		public bool escCloses = true;
		public bool disableDrag;
		public Vector2 offset;

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
		public bool Open( HiveObject target = null, int x = 0, int y = 0, int xs = 100, int ys = 100 )
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
				x = (int)( Screen.width - xs * uiScale ) / 2;
				y = -(int)( Screen.height - ys * uiScale ) / 2;
			}
			Root.panels.Add( this );
			name = "Panel";
			frame = gameObject.AddComponent<Image>();
			Init( frame.rectTransform, (int)( x / uiScale ), (int)( y / uiScale ), 100, 100, Root );
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

		public Image Frame( int x, int y, int xs, int ys, float pixelsPerUnitMultiplier = 1.5f, Component parent = null )
		{
			Image i = Image( x, y, xs, ys, iconTable.GetMediaData( Icon.frame ) );
			i.type = UnityEngine.UI.Image.Type.Sliced;
			i.pixelsPerUnitMultiplier = pixelsPerUnitMultiplier / uiScale; 
			return i;

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
				t.offsetMin = new Vector2( (int)( -20 * uiScale ) , 0 );
				t.offsetMax = new Vector2( 0, vertical ? (int)( -20 * uiScale ) : 0 );
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
				t.offsetMin = new Vector2( (int)(-20 * uiScale ), 0 );
				t.offsetMax = new Vector2( 0, horizontal ? (int)( -20 * uiScale ) : 0 );
			}
			var content = new GameObject().AddComponent<Image>();
			content.name = "Content";
			content.transform.SetParent( scroll.transform, false );
			scroll.content = content.rectTransform;
			content.enabled = false;
			content.rectTransform.anchorMin = Vector2.zero;
			content.rectTransform.anchorMax = new Vector2( 1, 0 );
			content.rectTransform.offsetMax = new Vector2( horizontal ? (int)( uiScale * 20 ) : 0, vertical ? (int)( uiScale * 20 ) : 0 );

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
				WorkshopPanel.Create().Open( workshop, WorkshopPanel.Content.everything, true );
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
			const int border = 4;
			Image i = Image( x, y, xs, ys, null, parent );
			i.sprite = Resources.Load<Sprite>( "simple UI & icons/button/button_round" );
			i.type = UnityEngine.UI.Image.Type.Sliced;
			i.pixelsPerUnitMultiplier = 6;
			var t = Text( border, -border, xs - 2 * border, ys - 2 * border, text, i );
			t.color = Color.black;
			t.alignment = TextAnchor.MiddleCenter;
			t.resizeTextForBestFit = true;
			return i.gameObject.AddComponent<Button>();
		}

		public Text Text( int x, int y, int xs, int ys, string text = "", Component parent = null )
		{
			Text t = new GameObject().AddComponent<Text>();
			t.name = "Text";
			Init( t.rectTransform, x, y, xs, ys, parent );
			t.font = Interface.font;
			t.fontSize = (int)( t.fontSize * uiScale );
			t.text = text;
			t.color = Color.yellow;
			return t;
		}

		public InputField InputField( int x, int y, int xs, int ys, string text = "", Component parent = null )
		{
			var o = Instantiate( Resources.Load<GameObject>( "InputField" ) );
			var i = o.GetComponent<InputField>();
			var image = i.GetComponent<Image>();
			Init( image.rectTransform, x, y, xs, ys, parent );
			i.name = "InputField";
			i.text = text;
			return i;
		}

		public Dropdown Dropdown( int x, int y, int xs, int ys, Component parent = null )
		{
			var o = Instantiate( Resources.Load<GameObject>( "Dropdown" ) );
			var d = o.GetComponent<Dropdown>();
			var image = d.GetComponent<Image>();
			Init( image.rectTransform, x, y, xs, ys, parent );
			d.name = "InputField";
			return d;
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
			t.anchoredPosition = new Vector2( x * uiScale, y * uiScale );
			if ( xs != 0 && ys != 0 )
				t.sizeDelta = new Vector2( xs * uiScale, ys * uiScale );
		}

		public virtual void Update()
		{
			UpdatePosition();
		}

		void UpdatePosition()
		{
			if ( target == null || !followTarget )
				return;

			MoveTo( target.Node.position + Vector3.up * GroundNode.size );
		}

		public void MoveTo( Vector3 position )
		{
			Vector3 screenPosition = root.viewport.camera.WorldToScreenPoint( position );
			screenPosition.x += offset.x;
			screenPosition.y += offset.y;
			if ( screenPosition.y > Screen.height )
				screenPosition = World.instance.eye.camera.WorldToScreenPoint( target.Node.position - Vector3.up * GroundNode.size );
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
				if ( GetKey( KeyCode.LeftShift ) || GetKey( KeyCode.RightShift ) )
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

			public void Update()
			{
				image.color = area.center != null ? Color.green : Color.grey;
				if ( GetKeyDown( KeyCode.Comma ) )
				{
					if ( area.radius > 1 )
						area.radius--;
				}
				if ( GetKeyDown( KeyCode.Period ) )
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

			public bool OnObjectClicked( HiveObject target )
			{
				return OnNodeClicked( target.Node );
			}
		}

		public class ItemImage : Image, IPointerEnterHandler, IPointerExitHandler
		{
			public Item item;
			public Item.Type itemType = Item.Type.unknown;
			public string additionalTooltip;
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

			public new void OnDestroy()
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
					tooltip.SetText( this, item.type.ToString(), Item.sprites[(int)item.type], path = CreateUIPath( item.path ), additionalTooltip );
				else
					tooltip.SetText( this, itemType.ToString(), Item.sprites[(int)itemType], null, additionalTooltip );
			}

			public void OnPointerExit( PointerEventData eventData )
			{
				tooltip.Clear();
			}
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
		public Text resourcesLeft;

		public List<Buffer> buffers;
		public Buffer outputs;

		public static WorkshopPanel Create()
		{
			return new GameObject().AddComponent<WorkshopPanel>();
		}

		public enum Content
		{
			name = 1,
			buffers = 2,
			output = 4,
			progress = 8,
			resourcesLeft = 16,
			controlIcons = 32,
			itemsProduced = 64,
			everything = -1
		}

		public void Open( Workshop workshop, Content contentToShow = Content.everything, bool show = false )
		{
			if ( base.Open( workshop ) )
				return;

			name = "Workshop panel";
			this.workshop = workshop;
			bool showOutputBuffer = false, showProgressBar = false;
			if ( workshop.productionConfiguration.outputType != Item.Type.unknown || workshop.type == Workshop.Type.forester )
			{
				showProgressBar = true;
				showOutputBuffer = workshop.productionConfiguration.outputType != Item.Type.unknown;
			}

			if ( ( contentToShow & Content.progress ) == 0 )
				showProgressBar = false;
			if ( ( contentToShow & Content.output ) == 0 )
				showOutputBuffer = false;

			int displayedBufferCount = workshop.buffers.Count + ( showOutputBuffer ? 1 : 0 );
			var backGround = Frame( 0, 0, 240, 100 );
			Button( 210, -10, 20, 20, iconTable.GetMediaData( Icon.exit ) ).onClick.AddListener( Close );

			int row = -20;
			if ( ( contentToShow & Content.name ) > 0 )
			{
				Text( 20, row, 160, 20, workshop.type.ToString() );
				row -= 20;
			}

			if ( ( contentToShow & Content.buffers ) > 0 )
			{
				int col = 20;
				buffers = new List<Buffer>();
				foreach ( var b in workshop.buffers )
				{
					var bui = new Buffer();
					bui.Setup( this, b, col, row, iconSize + 5 );
					buffers.Add( bui );
					row -= iconSize * 3 / 2;
				}
			}

			if ( showProgressBar )
			{
				if ( showOutputBuffer )
				{
					outputs = new Buffer();
					outputs.Setup( this, workshop.productionConfiguration.outputType, workshop.productionConfiguration.outputMax, 20, row, iconSize + 5, workshop.outputArea );
					row -= iconSize * 3 / 2;
				}
				progressBar = Image( 20, row, ( iconSize + 5 ) * 7, iconSize, iconTable.GetMediaData( Icon.progress ) );
				row -= 25;

				if ( ( contentToShow & Content.itemsProduced ) > 0 )
				{
					itemsProduced = Text( 20, row, 200, 20 );
					productivity = Text( 180, -20, 30, 20 );
					row -= 25;
				}
			}
			if ( workshop.gatherer && ( contentToShow & Content.resourcesLeft ) > 0 )
			{
				resourcesLeft = Text( 20, row, 150, 20, "Resources left: 0" );
				row -= 25;
			}

			if ( ( contentToShow & Content.controlIcons ) != 0 )
			{
				Button( 190, row, 20, 20, iconTable.GetMediaData( Icon.destroy ) ).onClick.AddListener( Remove );
				Button( 170, row, 20, 20, iconTable.GetMediaData( Icon.hauler ) ).onClick.AddListener( ShowWorker );
				var changeModeButton = Button( 150, row, 20, 20, GetModeIcon() );
				changeModeButton.onClick.AddListener( ChangeMode );
				changeModeImage = changeModeButton.gameObject.GetComponent<Image>();
			}

			backGround.rectTransform.sizeDelta = new Vector2( (int)(uiScale * 240 ), (int)( uiScale * ( -row + 20 ) ) );

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

			workshop.assert.Fail();
			return null;
		}

		public override void Update()
		{
			base.Update();
			if ( buffers != null )
				foreach ( var buffer in buffers )
					buffer.Update();

			outputs?.Update( workshop.output, 0 );

			if ( progressBar )
			{
				if ( workshop.working )
				{
					progressBar.rectTransform.sizeDelta = new Vector2( iconSize * 7 * workshop.GetProgress(), iconSize );
					progressBar.color = Color.white;
				}
				else
				{
					if ( workshop.gatherer && !workshop.working )
						progressBar.color = Color.green;
					else
						progressBar.color = Color.red;
				}
				if ( productivity )
					productivity.text = ( (int)( workshop.productivity.current * 100 ) ).ToString() + "%";
				if ( itemsProduced )
					itemsProduced.text = "Items produced: " + workshop.itemsProduced;
			}
			if ( resourcesLeft )
			{
				int left = 0;
				void CheckNode( GroundNode node )
				{
					var resource = node.resource;
					if ( resource == null || resource.type != workshop.productionConfiguration.gatheredResource )
						return;
					if ( !resource.underGround || node == workshop.node || resource.exposed.inProgress )
						left++;
				}
				CheckNode( workshop.node );
				foreach ( var o in Ground.areas[workshop.productionConfiguration.gatheringRange] )
					CheckNode( workshop.node + o );
				resourcesLeft.text = "Resources left: " + left;
			}
			if ( changeModeImage )
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
				boss.Text( x, y, 20, 20, "?" ).gameObject.AddComponent<Button>().onClick.AddListener( delegate { PotentialList.Create().Open( boss.building, itemType ); } );
				if ( area != null )
					boss.AreaIcon( x + 15, y, area );
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

	public class GuardHousePanel : BuildingPanel
	{
		public GuardHouse guardHouse;

		public static GuardHousePanel Create()
		{
			return new GameObject().AddComponent<GuardHousePanel>();
		}

		public void Open( GuardHouse guardHouse, bool show = false )
		{
			this.guardHouse = guardHouse;
			if ( base.Open( guardHouse ) )
				return;
			name = "Guard House panel";
			Frame( 0, 0, 300, 200 );
			Button( 270, -10, 20, 20, iconTable.GetMediaData( Icon.exit ) ).onClick.AddListener( Close );
			Button( 270, -160, 20, 20, iconTable.GetMediaData( Icon.destroy ) ).onClick.AddListener( Remove );
			if ( show )
				Root.world.eye.FocusOn( guardHouse );
		}
		void Remove()
		{
			if ( guardHouse && guardHouse.Remove( false ) )
				Close();
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
			if ( GetKey( KeyCode.LeftAlt ) && GetKey( KeyCode.LeftControl ) )
			{
				stock.content[(int)itemType] = 0;
				return;
			}
			if ( GetKey( KeyCode.LeftShift ) && GetKey( KeyCode.LeftControl ) )
			{
				stock.content[(int)itemType]++;
				return;
			}
			if ( GetKey( KeyCode.LeftShift ) )
			{
				PotentialList.Create().Open( stock, itemType );
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
			total.fontSize = (int)( 16 * uiScale );

			int row = -55;
			for ( int j = 0; j < (int)Item.Type.total; j++ )
			{
				int offset = j % 2 > 0 ? 140 : 0;
				var t = (Item.Type)j;
				var i = ItemIcon( 20 + offset, row, iconSize, iconSize, (Item.Type)j );
				i.additionalTooltip = "Shift+LMB Show potentials\nShift+Ctrl+LMB Add one more\nAlt+Ctrl+LMB Clear";
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
			selected.additionalTooltip = "LMB Set cart target\nShift+LMB Show current target\nCtrl+LMB Clear target";
			selected.GetComponent<Button>().onClick.RemoveAllListeners();
			selected.GetComponent<Button>().onClick.AddListener( SetTarget );
			inputMin = Text( ipx - 40, ipy, 40, 20 );
			TooltipSource.AddTo( inputMin, "If this number is higher than the current content, the stock will request new items at high priority" );
			inputMax = Text( ipx + 50, ipy, 40, 20 );
			TooltipSource.AddTo( inputMax, "If the stock has at least this many items, it will no longer accept surplus" );
			outputMin = Text( ipx - 40, ipy - 20, 40, 20 );
			TooltipSource.AddTo( outputMin, "The stock will only supply other buildings with the item if it has at least this many" );
			outputMax = Text( ipx + 50, ipy - 20, 40, 20 );
			TooltipSource.AddTo( outputMax, "If the stock has more items than this number, then it will send the surplus even to other stocks" );
		}

		void SetTarget()
		{
			if ( GetKey( KeyCode.LeftShift ) || GetKey( KeyCode.RightShift ) )
			{
				SelectBuilding( stock.destinations[(int)selectedItemType] );
				return;
			}
			if ( GetKey( KeyCode.LeftControl ) || GetKey( KeyCode.RightControl ) )
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

			if ( GetKeyDown( KeyCode.Mouse0 ) )
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
				if ( GetKey( KeyCode.Mouse0 ) )
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
			return false;	// Cancel?
		}

		public bool OnObjectClicked( HiveObject target )
		{
			var destination = target as Stock;
			if ( destination == null )
				return true;

			stock.destinations[(int)itemTypeForRetarget] = destination;
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

		public void Open( GroundNode node, bool show = false )
		{
			base.Open( node );
			this.node = node;
			name = "Node panel";

			Frame( 0, 0, 380, 180 );
			Button( 350, -20, 20, 20, iconTable.GetMediaData( Icon.exit ) ).onClick.AddListener( Close );

#if DEBUG
			BuildButton( 20, -60, "Tree", !node.IsBlocking( true ) && node.CheckType( GroundNode.Type.land ), AddTree );
			BuildButton( 20, -80, "Remove", node.IsBlocking( true ), Remove );
			BuildButton( 20, -100, "Raise", true, delegate { AlignHeight( 0.1f ); } );
			BuildButton( 20, -120, "Lower", true, delegate { AlignHeight( -0.1f ); } );
			BuildButton( 20, -140, "Cave", !node.IsBlocking( true ), AddCave );

			BuildButton( 200, -60, "Gold patch", true, delegate { AddResourcePatch( Resource.Type.gold ); } );
			BuildButton( 200, -80, "Coal patch", true, delegate { AddResourcePatch( Resource.Type.coal ); } );
			BuildButton( 200, -100, "Iron patch", true, delegate { AddResourcePatch( Resource.Type.iron ); } );
			BuildButton( 200, -120, "Stone patch", true, delegate { AddResourcePatch( Resource.Type.stone ); } );
			BuildButton( 200, -140, "Salt patch", true, delegate { AddResourcePatch( Resource.Type.salt ); } );
#endif
			if ( node.resource && ( !node.resource.underGround || !node.resource.exposed.done ) )
				Text( 20, -40, 160, 20, "Resource: " + node.resource.type );
			if ( show )
				root.world.eye.FocusOn( node );
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

		void AddTree()
		{
			Resource.Create().Setup( node, Resource.Type.tree )?.life.Start( -2 * Resource.treeGrowthMax );
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
	}

	public class BuildPanel : Panel
	{
		public int showID;
		Workshop.Type showType;
		public static BuildPanel Create()
		{
			return new GameObject().AddComponent<BuildPanel>();
		}

		public void Open()
		{
			base.Open();
			name = "Build panel";

			Frame( 0, 0, 360, 320 );
			Button( 330, -20, 20, 20, iconTable.GetMediaData( Icon.exit ) ).onClick.AddListener( Close );

			int row = -20;
			var workshops = FindObjectsOfType<Workshop>( true );

			for ( int i = 0; i < (int)Workshop.Type.total; i++ )
			{
				var type = (Workshop.Type)i;
				int c = 0;
				foreach ( var workshop in workshops )
					if ( workshop.type == type && workshop.owner == root.mainPlayer )
						c++;
				BuildButton( i % 2 == 0 ? 20 : 180, row, $"{type} ({c})", delegate { BuildWorkshop( type ); } );
				if ( i % 2 != 0 )
					row -= 20;
			}
			BuildButton( 20, -260, "Flag", AddFlag );
			BuildButton( 180, -260, "Crossing", AddCrossing );

			BuildButton( 20, -280, "Guardhouse", AddGuardHouse );
			BuildButton( 180, -280, "Stock", AddStock );
		}

		void BuildButton( int x, int y, string title, UnityEngine.Events.UnityAction action )
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
			root.viewport.constructionMode = Viewport.Construct.flag;
			Root.viewport.showPossibleBuildings = true;
			Close();
		}

		void AddCrossing()
		{
			root.viewport.constructionMode = Viewport.Construct.crossing;
			Root.viewport.showPossibleBuildings = true;
			Close();
		}

		void AddStock()
		{
			root.viewport.constructionMode = Viewport.Construct.stock;
			Root.viewport.showPossibleBuildings = true;
			Close();
		}

		void AddGuardHouse()
		{
			root.viewport.constructionMode = Viewport.Construct.guardHouse;
			Root.viewport.showPossibleBuildings = true;
			Close();
		}

		public void BuildWorkshop( Workshop.Type type )
		{
			if ( GetKey( KeyCode.LeftShift ) )
			{
				if ( type != showType )
					showID = 0;
				var workshops = FindObjectsOfType<Workshop>( true );
				for ( int i = showID; i < workshops.Length; i++ )
				{
					if ( workshops[i].type == type && workshops[i].owner == root.mainPlayer )
					{
						WorkshopPanel.Create().Open( workshops[i], WorkshopPanel.Content.everything, true );
						showType = type;
						showID = i + 1;
						return;
					}
				}
				showID = 0;
				return;
			}
			root.viewport.constructionMode = Viewport.Construct.workshop;
			Root.viewport.workshopType = type;
			Root.viewport.showPossibleBuildings = true;
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
			base.Open( road );
			this.road = road;
			this.node = node;
			Frame( 0, 0, 210, 165, 3 );
			Button( 190, 0, 20, 20, iconTable.GetMediaData( Icon.exit ) ).onClick.AddListener( Close );
			Button( 170, -10, 20, 20, iconTable.GetMediaData( Icon.hauler ) ).onClick.AddListener( Hauler );
			Button( 150, -10, 20, 20, iconTable.GetMediaData( Icon.destroy ) ).onClick.AddListener( Remove );
			Button( 130, -10, 20, 20, iconTable.GetMediaData( Icon.box ) ).onClick.AddListener( Split );
			jam = Text( 12, -4, 120, 20, "Jam" );
			workers = Text( 12, -28, 120, 20, "Worker count" );
			name = "Road panel";
			var t = Dropdown( 20, -44, 150, 25 );
			t.ClearOptions();
			t.AddOptions( new List<string> { "Auto", "1", "2", "3", "4" } );
			t.value = road.targetWorkerCount;
			t.onValueChanged.AddListener( TargetWorkerCountChanged );

			for ( int i = 0; i < itemsDisplayed; i++ )
			{
				int row = i * (iconSize + 5 ) - 128;
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
			if ( GetKey( KeyCode.LeftShift ) )
				road.CallNewWorker();
			else
				WorkerPanel.Create().Open( road.workers[0], true ); // TODO Make it possibe to view additional workers
		}

		void Split()
		{
			if ( Flag.Create().Setup( node, node.owner ) != null )
				Close();
			ValidateAll();
		}

		void TargetWorkerCountChanged( int newValue )
		{
			if ( road )
				road.targetWorkerCount = newValue;
		}

		public override void Update()
		{
			base.Update();
			jam.text = "Items waiting: " + road.Jam;
			workers.text = "Worker count: " + road.workers.Count;

			bool reversed = false;
			var camera = World.instance.eye.camera;
			float x0 = camera.WorldToScreenPoint( road.nodes[0].position ).x;
			float x1 = camera.WorldToScreenPoint( road.lastNode.position ).x;
			if ( x1 < x0 )
				reversed = true;

			for ( int j = 0; j < itemsDisplayed; j++ )
			{
				int i = itemsDisplayed - 1 - j;
				Worker worker;
				if ( j < road.workers.Count && (worker = road.workers[j]) && worker.taskQueue.Count > 0 )
				{
					Item item = worker.itemsInHands[0];	// TODO show the second item somehow	
					centerItems[i].SetItem( item );
					if ( item )
					{
						Flag flag = item.nextFlag;
						if ( flag == null )
							flag = item.destination.flag;
						if ( flag == road.ends[reversed ? 1 : 0] )
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
				var flag = road.ends[i];
				var items = flag.items;
				foreach ( var item in items )
				{
					if ( item != null && item.road == road && item.flag == flag )
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
		public Image shovelingIcon, convertIcon;

		public static FlagPanel Create()
		{
			return new GameObject().AddComponent<FlagPanel>();
		}

		public void Open( Flag flag, bool show = false )
		{
#if DEBUG
			Selection.activeGameObject = flag.gameObject;
#endif
			if ( base.Open( flag ) )
				return;

			this.flag = flag;
			int col = 16;
			Frame( 0, 0, 250, 75, 3 );
			Button( 230, 0, 20, 20, iconTable.GetMediaData( Icon.exit ) ).onClick.AddListener( Close );
			Button( 210, -45, 20, 20, iconTable.GetMediaData( Icon.destroy ) ).onClick.AddListener( Remove );
			Button( 20, -45, 20, 20, iconTable.GetMediaData( Icon.newRoad ) ).onClick.AddListener( StartRoad );
			Button( 45, -45, 20, 20, iconTable.GetMediaData( Icon.magnet ) ).onClick.AddListener( CaptureRoads );
			var shovelingButton = Button( 65, -45, 20, 20, iconTable.GetMediaData( Icon.shovel ) );
			shovelingButton.onClick.AddListener( Flatten );
			shovelingIcon = shovelingButton.GetComponent<Image>();
			var convertButton = Button( 85, -45, 20, 20, iconTable.GetMediaData( Icon.crossing ) );
			convertButton.onClick.AddListener( Convert );
			convertIcon = convertButton.GetComponent<Image>();

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
				Root.viewport.showGridAtMouse = true;
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
					if ( A.road.ends[0] == flag || A.road.ends[1] == flag )
						continue;
					A.road.Split( flag );
					return;
				}
			}
		}

		void Convert()
		{
			if ( !flag.crossing )
				flag.ConvertToCrossing();
			else
				flag.ConvertToNormal();
		}

		void Flatten()
		{
			flag.requestFlattening = true;
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
						int timeAtFlag = flag.items[i].atFlag.age;
						itemTimers[i].rectTransform.sizeDelta = new Vector2( Math.Min( iconSize, timeAtFlag / 3000 ), 3 );
						itemTimers[i].color = Color.Lerp( Color.green, Color.red, timeAtFlag / 30000f );
					}
					else
						items[i].color = new Color( 1, 1, 1, 0.4f );
				}
			}

			if ( flag.flattening != null )	// This should never be null unless after loaded old files.
				shovelingIcon.color = flag.flattening.flattened ? Color.grey : Color.white;
			convertIcon.color = flag.crossing ? Color.red : Color.white;
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
				item.SetItem( worker.itemsInHands[0] );	// TODO Show the second item
			itemCount.text = "Items delivered: " + worker.itemsDelivered;
			if ( followTarget )
				MoveTo( worker.transform.position + Vector3.up * GroundNode.size );
		}

		public new void OnDestroy()
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
			planks.Setup( this, Item.Type.plank, construction.boss.configuration.plankNeeded, 20, -40, iconSize + 5 );
			stones = new WorkshopPanel.Buffer();
			stones.Setup( this, Item.Type.stone, construction.boss.configuration.stoneNeeded, 20, -64, iconSize + 5 );

			progressBar = Image( 20, -90, ( iconSize + 5 ) * 8, iconSize, iconTable.GetMediaData( Icon.progress ) );

			if ( show )
				Root.world.eye.FocusOn( workshop );
		}

		public new void Update()
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

			Frame( 0, 0, 300, 150, 1.5f );
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

			if ( item.flag )
				stats.text = "Age: " + item.life.age / 50 + " secs, at flag for " + item.atFlag.age / 50 + " secs";
			else
				stats.text = "Age: " + item.life.age / 50 + " secs";

			if ( item.destination && route == null )
			{
				route = CreateUIPath( item.path );
				route?.transform.SetParent( Root.transform );
			}
			if ( item.flag )
				mapIcon.transform.position = item.flag.node.position + Vector3.up * 4;
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

		public new void OnDestroy()
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
		public Vector3 lastMouseOnGround;
		static int gridMaskXID;
		static int gridMaskZID;
		public bool showGridAtMouse;
		public bool showPossibleBuildings;
		static readonly List<BuildPossibility> buildCategories = new List<BuildPossibility>();
		public HiveObject currentBlueprint;
		public WorkshopPanel currentBlueprintPanel;

		public enum Construct
		{
			nothing,
			workshop,
			stock,
			guardHouse,
			flag,
			crossing
		}
		public Construct constructionMode = Construct.nothing;
		public Workshop.Type workshopType;
		public int currentFlagDirection = 1;	// 1 is a legacy value.

		struct BuildPossibility
		{
			public Building.Configuration configuration;
			public Material material;
			public Mesh mesh;
			public float scale;
		}

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
			{
				if ( constructionMode != Construct.nothing )
				{
					constructionMode = Construct.nothing;
					CancelBlueprint();
					showPossibleBuildings = false;
					return true;
				}
				return false;
			}
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
			invisible,
			direction0,
			direction1,
			direction2,
			direction3,
			direction4,
			direction5,
			total
		}

		public void CancelBlueprint()
		{
			currentBlueprint?.Remove();
			currentBlueprint = null;
			currentBlueprintPanel?.Close();
			currentBlueprintPanel = null;
		}

		public static void Initialize()
		{
			gridMaskXID = Shader.PropertyToID( "_GridMaskX" );
			gridMaskZID = Shader.PropertyToID( "_GridMaskZ" );

			var greenMaterial = new Material( World.defaultShader )		{ color = new Color( 0.5f, 0.5f, 0.35f ) };
			var blueMaterial = new Material( World.defaultShader )		{ color = new Color( 0.3f, 0.45f, 0.6f ) };
			var yellowMaterial = new Material( World.defaultShader )	{ color = new Color( 177 / 255f, 146 / 255f, 97 / 255f ) };
			var orangeMaterial = new Material( World.defaultShader )	{ color = new Color( 191 / 255f, 134 / 255f, 91 / 255f ) };
			var greyMaterial = new Material( World.defaultShader )		{ color = Color.grey };
			buildCategories.Add( new BuildPossibility
			{
				configuration = Workshop.GetConfiguration( Workshop.Type.farm ),
				material = greenMaterial,
				mesh = Resources.Load<Mesh>( "meshes/groundSigns/bigHouse" ),
				scale = 1.5f
			} );
			buildCategories.Add( new BuildPossibility
			{
				configuration = Workshop.GetConfiguration( Workshop.Type.sawmill ),
				material = blueMaterial,
				mesh = Resources.Load<Mesh>( "meshes/groundSigns/mediumHouse" ),
				scale = 1.5f
			} );
			buildCategories.Add( new BuildPossibility
			{
				configuration = Workshop.GetConfiguration( Workshop.Type.woodcutter ),
				material = yellowMaterial,
				mesh = Resources.Load<Mesh>( "meshes/groundSigns/smallHouse" ),
				scale = 1.5f
			} );
			buildCategories.Add( new BuildPossibility
			{
				configuration = Workshop.GetConfiguration( Workshop.Type.ironmine ),
				material = orangeMaterial,
				mesh = Resources.Load<Mesh>( "meshes/groundSigns/mine" ),
				scale = 0.7f
			} );
			buildCategories.Add( new BuildPossibility
			{
				configuration = null,
				material = greyMaterial,
				mesh = Resources.Load<Mesh>( "meshes/groundSigns/flag" ),
				scale = 0.15f
			} );
			foreach ( var c in buildCategories )
				Assert.global.IsNotNull( c.mesh );
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

		public HiveObject FindObjectAt( Vector3 screenPosition )
		{
			if ( camera == null )
				camera = World.instance.eye.camera;
			Ray ray = camera.ScreenPointToRay( screenPosition );
			if ( !Physics.Raycast( ray, out RaycastHit hit, 1000, 1 << World.layerIndexPickable ) ) // TODO How long the ray should really be?
				return null;

			var hiveObject = hit.collider.GetComponent<HiveObject>();
			if ( hiveObject == null )
				hiveObject = hit.collider.transform.parent.GetComponent<HiveObject>();
			Assert.global.IsNotNull( hiveObject );

			var ground = World.instance.ground;

			var b = hiveObject as Building;
			if ( b && !b.construction.done )
				hiveObject = ground;

			if ( hiveObject == ground )
			{
				Vector3 localPosition = ground.transform.InverseTransformPoint( hit.point );
				return GroundNode.FromPosition( localPosition, ground );
			}
			
			return hiveObject;
		}

		public GroundNode FindNodeAt( Vector3 screenPosition )
		{
			var c = World.instance.ground.collider;
			if ( c == null )
				return null;

			if ( camera == null )
				camera = World.instance.eye.camera;
			Ray ray = camera.ScreenPointToRay( screenPosition );
			if ( !c.Raycast( ray, out RaycastHit hit, 1000 ) ) // TODO How long the ray should really be?
				return null;

			var ground = World.instance.ground;
			lastMouseOnGround = ground.transform.InverseTransformPoint( hit.point );

			if ( showGridAtMouse )
			{
				World.instance.ground.material.SetFloat( gridMaskXID, hit.point.x );
				World.instance.ground.material.SetFloat( gridMaskZID, hit.point.z );
			}
			else
			{
				World.instance.ground.material.SetFloat( gridMaskXID, 10000 );
				World.instance.ground.material.SetFloat( gridMaskZID, 10000 );
			}
			return GroundNode.FromPosition( lastMouseOnGround, ground );
		}

		public void OnPointerClick( PointerEventData eventData )
		{
			if ( currentBlueprint )
			{
				currentBlueprint.Materialize();
				currentBlueprint = null;
				currentBlueprintPanel?.Close();
				currentBlueprintPanel = null;
				constructionMode = Construct.nothing;
				showPossibleBuildings = false;
				return;
			}

			var hiveObject = FindObjectAt( Input.mousePosition );
			if ( hiveObject == null )
			{
				UnityEngine.Debug.Log( "Clicked on nothing?" );
				return;
			}
			var node = hiveObject as GroundNode;
			if ( node )
			{
				if ( !inputHandler.OnNodeClicked( node ) )
					inputHandler = this;
			}
			else
				if ( !inputHandler.OnObjectClicked( hiveObject ) )
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

		public void Start()
		{
			Image image = gameObject.AddComponent<Image>();
			image.rectTransform.anchorMin = Vector2.zero;
			image.rectTransform.anchorMax = Vector2.one;
			image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
			image.color = new Color( 1, 1, 1, 0 );

			inputHandler = this;
		}

		public void Update()
		{
			if ( GetKeyDown( KeyCode.Alpha1 ) )
				BuildPanel.Create().Open();
			if ( GetKeyDown( KeyCode.Alpha2 ) )
				showGridAtMouse = !showGridAtMouse;
			if ( GetKeyDown( KeyCode.Comma ) )
			{
				if ( currentFlagDirection == 0 )
					currentFlagDirection = 5;
				else
					currentFlagDirection--;
				CancelBlueprint();
			}
			if ( GetKeyDown( KeyCode.Period ) )
			{
				if ( currentFlagDirection == 5 )
					currentFlagDirection = 0;
				else
					currentFlagDirection++;
				CancelBlueprint();
			}

			if ( GetKeyDown( KeyCode.Alpha3 ) )
				showPossibleBuildings = !showPossibleBuildings;
			if ( inputHandler == null || inputHandler.Equals( null ) )
				inputHandler = this;
			if ( !mouseOver )
				return;
			GroundNode node = FindNodeAt( Input.mousePosition );
			if ( cursor && node )
				cursor.transform.localPosition = node.position;
			if ( !inputHandler.OnMovingOverNode( node ) )
				inputHandler = this;
#if DEBUG
			if ( GetKeyDown( KeyCode.PageUp ) && node )
				node.SetHeight( node.height + 0.05f );
			if ( GetKeyDown( KeyCode.PageDown ) && node )
				node.SetHeight( node.height - 0.05f );
#endif
			if ( showPossibleBuildings && node )
			{
				foreach ( var o in Ground.areas[6] )
				{
					var n = node + o;
					foreach ( var p in buildCategories )
					{
						if ( p.configuration != null )
						{
							if ( !Building.IsNodeSuitable( n, root.mainPlayer, p.configuration, currentFlagDirection ) )
								continue;
						}
						else
						{
							if ( !Flag.IsNodeSuitable( n, root.mainPlayer ) )
								continue;
						}

						Graphics.DrawMesh( p.mesh, Matrix4x4.TRS( n.position, Quaternion.identity, new Vector3( p.scale, p.scale, p.scale ) ), p.material, 0 );
						break;
					}
				}
			}
		}

		public bool OnMovingOverNode( GroundNode node )
		{
			if ( node != null )
			{
				if ( constructionMode == Construct.nothing )
				{
					CursorType t = CursorType.nothing;
					GroundNode flagNode = node.Neighbour( currentFlagDirection );
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
					return true;
				}

				SetCursorType( CursorType.building, currentFlagDirection );
				if ( currentBlueprint && currentBlueprint.Node != node )
					CancelBlueprint();
				if ( currentBlueprint )
					return true;
				switch ( constructionMode )
				{
					case Construct.workshop:
					{
						var workshop = Workshop.Create().Setup( node, root.mainPlayer, workshopType, currentFlagDirection, true );
						if ( workshop && workshop.gatherer )
						{
							currentBlueprintPanel = WorkshopPanel.Create();
							currentBlueprintPanel.offset = new Vector2( 100, 0 );
							currentBlueprintPanel.Open( workshop, WorkshopPanel.Content.resourcesLeft );
						}
						currentBlueprint = workshop;
						break;
					};
					case Construct.flag:
					{
						currentBlueprint = Flag.Create().Setup( node, root.mainPlayer, true );
						break;
					};
					case Construct.crossing:
					{
						currentBlueprint = Flag.Create().Setup( node, root.mainPlayer, true, true );
						break;
					};
					case Construct.stock:
					{
						currentBlueprint = Stock.Create().Setup( node, root.mainPlayer, currentFlagDirection, true );
						break;
					};
					case Construct.guardHouse:
					{
						currentBlueprint = GuardHouse.Create().Setup( node, root.mainPlayer, currentFlagDirection, true );
						break;
					};
				};
			}
			return true;
		}

		public void SetCursorType( CursorType cursortype, int roadDirection = -1 )
		{
			if ( cursor == null )
			{
				cursor = Instantiate( Resources.Load<GameObject>( "prefabs/others/cursor" ) );
				cursor.transform.SetParent( World.instance.ground.transform );
				for ( int i = 0; i < cursorTypes.Length; i++ )
				{
					cursorTypes[i] = World.FindChildRecursive( cursor.transform, ( (CursorType)i ).ToString() )?.gameObject;
					if ( i != (int)CursorType.invisible )
						Assert.global.IsNotNull( cursorTypes[i] );
				}
			}
			
			for ( int i = 0; i < cursorTypes.Length; i++ )
				cursorTypes[i]?.SetActive( i == (int)cursortype );
			if ( roadDirection >= 0 )
				cursorTypes[(int)CursorType.direction0 + roadDirection].SetActive( true );
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

		public bool OnObjectClicked( HiveObject target )
		{
			target.OnClicked();
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
			if ( base.Open( null, 0, 0, 400, 320 ) )
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

		public new void OnDestroy()
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
				Text( 230, row, 50, 20, ( item.life.age / 50 ).ToString(), scroll.content );
				if ( item.path )
					Text( 280, row, 30, 20, item.path.roadPath.Count.ToString(), scroll.content );
				row -= iconSize + 5;
			}
			var t = scroll.content.transform as RectTransform;
			t.sizeDelta = new Vector2( (int)( uiScale * 340 ), (int)( uiScale * sortedItems.Count * ( iconSize + 5 ) ) );
			scroll.verticalNormalizedPosition = 1;
		}

		static public int CompareByAge( Item itemA, Item itemB )
		{
			if ( itemA.life.age == itemB.life.age )
				return 0;
			if ( itemA.life.age < itemB.life.age )
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

	public class ResourceList : Panel
	{
		ScrollRect scroll;

		public static ResourceList Create()
		{
			return new GameObject().AddComponent<ResourceList>();
		}

		public void Open()
		{
			if ( base.Open( null, 0, 0, 400, 320 ) )
				return;
			name = "Resource list panel";

			Frame( 0, 0, 400, 320 );
			Button( 370, -10, 20, 20, iconTable.GetMediaData( Icon.exit ) ).onClick.AddListener( Close );
			Text( 50, -20, 100, 20, "Type" ).gameObject.AddComponent<Button>().onClick.AddListener( delegate
			{ Fill( CompareByType ); } );
			Text( 150, -20, 100, 20, "Last" ).gameObject.AddComponent<Button>().onClick.AddListener( delegate
			{ Fill( CompareByLastMined ); } );
			Text( 250, -20, 100, 20, "Ready" ).gameObject.AddComponent<Button>();

			scroll = ScrollRect( 20, -40, 360, 260 );
			Fill( CompareByType );
		}

		void Fill( Comparison<Resource> comparison )
		{
			int row = 0;
			foreach ( Transform child in scroll.content )
				Destroy( child.gameObject );

			List<Resource> sortedResources = new List<Resource>();
			foreach ( var resource in Resources.FindObjectsOfTypeAll<Resource>() )
			{
				if ( resource.underGround )
					sortedResources.Add( resource );
			}
			sortedResources.Sort( comparison );

			foreach ( var resource in sortedResources )
			{
				Text( 30, row, 100, 20, resource.type.ToString(), scroll.content ).gameObject.AddComponent<Button>().onClick.AddListener( delegate { GroundNode node = resource.node; NodePanel.Create().Open( node, true ); } );
				Text( 130, row, 50, 20, ( resource.gathered.age / 50 ).ToString(), scroll.content );
				Text( 230, row, 30, 20, resource.keepAway.inProgress ? "no" : "yes", scroll.content );
				row -= iconSize + 5;
			}
			var t = scroll.content.transform as RectTransform;
			t.sizeDelta = new Vector2( (int)( uiScale * 340 ), (int)( uiScale * sortedResources.Count * ( iconSize + 5 ) ) );
			scroll.verticalNormalizedPosition = 1;
		}

		static public int CompareByType( Resource resA, Resource resB )
		{
			if ( resA.type == resB.type )
				return 0;
			if ( resA.type < resB.type )
				return -1;
			return 1;
		}
		static public int CompareByLastMined( Resource resA, Resource resB )
		{
			if ( resA.gathered.age == resB.gathered.age )
				return 0;
			if ( resA.gathered.age > resB.gathered.age )
				return -1;
			return 1;
		}
	}
	public class PotentialList : Panel
	{
		ScrollRect scroll;
		Building building;
		Item.Type itemType;
		float timeSpeedToRestore;
		bool filled;

		public static PotentialList Create()
		{
			return new GameObject().AddComponent<PotentialList>();
		}

		public void Open( Building building, Item.Type itemType )
		{
			timeSpeedToRestore = World.instance.timeFactor;
			World.instance.SetTimeFactor( 0 );
			root.mainPlayer.itemDispatcher.queryBuilding = this.building = building;
			root.mainPlayer.itemDispatcher.queryItemType = this.itemType = itemType;

			if ( base.Open( null, 0, 0, 500, 320 ) )
				return;
			name = "Potential list panel";

			Frame( 0, 0, 500, 320 );
			Button( 470, -10, 20, 20, iconTable.GetMediaData( Icon.exit ) ).onClick.AddListener( Close );

			Text( 20, -20, 250, 20, "List of potentials for       at" );
			ItemIcon( 150, -20, 0, 0, itemType );
			BuildingIcon( 190, -20, building );

			Text( 20, -40, 100, 20, "Building" ).fontSize = (int)( uiScale * 10 );
			Text( 100, -40, 100, 20, "Distance" ).fontSize = (int)( uiScale * 10 );
			Text( 140, -40, 100, 20, "Direction" ).fontSize = (int)( uiScale * 10 );
			Text( 190, -40, 100, 20, "Priority" ).fontSize = (int)( uiScale * 10 );
			Text( 230, -40, 100, 20, "Result" ).fontSize = (int)( uiScale * 10 );

			scroll = ScrollRect( 20, -60, 460, 240 );
			Fill();
		}

		public void OnDestroy()
		{
			base.OnDestroy();
			root.mainPlayer.itemDispatcher.queryBuilding = null;
			root.mainPlayer.itemDispatcher.queryItemType = Item.Type.unknown;
			World.instance.SetTimeFactor( timeSpeedToRestore );
		}

		public void Update()
		{
			if ( root.mainPlayer.itemDispatcher.results != null && !filled )
			{
				filled = true;
				Fill();
			}
		}

		void Fill()
		{
			int row = 0;
			foreach ( Transform child in scroll.content )
				Destroy( child.gameObject );

			foreach ( var result in root.mainPlayer.itemDispatcher.results )
			{
				if ( result.building )
				{
					BuildingIcon( 0, row, result.building, scroll.content );
					Text( 100, row, 50, 20, result.building.node.DistanceFrom( building.node ).ToString(), scroll.content );
					Text( 130, row, 50, 20, result.incoming ? "Out" : "In", scroll.content );
					Text( 170, row, 50, 20, result.priority.ToString(), scroll.content );
				}
				string message = result.result switch
				{
					ItemDispatcher.Result.flagJam => "Jam at outpit flag",
					ItemDispatcher.Result.match => "Matched",
					ItemDispatcher.Result.noDispatcher => "Dispatcher is not free",
					ItemDispatcher.Result.notInArea => "Outside of area",
					ItemDispatcher.Result.otherAreaExcludes => "Their area excludes this",
					ItemDispatcher.Result.tooLowPriority => "Combined priority is too low",
					ItemDispatcher.Result.outOfItems => "Out of items",
					ItemDispatcher.Result.full => "Full",
					_ => "Unknown"

				};				
				Text( 210, row, 200, 40, message, scroll.content );
				row -= iconSize + 5;
			}
			var t = scroll.content.transform as RectTransform;
			var y = t.offsetMax;
			y.y = (int)( uiScale * root.mainPlayer.itemDispatcher.results.Count * ( iconSize + 5 ) );
			t.offsetMax = y;
			t.offsetMin = Vector2.zero;
			scroll.verticalNormalizedPosition = 1;
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
			if ( base.Open( null, 0, 0, 370, 300 ) )
				return;

			name = "Item stats panel";
			this.player = player;
			Frame( 0, 0, 370, 300 );
			Text( 70, -20, 50, 20, "In stock" ).fontSize = (int)( uiScale * 10 );
			Text( 120, -20, 50, 20, "On Road" ).fontSize = (int)( uiScale * 10 );
			Text( 170, -20, 50, 20, "Surplus" ).fontSize = (int)( uiScale * 10 );
			Text( 220, -20, 50, 20, "Per minute" ).fontSize = (int)( uiScale * 10 );
			Text( 270, -20, 50, 20, "Efficiency" ).fontSize = (int)( uiScale * 10 );
			scroll = ScrollRect( 20, -45, 330, 205 );
			Button( 340, -10, 20, 20, iconTable.GetMediaData( Icon.exit ) ).onClick.AddListener( Close );
			finalEfficiency = Text( 100, -260, 100, 30 );
			finalEfficiency.fontSize = (int)( uiScale * 16 );

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

		public new void Update()
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

			if ( base.Open( null, 0, 0, 450, 300 ) )
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
			float max = float.MinValue;
			for ( int i = a.data.Count - t.width; i < a.data.Count; i++ )
			{
				if ( i < 0 )
					continue;
				if ( a.data[i] > max )
					max = a.data[i];
			}
			float scale = t.height / max;
			int row = -1;
			for ( int x = t.width - 1; x >= 0; x-- )
			{
				int index = a.data.Count - t.width + x;
				int recordRow = (int)(scale * a.record);
				for ( int y = 0; y < t.height; y++ )
					t.SetPixel( x, y, index == a.recordIndex ? Color.yellow : y == recordRow ? Color.grey : Color.black );
				if ( 0 <= index )
				{
					int newRow = (int)Math.Min( (float)t.height - 1, scale * a.data[index] );
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
			itemFrame.rectTransform.anchoredPosition = new Vector2( (int)( uiScale * ( 17 + iconSize * (int)selected ) ), (int)( uiScale * -17 ) );
			lastAverageEfficiency = player.averageEfficiencyHistory.current;
		}
	}

	public class Main : Panel
	{
		InputField seed;
		InputField saveName;
		Dropdown loadNames;
		FileSystemWatcher watcher;
		Dropdown size;
		bool loadNamesRefreshNeeded = true;
		static int savedSize = 1;
		Eye grabbedEye;

		public static Main Create()
		{
			return new GameObject().AddComponent<Main>();
		}

		public void Open( bool focusOnMainBuilding = false )
		{
			Open( null, ( Screen.width - (int)( 300 * uiScale ) ) / 2, -Screen.height + (int)( 250 * uiScale ) );

			name = "Main Panel";
			Frame( 0, 0, 300, 210 );
			Button( 110, -20, 80, 20, "Continue" ).onClick.AddListener( Close );
			Image( 20, -45, 260, 1 );

			Button( 90, -50, 120, 20, "Start New World" ).onClick.AddListener( StartNewGame );
			Text( 20, -75, 40, 20, "Seed" ).fontSize = (int)( uiScale * 12 );
			seed = InputField( 60, -70, 100, 25 );
			seed.contentType = UnityEngine.UI.InputField.ContentType.IntegerNumber;
			Button( 165, -73, 60, 20, "Randomize" ).onClick.AddListener( RandomizeSeed );
			Text( 20, -100, 30, 20, "Size" ).fontSize = (int)( uiScale * 12 );
			size = Dropdown( 60, -95, 80, 25 );
			size.ClearOptions();
			size.AddOptions( new List<string>() { "Small", "Medium", "Big" } );
			size.value = savedSize;
			Image( 20, -125, 260, 1 );

			Button( 20, -133, 50, 20, "Load" ).onClick.AddListener( Load );
			loadNames = Dropdown( 80, -130, 200, 25 );
			Image( 20, -158, 260, 1 );

			Button( 20, -168, 50, 20, "Save" ).onClick.AddListener( Save );
			saveName = InputField( 80, -165, 100, 25 );
			saveName.text = new System.Random().Next().ToString();

			RandomizeSeed();
			watcher = new FileSystemWatcher( Application.persistentDataPath + "/Saves" );
			watcher.Created += SaveFolderChanged;
			watcher.Deleted += SaveFolderChanged;
			watcher.EnableRaisingEvents = true;

			if ( focusOnMainBuilding && root.mainPlayer )
			{
				grabbedEye = root.world.eye;
				grabbedEye.FocusOn( root.mainPlayer.mainBuilding?.flag?.node, true );
				escCloses = false;
			}
		}

		public new void OnDestroy()
		{
			base.OnDestroy();
			if ( grabbedEye == root.world.eye )
				root?.world?.eye?.ReleaseFocus( null, true );
		}

		public new void Update()
		{
			base.Update();
			if ( loadNamesRefreshNeeded )
				UpdateLoadNames();
			savedSize = size.value;
		}

		void StartNewGame()
		{
			root.world.settings = ScriptableObject.CreateInstance<World.Settings>();
			root.world.settings.size = 32 + 16 * size.value;
			root.NewGame( int.Parse( seed.text ) );
			Close();
		}

		void Load()
		{
			root.Load( Application.persistentDataPath + "/Saves/" + loadNames.options[loadNames.value].text );
			Close();
		}

		void Save()
		{
			root.Save( Application.persistentDataPath + "/Saves/" + saveName.text + ".json" );
		}

		void SaveFolderChanged( object sender, FileSystemEventArgs args )
		{
			loadNamesRefreshNeeded = true;
		}

		void UpdateLoadNames()
		{
			loadNamesRefreshNeeded = false;
			loadNames.ClearOptions();

			List<string> files = new List<string>();
			var directory = new DirectoryInfo( Application.persistentDataPath+"/Saves" );
			if ( !directory.Exists )
				return;

			var saveGameFiles = directory.GetFiles().OrderByDescending( f => f.LastWriteTime );
			foreach ( var f in saveGameFiles )
				files.Add( f.Name );

			loadNames.AddOptions( files );
		}

		void RandomizeSeed()
		{
			seed.text = new System.Random().Next().ToString();
		}
	}

	public interface IInputHandler
	{
		bool OnMovingOverNode( GroundNode node );
		bool OnNodeClicked( GroundNode node );
		bool OnObjectClicked( HiveObject target );
		void OnLostInput();
	}
}

