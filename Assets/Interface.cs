using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Profiling;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.UI;

public class Interface : OperationHandler
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
		emptyFrame,
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
		crossing,
		resizer
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

		if ( mainPlayer && mainPlayer.averageEfficiency >= world.efficiencyGoal && !world.victory )
		{
			WorldProgressPanel.Create().Open( true );
			world.victory = true;
		}
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
		"arrow", Icon.rightArrow,
		"brick", Icon.progress,
		"cross", Icon.exit };
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

	new public void Start()
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
		Water.Initialize();

		Directory.CreateDirectory( Application.persistentDataPath + "/Saves" );

		canvas = gameObject.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		gameObject.AddComponent<GraphicRaycaster>();

		viewport = Viewport.Create();

		var esObject = new GameObject( "Event System" );
		esObject.AddComponent<EventSystem>();
		esObject.AddComponent<StandaloneInputModule>();

		debug = new GameObject( "Debug" );
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

		MainPanel.Create().Open( true );
	}

	void NewGame( int seed )
	{
		world.NewGame( seed );
		if ( world.players.Count > 0 )
			mainPlayer = world.players[0];
		else
			mainPlayer = null;
	}

	public void Load( string fileName )
	{
		print( "Loading " + fileName );
		world.Load( fileName );
		if ( world.players.Count > 0 )
			mainPlayer = world.players[0];
	}

	public void Save( string fileName = "" )
	{
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
		// if ( GetKey( KeyCode.R ) )
		// {
		// 	bool localReset = false;
		// 	foreach ( var panel in panels )
		// 	{
		// 		if ( panel.target )
		// 		{
		// 			panel.target?.Reset();
		// 			localReset = true;
		// 		}
		// 	}
		// 	if ( !localReset )
		// 		world.Reset();
		// }
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
		if ( GetKeyDown( KeyCode.Z ) && ( GetKey( KeyCode.LeftControl ) || GetKey( KeyCode.RightControl ) ) )
			Undo();
		if ( GetKeyDown( KeyCode.Y ) && ( GetKey( KeyCode.LeftControl ) || GetKey( KeyCode.RightControl ) ) )
			Redo();
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
		if ( GetKeyDown( KeyCode.B ) )
		{
			BuildingList.Create().Open();
		}
		if ( GetKeyDown( KeyCode.P ) )
		{
			WorldProgressPanel.Create().Open();
		}
		if ( GetKeyDown( KeyCode.Escape ) )
		{
			if ( !viewport.ResetInputHandler() )
			{
				bool closedSomething = false;
				bool isMainOpen = false;
				for ( int i = panels.Count - 1; i >= 0; i-- )
				{
					if ( panels[i] as MainPanel )
						isMainOpen = true;
					if ( !panels[i].escCloses )
						continue;
					panels[panels.Count - 1].Close();
					closedSomething = true;
					break;
				}
				if ( !closedSomething && !isMainOpen )
					MainPanel.Create().Open();
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

		highlightVolume.transform.localPosition = highlightVolumeCenter.GetPositionRelativeTo( viewport.visibleAreaCenter );
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
		if ( chain )
			world.Validate( true );
		if ( highlightType == HighlightType.volume )
			Assert.global.IsNotNull( highlightVolume );
		if ( highlightType == HighlightType.area )
		{
			Assert.global.IsNotNull( highlightArea );
			Assert.global.IsNotNull( highlightArea.center );
		}
#endif

	}

	public override GroundNode location { get { return null; } }

	public class PathVisualization : MonoBehaviour
	{
	    Vector3 lastAbsoluteEyePosition;
		GroundNode start;

		public static PathVisualization Create()
		{
			return new GameObject().AddComponent<PathVisualization>();
		}

		public PathVisualization Setup( Path path, Vector3 view )
		{
			if ( path == null )
			{
				Destroy( this );
				return null;
			}

			var renderer = gameObject.AddComponent<MeshRenderer>();
			renderer.material = materialUIPath;
			renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			var route = gameObject.AddComponent<MeshFilter>().mesh = new Mesh();

			List<Vector3> vertices = new List<Vector3>();
			List<Vector2> uvs = new List<Vector2>();
			List<int> triangles = new List<int>();
			Vector3? currentPosition = null;
			for ( int j = 0; j < path.roadPath.Count; j++ )
			{
				Road road = path.roadPath[j];
				for ( int i = 0; i < road.nodes.Count - 1; i++ )
				{
					int c = i, n = i + 1;
					if ( path.roadPathReversed[j] )
					{
						c = road.nodes.Count - i - 1;
						n = c - 1;
					}
					if ( currentPosition == null )
					{
						start = road.nodes[c];
						currentPosition = road.nodes[c].GetPositionRelativeTo( view ) + Vector3.up * 0.1f;
					}
					Vector3 dif = road.nodes[n].GetPositionRelativeTo( road.nodes[c] ) - road.nodes[c].position;

					Vector3 side = dif * 0.1f;
					side = new Vector3( -side.z, side.y, side.x );

					triangles.Add( vertices.Count + 0 );
					triangles.Add( vertices.Count + 1 );
					triangles.Add( vertices.Count + 2 );
					triangles.Add( vertices.Count + 1 );
					triangles.Add( vertices.Count + 3 );
					triangles.Add( vertices.Count + 2 );

					vertices.Add( currentPosition.Value - side );
					vertices.Add( currentPosition.Value + side );
					vertices.Add( currentPosition.Value + dif - side );
					vertices.Add( currentPosition.Value + dif + side );
					currentPosition = currentPosition.Value + dif;

					uvs.Add( new Vector2( 0, 0 ) );
					uvs.Add( new Vector2( 1, 0 ) );
					uvs.Add( new Vector2( 0, 6 ) );
					uvs.Add( new Vector2( 1, 6 ) );
				}
			}

			route.vertices = vertices.ToArray();
			route.triangles = triangles.ToArray();
			route.uv = uvs.ToArray();
		    lastAbsoluteEyePosition = World.instance.eye.absolutePosition;
			return this;
		}

		public void Start()
		{
			name = "Path visualization";
			transform.SetParent( World.instance.transform );
		}

		public void Update()
		{
			var currentAbsoluteEyePosition = World.instance.eye.absolutePosition;
			transform.localPosition -= currentAbsoluteEyePosition - lastAbsoluteEyePosition;
			lastAbsoluteEyePosition = currentAbsoluteEyePosition;
		}

		public void OnDestroy()
		{
			Destroy( gameObject );
		}

	}

	public class Tooltip : Panel
	{
		public Component origin;
		Text text, additionalText;
		Image image;
		int width, height;

		public static Tooltip Create()
		{
			return new GameObject( "Tooltip", typeof( RectTransform ) ).AddComponent<Tooltip>();
		}

		public void Open()
		{
			borderWidth = 10;
			noCloseButton = true;
			base.Open( width = 100, height = 100 );
			escCloses = false;

			image = Image().Pin( 20, -20, 100, 100 );
			text = Text().Pin( 15, -10, 270, 60 );
			additionalText = Text().Pin( 20, -30, 150, 60 );
			additionalText.fontSize = (int)( 10 * uiScale );
			gameObject.SetActive( false );
			FollowMouse();
		}

		public void SetText( Component origin, string text = "", Sprite imageToShow = null, string additionalText = "" )
		{
			this.origin = origin;
			this.text.text = text;
			this.additionalText.text = additionalText;
			if ( imageToShow )
			{
				image.sprite = imageToShow;
				image.enabled = true;
				SetSize( width = (int)( uiScale * 140 ), height = (int)( uiScale * 100 ) );
			}
			else
			{
				image.enabled = false;
				if ( text.Length > 20 ) // TODO Big fat hack
					SetSize( width = 300, height = 70 );
				else
					SetSize( width = 200, height = 40 );
			}
			gameObject.SetActive( true );
			FollowMouse();
		}

		public void Clear()
		{
			origin = null;
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
			this.Pin( (int)( (Input.mousePosition.x + 20) / uiScale ), (int)( (Input.mousePosition.y - Screen.height) / uiScale ), width, height );
		}
	}

	class TooltipSource : MonoBehaviour
	{
		public string text;
		public string additionalText;
		public Sprite image;

		public void Update()
		{
			if ( GetUIElementUnderCursor() == gameObject )
				tooltip.SetText( this, text, image, additionalText );
			else if ( tooltip.origin == this )
				tooltip.Clear();
		}

		public static TooltipSource Add( Component widget, string text, Sprite image = null, string additionalText = "" )
		{
			var s = widget.gameObject.AddComponent<TooltipSource>();
			s.text = text;
			s.image = image;
			s.additionalText = additionalText;
			return s;
		}

		public static TooltipSource Set( Component widget, string text, Sprite image = null, string additionalText = "" )
		{
			var s = widget.gameObject.GetComponent<TooltipSource>();
			if ( s == null )
				s = widget.gameObject.AddComponent<TooltipSource>();
			s.text = text;
			s.image = image;
			s.additionalText = additionalText;
			return s;
		}
	}

	public class Panel : MonoBehaviour, IDragHandler, IBeginDragHandler, IPointerClickHandler
	{
		public HiveObject target;
		public bool followTarget = true;
		public Image frame;
		Image resizer;
		public bool escCloses = true;
		public bool disableDrag;
		public Vector2 offset;
		public int borderWidth = 20;
		public bool noCloseButton;
		public bool noResize;
		bool dragResizes;

		public const int itemIconBorderSize = 2;

		public enum CompareResult
		{
			different,
			sameButDifferentTarget,
			same
		}

		// Summary:
		// Return true if the caller should give
		public bool Open( HiveObject target, int x, int y, int xs, int ys )
		{
			if ( !(transform is RectTransform) )
				gameObject.AddComponent<RectTransform>();

			foreach ( var panel in root.panels )
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
			root.panels.Add( this );
			transform.SetParent( root.transform, false );
			if ( borderWidth != 0 )
			{
				frame = Frame( borderWidth ).Stretch( 0, 0, 0, 0 );
				frame.name = "Background";
			}
			if ( !noCloseButton )
			{
				var cb = Image( iconTable.GetMediaData( Icon.exit ) ).Pin( -borderWidth, 0, borderWidth, borderWidth, 1, 1 ).AddClickHandler( Close );
				cb.name = "Close button";
				cb.color = Color.red;
			}
			if ( !noResize )
			{
				resizer = Image( iconTable.GetMediaData( Icon.resizer ) ).Pin( -iconSize, iconSize, iconSize, iconSize, 1, 0 );
				resizer.name = "Resizer";
			}
			this.target = target;
			this.Pin( x, y, xs, ys );
			UpdatePosition();
			return false;
		}

		public bool Open( HiveObject target, int xs, int ys )
		{
			return Open( target, 0, 0, xs, ys );
		}
		
		public bool Open( int xs, int ys )
		{
			return Open( null, xs, ys );
		}
		
		public virtual CompareResult IsTheSame( Panel other )
		{
			if ( other.GetType() == GetType() )
				return CompareResult.same;

			return CompareResult.different;
		}

		public void OnDestroy()
		{
			root.panels.Remove( this );
		}

		public void SetSize( int x, int y )
		{
			if ( transform is RectTransform t )
				t.offsetMax = t.offsetMin + new Vector2( x * uiScale, y * uiScale );
		}

		public Image Image( Sprite picture = null )
		{
			Image i = new GameObject().AddComponent<Image>();
			i.name = "Image";
			i.sprite = picture;
			i.transform.SetParent( transform );
			return i;
		}

		public ProgressBar Progress( Sprite picture = null )
		{
			Image i = new GameObject().AddComponent<Image>();
			i.name = "Progress Bar";
			i.sprite = picture;
			i.transform.SetParent( transform );
			var p = i.gameObject.AddComponent<ProgressBar>();
			p.Open();
			return p;
		}

		public Image Frame( int borderWidth = iconSize )
		{
			var s = iconTable.GetMediaData( Icon.frame );
			int originalBorder = (int)( ( s.border.x + s.border.y + s.border.z + s.border.w ) * 0.25f );

			Image i = Image( s );
			i.type = UnityEngine.UI.Image.Type.Sliced;
			i.pixelsPerUnitMultiplier = originalBorder / uiScale / borderWidth; 
			return i;

		}

		public ScrollRect ScrollRect( bool vertical = true, bool horizontal = false )
		{
			var scroll = new GameObject().AddComponent<ScrollRect>();
			scroll.name = "Scroll view";
			var mask = scroll.gameObject.AddComponent<RectMask2D>();
			scroll.transform.SetParent( transform );
			scroll.vertical = vertical;
			scroll.horizontal = horizontal;

			if ( horizontal )
			{
				var scrollBarObject = GameObject.Instantiate( Resources.Load<GameObject>( "scrollbar" ) );
				var scrollBar = scrollBarObject.GetComponent<Scrollbar>();	// TODO Would be better to do this from script
				scrollBar.name = "Horizontal scroll bar";
				scrollBar.direction = Scrollbar.Direction.LeftToRight;
				scroll.horizontalScrollbar = scrollBar;
				scrollBar.Link( scroll );
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
				scrollBar.Link( scroll );
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

		public void SetScrollRectContentSize( ScrollRect scroll, int x = 0, int y = 0 )
		{
			var t = scroll.content.transform as RectTransform;
			var m = t.offsetMax;
			if ( y != 0 )
				m.y = (int)( uiScale * y );
			if ( x != 0 )
				m.x = (int)( uiScale * x );
			t.offsetMax = m;
			t.offsetMin = Vector2.zero;
			scroll.verticalNormalizedPosition = 1;
		}

		public ItemImage ItemIcon( Item.Type type = Item.Type.unknown )
		{
			var i = ItemImage.Create().Open( type );
			i.name = "ItemImage";
			if ( type != Item.Type.unknown )
				i.picture.sprite = Item.sprites[(int)type];
			else
				i.enabled = false;
			i.itemType = type;
			i.Stretch();
			i.transform.SetParent( transform );
			i.gameObject.AddComponent<Button>().onClick.AddListener( i.Track );
			return i;
		}

		public Image AreaIcon( Ground.Area area )
		{
			var bg = Image( iconTable.GetMediaData( Icon.smallFrame ) );
			bg.color = Color.grey;
			var i = Image( iconTable.GetMediaData( Icon.crosshair ) ).AddOutline();
			var a = i.gameObject.AddComponent<AreaControl>();
			a.Setup( area );
			i.Link( bg );
			i.rectTransform.anchorMin = new Vector2( 0.2f, 0.2f );
			i.rectTransform.anchorMax = new Vector2( 0.8f, 0.8f );
			i.rectTransform.offsetMin = i.rectTransform.offsetMax = Vector2.zero;
			return bg;
		}

		public Text BuildingIcon( Building building, int fontSize = 12 )
		{
			if ( building == null )
				return null;

			var text = Text( building.title, fontSize );
			text.AddClickHandler( delegate { SelectBuilding( building ); } );
			return text;
		}

		public static void SelectBuilding( Building building )
		{
			building?.OnClicked( true );
		}

		public Image Button( string text )
		{
			Image i = Image( Resources.Load<Sprite>( "button" ) );
			i.type = UnityEngine.UI.Image.Type.Sliced;
			i.pixelsPerUnitMultiplier = 6;
			var t = Text( text ).Link( i ).Stretch( 6, 6, -6, -6 );
			t.color = Color.yellow;
			t.alignment = TextAnchor.MiddleCenter;
			t.resizeTextForBestFit = true;
			return i;
		}

		public Text Text( string text = "", int fontSize = 12 )
		{
			Text t = new GameObject().AddComponent<Text>();
			t.name = "Text";
			t.transform.SetParent( transform );
			t.font = Interface.font;
			t.fontSize = (int)( fontSize * uiScale );
			t.color = Color.black;
			t.text = text;
			return t;
		}

		public InputField InputField( string text = "" )
		{
			var o = Instantiate( Resources.Load<GameObject>( "InputField" ) );
			var i = o.GetComponent<InputField>();
			var image = i.GetComponent<Image>();
			i.transform.SetParent( transform );
			i.name = "InputField";
			i.text = text;
			return i;
		}

		public Dropdown Dropdown()
		{
			var o = Instantiate( Resources.Load<GameObject>( "Dropdown" ) );
			var d = o.GetComponent<Dropdown>();
			d.ClearOptions();
			var image = d.GetComponent<Image>();
			d.transform.SetParent( transform );
			d.name = "InputField";
			return d;
		}

		public virtual void Close()
		{
			Destroy( gameObject );
		}

		public virtual void Update()
		{
			UpdatePosition();
		}

		void UpdatePosition()
		{
			if ( target == null || !followTarget )
				return;

			MoveTo( target.location.GetPositionRelativeTo( root.world.eye.position ) + Vector3.up * GroundNode.size );
		}

		public void MoveTo( Vector3 position )
		{
			Vector3 screenPosition = root.viewport.camera.WorldToScreenPoint( position );
			screenPosition.x += offset.x;
			screenPosition.y += offset.y;
			if ( screenPosition.y > Screen.height )
				screenPosition = World.instance.eye.camera.WorldToScreenPoint( target.location.position - Vector3.up * GroundNode.size );
			screenPosition.y -= Screen.height;
			if ( transform is RectTransform t )
			{
				if ( screenPosition.x + t.rect.width > Screen.width )
					screenPosition.x -= t.rect.width + 2 * offset.x;
				float width = t.offsetMax.x - t.offsetMin.x;
				float height = t.offsetMax.y - t.offsetMin.y;
				t.offsetMin = screenPosition - Vector3.up * height;
				t.offsetMax = t.offsetMin + new Vector2( width, height );
			}
		}

		public void OnBeginDrag( PointerEventData eventData )
		{
			if ( resizer != null && resizer.Contains( Input.mousePosition ) )
				dragResizes = true;
			else
				dragResizes = false;
		}

		public void OnDrag( PointerEventData eventData )
		{
			if ( disableDrag )
				return;

			var t = transform as RectTransform;
			if ( t == null )
				return;

			if ( dragResizes )
			{
				t.offsetMin += Vector2.up * eventData.delta.y;
				t.offsetMax += Vector2.right * eventData.delta.x;
			}
			else
			{
				t.offsetMin += eventData.delta;
				t.offsetMax += eventData.delta;
				followTarget = false;
			}
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

		public class ProgressBar : MonoBehaviour
		{
			Image bar;
			public void Open()
			{
				Image frame = gameObject.GetComponent<Image>();
				frame.sprite = iconTable.GetMediaData( Icon.smallFrame );
				frame.pixelsPerUnitMultiplier = 16 / uiScale;
				frame.type = UnityEngine.UI.Image.Type.Sliced;
				frame.color = Color.Lerp( Color.red, Color.yellow, 0.5f ).Dark();

				bar = new GameObject( "Bar" ).AddComponent<Image>();
				bar.sprite = iconTable.GetMediaData( Icon.emptyFrame );
				bar.pixelsPerUnitMultiplier = 24 / uiScale;
				bar.type = UnityEngine.UI.Image.Type.Sliced;
				bar.rectTransform.SetParent( transform, false );
				bar.rectTransform.anchorMin	= Vector2.zero;
				bar.rectTransform.anchorMax = Vector2.one;
				bar.rectTransform.offsetMin = Vector2.one * uiScale * 3;
				bar.rectTransform.offsetMax = -Vector2.one * uiScale * 3;
				bar.color = Color.yellow;
			}
			public float progress
			{
				get
				{
					return bar.rectTransform.anchorMax.x;
				}
				set
				{
					bar.rectTransform.anchorMax = new Vector2( Math.Min( value, 1 ), 1 );
				}
			}
			public Color color
			{
				get
				{
					return bar.color;
				}
				set
				{
					bar.color = value;
				}
			}

		}

		public class AreaControl : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IInputHandler
		{
			public Ground.Area area;
			public GroundNode oldCenter;
			public int oldRadius;
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
				{
					root.highlightType = HighlightType.none;
					root.highlightArea = null;
				}
				root.RegisterChangeArea( area, oldCenter, oldRadius );
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
				oldCenter = area.center;
				oldRadius = area.radius;
				area.center = World.instance.ground.nodes[0];
				area.radius = 4;
				root.highlightType = HighlightType.area;
				root.highlightArea = area;
				root.highlightOwner = gameObject;
				root.viewport.inputHandler = this;
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
				if ( root.viewport.inputHandler != this as IInputHandler && root.highlightArea == area )
					root.highlightType = HighlightType.none;
			}

			public void Update()
			{
				image.color = area.center != null ? Color.green : Color.white;
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
				if ( root.highlightArea != area )
					return;
				area.center = oldCenter;
				area.radius = oldRadius;
				root.highlightType = HighlightType.none;
			}

			public bool OnObjectClicked( HiveObject target )
			{
				return OnNodeClicked( target.location );
			}
		}

		public class ItemImage : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
		{
			public Item item;
			public Item.Type itemType = Item.Type.unknown;
			public string additionalTooltip;
			PathVisualization pathVisualization;
			public Image inTransit;
			public Image picture;

			public static ItemImage Create()
			{
				return new GameObject( "ItemImage", typeof( RectTransform ) ).AddComponent<ItemImage>();
			}

			public ItemImage Open( Item.Type itemType )
			{
				this.itemType = itemType;

				var frame = new GameObject().AddComponent<Image>().Link( this ).Stretch( -itemIconBorderSize, -itemIconBorderSize, itemIconBorderSize, itemIconBorderSize );
				frame.name = "Smallframe";
				frame.sprite = iconTable.GetMediaData( Icon.smallFrame );

				picture = new GameObject().AddComponent<Image>().Link( this ).Stretch();
				picture.name = "ItemIcon";
				picture.enabled = itemType != Item.Type.unknown;
				if ( itemType != Item.Type.unknown )
					picture.sprite = Item.sprites[(int)itemType];
				return this;
			}

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

				Destroy( pathVisualization );
				pathVisualization = null;

				this.itemType = itemType;
				if ( itemType == Item.Type.unknown )
				{
					picture.enabled = false;
					return;
				}
				picture.enabled = true;
				picture.sprite = Item.sprites[(int)itemType];
			}

			public void SetInTransit( bool show )
			{
				if ( inTransit == null )
				{
					inTransit = new GameObject().AddComponent<Image>();
					inTransit.transform.SetParent( transform, false );
					inTransit.sprite = Worker.arrowSprite;
					inTransit.rectTransform.anchorMin = new Vector2( 0.5f, 0.0f );
					inTransit.rectTransform.anchorMax = new Vector2( 1.0f, 0.5f );
					inTransit.rectTransform.offsetMin = inTransit.rectTransform.offsetMax = Vector2.zero;
				}
				inTransit.gameObject.SetActive( show );
			}

			public void OnDestroy()
			{
				Destroy( pathVisualization );
				pathVisualization = null;
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
				{
					pathVisualization = PathVisualization.Create().Setup( item.path, Interface.root.viewport.visibleAreaCenter );
					tooltip.SetText( this, item.type.ToString(), Item.sprites[(int)item.type], additionalTooltip );
				}
				else
					tooltip.SetText( this, itemType.ToString(), Item.sprites[(int)itemType], additionalTooltip );
			}

			public void OnPointerExit( PointerEventData eventData )
			{
				Destroy( pathVisualization );
				pathVisualization = null;
				tooltip.Clear();
			}

			void Update()
			{
				if ( itemType == Item.Type.unknown || picture.color.a < 0.5f )
					SetInTransit( false );
				if ( item?.path && inTransit )
				{
					if ( item.path.stepsLeft > 7 )
						inTransit.color = Color.red;
					else if ( item.path.stepsLeft < 3 )
						inTransit.color = Color.green;
					else
						inTransit.color = Color.white;

				}
			}
		}
	}

	public class BuildingPanel : Panel
	{
		public Building building;
		public bool Open( Building building, int xs, int ys )
		{
#if DEBUG
			Selection.activeGameObject = building.gameObject;
#endif
			this.building = building;
			return base.Open( building.node, xs, ys );
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
		public ProgressBar progressBar;
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
			noResize = true;
			if ( base.Open( workshop, 250, 150 ) )
				return;

			name = "Workshop panel";
			this.workshop = workshop;
			bool showOutputBuffer = false, showProgressBar = false;
			showProgressBar = true;
			showOutputBuffer = workshop.productionConfiguration.outputType != Item.Type.unknown;

			if ( ( contentToShow & Content.progress ) == 0 )
				showProgressBar = false;
			if ( ( contentToShow & Content.output ) == 0 )
				showOutputBuffer = false;

			int displayedBufferCount = workshop.buffers.Count + ( showOutputBuffer ? 1 : 0 );

			int row = -20;
			if ( ( contentToShow & Content.name ) > 0 )
			{
				Text( workshop.type.ToString() ).Pin( 20, row, 160, 20 );
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
					outputs.Setup( this, workshop.productionConfiguration.outputType, workshop.productionConfiguration.outputMax, 20, row, iconSize + 5, workshop.outputArea, false );
					row -= iconSize * 3 / 2;
				}
				int progressWidth = ( iconSize + 5 ) * 7;
				progressBar = Progress().Pin( iconSize, row, iconSize + progressWidth, iconSize );
				row -= 25;

				if ( ( contentToShow & Content.itemsProduced ) > 0 )
				{
					itemsProduced = Text().Pin( 20, row, 200, 20 );
					productivity = Text().Pin( 150, -20, 50, 20 ).AddOutline();
					productivity.alignment = TextAnchor.MiddleRight;
					productivity.AddClickHandler( ShowPastStatuses );
					row -= 25;
				}
			}
			if ( workshop.gatherer && ( contentToShow & Content.resourcesLeft ) > 0 )
				resourcesLeft = Text( "Resources left: 0" ).Pin( 20, row, 150, 20 );

			if ( ( contentToShow & Content.controlIcons ) != 0 )
			{
				Image( iconTable.GetMediaData( Icon.destroy ) ).Pin( 190, row ).AddClickHandler( Remove );
				Image( iconTable.GetMediaData( Icon.hauler ) ).Pin( 170, row ).AddClickHandler( ShowWorker );
				changeModeImage = Image( GetModeIcon() ).Pin( 150, row ).AddClickHandler( ChangeMode );
				changeModeImage.color = Color.black;
				row -= 25;
			}

			this.Pin( 0, 0, 250, 15 - row );
			if ( show )
				root.world.eye.FocusOn( workshop, true );
		}

		void ShowPastStatuses()
		{
			PastStatuses.Create().Open( workshop );
		}

		void Remove()
		{
			if ( workshop )
				root.ExecuteRemoveBuilding( workshop );

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
					progressBar.progress = workshop.GetProgress();
					progressBar.color = Color.yellow;
				}
				else
				{
					if ( workshop.gatherer && !workshop.working )
						progressBar.color = Color.green;
					else
						progressBar.color = Color.red;
				}
				if ( productivity )
					UpdateProductivity( productivity, workshop );
				if ( itemsProduced )
					itemsProduced.text = "Items produced: " + workshop.itemsProduced;
			}
			if ( resourcesLeft )
			{
				int left = 0;
				void CheckNode( GroundNode node )
				{
					foreach ( var resource in node.resources )
					{
						if ( resource == null || resource.type != workshop.productionConfiguration.gatheredResource )
							return;
						if ( !resource.underGround || node == workshop.node || resource.node.owner == workshop.owner )
						{
							if ( resource.infinite )
								left++;
							else
								left += resource.charges;
						}
					}
				}
				CheckNode( workshop.node );
				foreach ( var o in Ground.areas[workshop.productionConfiguration.gatheringRange] )
					CheckNode( workshop.node + o );
				resourcesLeft.text = "Resources left: " + left;
			}
			if ( changeModeImage )
				changeModeImage.sprite = GetModeIcon();
		}

		static public void UpdateProductivity( Text text, Workshop workshop )
		{
			var percentage = (int)Math.Min( workshop.productivity.current * 101, 100 );
			text.text = percentage.ToString() + "%";
			if ( percentage == 100 )
				text.color = Color.green;
			else if ( percentage < 20 )
				text.color = Color.red;
			else
				text.color = Color.yellow;
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

			public void Setup( BuildingPanel boss, Item.Type itemType, int itemCount, int x, int y, int xi, Ground.Area area = null, bool input = true )
			{
				items = new ItemImage[itemCount];
				this.boss = boss;
				this.itemType = itemType;
				for ( int i = 0; i < itemCount; i++ )
				{
					items[i] = boss.ItemIcon( itemType ).Pin( x, y );
					x += xi;
				}
				boss.Text( "?" ).Pin( x, y, 20, 20 ).AddClickHandler( delegate { LogisticList.Create().Open( boss.building, itemType, input ? ItemDispatcher.Potential.Type.request : ItemDispatcher.Potential.Type.offer ); } );
				if ( area != null )
					boss.AreaIcon( area ).Pin( x + 15, y );
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
					items[i].picture.color = Color.white;
					items[i].SetType( itemType );
					if ( i < inStock )
					{
						items[i].SetInTransit( false );
						items[i].item = null;
					}
					else
					{
						if ( i < inStock + onTheWay )
						{
							items[i].SetInTransit( true );
							while ( itemsOnTheWay[k].type != itemType )
								k++;
							items[i].item = itemsOnTheWay[k++];
						}
						else
							items[i].picture.color = new Color( 1, 1, 1, 0 );
					}
				}
			}

			public void Update()
			{
				Assert.global.IsNotNull( buffer );
				Update( buffer.stored, buffer.onTheWay );
			}
		}
		public class PastStatuses : Panel
		{
			public static PastStatuses Create()
			{
				return new GameObject( "Past Statuses Panel").AddComponent<PastStatuses>();
			}

			public void Open( Workshop workshop )
			{
				if ( base.Open( workshop, 300, 150 ) )
					return;

				int[] ticksInStatus = new int[(int)Workshop.Status.total];
				int[] percentInStatus = new int[(int)Workshop.Status.total];
				int totalTicks = 0;
				void ProcessStatusList( List<Workshop.PastStatus> past )
				{
					foreach ( var s in past )
					{
						ticksInStatus[(int)s.status] += s.length;
						totalTicks += s.length;
					}
				}
				ProcessStatusList( workshop.pastStatuses );
				ProcessStatusList( workshop.previousPastStatuses );
				if ( totalTicks == 0 )
					return;

				List<Workshop.Status> statusList = new List<Workshop.Status>();
				for ( int i = 0; i < ticksInStatus.Length; i++ )
				{
					percentInStatus[i] = 100 * ticksInStatus[i] / totalTicks;
					for ( int j = 0; j < percentInStatus[i]; j++ )
						statusList.Add( (Workshop.Status)i );
				}
				while ( statusList.Count < 101 )
					statusList.Add( statusList[0] );

				Color[] statusColors = { Color.green, Color.red, Color.yellow, Color.cyan, Color.magenta, Color.grey, Color.red.Light(), Color.blue.Light() };
				Assert.global.AreEqual( statusColors.Length, (int)Workshop.Status.total );

				UIHelpers.currentRow = -20;
				Text( $"Last {totalTicks / 60 / 50} minutes" ).PinDownwards( 150, 350, -iconSize, 0 );
				for ( int i = 0; i < (int)Workshop.Status.total; i++ )
				{
					if ( ticksInStatus[i] == 0 )
						continue;
					string statusName = (Workshop.Status)i switch
					{
						Workshop.Status.working => "Working",
						Workshop.Status.waitingForAnyInput => "Waiting for input",
						Workshop.Status.waitingForInput0 => $"Waiting for {workshop.buffers[0].itemType.ToString()}",
						Workshop.Status.waitingForInput1 => $"Waiting for {workshop.buffers[1].itemType.ToString()}",
						Workshop.Status.waitingForInput2 => $"Waiting for {workshop.buffers[2].itemType.ToString()}",
						Workshop.Status.waitingForInput3 => $"Waiting for {workshop.buffers[3].itemType.ToString()}",
						Workshop.Status.waitingForOutputSlot => "Waiting for output slot",
						Workshop.Status.waitingForResource => "Waiting for resource",
						_ => "Unknown"
					};
					var e = Text( $"{percentInStatus[i]}% " + statusName, 10 ).PinDownwards( 150, 0, 350, (int)( iconSize * 0.8f ) ).AddOutline();
					e.color = statusColors[i];
					e.name = $"Reason {i}";
				}

				var g = Image().Stretch( 20, 20, -170, -20 );
				g.name = "Circle";
				var t = new Texture2D( (int)g.rectTransform.rect.width, (int)g.rectTransform.rect.height );

				for ( int x = 0; x < t.width; x++ )
				{
					for ( int y = 0; y < t.height; y++ )
					{
						int xv = x - ( t.width / 2 );
						int yv = y - ( t.height / 2 );
						var dist = Math.Sqrt( xv * xv + yv * yv );
						var radius = Math.Min( t.width / 2, t.height / 2 );
						if ( dist > radius )
							t.SetPixel( x, y, new Color( 1, 1, 1, 0 ) );
						else
						{
							if ( dist > radius * 0.97 )
								t.SetPixel( x, y, Color.black );
							else
							{
								int percent = Math.Min( (int)( Math.Atan2( xv, yv ) / Math.PI * 50 + 50 ), 100 );
								t.SetPixel( x, y, statusColors[(int)statusList[percent]] );
							}
						}
					}
				}

				t.Apply();
				g.sprite = Sprite.Create( t, new Rect( 0, 0, t.width, t.height ), Vector2.zero );
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
			noResize = true;
			if ( base.Open( guardHouse, 50, 50 ) )
				return;
			name = "Guard House panel";
			Image( iconTable.GetMediaData( Icon.destroy ) ).PinCenter( 0, 0, iconSize, iconSize, 0.5f, 0.5f ).AddClickHandler( Remove );
			if ( show )
				root.world.eye.FocusOn( guardHouse, true );
		}
		void Remove()
		{
			if ( guardHouse )
				root.ExecuteRemoveBuilding( guardHouse );
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
		public int destinationIndex;
		public RectTransform controls;

		float lastMouseXPosition;
		List<int>listToChange;
		int min, max;

		public static StockPanel Create()
		{
			return new GameObject( "Stock panel").AddComponent<StockPanel>();
		}

		public void Open( Stock stock, bool show = false )
		{
			this.stock = stock;
			noResize = true;
			if ( base.Open( stock, 300, 350 ) )
				return;
			RecreateControls();
			if ( show )
				root.world.eye.FocusOn( stock, true );
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
				LogisticList.Create().Open( stock, itemType, ItemDispatcher.Potential.Type.request );
				return;
			}
			if ( GetKey( KeyCode.LeftControl ) )
			{
				LogisticList.Create().Open( stock, itemType, ItemDispatcher.Potential.Type.offer );
				return;
			}
			selectedItemType = itemType;
		}

		void RecreateControls()
		{
			if ( controls )
				Destroy( controls.gameObject );
			controls = new GameObject( "Stock controls" ).AddComponent<RectTransform>();
			controls.Link( this ).Stretch();

			int height = 340;
			Image( iconTable.GetMediaData( Icon.destroy ) ).Link( controls ).Pin( 250, 40 - height ).AddClickHandler( Remove ).name = "Remover";
			AreaIcon( stock.inputArea ).Link( controls ).Pin( 30, -25, 30, 30 ).name = "Input area";
			AreaIcon( stock.outputArea ).Link( controls ).Pin( 250, -25, 30, 30 ).name = "Output area";
			Image( iconTable.GetMediaData( Icon.reset ) ).Link( controls ).Pin( 140, -30 ).AddClickHandler( stock.ClearSettings ).name = "Reset";;
			total = Text( "", 16 ).Link( controls ).Pin( 35, 35 - height, 100 );
			total.name = "Total";

			int row = -55;
			for ( int j = 0; j < (int)Item.Type.total; j++ )
			{
				int offset = j % 2 > 0 ? 140 : 0;
				var t = (Item.Type)j;
				var i = ItemIcon( (Item.Type)j ).Link( controls ).Pin( 20 + offset, row );
				i.additionalTooltip = "Shift+LMB Show input potentials\nCtrl+LMB Show output potentials\nShift+Ctrl+LMB Add one more\nAlt+Ctrl+LMB Clear";
				if ( stock.GetSubcontractors( (Item.Type)j ).Count > 0 )
					Image( iconTable.GetMediaData( Icon.rightArrow ) ).Link( i ).PinCenter( 0, 0, iconSize / 2, iconSize / 2, 0, 0.5f );
				if ( stock.destinationLists[j].Count > 0 )
				{
					Image( iconTable.GetMediaData( Icon.rightArrow ) ).Link( i ).PinCenter( 0, 0, iconSize / 2, iconSize / 2, 1, 0.5f );
					offset += 10;
				}
				i.AddClickHandler( delegate { SelectItemType( t ); } );
				counts[j] = Text().Link( controls ).Pin( 44 + offset, row, 100 );
				if ( j % 2 > 0 )
					row -= iconSize + 5;
			}

			for ( int i = 0; i < counts.Length; i++ )
			{
				Item.Type j = (Item.Type)i;
				counts[i].AddClickHandler( delegate { SelectItemType( j ); } );
			}

			int ipx = 165, ipy = -280;
			selected = ItemIcon( selectedItemType ).Link( controls ).Pin( ipx, ipy, 2 * iconSize, 2 * iconSize ).AddClickHandler( SetTarget );
			selected.additionalTooltip = "LMB Set cart target\nShift+LMB Show current target\nCtrl+LMB Clear target\nAlt+LMB Show inputs";
			selected.name = "Selected item";
			inputMin = Text().Link( controls ).Pin( ipx - 40, ipy, 40 );
			TooltipSource.Add( inputMin, "If this number is higher than the current content, the stock will request new items at high priority" );
			inputMax = Text().Link( controls ).Pin( ipx + 50, ipy, 40 );
			TooltipSource.Add( inputMax, "If the stock has at least this many items, it will no longer accept surplus" );
			outputMin = Text().Link( controls ).Pin( ipx - 40, ipy - 20, 40 );
			TooltipSource.Add( outputMin, "The stock will only supply other buildings with the item if it has at least this many" );
			outputMax = Text().Link( controls ).Pin( ipx + 50, ipy - 20, 40 );
			TooltipSource.Add( outputMax, "If the stock has more items than this number, then it will send the surplus even to other stocks" );
		}

		void SetTarget()
		{
			if ( GetKey( KeyCode.LeftShift ) || GetKey( KeyCode.RightShift ) )
			{
				if ( stock.destinationLists[(int)selectedItemType].Count > 0 )
				{
					if ( destinationIndex >= stock.destinationLists[(int)selectedItemType].Count )
						destinationIndex = 0;
					World.instance.eye.FocusOn( stock.destinationLists[(int)selectedItemType][destinationIndex].node, true );
					destinationIndex++;
				}
				return;
			}
			if ( GetKey( KeyCode.LeftControl ) || GetKey( KeyCode.RightControl ) )
			{
				stock.destinationLists[(int)selectedItemType].Clear();
				RecreateControls();
				return;
			}
			if ( GetKey( KeyCode.LeftAlt ) || GetKey( KeyCode.RightAlt ) )
			{
				var sources = stock.GetSubcontractors( selectedItemType );
				if ( sources.Count > 0 )
					SelectBuilding( sources[0] );	// TODO Show the rest?
				return;
			}
			itemTypeForRetarget = selectedItemType;
			root.highlightOwner = gameObject;
			root.viewport.inputHandler = this;
			root.highlightType = HighlightType.stocks;
		}

		void Remove()
		{
			if ( stock )
				root.ExecuteRemoveBuilding( stock );
			Close();
		}

		public override void Update()
		{
			base.Update();
			for ( int i = 0; i < (int)Item.Type.total; i++ )
			{
				Color c = Color.black;
				if ( stock.content[i] < stock.inputMin[i] )
					c = Color.red.Dark();
				if ( stock.content[i] > stock.inputMax[i] )
					c = Color.green.Dark();
				counts[i].color = c;
				counts[i].text = stock.content[i] + " (+" + stock.onWay[i] + ")";
			}
			total.text = stock.total + " => " + stock.totalTarget;
			selected?.SetType( selectedItemType );
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
					max = stock.maxItems;
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
					max = stock.maxItems;
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

			var l = stock.destinationLists[(int)itemTypeForRetarget];
			if ( l.Contains( destination ) )
				l.Remove( destination );
			else
			{
				l.Add( destination );
				destination.inputMax[(int)itemTypeForRetarget] = Math.Max( (int)( Stock.Cart.capacity * 1.5f ), destination.inputMax[(int)itemTypeForRetarget] );
			}

			root.highlightType = HighlightType.none;
			RecreateControls();
			return false;
		}

		public void OnLostInput()
		{
			root.highlightType = HighlightType.none;
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
			noResize = true;
			base.Open( node, 0, 0, 380, 180 );
			this.node = node;
			name = "Node panel";
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
			string resources = "";
			foreach ( var resource in node.resources )
			{
				if ( resources != "" )
					resources += ", ";
				resources += resource.type;

			}
			if ( resources != "" )
				Text( "Resource: " + resources ).Pin( 20, -40, 160 );
			if ( show )
				root.world.eye.FocusOn( node, true );
		}

		void BuildButton( int x, int y, string title, bool enabled, UnityEngine.Events.UnityAction action )
		{
			Image button = Button( title ).Pin( x, y, 160 ).AddClickHandler( action );
			if ( !enabled )
			{
				Text text = button.gameObject.GetComponentInChildren<Text>();
				if ( text )
					text.color = Color.red;
			}
		}

		void AddResourcePatch( Resource.Type resourceType )
		{
			node.AddResourcePatch( resourceType, 3, 10, true );
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
			if ( node.resources.Count > 0 )
				node.resources[0].Remove( false );
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
			noResize = true;
			base.Open( null, 0, 0, 360, 320 );
			name = "Build panel";

			int row = -20;
			var workshops = FindObjectsOfType<Workshop>( true );

			for ( int i = 0; i < (int)Workshop.Type.total; i++ )
			{
				var type = (Workshop.Type)i;
				if ( type.ToString().StartsWith( "_" ) )
					continue;
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
			Image button = Button( title ).Pin( x, y, 160 ).AddClickHandler( action );
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
			root.viewport.nodeInfoToShow = Viewport.NodeInfoType.possibleBuildings;
			Close();
		}

		void AddCrossing()
		{
			root.viewport.constructionMode = Viewport.Construct.crossing;
			root.viewport.nodeInfoToShow = Viewport.NodeInfoType.possibleBuildings;
			Close();
		}

		void AddStock()
		{
			root.viewport.constructionMode = Viewport.Construct.stock;
			root.viewport.nodeInfoToShow = Viewport.NodeInfoType.possibleBuildings;
			Close();
		}

		void AddGuardHouse()
		{
			root.viewport.constructionMode = Viewport.Construct.guardHouse;
			root.viewport.nodeInfoToShow = Viewport.NodeInfoType.possibleBuildings;
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
			root.viewport.workshopType = type;
			root.viewport.nodeInfoToShow = Viewport.NodeInfoType.possibleBuildings;
			Close();
		}
	}


	public class RoadPanel : Panel
	{
		public Road road;
		public List<ItemImage> leftItems = new List<ItemImage>(), rightItems = new List<ItemImage>(), centerItems = new List<ItemImage>();
		public List<Text> leftNumbers = new List<Text>(), rightNumbers = new List<Text>(), centerDirections = new List<Text>();
		public GroundNode node;
		public Dropdown targetWorkerCount;
		public Text jam;
		public Text workers;

		const int itemsDisplayed = 3;

		public static RoadPanel Create()
		{
			return new GameObject().AddComponent<RoadPanel>();
		}

		public void Open( Road road, GroundNode node )
		{
			borderWidth = 10;
			noResize = true;
			base.Open( road, 0, 0, 210, 165 );
			this.road = road;
			this.node = node;
			Image( iconTable.GetMediaData( Icon.hauler ) ).Pin( 170, -10 ).AddClickHandler( Hauler );
			Image( iconTable.GetMediaData( Icon.destroy ) ).Pin( 150, -10 ).AddClickHandler( Remove );
			Image( iconTable.GetMediaData( Icon.box ) ).Pin( 130, -10, 20, 20 ).AddClickHandler( Split );
			jam = Text( "Jam" ).Pin( 12, -4, 120 );
			workers = Text( "Worker count" ).Pin( 12, -28, 120 );
			name = "Road panel";
			targetWorkerCount = Dropdown().Pin( 20, -44, 150, 25 );
			targetWorkerCount.AddOptions( new List<string> { "Auto", "1", "2", "3", "4" } );
			targetWorkerCount.value = road.targetWorkerCount;
			targetWorkerCount.onValueChanged.AddListener( TargetWorkerCountChanged );

			for ( int i = 0; i < itemsDisplayed; i++ )
			{
				int row = i * (iconSize + 5 ) - 128;
				leftItems.Add( ItemIcon().Pin( 15, row ) );
				leftNumbers.Add( Text( "0" ).Pin( 40, row, 30 ) );
				rightNumbers.Add( Text( "0" ).Pin( 150, row ) );
				rightItems.Add( ItemIcon().Pin( 170, row ) );
				centerDirections.Add( Text().Pin( 80, row, 60 ) );
				centerItems.Add( ItemIcon().Pin( 90, row ) );
			}
#if DEBUG
			Selection.activeGameObject = road.gameObject;
#endif
		}

		void Remove()
		{
			if ( road )
				root.ExecuteRemoveRoad( road );
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
			if ( road && road.targetWorkerCount != newValue )
				root.ExecuteChangeRoadWorkerCount( road, newValue );
		}

		public override void Update()
		{
			base.Update();
			jam.text = "Items waiting: " + road.jam;
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
			if ( road )
				targetWorkerCount.value = road.targetWorkerCount;
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
			borderWidth = 10;
			noResize = true;
			if ( base.Open( flag, 0, 0, 250, 75 ) )
				return;

			this.flag = flag;
			int col = 16;
			Image( iconTable.GetMediaData( Icon.destroy ) ).Pin( 210, -45 ).AddClickHandler( Remove );
			Image( iconTable.GetMediaData( Icon.newRoad ) ).Pin( 20, -45 ).AddClickHandler( StartRoad );
			Image( iconTable.GetMediaData( Icon.magnet ) ).Pin( 45, -45 ).AddClickHandler( CaptureRoads );
			shovelingIcon = Image( iconTable.GetMediaData( Icon.shovel ) ).Pin( 65, -45 ).AddClickHandler( Flatten );
			convertIcon = Image( iconTable.GetMediaData( Icon.crossing ) ).Pin( 85, -45 ).AddClickHandler( Convert );

			for ( int i = 0; i < Flag.maxItems; i++ )
			{
				itemTimers[i] = Image().Pin( col, -8, iconSize, 3 );
				items[i] = ItemIcon().Pin( col, -13 );
				int j = i;
				items[i].name = "item " + i;
				col += iconSize+5;
			}
			name = "Flag panel";
			if ( show )
				root.world.eye.FocusOn( flag, true );
			Update();
		}

		void Remove()
		{
			if ( flag )
				root.ExecuteRemoveFlag( flag );
			Close();
		}

		void StartRoad()
		{
			if ( flag )
			{
				Road road = Road.Create().Setup( flag );
				root.viewport.inputHandler = road;
				root.viewport.showGridAtMouse = true;
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
						items[i].SetInTransit( false );
						int timeAtFlag = flag.items[i].atFlag.age;
						itemTimers[i].rectTransform.sizeDelta = new Vector2( Math.Min( iconSize, timeAtFlag / 3000 ), 3 );
						itemTimers[i].color = Color.Lerp( Color.green, Color.red, timeAtFlag / 30000f );
					}
					else
						items[i].SetInTransit( true );
				}
			}

			if ( flag.flattening != null )	// This should never be null unless after loaded old files.
				shovelingIcon.color = flag.flattening.worker ? Color.grey : Color.white;
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
		Text destinationBuilding;
		PathVisualization cartPath;

		public static WorkerPanel Create()
		{
			return new GameObject().AddComponent<WorkerPanel>();
		}

		public void Open( Worker worker, bool show )
		{
			var cart = worker as Stock.Cart;
			borderWidth = 10;
			noResize = true;
			if ( base.Open( worker.node, 0, 0, 200, cart ? 140 : 80 ) )
				return;
			name = "Worker panel";
			this.worker = worker;
			item = ItemIcon().Pin( 20, -20 );
			itemsInCart = Text().Pin( 45, -20, 150 );
			itemCount = Text( "Items" ).Pin( 20, -44, 160 );
			if ( cart )
			{
				Text( "From:" ).Pin( 20, -70, 50 );
				BuildingIcon( cart.building ).Pin( 70, -70 );
				Text( "To:" ).Pin( 20, -95, 50 );
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
					Destroy( destinationBuilding.gameObject );
					if ( cart.destination )
						destinationBuilding = BuildingIcon( cart.destination ).Pin( 70, -95, 80 );
					var path = cart.FindTaskInQueue<Worker.WalkToFlag>()?.path;
					Destroy( cartPath );
					cartPath = PathVisualization.Create().Setup( path, Interface.root.viewport.visibleAreaCenter );
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
		public ProgressBar progress;
		public Building.Construction construction;
		public WorkshopPanel.Buffer planks;
		public WorkshopPanel.Buffer stones;

		public static ConstructionPanel Create()
		{
			return new GameObject( "Contruction panel").AddComponent<ConstructionPanel>();
		}

		public void Open( Building.Construction construction, bool show = false )
		{
			base.Open( construction.boss, 150, 150 );
			this.construction = construction;
			Image( iconTable.GetMediaData( Icon.destroy ) ).Pin( 190, -150 ).AddClickHandler( Remove );

			Workshop workshop = construction.boss as Workshop;
			if ( workshop )
				Text( workshop.type.ToString() ).Pin( 20, -20, 160 );

			planks = new WorkshopPanel.Buffer();
			planks.Setup( this, Item.Type.plank, construction.boss.configuration.plankNeeded, 20, -40, iconSize + 5 );
			stones = new WorkshopPanel.Buffer();
			stones.Setup( this, Item.Type.stone, construction.boss.configuration.stoneNeeded, 20, -64, iconSize + 5 );

			progress = Progress().Pin( 20, -90, ( iconSize + 5 ) * 4 );

			if ( show )
				root.world.eye.FocusOn( construction.boss, true );
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
			progress.progress = construction.progress;
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
		public PathVisualization route;
		public Text stats;
		GameObject mapIcon;

		static public ItemPanel Create()
		{
			return new GameObject().AddComponent<ItemPanel>();
		}

		public void Open( Item item )
		{
			this.item = item;

			noResize = true;
			if ( base.Open( null, 0, 0, 300, 150 ) )
				return;

			name = "Item panel";

			Text( item.type.ToString() ).Pin( 15, -15, 100 );
			stats = Text().Pin( 15, -35, 250 );
			Text( "Origin:" ).Pin( 15, -55, 170 );
			BuildingIcon( item.origin ).Pin( 100, -55, 80 ).AddClickHandler( delegate { Destroy( route ); route = null; } );
			Text( "Destination:" ).Pin( 15, -75, 170 );
			BuildingIcon( item.destination ).Pin( 100, -75, 80 );

			mapIcon = new GameObject( "Map icon" );
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
				route = PathVisualization.Create().Setup( item.path, Interface.root.viewport.visibleAreaCenter );
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

	public class BuildingList : Panel
	{
		ScrollRect scroll;
		List<Building> buildings = new List<Building>();
		List<Text> productivities = new List<Text>();
		List<Text> outputs = new List<Text>();
		static string filter = "";
		List<List<Text>> inputs = new List<List<Text>>();
		static bool reversed;
		static Comparison<Building> comparison = CompareTypes;

		static public BuildingList Create()
		{
			return new GameObject().AddComponent<BuildingList>();
		}

		public void Open()
		{
			base.Open( null, 0, 0, 500, 420 );

			Text( "Filter:" ).Pin( 20, -20, 150 );
			var d = Dropdown().Pin( 80, -20, 150 );
			List<string> options = new List<string>();
			for ( int j = 0; j < (int)Workshop.Type.total; j++ )
			{
				string typeName = ((Workshop.Type)j).ToString();
				if ( !typeName.Contains( "Obsolete" ) )
					options.Add( typeName );
			}
			options.Add( "stock" );
			options.Add( "guardhouse" );
			options.Sort();
			options.Add( "all" );
			d.AddOptions( options );
			d.value = options.Count - 1;
			if ( options.Contains( filter ) )
				d.value = options.IndexOf( filter );
			d.onValueChanged.AddListener( delegate { SetFilter( d ); } );

			var t = Text( "type", 10 ).Pin( 20, -40, 150 ).AddClickHandler( delegate { ChangeComparison( CompareTypes ); } );
			var p = Text( "productivity", 10 ).Pin( 170, -40, 150 ).AddClickHandler( delegate { ChangeComparison( CompareProductivities ); } );
			var o = Text( "output", 10 ).Pin( 380, -40, 150 ).AddClickHandler( delegate { ChangeComparison( CompareOutputs ); } );
			var i = Text( "input", 10 ).Pin( 235, -40, 150 ).AddClickHandler( delegate { ChangeComparison( CompareInputs ); } );
			scroll = ScrollRect().Stretch( 20, 20, -20, -60 );

			Fill();
		}

		void SetFilter( Dropdown d )
		{
			filter = d.options[d.value].text;
			if ( filter == "all" )
			filter = "";
			Fill();
		}

		void ChangeComparison( Comparison<Building> comparison )
		{
			if ( BuildingList.comparison == comparison )
				reversed = !reversed;
			else
				reversed = true;
			BuildingList.comparison = comparison;
			Fill();
		}

		void Fill()
		{
			buildings = new List<Building>();
			foreach ( var building in Resources.FindObjectsOfTypeAll<Building>() )
			{
				if ( building.owner != root.mainPlayer || building.blueprintOnly || !building.title.Contains( filter ) )
					continue;
				buildings.Add( building );
			}

			buildings.Sort( comparison );
			if ( reversed )
				buildings.Reverse();
			productivities.Clear();
			outputs.Clear();
			inputs.Clear();

			foreach ( Transform child in scroll.content )
				Destroy( child.gameObject );

			for ( int i = 0; i < buildings.Count; i++ )
			{
				BuildingIcon( buildings[i] ).Link( scroll.content ).Pin( 0, -iconSize * i, 80 );
				productivities.Add( Text( "" ).AddOutline().Link( scroll.content ).Pin( 150, -iconSize * i, 100 ) );
				outputs.Add( Text( "" ).Link( scroll.content ).Pin( 385, -iconSize * i, 50 ) );
				inputs.Add( new List<Text>() );
				if ( buildings[i] is Workshop workshop )
				{
					ItemIcon( workshop.productionConfiguration.outputType ).Link( scroll.content ).Pin( 360, -iconSize * i	);
					int bi = 0;
					foreach ( var buffer in workshop.buffers )
					{
						ItemIcon( buffer.itemType ).Link( scroll.content ).Pin( 215 + bi * 35, -iconSize * i );
						inputs[i].Add( Text( "0" ).Link( scroll.content ).Pin( 240 + bi * 35, -iconSize * i, 50 ) );
						bi++;
					}
				}
			}
			SetScrollRectContentSize( scroll, 0, iconSize * buildings.Count );
		}

		new public void Update()
		{
			for ( int i = 0; i < buildings.Count; i++ )
			{
				if ( buildings[i] is Workshop workshop )
				{
					WorkshopPanel.UpdateProductivity( productivities[i], workshop );
					outputs[i].text = workshop.output.ToString();
					for ( int j = 0; j < workshop.buffers.Count; j++ )
						inputs[i][j].text = workshop.buffers[j].stored.ToString();
				}
			}
			base.Update();
		}

		static int CompareProductivities( Building a, Building b )
		{
			float ap = a is Workshop workshopA ? workshopA.productivity.current : -1;
			float bp = b is Workshop workshopB ? workshopB.productivity.current : -1;
			return ap.CompareTo( bp );
		}

		static int CompareOutputs( Building a, Building b )
		{
			int ao = a is Workshop workshopA ? workshopA.output : -1;
			int bo = b is Workshop workshopB ? workshopB.output : -1;
			return ao.CompareTo( bo );
		}

		static int CompareInputs( Building a, Building b )
		{
			int ai = 0, bi = 0;
			if ( a is Workshop workshopA )
			{
				foreach ( var buffer in workshopA.buffers )
					ai += buffer.stored;
			}
			else
				ai = -1;
			if ( b is Workshop workshopB )
			{
				foreach ( var buffer in workshopB.buffers )
					bi += buffer.stored;
			}
			else
				bi = -1;
			return ai.CompareTo( bi );
		}

		static int CompareTypes( Building a, Building b )
		{
			return a.name.CompareTo( b.name );
		}
	}

	public class Viewport : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IInputHandler
	{
		public bool mouseOver;
		public GameObject cursor;
		IInputHandler inputHandlerData;
		readonly GameObject[] cursorTypes = new GameObject[(int)CursorType.total];
		//readonly GameObject cursorFlag;
		//readonly GameObject cursorBuilding;
		public new Camera camera;
		public Vector3 lastMouseOnGround;
		static int gridMaskXID;
		static int gridMaskZID;
		public bool showGridAtMouse;
		public NodeInfoType nodeInfoToShow;
		static readonly List<BuildPossibility> buildCategories = new List<BuildPossibility>();
		public HiveObject currentBlueprint;
		public WorkshopPanel currentBlueprintPanel;
		public GroundNode currentNode;  // Node currently under the cursor
		static GameObject marker;
		public bool markEyePosition;

		public enum Construct
		{
			nothing,
			workshop,
			stock,
			guardHouse,
			flag,
			crossing
		}

		public enum NodeInfoType
		{
			none,
			possibleBuildings,
			undergroundResources
		}

		public Construct constructionMode = Construct.nothing;
		public Workshop.Type workshopType;
		public int currentFlagDirection = 1;    // 1 is a legacy value.

		struct BuildPossibility
		{
			public Building.Configuration configuration;
			public Material material;
			public Mesh mesh;
			public float scale;
		}

		public IInputHandler inputHandler
		{
			get { return inputHandlerData; }
			set
			{
				if ( inputHandlerData == value )
					return;
				inputHandlerData?.OnLostInput();
				inputHandlerData = value;
			}

		}

		static public Viewport Create()
		{
			return new GameObject().AddComponent<Viewport>();
		}


		public void Start()
		{
			transform.SetParent( root.transform );
			transform.SetSiblingIndex( 0 );
			name = "Viewport";

			Image image = gameObject.AddComponent<Image>();
			image.rectTransform.anchorMin = Vector2.zero;
			image.rectTransform.anchorMax = Vector2.one;
			image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
			image.color = new Color( 1, 1, 1, 0 );

			inputHandler = this;
			marker.transform.SetParent( transform );
		}

		public bool ResetInputHandler()
		{
			if ( inputHandler == this as IInputHandler )
			{
				if ( constructionMode != Construct.nothing )
				{
					constructionMode = Construct.nothing;
					CancelBlueprint();
					nodeInfoToShow = NodeInfoType.none;
					return true;
				}
				return false;
			}
			inputHandler = this;
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
			if ( currentBlueprintPanel )
				currentBlueprintPanel.Close();
			currentBlueprintPanel = null;
		}

		public static void Initialize()
		{
			gridMaskXID = Shader.PropertyToID( "_GridMaskX" );
			gridMaskZID = Shader.PropertyToID( "_GridMaskZ" );

			var greenMaterial = new Material( World.defaultShader )     { color = new Color( 0.5f, 0.5f, 0.35f ) };
			var blueMaterial = new Material( World.defaultShader )      { color = new Color( 0.3f, 0.45f, 0.6f ) };
			var yellowMaterial = new Material( World.defaultShader )    { color = new Color( 177 / 255f, 146 / 255f, 97 / 255f ) };
			var orangeMaterial = new Material( World.defaultShader )    { color = new Color( 191 / 255f, 134 / 255f, 91 / 255f ) };
			var greyMaterial = new Material( World.defaultShader )      { color = Color.grey };
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

			marker = Instantiate<GameObject>( Resources.Load<GameObject>( "prefabs/others/marker" ) );
			marker.name = "Viewport center marker";
			marker.transform.localScale = Vector3.one * 2;
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

		public Vector3 visibleAreaCenter
		{
			// TODO This should be way cleaner
			get
			{
				if ( World.instance.eye.camera.enabled )
					return World.instance.eye.visibleAreaCenter;
				else
					return camera.transform.position;
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

			if ( hiveObject is Ground.Block || hiveObject == ground )
			{
				Vector3 localPosition = ground.transform.InverseTransformPoint( hit.point );
				return GroundNode.FromPosition( localPosition, ground );
			}
			
			return hiveObject;
		}

		public GroundNode FindNodeAt( Vector3 screenPosition )
		{
			RaycastHit hit = new RaycastHit();
			if ( camera == null )
				camera = World.instance.eye.camera;
			Ray ray = camera.ScreenPointToRay( screenPosition );

			foreach ( var block in World.instance.ground.blocks )
			{
				var c = block.collider;
				if ( c == null )
					continue;

				if ( c.Raycast( ray, out hit, 1000 ) ) // TODO How long the ray should really be?
					break;
			}

			if ( hit.collider == null )
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
				if ( currentBlueprint is Building building )
					root.RegisterCreateBuilding( building );
				if ( currentBlueprint is Flag flag )
					root.RegisterCreateFlag( flag );
				currentBlueprint = null;
				currentBlueprintPanel?.Close();
				currentBlueprintPanel = null;
				constructionMode = Construct.nothing;
				nodeInfoToShow = NodeInfoType.none;
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

		public void Update()
		{
			if ( GetKeyDown( KeyCode.Alpha1 ) )
				BuildPanel.Create().Open();
			if ( GetKeyDown( KeyCode.Alpha2 ) )
				showGridAtMouse = !showGridAtMouse;
			if ( GetKeyDown( KeyCode.Alpha5 ) )
				ShowNearestPossibleConstructionSite( false );
			if ( GetKeyDown( KeyCode.Alpha6 ) )
				ShowNearestPossibleConstructionSite( true );
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
			{
				if ( nodeInfoToShow == NodeInfoType.possibleBuildings )
					nodeInfoToShow = NodeInfoType.none;
				else
					nodeInfoToShow = NodeInfoType.possibleBuildings;
			}
			if ( GetKeyDown( KeyCode.Alpha4 ) )
			{
				if ( nodeInfoToShow == NodeInfoType.undergroundResources )
					nodeInfoToShow = NodeInfoType.none;
				else
					nodeInfoToShow = NodeInfoType.undergroundResources;
			}
			if ( inputHandler == null || inputHandler.Equals( null ) )
				inputHandler = this;

			if ( markEyePosition && mouseOver )
			{
				marker.SetActive( true );
				var eye = root.world.eye;
				marker.transform.position = eye.position + Vector3.up * ( ( float )( root.world.ground.GetHeightAt( eye.x, eye.y ) + 1.5f * Math.Sin( 2 * Time.time ) ) );
				marker.transform.rotation = Quaternion.Euler( 0, Time.time * 200, 0 );
			}
			else
				marker.SetActive( false );

			if ( !mouseOver )
				return;
			currentNode = FindNodeAt( Input.mousePosition );
			if ( cursor && currentNode )
			{
				cursor.transform.localPosition = currentNode.position;
				cursor.transform.SetParent( currentNode.ground.FindClosestBlock( currentNode ).transform, false );
			}
			if ( !inputHandler.OnMovingOverNode( currentNode ) )
				inputHandler = this;
#if DEBUG
			if ( GetKeyDown( KeyCode.PageUp ) && currentNode )
				currentNode.SetHeight( currentNode.height + 0.05f );
			if ( GetKeyDown( KeyCode.PageDown ) && currentNode )
				currentNode.SetHeight( currentNode.height - 0.05f );
#endif
			if ( nodeInfoToShow != NodeInfoType.none && currentNode )
			{
				foreach ( var o in Ground.areas[6] )
				{
					var n = currentNode + o;
					if ( nodeInfoToShow == NodeInfoType.possibleBuildings )
					{
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
					if ( nodeInfoToShow == NodeInfoType.undergroundResources )
					{
						foreach ( var resource in n.resources )
						{

							if ( n.owner != root.mainPlayer || !resource.underGround )
								continue;
							var itemType = Resource.ItemType( resource.type );
							var body = Item.looks.GetMediaData( itemType );
							var renderer = body.GetComponent<MeshRenderer>();
							var meshFilter = body.GetComponent<MeshFilter>();
							World.DrawObject( body, Matrix4x4.TRS( n.position + Vector3.up * 0.2f, Quaternion.identity, Vector3.one * 0.3f ) );
							break;
						}
					}
				}
			}
		}

		void ShowNearestPossibleConstructionSite( bool anyDirection )
		{
			List<int> possibleDirections = new List<int>();
			if ( anyDirection )
			{
				for ( int i = 0; i < GroundNode.neighbourCount; i++ )
					possibleDirections.Add( i );
			}
			else
				possibleDirections.Add( currentFlagDirection );

			GroundNode bestSite = null;
			int bestDistance = int.MaxValue;
			int bestFlagDirection = -1;
			foreach ( var o in Ground.areas[Ground.maxArea - 1] )
			{
				GroundNode node = currentNode + o;
				foreach ( int flagDirection in possibleDirections )
				{
					bool suitable = false;
					switch ( constructionMode )
					{
						case Construct.workshop:
						{
							suitable = Workshop.IsNodeSuitable( node, root.mainPlayer, Workshop.GetConfiguration( workshopType ), flagDirection );
							break;
						}
						case Construct.stock:
						{
							suitable = Stock.IsNodeSuitable( node, root.mainPlayer, flagDirection );
							break;
						}
						case Construct.guardHouse:
						{
							suitable = GuardHouse.IsNodeSuitable( node, root.mainPlayer, flagDirection );
							break;
						}
						default:
						{
							break;
						}
					}
					if ( suitable )
					{
						int distance = node.DistanceFrom( currentNode );
						if ( distance < bestDistance )
						{
							bestDistance = distance;
							bestSite = node;
							bestFlagDirection = flagDirection;
						}
					}
				}
			}
			if ( bestSite )
			{
				root.world.eye.FocusOn( bestSite, true, true );
				currentFlagDirection = bestFlagDirection;
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
				if ( currentBlueprint && currentBlueprint.location != node )
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
		float timeSpeedToRestore;
		Comparison<Item> lastComparison;
		bool reversed;

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
			timeSpeedToRestore = World.instance.timeFactor;
			World.instance.SetTimeFactor( 0 );

			Text( "Origin" ).Pin( 50, -20, 100 ).AddClickHandler( delegate { Fill( CompareByOrigin ); } );
			Text( "Destination" ).Pin( 150, -20, 100 ).AddClickHandler( delegate { Fill( CompareByDestination ); } );
			Text( "Age" ).Pin( 250, -20, 100 ).AddClickHandler( delegate { Fill( CompareByAge ); } );
			Text( "Route" ).Pin( 300, -20, 100 ).AddClickHandler( delegate { Fill( CompareByPathLength ); } );

			scroll = ScrollRect().Stretch( 20, 20, -20, -40 );
			Fill( CompareByAge );
		}

		public override void Close()
		{
			base.Close();
			World.instance.SetTimeFactor( timeSpeedToRestore );
		}

		void Fill( Comparison<Item> comparison )
		{
			int row = 0;
			foreach ( Transform child in scroll.content )
				Destroy( child.gameObject );

			if ( comparison == lastComparison )
				reversed = !reversed;
			else
				reversed = true;
			lastComparison = comparison;

			List<Item> sortedItems = new List<Item>();
			foreach ( var item in player.items )
			{
				if ( item )
					sortedItems.Add( item );
			}
			sortedItems.Sort( comparison );
			if ( reversed )
				sortedItems.Reverse();

			foreach ( var item in sortedItems )
			{
				var i = ItemIcon( Item.Type.unknown ).Link( scroll.content ).Pin( 0, row );
				i.SetItem( item );

				BuildingIcon( item.origin ).Link( scroll.content ).Pin( 30, row, 80 );
				BuildingIcon( item.destination ).Link( scroll.content ).Pin( 130, row, 80 );
				Text( ( item.life.age / 50 ).ToString() ).Link( scroll.content ).Pin( 230, row, 50 );
				if ( item.path )
					Text( item.path.roadPath.Count.ToString() ).Link( scroll.content ).Pin( 280, row, 30 );				
				row -= iconSize + 5;
			}
			SetScrollRectContentSize( scroll, 0, sortedItems.Count * ( iconSize + 5 ) );
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

			if ( A.node.id == B.node.id )
				return 0;
			if ( A.node.id < B.node.id )
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

			Text( "Type" ).Pin( 50, -20, 100 ).AddClickHandler( delegate { Fill( CompareByType ); } );
			Text( "Last" ).Pin( 150, -20, 100 ).AddClickHandler( delegate { Fill( CompareByLastMined ); } );
			Text( "Ready" ).Pin( 250, -20, 100 );
			scroll = ScrollRect().Stretch( 20, 20, -20, -40 );
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
				Text( resource.type.ToString() ).Link( scroll.content ).Pin( 30, row, 100 ).AddClickHandler( delegate { GroundNode node = resource.node; NodePanel.Create().Open( node, true ); } );
				Text( ( resource.gathered.age / 50 ).ToString() ).Link( scroll.content ).Pin( 130, row, 50 );
				Text( resource.keepAway.inProgress ? "no" : "yes" ).Link( scroll.content ).Pin( 230, row, 30 );
				row -= iconSize + 5;
			}
			SetScrollRectContentSize( scroll, 0, sortedResources.Count * ( iconSize + 5 ) );
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
	public class LogisticList : Panel
	{
		ScrollRect scroll;
		Building building;
		Item.Type itemType;
		ItemDispatcher.Potential.Type direction;
		float timeSpeedToRestore;
		bool filled;

		public static LogisticList Create()
		{
			return new GameObject().AddComponent<LogisticList>();
		}

		public void Open( Building building, Item.Type itemType, ItemDispatcher.Potential.Type direction )
		{
			timeSpeedToRestore = World.instance.timeFactor;
			World.instance.SetTimeFactor( 0 );
			root.mainPlayer.itemDispatcher.queryBuilding = this.building = building;
			root.mainPlayer.itemDispatcher.queryItemType = this.itemType = itemType;
			root.mainPlayer.itemDispatcher.queryType = this.direction = direction;

			if ( base.Open( null, 0, 0, 540, 320 ) )
				return;
			name = "Logistic list panel";

			Text( "List of potentials for       at", 15 ).Pin( 20, -20, 250 );
			ItemIcon( itemType ).Pin( 150, -20 );
			BuildingIcon( building, 15 ).Pin( 190, -20, 100 );

			Text( "Building", 10 ).Pin( 20, -40, 100 );
			Text( "Distance", 10 ).Pin( 100, -40, 100 );
			Text( "Direction", 10 ).Pin( 140, -40, 100 );
			Text( "Priority", 10 ).Pin( 190, -40, 100 );
			Text( "Quantity", 10 ).Pin( 230, -40, 100 );
			Text( "Result", 10 ).Pin( 270, -40, 100 );

			scroll = ScrollRect().Stretch( 20, 20, -20, -60 );
		}

		new public void OnDestroy()
		{
			base.OnDestroy();
			root.mainPlayer.itemDispatcher.queryBuilding = null;
			root.mainPlayer.itemDispatcher.queryItemType = Item.Type.unknown;
			World.instance.SetTimeFactor( timeSpeedToRestore );
		}

		new public void Update()
		{
			base.Update();
			if ( root.mainPlayer.itemDispatcher.results != null && !filled )
			{
				filled = true;
				Fill();
				root.mainPlayer.itemDispatcher.queryBuilding = this.building = null;
				root.mainPlayer.itemDispatcher.queryItemType = this.itemType = Item.Type.unknown;
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
					BuildingIcon( result.building ).Link( scroll.content ).Pin( 0, row, 80 );
					Text( result.building.node.DistanceFrom( building.node ).ToString() ).Link( scroll.content ).Pin( 100, row, 50 );
					Text( result.incoming ? "Out" : "In" ).Link( scroll.content ).Pin( 130, row, 50 );
					Text( result.priority.ToString() ).Link( scroll.content ).Pin( 170, row, 50 );
					Text( result.quantity.ToString() ).Link( scroll.content ).Pin( 210, row, 50 );
				}
				string message = result.result switch
				{
					ItemDispatcher.Result.flagJam => "Jam at output flag",
					ItemDispatcher.Result.match => "Matched",
					ItemDispatcher.Result.noDispatcher => "Dispatcher is not free",
					ItemDispatcher.Result.notInArea => "Outside of area",
					ItemDispatcher.Result.tooLowPriority => "Combined priority is too low",
					ItemDispatcher.Result.outOfItems => "Out of items",
					_ => "Unknown"
				};
				if ( result.remote )
				{
					message = result.result switch
					{
						ItemDispatcher.Result.flagJam => "Jam at their output flag",
						ItemDispatcher.Result.noDispatcher => "Their dispatcher is not free",
						ItemDispatcher.Result.notInArea => "Their area excludes this",
						ItemDispatcher.Result.outOfItems => "They are out of items",
						_ => message
					};
				}
				if ( result.remote && !result.incoming && result.result == ItemDispatcher.Result.outOfItems )
					message = "Full";
				if ( !result.remote && result.result == ItemDispatcher.Result.outOfItems )
				{
					if ( direction == ItemDispatcher.Potential.Type.offer )
						message = "Out of items";
					else
						message = "Full";
				}
				Text( message ).Link( scroll.content ).Pin( 250, row, 200, 40 );
				row -= iconSize + 5;
			}
			SetScrollRectContentSize( scroll, 0, root.mainPlayer.itemDispatcher.results.Count * ( iconSize + 5 ) );
		}
	}

	public class ItemStats : Panel
	{
		ScrollRect scroll;
		Player player;
		Text finalEfficiency;
		static Comparison<int> currentComparison;
		static bool reverse = false;
		readonly Text[] inStock = new Text[(int)Item.Type.total];
		readonly Text[] onWay = new Text[(int)Item.Type.total];
		readonly Text[] surplus = new Text[(int)Item.Type.total];
		readonly Text[] production = new Text[(int)Item.Type.total];
		readonly Text[] weight = new Text[(int)Item.Type.total];
		readonly Text[] efficiency = new Text[(int)Item.Type.total];
		readonly Button[] stockButtons = new Button[(int)Item.Type.total];
		readonly ItemImage[] itemIcon = new ItemImage[(int)Item.Type.total];

		int[] inStockCount = new int[(int)Item.Type.total];
		int[] onWayCount = new int[(int)Item.Type.total];

		public static ItemStats Create()
		{
			return new GameObject().AddComponent<ItemStats>();
		}

		public void Open( Player player )
		{
			if ( base.Open( null, 0, 0, 420, 300 ) )
				return;

			name = "Item stats panel";
			this.player = player;
			UIHelpers.currentColumn = 50;

			Text( "In stock", 10 ).
			PinSideways( 0, -20, 50, 20 ).
			AddClickHandler( delegate { SetOrder( CompareInStock ); } );

			Text( "On Road", 10 ).
			PinSideways( 0, -20, 50, 20 ).
			AddClickHandler( delegate { SetOrder( CompareOnRoad ); } );

			Text( "Surplus", 10 ).
			PinSideways( 0, -20, 50, 20 ).
			AddClickHandler( delegate { SetOrder( CompareSurplus ); } );

			Text( "Per minute", 10 ).
			PinSideways( 0, -20, 50, 20 ).
			AddClickHandler( delegate { SetOrder( ComparePerMinute ); } );

			Text( "Weight", 10 ).
			PinSideways( 0, -20, 50, 20 ).
			AddClickHandler( delegate { SetOrder( CompareWeight ); } );

			Text( "Efficiency", 10 ).
			PinSideways( 0, -20, 50, 20 ).
			AddClickHandler( delegate { SetOrder( CompareEfficiency ); } );

			scroll = ScrollRect().Stretch( 20, 40, -20, -40 );
			finalEfficiency = Text().Pin( 100, 40, 200, 40, 0, 0 );
			finalEfficiency.fontSize = (int)( uiScale * 16 );

			for ( int i = 0; i < inStock.Length; i++ )
			{
				int row = i * - ( iconSize + 5 );
				itemIcon[i] = ItemIcon( (Item.Type)i ).Link( scroll.content ).Pin( 0, row );
				inStock[i] = Text( "0" ).AddOutline().Link( scroll.content ).Pin( 30, row, 40, iconSize );
				stockButtons[i] = inStock[i].gameObject.AddComponent<Button>();
				onWay[i] = Text( "0" ).AddOutline().Link( scroll.content ).Pin( 80, row, 40 );
				surplus[i] = Text( "0" ).AddOutline().Link( scroll.content ).Pin( 130, row, 40 );
				production[i] = Text( "0" ).AddOutline().Link( scroll.content ).Pin( 180, row, 40 );
				weight[i] = Text( "0" ).AddOutline().Link( scroll.content ).Pin( 230, row, 40 );
				efficiency[i] = Text( "0" ).AddOutline().Link( scroll.content ).Pin( 280, row, 40 );
			}

			SetScrollRectContentSize( scroll, 0, (int)Item.Type.total * ( iconSize + 5 ) );
		}

		int CompareInStock( int a, int b )
		{
			return inStockCount[a].CompareTo( inStockCount[b] );
		}

		int CompareOnRoad( int a, int b )
		{
			return onWayCount[a].CompareTo( onWayCount[b] );
		}

		int CompareSurplus( int a, int b )
		{
			return player.surplus[a].CompareTo( player.surplus[b] );
		}

		int ComparePerMinute( int a, int b )
		{
			return player.itemEfficiencyHistory[a].production.CompareTo( player.itemEfficiencyHistory[b].production );
		}

		int CompareWeight( int a, int b )
		{
			return player.itemEfficiencyHistory[a].weight.CompareTo( player.itemEfficiencyHistory[b].weight );
		}

		int CompareEfficiency( int a, int b )
		{
			var ai = player.itemEfficiencyHistory[a];
			var bi = player.itemEfficiencyHistory[b];
			float ae = ai.weight == 0 ? -1 : ai.weighted;
			float be = bi.weight == 0 ? -1 : bi.weighted;
			return ae.CompareTo( be );
		}

		void SetOrder( Comparison<int> comparison )
		{
			if ( currentComparison == comparison )
				reverse =! reverse;
			else
			{
				currentComparison = comparison;
				reverse = false;
			}
		}

		public new void Update()
		{
			base.Update();
			for ( int i = 0; i < inStockCount.Length; i++ )
			{
				inStockCount[i] = 0;
				onWayCount[i] = 0;
			}
			Stock[] richestStock = new Stock[(int)Item.Type.total];
			int[] maxStockCount = new int[(int)Item.Type.total];
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

			foreach ( var item in player.items )
			{
				if ( item == null )
					continue;
				onWayCount[(int)item.type]++;
			}

			List<int> order = new List<int>();
			for ( int i = 0; i < inStock.Length; i++ )
				order.Add( i );
			if ( currentComparison != null )
				order.Sort( currentComparison );
			if ( reverse )
				order.Reverse();

			for ( int i = 0; i < inStock.Length; i++ )
			{
				Color textColor = Color.yellow;
				if ( player.itemEfficiencyHistory[i].weight != 0 )
					textColor = Color.green;
				if ( (int)player.worseItemType == order[i] )
					textColor = Color.red;
				surplus[i].color = weight[i].color = inStock[i].color = onWay[i].color = production[i].color = efficiency[i].color = textColor;

				itemIcon[i].SetType( (Item.Type)order[i] );
				inStock[i].text = inStockCount[order[i]].ToString();
				stockButtons[i].onClick.RemoveAllListeners();
				Stock stock = richestStock[order[i]];
				stockButtons[i].onClick.AddListener( delegate { SelectBuilding( stock ); } );
				onWay[i].text = onWayCount[order[i]].ToString();
				surplus[i].text = player.surplus[order[i]].ToString();

				var itemData = player.itemEfficiencyHistory[order[i]];
				production[i].text = itemData.current.ToString( "n2" );
				weight[i].text = itemData.weight.ToString( "n2" );
				efficiency[i].text = itemData.weight == 0 ? "-" : itemData.weighted.ToString( "n2" );
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
		public float scale = 1;

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
			for ( int i = 0; i < (int)Item.Type.total; i++ )
			{
				var t = (Item.Type)i;
				ItemIcon( (Item.Type)i ).Pin( 20 + i * iconSize, -20 ).AddClickHandler( delegate { selected = t; lastAverageEfficiency = -1; } );
			}
			Image( iconTable.GetMediaData( Icon.summa ) ).Pin( 400, -20 ).AddClickHandler( delegate { selected = Item.Type.total; lastAverageEfficiency = -1; } );
			itemFrame = Image( iconTable.GetMediaData( Icon.tinyFrame ) ).Pin( 17, -17, 26, 26 );
			chart = Image().Pin( 20, -40, 410, 240 );
			record = Text().Pin( 25, -45, 150 );
			record.color = Color.yellow;
			selected = Item.Type.total;
			lastAverageEfficiency = -1;
		}

		int PerMinuteToPixel( float perMinute )
		{
			return (int)( perMinute * scale );
		}

		float PixelToPerMinute( int pixel )
		{
			return pixel / scale;
		}

		new public void Update()
		{
			// TODO Clean up this function, its a mess
			base.Update();
			var a = player.averageEfficiencyHistory;
			if ( selected < Item.Type.total )
				a = player.itemEfficiencyHistory[(int)selected];

			if ( chart.Contains( Input.mousePosition ) )
			{
				Vector3[] corners = new Vector3[4];
				chart.rectTransform.GetWorldCorners( corners );
				var cursorInsideChart = Input.mousePosition - corners[0];
				int ticks = Player.efficiencyUpdateTime * (int)( corners[2].x - Input.mousePosition.x );
				var hours = ticks / 60 / 60 / 50;
				string time = $"{(ticks/60/50)%60} minutes ago";
				if ( hours > 1 )
					time = $"{hours} hours and " + time;
				else if ( hours == 1 )
					time = "1 hour and " + time;
				TooltipSource.Set( chart, $"{time}\n{PixelToPerMinute( (int)cursorInsideChart.y )} per minute" );
			}

			if ( lastAverageEfficiency == player.averageEfficiencyHistory.current )
				return;

			var t = new Texture2D( (int)chart.rectTransform.sizeDelta.x, (int)chart.rectTransform.sizeDelta.y );
			for ( int x = 0; x < t.width; x++ )
			{
				for ( int y = 0; y < t.height; y++ )
					t.SetPixel( x, y, Color.black );
			}
			float max = float.MinValue;
			for ( int i = a.data.Count - t.width; i < a.data.Count; i++ )
			{
				if ( i < 0 )
					continue;
				if ( a.data[i] > max )
					max = a.data[i];
			}
			max = Math.Max( max, 0.0001f );
			int tickPerBuilding = 2000, workshopCount = 0;
			foreach ( var c in Workshop.configurations )
			{
				if ( c.outputType == selected && c.productionTime != 0 )
				{
					tickPerBuilding = c.productionTime / c.outputStackSize;
					foreach ( var w in Resources.FindObjectsOfTypeAll<Workshop>() ) 
					if ( w.owner == root.mainPlayer && w.type == c.type )
						workshopCount++;
					break;
				}
			}
			float itemsPerMinuteInOneWorkshop = 3000f / tickPerBuilding;
			scale = t.height / max;
			if ( workshopCount * itemsPerMinuteInOneWorkshop != 0 )
				scale = t.height / ( workshopCount * itemsPerMinuteInOneWorkshop );
			Color hl = Color.grey;
			float hi = itemsPerMinuteInOneWorkshop;
			if ( PerMinuteToPixel( hi ) >= t.height - 1 )
			{
				hl = Color.grey.Dark();
				hi = hi / 4;
			}
			float yb = hi;
			while ( PerMinuteToPixel( yb ) < t.height )
			{
				HorizontalLine( PerMinuteToPixel( yb ), hl );
				yb += hi;
			}
			int row = -1;
			void VerticalLine( int x, Color c )
			{
				for ( int y = 0; y < t.height; y++ )
					t.SetPixel( x, y, c );
			}
			void HorizontalLine( int y, Color c )
			{
				for ( int x = 0; x < t.width; x++ )
					t.SetPixel( x, y, c );
			}
			int xh = t.width - ( World.instance.time % World.hourTickCount ) / Player.efficiencyUpdateTime;
			while ( xh >= 0 )
			{
				VerticalLine( xh, Color.grey );
				xh -= World.hourTickCount / Player.efficiencyUpdateTime;
			}

			int recordColumn = t.width - ( a.data.Count - a.recordIndex );
			if ( recordColumn >= 0 )
				VerticalLine( recordColumn, Color.Lerp( Color.grey, Color.white, 0.5f ) );

			for ( int x = t.width - 1; x >= 0; x-- )
			{
				int index = a.data.Count - t.width + x;
				if ( 0 <= index )
				{
					int newRow = (int)Math.Min( (float)t.height - 1, PerMinuteToPixel( a.data[index] ) );
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
			itemFrame.PinCenter( 30 + (int)selected * iconSize, -30 );
			lastAverageEfficiency = player.averageEfficiencyHistory.current;
		}
	}

	public class WorldProgressPanel : Panel
	{
		Text worldTime;
		Text currentEfficiency;
		Text recordEfficiency;
		ProgressBar efficiencyProgress;
		float originalSpeed = -1;

		public static WorldProgressPanel Create()
		{
			return new GameObject( "World Progress Panel" ).AddComponent<WorldProgressPanel>();
		}

		public void Open( bool victory = false )
		{
			if ( base.Open( 200, 200 ) )
				return;
			name = "World Progress Panel";
			this.Pin( -200, 200, -100, 100, 0.5f, 0.5f );
			UIHelpers.currentRow = -30;
			if ( victory )
			{
				var t = Text( "VICTORY!" );
				t.PinDownwards( -100, 100, -30, 0, 0.5f );
				t.color = Color.red;
				t.alignment = TextAnchor.MiddleCenter;
				originalSpeed = root.world.timeFactor;
				root.world.eye.FocusOn( root.mainPlayer.mainBuilding.flag.node, true );
				root.world.SetTimeFactor( 0 );
			}
			worldTime = Text().PinDownwards( -200, 200, -30, 0, 0.5f );
			worldTime.alignment = TextAnchor.MiddleCenter;
			Text( $"Efficiency goal: {World.instance.efficiencyGoal}" ).
			PinDownwards( -200, 200, -iconSize, 0, 0.5f ).alignment = TextAnchor.MiddleCenter;
			recordEfficiency = Text().PinDownwards( -200, 200, -iconSize, 0, 0.5f );
			recordEfficiency.alignment = TextAnchor.MiddleCenter;
			currentEfficiency = Text().PinDownwards( -200, 200, -iconSize, 0, 0.5f );
			currentEfficiency.alignment = TextAnchor.MiddleCenter;
			efficiencyProgress = Progress().PinDownwards( -100, 100, -iconSize, 0, 0.5f );
		}

		new public void Update()
		{
			var t = World.instance.time;
			worldTime.text = $"World time: {t / 24 / 60 / 60 / 50}:{( ( t / 60 / 60 / 50 ) % 24 ).ToString( "D2" )}:{( ( t / 60 / 50) % 60 ).ToString( "D2" )}";
			recordEfficiency.text = $"Record efficiency: {root.mainPlayer.averageEfficiencyHistory.record.ToString()}";
			currentEfficiency.text = $"Current efficiency: {root.mainPlayer.averageEfficiency.ToString()}";
			efficiencyProgress.progress = root.mainPlayer.averageEfficiency / World.instance.efficiencyGoal;
			base.Update();
		}

		new public void OnDestroy()
		{
			if ( originalSpeed > 0 )
				root.world.SetTimeFactor( originalSpeed );
			root.world.eye.ReleaseFocus( null, true );
			base.OnDestroy();
		}
	}

	public class MainPanel : Panel
	{
		InputField seed;
		InputField saveName;
		Dropdown loadNames;
		FileSystemWatcher watcher;
		Dropdown size;
		bool loadNamesRefreshNeeded = true;
		static int savedSize = 1;
		Eye grabbedEye;

		public static MainPanel Create()
		{
			return new GameObject( "Main Panel", typeof( RectTransform ) ).AddComponent<MainPanel>();
		}

		public void Open( bool focusOnMainBuilding = false )
		{
			noCloseButton = true;
			noResize = true;
			Open( null, 0, 0, 300, 230 );
			this.PinCenter( 0, 0, 300, 230, 0.5f, 0.3f );

			Button( "Continue" ).PinCenter( 0, -34, 100, 25, 0.5f ).AddClickHandler( Close );
			Image().PinCenter( 0, -50, 260, 1, 0.5f ).color = Color.black;

			Button( "Start New World" ).PinCenter( 0, -67, 120, 25, 0.5f ).AddClickHandler( StartNewGame );
			Text( "Seed", 12 ).Pin( 20, -85, 40, 20 );
			seed = InputField().Pin( 60, -80, 100, 25 );
			seed.contentType = UnityEngine.UI.InputField.ContentType.IntegerNumber;
			Button( "Randomize" ).Pin( 165, -83, 60, 25 ).AddClickHandler( RandomizeSeed );
			Text( "Size", 12 ).Pin( 20, -115, 30 );
			size = Dropdown().Pin( 60, -110, 80, 25 );
			size.ClearOptions();
			size.AddOptions( new List<string>() { "Small (24x24)", "Medium (32x32)", "Big (48x48)" } );
			size.value = savedSize;
			Image().PinCenter( 0, -140, 260, 1, 0.5f ).color = Color.black;

			Button( "Load" ).Pin( 20, -148, 60, 25 ).AddClickHandler( Load );
			loadNames = Dropdown().Pin( 80, -145, 200, 25 );
			Image().Pin( 20, -173, 260, 1 ).color = Color.black;

			Button( "Save" ).Pin( 20, -178, 60, 25 ).AddClickHandler( Save );
			saveName = InputField().Pin( 80, -180, 100, 25 );
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
			root.world.settings.size = size.value switch 
			{
				0 => 24,
				1 => 32,
				2 => 48,
				_ => 32
			};
			if ( size.value == 0 )
				root.world.settings.maxHeight = 3;
			if ( size.value == 2 )
				root.world.settings.randomness = 2.1f;
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

public static class UIHelpers
{
	public static int currentRow = 0, currentColumn = 0;

	public static UIElement Pin<UIElement>( this UIElement g, int x, int y, int xs = Interface.iconSize, int ys = Interface.iconSize, float xa = 0, float ya = 1 ) where UIElement : Component
	{
		if ( g.transform is RectTransform t )
		{
			t.anchorMin = t.anchorMax = new Vector2( xa, ya );
			t.offsetMin = new Vector2( (int)( x * Interface.uiScale ), (int)( ( y - ys ) * Interface.uiScale ) );
			t.offsetMax = new Vector2( (int)( ( x + xs ) * Interface.uiScale ), (int)( y * Interface.uiScale ) );
		}
		return g;
	}

	public static UIElement PinCenter<UIElement>( this UIElement g, int x, int y, int xs = Interface.iconSize, int ys = Interface.iconSize, float xa = 0, float ya = 1 ) where UIElement : Component
	{
		return g.Pin( x - xs / 2, y + ys / 2, xs, ys, xa, ya );
	}

	public static UIElement PinDownwards<UIElement>( this UIElement g, int x, int y, int xs = Interface.iconSize, int ys = Interface.iconSize, float xa = 0, float ya = 1 ) where UIElement : Component
	{
		g.Pin( x, y + currentRow, xs, ys, xa, ya );
		currentRow -= ys;
		return g;
	}

	public static UIElement PinSideways<UIElement>( this UIElement g, int x, int y, int xs = Interface.iconSize, int ys = Interface.iconSize, float xa = 0, float ya = 1 ) where UIElement : Component
	{
		g.Pin( x + currentColumn, y, xs, ys, xa, ya );
		currentColumn += xs;
		return g;
	}

	public static UIElement Stretch<UIElement>( this UIElement g, int x0 = 0, int y0 = 0, int x1 = 0, int y1 = 0 ) where UIElement : Component
	{
		if ( g.transform is RectTransform t )
		{
			t.anchorMin = Vector2.zero;
			t.anchorMax = Vector2.one;
			t.offsetMin = new Vector2( (int)( x0 * Interface.uiScale ), (int)( y0 * Interface.uiScale ) );
			t.offsetMax = new Vector2( (int)( x1 * Interface.uiScale ), (int)( y1 * Interface.uiScale ) );
		}
		return g;
	}

	public static UIElement AddClickHandler<UIElement>( this UIElement g, UnityAction callBack ) where UIElement : Component
	{
		Button b = g.gameObject.GetComponent<Button>();
		if ( b == null )
			b = g.gameObject.AddComponent<Button>();
		b.onClick.AddListener( callBack );
		return g;
	}

	public static UIElement Link<UIElement>( this UIElement g, Component parent ) where UIElement : Component
	{
		g.transform.SetParent( parent.transform, false );
		return g;
	}

	public static UIElement AddOutline<UIElement>( this UIElement g, Color? color = null, float distance = 1 ) where UIElement : Component
	{
		Outline o = g.gameObject.AddComponent<Outline>();
		if ( o != null )
		{
			o.effectColor = color ?? Color.black;
			o.effectDistance = Vector2.one * distance;
		}
		return g;
	}

	public static Color Light( this Color color )
	{
		return Color.Lerp( color, Color.white, 0.5f );
	}

	public static Color Dark( this Color color )
	{
		return Color.Lerp( color, Color.black, 0.5f );
	}

	public static bool Contains<UIElement>( this UIElement g, Vector2 position ) where UIElement : Component
	{
		if ( g.transform is RectTransform t )
		{
			Vector3[] corners = new Vector3[4];
			t.GetWorldCorners( corners );
			var rect = new Rect( corners[0], corners[2] - corners[0] );
			return rect.Contains( position );
		}
		return false;
	}
}


