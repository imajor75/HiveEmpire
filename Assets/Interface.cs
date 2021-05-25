﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
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
			image = Image( 20, -20, 100, 100 );
			text = Text( 15, -10, 270, 60 );
			additionalText = Text( 20, -30, 150, 60 );
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
			tooltip.SetText( this, text, image, additionalText );
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

		// Summary:
		// Return true if the caller should give
		public bool Open( HiveObject target = null, int x = 0, int y = 0, int xs = 100, int ys = 100 )
		{
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
			name = "Panel";
			frame = gameObject.AddComponent<Image>();
			Init( frame.rectTransform, (int)( x / uiScale ), (int)( y / uiScale ), 100, 100, root );
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
			root.panels.Remove( this );
		}

		public Image Image( int x, int y, int xs, int ys, Sprite picture = null, Component parent = null )
		{
			Image i = new GameObject().AddComponent<Image>();
			i.name = "Image";
			i.sprite = picture;
			Init( i.rectTransform, x, y, xs, ys, parent );
			return i;
		}

		public ProgressBar Progress( int x = 0, int y = 0, int xs = 0, int ys = 0, Sprite picture = null, Component parent = null )
		{
			Image i = new GameObject().AddComponent<Image>();
			i.name = "Progress Bar";
			i.sprite = picture;
			Init( i.rectTransform, x, y, xs, ys, parent );
			var p = i.gameObject.AddComponent<ProgressBar>();
			p.Open();
			return p;
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

		public Button BuildingIcon( int x, int y, Building building, Component parent = null )
		{
			if ( building == null )
				return null;

			var text = Text( x, y, 150, 20, building.title, parent );
			var button = text.gameObject.AddComponent<Button>();
			button.onClick.AddListener( delegate { SelectBuilding( building ); } );
			return button;
		}

		public static void SelectBuilding( Building building )
		{
			building.OnClicked( true );
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

		public Text Text( int x = 0, int y = 0, int xs = 100, int ys = 20, string text = "", Component parent = null )
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

		public static UIElement Stretch<UIElement>( UIElement g, float x0 = 0, float x1 = 1, float y0 = 0, float y1 = 1 ) where UIElement : Component
		{
			if ( g.transform is RectTransform t )
			{
				t.anchorMin = new Vector2( x0, y0 );
				t.anchorMax = new Vector2( x1, y1 );
				t.offsetMin = t.offsetMax = Vector2.zero;
			}
			return g;
		}

		public int currentRow = 0;

		public static UIElement Pin<UIElement>( UIElement g, int x0, int x1, int y0, int y1, float xa = 0, float ya = 1 ) where UIElement : Component
		{
			if ( g.transform is RectTransform t )
			{
				t.anchorMin = t.anchorMax = new Vector2( xa, ya );
				t.offsetMin = new Vector2( x0, y0 );
				t.offsetMax = new Vector2( x1, y1 );
			}
			return g;
		}

		public UIElement PinDownwards<UIElement>( UIElement g, int x0, int x1, int y0, int y1, float xa = 0, float ya = 1 ) where UIElement : Component
		{
			var r = Pin( g, x0, x1, y0 + currentRow, y1 + currentRow, xa, ya );
			currentRow -= y1 - y0;
			return r;
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

			MoveTo( target.location.GetPositionRelativeTo( new Vector3( root.world.eye.x, 0, root.world.eye.y ) ) + Vector3.up * GroundNode.size );
		}

		public void MoveTo( Vector3 position )
		{
			Vector3 screenPosition = root.viewport.camera.WorldToScreenPoint( position );
			screenPosition.x += offset.x;
			screenPosition.y += offset.y;
			if ( screenPosition.y > Screen.height )
				screenPosition = World.instance.eye.camera.WorldToScreenPoint( target.location.position - Vector3.up * GroundNode.size );
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
				screenPosition.x -= size.width + 2 * offset.x;
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

		public class ProgressBar : MonoBehaviour
		{
			Image bar;
			public void Open()
			{
				Image frame = gameObject.GetComponent<Image>();
				frame.sprite = iconTable.GetMediaData( Icon.frame );
				frame.pixelsPerUnitMultiplier = 8 / uiScale;
				frame.type = UnityEngine.UI.Image.Type.Sliced;
				bar = new GameObject( "Bar" ).AddComponent<Image>();
				bar.rectTransform.SetParent( transform, false );
				bar.rectTransform.anchorMin	= Vector2.zero;
				bar.rectTransform.anchorMax = Vector2.one;
				bar.rectTransform.offsetMin = Vector2.one * uiScale * 4;
				bar.rectTransform.offsetMax = -Vector2.one * uiScale * 4;
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

		public class ItemImage : Image, IPointerEnterHandler, IPointerExitHandler
		{
			public Item item;
			public Item.Type itemType = Item.Type.unknown;
			public string additionalTooltip;
			PathVisualization pathVisualization;
			public Image inTransit;

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
					enabled = false;
					return;
				}
				enabled = true;
				sprite = Item.sprites[(int)itemType];
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

			public new void OnDestroy()
			{
				base.OnDestroy();
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
					outputs.Setup( this, workshop.productionConfiguration.outputType, workshop.productionConfiguration.outputMax, 20, row, iconSize + 5, workshop.outputArea, false );
					row -= iconSize * 3 / 2;
				}
				int progressWidth = ( iconSize + 5 ) * 7;
				progressBar = Progress( 20, row, progressWidth, iconSize );
				row -= 25;

				if ( ( contentToShow & Content.itemsProduced ) > 0 )
				{
					itemsProduced = Text( 20, row, 200, 20 );
					productivity = Text( 150, -20, 50, 20 );
					productivity.alignment = TextAnchor.MiddleRight;
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
				root.world.eye.FocusOn( workshop, true );
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
					items[i] = boss.ItemIcon( x, y, iconSize, iconSize, itemType );
					x += xi;
				}
				boss.Text( x, y, 20, 20, "?" ).gameObject.AddComponent<Button>().onClick.AddListener( delegate { LogisticList.Create().Open( boss.building, itemType, input ? ItemDispatcher.Potential.Type.request : ItemDispatcher.Potential.Type.offer ); } );
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
					items[i].color = Color.white;
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
							items[i].color = new Color( 1, 1, 1, 0  );
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
				i.additionalTooltip = "Shift+LMB Show input potentials\nCtrl+LMB Show output potentials\nShift+Ctrl+LMB Add one more\nAlt+Ctrl+LMB Clear";
				if ( stock.GetSubcontractors( (Item.Type)j ).Count > 0 )
					Image( 10 + offset, row, 20, 20, iconTable.GetMediaData( Icon.rightArrow ) );
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
			selected.additionalTooltip = "LMB Set cart target\nShift+LMB Show current target\nCtrl+LMB Clear target\nAlt+LMB Show inputs";
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
			string resources = "";
			foreach ( var resource in node.resources )
			{
				if ( resources != "" )
					resources += ", ";
				resources += resource.type;

			}
			if ( resources != "" )
				Text( 20, -40, 160, 20, "Resource: " + resources );
			if ( show )
				root.world.eye.FocusOn( node, true );
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
			base.Open();
			name = "Build panel";

			Frame( 0, 0, 360, 320 );
			Button( 330, -20, 20, 20, iconTable.GetMediaData( Icon.exit ) ).onClick.AddListener( Close );

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
			targetWorkerCount = Dropdown( 20, -44, 150, 25 );
			targetWorkerCount.ClearOptions();
			targetWorkerCount.AddOptions( new List<string> { "Auto", "1", "2", "3", "4" } );
			targetWorkerCount.value = road.targetWorkerCount;
			targetWorkerCount.onValueChanged.AddListener( TargetWorkerCountChanged );

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
		Button destinationBuilding;
		PathVisualization cartPath;

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
					Destroy( destinationBuilding.gameObject );
					if ( cart.destination )
						destinationBuilding = BuildingIcon( 70, -95, cart.destination );
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

			if ( base.Open() )
				return;

			name = "Item panel";

			Frame( 0, 0, 300, 150, 1.5f );
			Button( 270, -10, 20, 20, iconTable.GetMediaData( Icon.exit ) ).onClick.AddListener( Close );
			Text( 15, -15, 100, 20, item.type.ToString() );
			stats = Text( 15, -35, 250, 20 );
			Text( 15, -55, 170, 20, "Origin:" );
			BuildingIcon( 100, -55, item.origin ).onClick.AddListener( delegate { Destroy( route ); route = null; } );
			Text( 15, -75, 170, 20, "Destination:" );
			BuildingIcon( 100, -75, item.destination );

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
		List<List<Text>> inputs = new List<List<Text>>();
		bool reversed;
		Comparison<Building> lastComparisonUsed;

		static public BuildingList Create()
		{
			return new GameObject().AddComponent<BuildingList>();
		}

		public void Open()
		{
			base.Open( null, 0, 0, 500, 400 );

			Frame( 0, 0, 500, 400 );
			Button( 470, -10, 20, 20, iconTable.GetMediaData( Icon.exit ) ).onClick.AddListener( Close );
			var t = Text( 20, -20, 150, iconSize, "type" );
			t.fontSize = (int)( uiScale * 10 );
			t.gameObject.AddComponent<Button>().onClick.AddListener( delegate { Fill( CompareTypes ); } );
			var p = Text( 170, -20, 150, iconSize, "productivity" );
			p.fontSize = (int)( uiScale * 10 );
			p.gameObject.AddComponent<Button>().onClick.AddListener( delegate { Fill( CompareProductivities ); } );
			var o = Text( 380, -20, 150, iconSize, "output" );
			o.fontSize = (int)( uiScale * 10 );
			o.gameObject.AddComponent<Button>().onClick.AddListener( delegate { Fill( CompareOutputs ); } );
			var i = Text( 235, -20, 150, iconSize, "input" );
			i.fontSize = (int)( uiScale * 10 );
			i.gameObject.AddComponent<Button>().onClick.AddListener( delegate { Fill( CompareInputs ); } );
			scroll = ScrollRect( 20, -40, 460, 340 );

			foreach ( var building in Resources.FindObjectsOfTypeAll<Building>() )
			{
				if ( building.owner != root.mainPlayer || building.blueprintOnly )
					continue;
				buildings.Add( building );
			}
			Fill( CompareTypes );
		}

		void Fill( Comparison<Building> comparison )
		{
			if ( lastComparisonUsed == comparison )
				reversed = !reversed;
			else
				reversed = true;
			lastComparisonUsed = comparison;

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
				BuildingIcon( 0, -iconSize * i, buildings[i], scroll.content );
				productivities.Add( Text( 150, -iconSize * i, 100, iconSize, "", scroll.content ) );
				outputs.Add( Text( 385, -iconSize * i, 50, iconSize, "", scroll.content ) );
				inputs.Add( new List<Text>() );
				if ( buildings[i] is Workshop workshop )
				{
					ItemIcon( 360, -iconSize * i, 0, 0, workshop.productionConfiguration.outputType, scroll.content	);
					int bi = 0;
					foreach ( var buffer in workshop.buffers )
					{
						ItemIcon( 215 + bi * 35, -iconSize * i, 0, 0, buffer.itemType, scroll.content	);
						inputs[i].Add( Text( 240 + bi * 35, -iconSize * i, 50, iconSize, "0", scroll.content ) );
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
				ShowNearestPossibleConstructionSite();
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

		void ShowNearestPossibleConstructionSite()
		{
			GroundNode bestSite = null;
			int bestDistance = int.MaxValue;
			int bestFlagDirection = -1;
			foreach ( var o in Ground.areas[Ground.maxArea - 1] )
			{
				GroundNode node = currentNode + o;
				for ( int flagDirection = 0; flagDirection < GroundNode.neighbourCount; flagDirection++ )
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
				var i = ItemIcon( 0, row, 0, 0, Item.Type.unknown, scroll.content );
				i.SetItem( item );

				BuildingIcon( 30, row, item.origin, scroll.content );
				BuildingIcon( 130, row, item.destination, scroll.content );
				Text( 230, row, 50, 20, ( item.life.age / 50 ).ToString(), scroll.content );
				if ( item.path )
					Text( 280, row, 30, 20, item.path.roadPath.Count.ToString(), scroll.content );
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

			Frame( 0, 0, 540, 320 );
			Button( 510, -10, 20, 20, iconTable.GetMediaData( Icon.exit ) ).onClick.AddListener( Close );

			Text( 20, -20, 250, 20, "List of potentials for       at" );
			ItemIcon( 150, -20, 0, 0, itemType );
			BuildingIcon( 190, -20, building );

			Text( 20, -40, 100, 20, "Building" ).fontSize = (int)( uiScale * 10 );
			Text( 100, -40, 100, 20, "Distance" ).fontSize = (int)( uiScale * 10 );
			Text( 140, -40, 100, 20, "Direction" ).fontSize = (int)( uiScale * 10 );
			Text( 190, -40, 100, 20, "Priority" ).fontSize = (int)( uiScale * 10 );
			Text( 230, -40, 100, 20, "Quantity" ).fontSize = (int)( uiScale * 10 );
			Text( 270, -40, 100, 20, "Result" ).fontSize = (int)( uiScale * 10 );

			scroll = ScrollRect( 20, -60, 500, 240 );
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
					BuildingIcon( 0, row, result.building, scroll.content );
					Text( 100, row, 50, 20, result.building.node.DistanceFrom( building.node ).ToString(), scroll.content );
					Text( 130, row, 50, 20, result.incoming ? "Out" : "In", scroll.content );
					Text( 170, row, 50, 20, result.priority.ToString(), scroll.content );
					Text( 210, row, 50, 20, result.quantity.ToString(), scroll.content );
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
				Text( 250, row, 200, 40, message, scroll.content );
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

			SetScrollRectContentSize( scroll, 0, (int)Item.Type.total * ( iconSize + 5 ) );
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
				if ( player.itemEfficiencyHistory[i].weight != 0 )
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
			// TODO Clean up this function, its a mess
			base.Update();
			if ( lastAverageEfficiency == player.averageEfficiencyHistory.current )
				return;

			var t = new Texture2D( 400, 260 );
			for ( int x = 0; x < t.width; x++ )
			{
				for ( int y = 0; y < t.height; y++ )
					t.SetPixel( x, y, Color.black );
			}
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
			float itemsPerEfficiencyUpdate = (float)( Player.efficiencyUpdateTime ) / tickPerBuilding;
			float scale = t.height / max;
			if ( workshopCount * itemsPerEfficiencyUpdate != 0 )
				scale = t.height / ( workshopCount * itemsPerEfficiencyUpdate );
			int hi = (int)( itemsPerEfficiencyUpdate * scale );
			Color hl = Color.grey;
			if ( hi >= t.height - 1 )
			{
				hl = Color.Lerp( Color.grey, Color.black, 0.5f );
				hi = hi / 4;
			}
			int yb = hi;
			while ( yb < t.height )
			{
				HorizontalLine( yb, hl );
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

	public class WorldProgressPanel : Panel
	{
		Text worldTime;
		Text currentEfficiency;
		ProgressBar efficiencyProgress;
		float originalSpeed = -1;

		public static WorldProgressPanel Create()
		{
			return new GameObject( "World Progress Panel" ).AddComponent<WorldProgressPanel>();
		}

		public void Open( bool victory = false )
		{
			if ( base.Open() )
				return;
			name = "World Progress Panel";
			Pin( frame, -200, 200, -100, 100, 0.5f, 0.5f );
			Stretch( Frame( 0, 0, 1, 1 ) );
			var closeButton = Button( 0, 0, 1, 1, iconTable.GetMediaData( Icon.exit ) );
			Pin( closeButton.GetComponent<Image>(), -30, -10, -30, -10, 1, 1 );
			closeButton.onClick.AddListener( Close );
			currentRow = -30;
			if ( victory )
			{
				var t = PinDownwards( Text( 0, 0, 0, 0, "VICTORY!" ), -100, 100, -30, 0, 0.5f );
				t.color = Color.red;
				t.alignment = TextAnchor.MiddleCenter;
				originalSpeed = root.world.timeFactor;
				root.world.eye.FocusOn( root.mainPlayer.mainBuilding.flag.node, true );
				root.world.SetTimeFactor( 0 );
			}
			worldTime = PinDownwards( Text(), -200, 200, -30, 0, 0.5f );
			worldTime.alignment = TextAnchor.MiddleCenter;
			PinDownwards( Text( 0, 0, 1, 1, $"Efficiency goal: {World.instance.efficiencyGoal}" ), -200, 200, -30, 0, 0.5f ).alignment = TextAnchor.MiddleCenter;
			currentEfficiency = PinDownwards( Text(), -200, 200, -30, 0, 0.5f );
			currentEfficiency.alignment = TextAnchor.MiddleCenter;
			efficiencyProgress = PinDownwards( Progress(), -100, 100, -30, 0, 0.5f );
		}

		new public void Update()
		{
			var t = World.instance.time;
			worldTime.text = $"World time: {t / 24 / 60 / 60 / 50}:{( t / 60 / 60 / 50 ) % 60}:{( t / 60 / 50) % 60}";
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
			return new GameObject().AddComponent<MainPanel>();
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
			root.world.settings.size = 16 + 16 * size.value;
			if ( size.value == 0 )
				root.world.settings.maxHeight = 2;
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

