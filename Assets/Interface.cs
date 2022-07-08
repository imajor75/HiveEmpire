using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static UnityEngine.UI.Dropdown;
#pragma warning disable 0618

public class Interface : HiveObject
{
	public StreamWriter logFile;
	public List<Panel> panels = new List<Panel>();
	public PostProcessResources postProcessResources;
	public const int iconSize = 20;
	public static float uiScale = 1.5f;
	public static Font font;
	public new World world;
	Canvas canvas;
	public Workshop.Type selectedWorkshopType = Workshop.Type.unknown;
	public Viewport viewport;
	static public MediaTable<Sprite, Icon> iconTable;
	public GameObject debug;
	public new static Interface root;
	public bool heightStrips;
	public bool showReplayAction = true;
	public Player mainPlayer;
	public static Tooltip tooltip;
	public int fullValidate = fullValidateInterval;
	const int fullValidateInterval = 500;
	Image[] speedButtons = new Image[3];
	public List<World.Challenge> challenges;
	public MonoBehaviour replayIcon;
	Operation lastShownOperation;
	public int selectByID;
	public Text messageButton;
	public string delayedSaveName;
	public bool delayedManualSave;
	public bool delayedSaveValid;
	public float lastSave = -1;
	public bool defeatReported;
	public bool requestUpdate;
    public bool purgeOperationHandlerCRCTable;
	public Text FPSDisplay;
	public float FPS;

	public static Material materialUIPath;
	static bool focusOnInputField, focusOnDropdown;
	static KeyCode ignoreKey = KeyCode.None;

	public Image buildButton, worldProgressButton;

	static public Hotkey headquartersHotkey = new Hotkey( "Show headquarters", KeyCode.Home );
	static public Hotkey changePlayerHotkey = new Hotkey( "Change player", KeyCode.D, true );
	static public Hotkey closeWindowHotkey = new Hotkey( "Close window", KeyCode.Escape );
	static public Hotkey cameraBackHotkey = new Hotkey( "Camera back", KeyCode.LeftArrow, false, true );

	static public Hotkey mapHotkey = new Hotkey( "Map", KeyCode.M );

	static public Hotkey cameraLeftHotkey = new Hotkey( "Camera move left (continuous)", KeyCode.A );
	static public Hotkey cameraRightHotkey = new Hotkey( "Camera move right (continuous)", KeyCode.D );
	static public Hotkey cameraUpHotkey = new Hotkey( "Camera move up (continuous)", KeyCode.W );
	static public Hotkey cameraDownHotkey = new Hotkey( "Camera move down (continuous)", KeyCode.S );
	static public Hotkey cameraRotateCCWHotkey = new Hotkey( "Camera rotate CW (continuous)", KeyCode.E );
	static public Hotkey cameraRotateCWHotkey = new Hotkey( "Camera rotate CCW (continuous)", KeyCode.Q );
	static public Hotkey cameraZoomInHotkey = new Hotkey( "Camera zoom in (continuous)", KeyCode.Z );
	static public Hotkey cameraZoomOutHotkey = new Hotkey( "Camera zoom out (continuous)", KeyCode.Y );
	static public Hotkey cameraRaiseHotkey = new Hotkey( "Camera raise (continuous)", KeyCode.R );
	static public Hotkey cameraLowerHotkey = new Hotkey( "Camera lower (continuous)", KeyCode.F );

	static public Hotkey mapZoomInHotkey = new Hotkey( "Map zoom in", KeyCode.KeypadPlus );
	static public Hotkey mapZoomOutHotkey = new Hotkey( "Map zoom out", KeyCode.KeypadMinus );

	static public Hotkey showFPSHotkey = new Hotkey( "Show FPS", KeyCode.UpArrow, true, true );

	public bool playerInCharge { get { return world.operationHandler.mode == OperationHandler.Mode.recording; } }
	public Team mainTeam { get { return mainPlayer?.team; } }

	public class Hotkey
	{
		public string action;
		public KeyCode key;
		public bool alt, shift, ctrl;
		[JsonIgnore]
		public bool core;
		[JsonIgnore]
		public Hotkey original;
		public static List<Hotkey> instances = new List<Hotkey>();

		public Hotkey()
		{
		}

		public void Remove()
		{
			Assert.global.IsTrue( core );
			Assert.global.IsTrue( instances.Contains( this ) );
			instances.Remove( this );
		}

		public Hotkey( string action, KeyCode key, bool ctrl = false, bool alt = false, bool shift = false )
		{
			this.action = action;
			this.key = key;
			this.ctrl = ctrl;
			this.alt = alt;
			this.shift = shift;
			original = new Hotkey { key = key, alt = alt, ctrl = ctrl, shift = shift };
			core = true;
			instances.Add( this );
		}

		public void Reset()
		{
			Assert.global.IsNotNull( original );
			key = original.key;
			alt = original.alt;
			ctrl = original.ctrl;
			shift = original.shift;
		}

		public void CopyTo( Hotkey dest )
		{
			Assert.global.IsTrue( dest.core );
			Assert.global.IsFalse( core );

			dest.key = key;
			dest.alt = alt;
			dest.ctrl = ctrl;
			dest.shift = shift;
		}

		[JsonIgnore]
		public string keyName
		{
			get
			{
				string keyName = "";
				if ( alt )
					keyName += "Alt+";
				if ( ctrl )
					keyName += "Ctrl+";
				if ( shift )
					keyName += "Shift+";
				return keyName + GetKeyName( key );
			}
		}

		public bool IsSecondaryHold()
		{
			if ( alt != ( GetKey( KeyCode.LeftAlt ) || GetKey( KeyCode.RightAlt ) ) )
				return false;
			if ( shift != ( GetKey( KeyCode.LeftShift ) || GetKey( KeyCode.RightShift ) ) )
				return false;
			if ( ctrl != ( GetKey( KeyCode.LeftControl ) || GetKey( KeyCode.RightControl ) ) )
				return false;

			return true;
		}

		public bool IsPressed()
		{
			return IsSecondaryHold() && GetKeyDown( key );
		}

		public bool IsDown()
		{
			return IsSecondaryHold() && GetKey( key );
		}

		public class List
		{
			public List<Hotkey> list;
		}
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
		destroy,
		newRoad,
		magnet,
		rightArrow,
		crosshair,
		tinyFrame,
		reset,
		sleeping,
		clock,
		alarm,
		shovel,
		crossing,
		resizer,
		cart,
		pin,
		home,
		ring,
		house,
		hammer,
		junction,
		crate,
		itemPile,
		resource,
		history,
		map,
		hive,
		cup,
		cursor,
		grid,
		buildings,
		move,
		key,
		pause,
		play,
		fast,
		stock,
		exc,
		replay,
		yes,
		no,
		cave,
		box,
		bar,
		ground
	}

	public Interface()
	{
		root = this;
	}

	static public string GetKeyName( KeyCode k )
	{
		if ( k >= KeyCode.Alpha0 && k <= KeyCode.Alpha9 )
			return ( k - KeyCode.Alpha0 ).ToString();
		
		return k.ToString();
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

	void OnGUI()
	{ 
		if ( Event.current.type == EventType.KeyUp && ignoreKey == Event.current.keyCode )
			ignoreKey = KeyCode.None;
	}

	public void ShowOperation( Operation operation )
	{
		if ( eye.target && lastShownOperation == operation )
			return;

		MessagePanel.Create( operation.name, operation.place, 5 );
		lastShownOperation = operation;
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
		oh?.SaveReplay();
		world.Clear();
		logFile.Close();
	}

	public static bool GetKey( KeyCode key )
	{
		if ( focusOnInputField || ignoreKey == key )
			return false;

		return Input.GetKey( key );
	}

	public static bool GetKeyDown( KeyCode key )
	{
		if ( focusOnInputField || ignoreKey == key )
			return false;

		return Input.GetKeyDown( key );
	}

	public void FixedUpdate()
	{
#if DEBUG
		if ( --fullValidate < 0 && world.autoValidate )
		{
			ValidateAll();
			fullValidate = fullValidateInterval;
		}
#endif

		if ( materialUIPath )
		{
			var o = materialUIPath.mainTextureOffset;
			o.y -= 0.015f;
			if ( o.y < 0 )
				o.y += 1;
			materialUIPath.mainTextureOffset = o;
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
		// {
		// 	var alpha = Resources.Load<Texture2D>( "icons/arrow" );
		// 	var color = Resources.Load<Texture2D>( "icons/arrow2" );
		// 	var d = new Texture2D( color.width, color.height );
		// 	for ( int x = 0; x < color.width; x++ )
		// 	{
		// 		for ( int y = 0; y < color.height; y++ )
		// 		{
		// 			var c = color.GetPixel( x, y );
		// 			var a = alpha.GetPixel( x, y );
		// 			c.a = a.a;
		// 			d.SetPixel( x, y, c );
		// 		};
		// 	}
		// 	d.Apply();
		// 	System.IO.File.WriteAllBytes( "target.png", d.EncodeToPNG() );
		// }

		font = (Font)Resources.GetBuiltinResource( typeof( Font ), "Arial.ttf" );
		Assert.global.IsNotNull( font );
		object[] table = {
		"greenCheck", Icon.yes,
		"redCross", Icon.no,
		"arrow", Icon.rightArrow,
		"brick", Icon.progress,
		"mainIcon", Icon.crate,
		"cross", Icon.exit };
		iconTable.fileNamePrefix = "icons/";
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
		string logFileName = Application.isEditor ? "log-editor.txt" : "log.txt";
		try { File.Move( Application.persistentDataPath + "/" + logFileName, Application.persistentDataPath + "/log-prev.txt" ); } catch ( Exception ) {}
		logFile = new StreamWriter( Application.persistentDataPath + "/" + logFileName );

		Node.Initialize();
		Assert.Initialize();
		World.Initialize();
		Ground.Initialize();
		Item.Initialize();
		Building.Initialize();
		Road.Initialize();
		Unit.Initialize();
		Flag.Initialize();
		Resource.Initialize();
		Interface.Initialize();
		Workshop.Initialize();
		Stock.Initialize();
		GuardHouse.Initialize();
		Viewport.Initialize();
		Water.Initialize();
		Network.Initialize();
		BuildingMapWidget.Initialize();
		Eye.Highlight.Initialize();
		Ground.Grass.Initialize();

		Directory.CreateDirectory( Application.persistentDataPath + "/Saves" );
		Directory.CreateDirectory( Application.persistentDataPath + "/Settings" );
		Directory.CreateDirectory( Application.persistentDataPath + "/Replays" );

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

		var iconFolder = new GameObject( "Icons" ).AddComponent<Image>();
		iconFolder.transform.SetParent( transform, false );
		iconFolder.enabled = false;
		iconFolder.rectTransform.offsetMin = iconFolder.rectTransform.offsetMax = Vector2.zero;
		iconFolder.rectTransform.anchorMin = Vector2.zero;
		iconFolder.rectTransform.anchorMax = Vector2.one;

		this.Image( Icon.hive ).AddClickHandler( () => MainPanel.Create().Open() ).Link( iconFolder.transform ).Pin( 10, -10, iconSize * 2, iconSize * 2 );
		buildButton = this.Image( Icon.hammer ).AddClickHandler( OpenBuildPanel ).Link( iconFolder.transform ).PinSideways( 10, -10, iconSize * 2, iconSize * 2 ).AddHotkey( "Build", KeyCode.Space );
		buildButton.SetTooltip( () => $"Build new building (hotkey: {buildButton.GetHotkey().keyName})" );
		var buildingListButton = this.Image( Icon.house ).AddClickHandler( () => BuildingList.Create().Open() ).Link( iconFolder.transform ).PinSideways( 10, -10, iconSize * 2, iconSize * 2 ).AddHotkey( "Building list", KeyCode.B );
		buildingListButton.SetTooltip( () => $"List all buildings (hotkey: {buildingListButton.GetHotkey().keyName})" );
		var roadListButton = this.Image( Icon.newRoad ).AddClickHandler( () => RoadList.Create( mainTeam ) ).Link( iconFolder.transform ).PinSideways( 0, -10, iconSize * 2, iconSize * 2 ).AddHotkey( "Road list", KeyCode.R, true );
		roadListButton.SetTooltip( () => $"List all roads (hotkey: {roadListButton.GetHotkey().keyName})" );
		var itemListButton = this.Image( Icon.crate ).AddClickHandler( () => ItemList.Create().Open( mainTeam ) ).Link( iconFolder.transform ).PinSideways( 0, -10, iconSize * 2, iconSize * 2 ).AddHotkey( "Item list", KeyCode.I );
		itemListButton.SetTooltip( () => $"List all items on roads (hotkey: {itemListButton.GetHotkey().keyName})" );
		var itemStatsButton = this.Image( Icon.itemPile ).AddClickHandler( () => ItemStats.Create().Open( mainTeam ) ).Link( iconFolder.transform ).PinSideways( 0, -10, iconSize * 2, iconSize * 2 ).AddHotkey( "Item statistics", KeyCode.J );
		itemStatsButton.SetTooltip( () => $"Show item type statistics (hotkey: {itemStatsButton.GetHotkey().keyName})" );
		var resourceListButton = this.Image( Icon.resource ).AddClickHandler( () => ResourceList.Create().Open() ).Link( iconFolder.transform ).PinSideways( 0, -10, iconSize * 2, iconSize * 2 ).AddHotkey( "Resource list", KeyCode.K );
		resourceListButton.SetTooltip( () => $"Show item type statistics (hotkey: {resourceListButton.GetHotkey().keyName})" );
		var routeListButton = this.Image( Icon.cart ).AddClickHandler( () => RouteList.Create().Open( null, Item.Type.log, true ) ).Link( iconFolder.transform ).PinSideways( 0, -10, iconSize * 2, iconSize * 2 ).AddHotkey( "Route list", KeyCode.R, false, false, true );
		routeListButton.SetTooltip( () => $"List routes for all stocks (hotkey: {routeListButton.GetHotkey().keyName})" );
		worldProgressButton = this.Image( Icon.cup ).AddClickHandler( () => ChallengePanel.Create().Open() ).Link( iconFolder.transform ).PinSideways( 0, -10, iconSize * 2, iconSize * 2 ).AddHotkey( "Challenge progress", KeyCode.P );
		worldProgressButton.SetTooltip( () => $"Show challenge progress (hotkey: {worldProgressButton.GetHotkey().keyName})" );
		var historyButton = this.Image( Icon.history ).AddClickHandler( () => History.Create().Open( mainTeam ) ).Link( iconFolder.transform ).PinSideways( 0, -10, iconSize * 2, iconSize * 2 ).AddHotkey( "History", KeyCode.H );
		historyButton.SetTooltip( () => $"Show production history (hotkey: {historyButton.GetHotkey().keyName})" );
		var mapButton = this.Image( Icon.map ).AddClickHandler( () => Map.Create().Open() ).Link( iconFolder.transform ).PinSideways( 0, -10, iconSize * 2, iconSize * 2 ).AddHotkey( "Minimap", KeyCode.M, true );
		mapButton.SetTooltip( () => $"Minimap (hotkey: {mapButton.GetHotkey().keyName})" );
		var hotkeyButton = this.Image( Icon.key ).AddClickHandler( () => HotkeyList.Create().Open() ).Link( iconFolder.transform ).PinSideways( 0, -10, iconSize * 2, iconSize * 2 ).AddHotkey( "Hotkey list", KeyCode.H, true );
		hotkeyButton.SetTooltip( () => $"Show hotkeys (hotkey: {hotkeyButton.GetHotkey().keyName})" );
		var challengesButton = this.Image( Icon.exc ).AddClickHandler( () => ChallengeList.Create() ).Link( iconFolder.transform ).PinSideways( 0, -10, iconSize * 2, iconSize * 2 ).AddHotkey( "Challenge list", KeyCode.C );
		challengesButton.SetTooltip( () => $"Show list of possible challenges (hotkey: {challengesButton.GetHotkey().keyName})" );
		var showNearestCaveButton = this.Image( Icon.cave ).AddClickHandler( ShowNearestCave ).Link( iconFolder.transform ).PinSideways( 0, -10, iconSize * 2, iconSize * 2 ).AddHotkey( "Show nearest cave", KeyCode.C, true );
		showNearestCaveButton.SetTooltip( () => $"Show nearest animal cave (hotkey: {showNearestCaveButton.GetHotkey().keyName})" );

		var heightStripButton = this.Image( Icon.map ).AddToggleHandler( (state) => SetHeightStrips( state ) ).Link( iconFolder.transform ).Pin( -40, -50, iconSize * 2, iconSize * 2, 1 ).AddHotkey( "Show height strips", KeyCode.F7 );
		heightStripButton.SetTooltip( () => $"Show height strips (hotkey: {heightStripButton.GetHotkey().keyName})" );

		replayIcon = this.Image( Icon.replay ).Link( iconFolder.transform ).Pin( -200, 50, iconSize * 2, iconSize * 2, 1, 0 ).SetTooltip( ReplayTooltipGenerator, width:400 ).AddClickHandler( () => ReplayPanel.Create() );
		speedButtons[0] = this.Image( Icon.pause ).AddClickHandler( () => SetWorldSpeed( World.Speed.pause ) ).Link( iconFolder.transform ).PinSideways( 0, 50, iconSize * 2, iconSize * 2, 1, 0 ).AddHotkey( "Pause", KeyCode.Alpha0 );
		speedButtons[0].SetTooltip( () => $"Set game speed to pause (hotkey: {speedButtons[0].GetHotkey().keyName})" );
		speedButtons[1] = this.Image( Icon.play ).AddClickHandler( () => SetWorldSpeed( World.Speed.normal ) ).Link( iconFolder.transform ).PinSideways( 0, 50, iconSize * 2, iconSize * 2, 1, 0 ).AddHotkey( "Normal speed", KeyCode.Alpha1 );
		speedButtons[1].SetTooltip( () => $"Set game speed to normal (hotkey: {speedButtons[1].GetHotkey().keyName})" );
		speedButtons[2] = this.Image( Icon.fast ).AddClickHandler( () => SetWorldSpeed( World.Speed.fast ) ).Link( iconFolder.transform ).PinSideways( 0, 50, iconSize * 2, iconSize * 2, 1, 0 ).AddHotkey( "Fast speed", KeyCode.Alpha2 );
		speedButtons[2].SetTooltip( () => $"Set game speed to fast (hotkey: {speedButtons[2].GetHotkey().keyName})" );

		FPSDisplay = this.Text().Link( iconFolder.transform ).Pin( 20, 20, 200, 20, 0, 0 ).AddOutline();
		FPSDisplay.color = Color.white;
		FPSDisplay.enabled = false;

		messageButton = this.Text( "" ).Pin( iconSize, -50, 6 * iconSize, 2 * iconSize ).AddClickHandler( OnMessagesClicked );
		messageButton.fontSize = 40;
		messageButton.color = Color.yellow;
		messageButton.AddOutline();

		LoadHotkeys();
		LoadChallenges();

		world = World.Create().Setup();
		#if DEBUG
			StartCoroutine( ValidateCoroutine() );
		#endif
		OpenMainPanel();
	}

	void SetWorldSpeed( World.Speed speed )
	{
		if ( network.state == Network.State.client )
		{
			MessagePanel.Create( "Changing world speed in client mode is not allowed", null, 3 );
			return;
		}

		world.SetSpeed( speed );
	}

	public void OnMessagesClicked()
	{
		var message = mainPlayer.messages.First();
		MessagePanel.Create( message.text, message.location );
		mainPlayer.messages.RemoveFirst();
	}

	public void OpenMainPanel()
	{
		var directory = new DirectoryInfo( Application.persistentDataPath+"/Saves" );
		if ( directory.Exists )
		{
			var myFiles = directory.GetFiles( "*.json" ).OrderByDescending( f => f.LastWriteTime );
			if ( myFiles.Count() > 0 )
				Load( myFiles.First().FullName );
		}
		if ( !world.gameInProgress )
		{
			var demoFile = Application.streamingAssetsPath + "/demolevel.json";
			if ( File.Exists( demoFile ) )
				Load( demoFile );
			else
				NewGame( challenges.First() );
		}

		MainPanel.Create().Open( true );
	}

	string ReplayTooltipGenerator()
	{
		string text = $"Game is in replay mode. Time left from replay: {UIHelpers.TimeToString( oh.replayLength - time )}";
		var next = oh.NextToExecute( mainTeam );
		if ( next != null )
			text += $"\nNext action is {next.name} in {UIHelpers.TimeToString( next.scheduleAt - time )}";
		return text;
	}

	void OpenBuildPanel()
	{
		if ( root.playerInCharge )
			BuildPanel.Create().Open();
	}

	void ShowNearestCave()
	{
		Resource closest = null;
		int closestDistance = int.MaxValue;
		foreach ( var node in ground.nodes )
		{
			foreach ( var resource in node.resources )
			{
				if ( resource.type == Resource.Type.animalSpawner )
				{
					int distance = resource.node.DistanceFrom( eye.location );
					if ( distance < closestDistance )
					{
						closestDistance = distance;
						closest = resource;
					}
				}
			}
		}
		if ( closest )
			eye.FocusOn( closest, true );
	}

	public void NewGame( World.Challenge challenge )
	{
		world.NewGame( challenge );
		if ( world.players.Count > 0 )
			mainPlayer = world.players[0];
		else
			mainPlayer = null;
		eye.FocusOn( mainTeam.mainBuilding, approach:false );
		WelcomePanel.Create();
		lastSave = Time.unscaledTime;
		defeatReported = false;
	}

	public void Load( string fileName )
	{
		world.Load( fileName );
		mainPlayer = world.controllingPlayer;
		if ( mainPlayer == null && world.players.Count > 0 )
			mainPlayer = world.players[0];
		lastSave = Time.unscaledTime;
	}

	public void Save( string fileName = "", bool manualSave = false )
	{
		if ( fileName == "" )
			fileName = Application.persistentDataPath + "/Saves/" + world.nextSaveFileName + ".json";
		delayedSaveName = fileName;
		delayedManualSave = manualSave;
		delayedSaveValid = false;
		lastSave = Time.unscaledTime;
		MessagePanel.Create( $"Saving {fileName}", autoclose:1 );
	}

	public void LoadReplay( string name )
	{
		Log( $"Loading replay {name}", true );
		var o = OperationHandler.LoadReplay( name );
		ReplayLoader.Create( o );
	}

	public void SaveReplay( string name )
	{
		Log( $"Saving replay {name}", true );
		oh.SaveReplay( name );
	}

	public void SaveHotkeys()
	{
		Serializer.Write( Application.persistentDataPath + "/Settings/hotkeys.json", new Hotkey.List { list = Hotkey.instances }, true, true );
	}

	public void LoadHotkeys()
	{
		var hotkeys = Serializer.Read<Hotkey.List>( Application.persistentDataPath + "/Settings/hotkeys.json" );
		if ( hotkeys == null )
			return;

		var list = hotkeys.list;
		list.AddRange( Hotkey.instances );
		list.Sort( ( a, b ) => a.action.CompareTo( b.action ) );
		for ( int i = 0; i < list.Count-1; i++ )
		{
			if ( list[i].action != list[i+1].action )
				continue;
			if ( list[i].core )
				list[i+1].CopyTo( list[i] );
			else
				list[i].CopyTo( list[i+1] );
		}
	}

	public void LoadChallenges()
	{
		string file = Application.persistentDataPath + "/challenges.json";
		if ( !File.Exists( file ) )
			file = Application.streamingAssetsPath + "/challenges.json";
		challenges = Serializer.Read<World.Challenge.List>( file ).list;
		var challengeContainer = new GameObject( "Challenges" );
		challengeContainer.transform.SetParent( transform );
		foreach ( var challenge in challenges )
		{
			challenge.ParseConditions();
			challenge.transform.SetParent( challengeContainer.transform );
		}
	}

	public void OnGoalReached( World.Goal goal )
	{
		foreach ( var challenge in challenges )
		{
			if ( challenge.title == world.challenge.title && challenge.bestSolutionLevel < goal )
			{
				challenge.bestSolutionLevel = goal;
				challenge.bestSolutionReplayFileName = oh.SaveReplay();
				Serializer.Write( Application.persistentDataPath + "/challenges.json", new World.Challenge.List { list = challenges }, true, false );
			}
		}

		ChallengePanel.Create().Open( goal );
	}

	new public void Update()
	{
		requestUpdate = false;
		if ( Time.unscaledTime - lastSave > Constants.Interface.autoSaveIntervalInSecond )
			Save( Application.persistentDataPath + "/Saves/" + world.nextSaveFileName + ".json", false );
		if ( mainPlayer && messageButton )
		{
			if ( mainPlayer.messages.Count != 0 )
			{
				messageButton.text = mainPlayer.messages.Count.ToString();
				var color = Color.yellow;
				color.a = (float)( 1 - ( Time.unscaledTime - Math.Floor( Time.unscaledTime ) ) );
				messageButton.color = color;
				messageButton.gameObject.SetActive( true );
			}
			else
				messageButton.gameObject.SetActive( false );
		}

		if ( mainTeam && mainTeam.mainBuilding == null && !defeatReported )
		{
			defeatReported = true;
			ChallengeList.Create( true );
		}

		if ( EventSystem.current?.currentSelectedGameObject != null )
		{ 
			focusOnInputField = EventSystem.current.currentSelectedGameObject.GetComponent<InputField>() != null;
			focusOnDropdown = EventSystem.current.currentSelectedGameObject.GetComponent<Toggle>() != null;
		}
		else
			focusOnDropdown = focusOnInputField = false;
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

		if ( headquartersHotkey.IsPressed() && mainTeam?.mainBuilding )
			mainTeam.mainBuilding.OnClicked( true );
		if ( changePlayerHotkey.IsPressed() )
			PlayerSelectorPanel.Create( false );
		if ( closeWindowHotkey.IsPressed() )
		{
			if ( !viewport.ResetInputHandler() )
			{
				bool isMainOpen = false;
				Panel toClose = null;
				for ( int i = panels.Count - 1; i >= 0; i-- )
				{
					if ( panels[i] as MainPanel )
						isMainOpen = true;
					if ( !panels[i].escCloses )
						continue;
					if ( toClose == null || toClose.transform.GetSiblingIndex() < panels[i].transform.GetSiblingIndex() )
						toClose = panels[i];
				}
				if ( toClose == null && !isMainOpen )
					MainPanel.Create().Open();
				toClose?.Close();
			}
		}
		if ( cameraBackHotkey.IsPressed() )
			eye.RestoreOldPosition();
		if ( mapHotkey.IsPressed() )
			eye.SetMapMode( !eye.mapMode );
#if DEBUG
		if ( Input.GetKeyDown( KeyCode.Keypad0 ) )
		{
			var flagList = Resources.FindObjectsOfTypeAll<Flag>();
			var flag = flagList[new System.Random().Next( flagList.Length )];
			if ( flag != mainTeam?.mainBuilding?.flag )
			{
				eye.FocusOn( flag );
				oh.ScheduleRemoveFlag( flag );
			}
		}
		if ( Input.GetKeyDown( KeyCode.Keypad1 ) )
		{
			var itemList = Resources.FindObjectsOfTypeAll<Item>();
			var item = itemList[new System.Random().Next( itemList.Length )];
			eye.FocusOn( item );
			item.CancelTrip();
		}
		if ( Input.GetKeyDown( KeyCode.Keypad2 ) )
		{
			var unitList = Resources.FindObjectsOfTypeAll<Unit>();
			var unit = unitList[new System.Random().Next( unitList.Length )];
			eye.FocusOn( unit );
			unit.ResetTasks();
		}
		if ( Input.GetKeyDown( KeyCode.Keypad3 ) )
		{
			var buildingList = Resources.FindObjectsOfTypeAll<Building>();
			var building = buildingList[new System.Random().Next( buildingList.Length )];
			if ( building != mainTeam?.mainBuilding )
			{
				eye.FocusOn( building );
				world.operationHandler.ScheduleRemoveBuilding( building );
			}
		}
#endif

		if ( speedButtons[0] && world ) { speedButtons[0].color = world.timeFactor == 0 ? Color.white : Color.grey; };
		if ( speedButtons[1] && world ) { speedButtons[1].color = world.timeFactor == 1 ? Color.white : Color.grey; };
		if ( speedButtons[2] && world ) { speedButtons[2].color = world.timeFactor == 8 ? Color.white : Color.grey; };
 		if ( world?.operationHandler )	// This can be null during join
		{
			replayIcon.gameObject.SetActive( !playerInCharge );
			if ( !playerInCharge )
			{
				var next = world.operationHandler.NextToExecute( mainTeam );
				if ( showReplayAction && !playerInCharge && next != null && next.scheduleAt - time < Constants.Interface.showNextActionDuringReplay && next.location?.team == mainTeam )
					ShowOperation( next );
			}
		}

		if ( delayedSaveName != null && delayedSaveName != "" && delayedSaveValid )
		{
			world.Save( delayedSaveName, delayedManualSave );
			delayedSaveName = null;
		}
		delayedSaveValid = true;

        if ( purgeOperationHandlerCRCTable )
        {
            oh.PurgeCRCTable();
            purgeOperationHandlerCRCTable = false;
        }

		FPS = 0.9f * FPS + 0.1f * ( 1 / Time.unscaledDeltaTime );
		FPSDisplay.text = FPS.ToString( "F1" ) + " FPS";
		if ( showFPSHotkey.IsPressed() )
			FPSDisplay.enabled = !FPSDisplay.enabled;

		base.Update();
	}

	void OnValidate()
	{
#if DEBUG
		if ( selectByID != 0 )
		{
			Selection.activeGameObject = HiveObject.GetByID( selectByID )?.gameObject;
			selectByID = 0;
		}
#endif
	}

	void SetHeightStrips( bool value )
	{
		this.heightStrips = value;
		ground.material.SetInt( "_HeightStrips", value ? 1 : 0 );
	}

	public static void ValidateAll( bool skipNoAsserts = false )
	{
		foreach ( var ho in Resources.FindObjectsOfTypeAll<HiveObject>() )
		{
			if ( skipNoAsserts && ho.noAssert )
				continue;
			if ( !ho.destroyed )
				ho.Validate( false );
		}
	}

	IEnumerator ValidateCoroutine()
	{
		while ( true )
		{
			yield return new WaitForEndOfFrame();
			#if DEBUG
			if ( !EditorApplication.isPaused && world.autoValidate )
				Validate( true );
			#endif
		}
	}

	public override void Validate( bool chain )
	{
#if DEBUG
		if ( chain )
			world.Validate( true );

		if ( !chain )	// This function is caller after load, before the Start functions would be called, so in that case skip checking the number of objects in the root
			return;

		var roots = SceneManager.GetActiveScene().GetRootGameObjects();
		int activeObjectsAtRootLevel = 0;
		foreach ( var go in roots )
		{
			if ( go.active )
			{
				HiveObject ho;
				if ( go.TryGetComponent<HiveObject>( out ho ) && ho.destroyed )
					continue;
			
				activeObjectsAtRootLevel++;
			}
		}
		Assert.global.AreEqual( activeObjectsAtRootLevel, 4, "Interface, World, Network and the Event System should be the four active objects at root level" );
#endif
	}

	public override Node location { get { return null; } }

	public class ReplayLoader : Panel
	{
		public OperationHandler replay;
		public Dropdown saves;

		public static ReplayLoader Create( OperationHandler o )
		{
			var h = new GameObject( "Replay loader" ).AddComponent<ReplayLoader>();
			h.Open( o );
			return h;
		}

		void Open( OperationHandler replay )
		{
			this.replay = replay;
			base.Open( 300, 100 );
			Text( "Start from:" ).Pin( borderWidth, -borderWidth, 80, 2 * iconSize );
			saves = Dropdown().Pin( 80, -borderWidth, 200, iconSize );
			saves.AddOptions( new List<string>{ "Beginning" } );
			saves.AddOptions( replay.saveFileNames );
			Button( "Start" ).PinCenter( 0, -60, 100, 25, 0.5f, 1 ).AddClickHandler( StartReplay );
		}

		void StartReplay()
		{
			replay.StartReplay( saves.value - 1, GetKey( KeyCode.LeftControl ) || GetKey( KeyCode.RightControl ) );
		}
	}
	public class PathVisualization : MonoBehaviour
	{
	    Vector3 lastAbsoluteEyePosition;
		Path path;
		int lastProgress;

		public static PathVisualization Create()
		{
			return new GameObject( "Path visualization" ).AddComponent<PathVisualization>();
		}

		public PathVisualization Setup( Path path )
		{
			if ( path == null || !path.IsValid )
			{
				Destroy( this );
				return null;
			}
			this.path = path;

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
						float bestDistance = float.MaxValue;
						int bestRoad = 0;
						for ( int k = 0; k < path.roadPath.Count; k++ )
						{
							float distance = ( path.roadPath[k].nodes[0].GetPositionRelativeTo( eye.position ) - eye.position ).magnitude;
							if ( distance < bestDistance )
							{
								bestDistance = distance;
								bestRoad = k;
							}
						}
						var t = bestRoad;
						if ( path.roadPathReversed[t] )
							currentPosition = path.roadPath[t].ends[1].node.GetPositionRelativeTo( eye.position );
						else
							currentPosition = path.roadPath[t].ends[0].node.GetPositionRelativeTo( eye.position );
						while ( t > 0 )
						{
							t--;
							if ( path.roadPathReversed[t] )
								currentPosition += path.roadPath[t].difference;
							else
								currentPosition -= path.roadPath[t].difference;
						}
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
		    lastAbsoluteEyePosition = eye.absolutePosition;
			AlignColors();
			return this;
		}

		void AlignColors()
		{
			var route = gameObject.GetComponent<MeshFilter>().mesh;
			List<Color> colors = new List<Color>();
			for ( int j = 0; j < path.roadPath.Count; j++ )
			{
				Color segmentColor = j < path.progress ? Color.green.Light() : new Color( 0, 0.5f, 1 );
				Road road = path.roadPath[j];
				for ( int i = 0; i < road.nodes.Count - 1; i++ )
				{
					colors.Add( segmentColor );
					colors.Add( segmentColor );
					colors.Add( segmentColor );
					colors.Add( segmentColor );
				}
			}

			route.colors = colors.ToArray();
			lastProgress = path.progress;
		}

		public void Start()
		{
			transform.SetParent( HiveCommon.world.transform );
		}

		public void Update()
		{
			if ( lastProgress != path.progress )
				AlignColors();

			var currentAbsoluteEyePosition = eye.absolutePosition;
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
		public new bool pinned;
		int width, height;
		public World.Timer life = new World.Timer();

		public static Tooltip Create()
		{
			return new GameObject( "Tooltip", typeof( RectTransform ) ).AddComponent<Tooltip>();
		}

		public void Open()
		{
			borderWidth = 10;
			noCloseButton = true;
			noResize = true;
			allowInSpectateMode = true;
			noPin = true;
			base.pinned = true;
			reopen = true;
			base.Open( width = 100, height = 100 );
			escCloses = false;

			image = Image().Pin( borderWidth, -borderWidth, 100, 100 );
			text = Text().Stretch( borderWidth, 0, -borderWidth, -borderWidth );
			additionalText = Text();
			additionalText.fontSize = (int)( 10 * uiScale );
			gameObject.SetActive( false );
			FollowMouse();
		}

		public void SetText( Component origin, string text = null, Sprite imageToShow = null, string additionalText = "", float pinX = -1, float pinY = -1, int time = 0, int width = 300 )
		{
			this.origin = origin;
			this.text.text = text;
			if ( time != 0 )
				life.Start( time );
			else
				life.Reset();
			this.additionalText.text = additionalText;
			this.additionalText.Stretch( borderWidth, borderWidth, -borderWidth, -borderWidth - (int)(this.text.preferredHeight) );
			if ( imageToShow )
			{
				image.sprite = imageToShow;
				image.enabled = true;
				SetSize( this.width = (int)( uiScale * 140 ), height = (int)( uiScale * 100 ) );
			}
			else
			{
				image.enabled = false;
				SetSize( this.width = width, height = (int)( this.text.preferredHeight + this.additionalText.preferredHeight ) + 2 * borderWidth );
			}
			gameObject.SetActive( true );
			if ( pinX < 0 || pinY < 0 )
			{
				pinned = false;
				FollowMouse();
			}
			else
			{
				pinned = true;
				this.PinCenter( 0, 0, this.width, height, pinX, pinY );
			}
		}

		new public void Clear()
		{
			origin = null;
			gameObject.SetActive( false );
		}

		public override void Update()
		{
			if ( origin == null || !origin.gameObject.activeSelf || life.done )
			{
				gameObject.SetActive( false );
				return;
			}
			base.Update();
			if ( !pinned )
				FollowMouse();
			transform.SetAsLastSibling();
		}

		void FollowMouse()
		{
			const int offset = 20;
			Vector3 pos = Input.mousePosition;
			if ( pos.x + width * uiScale > Screen.width )
				pos.x -= (offset + width) * uiScale;
			else
				pos.x += offset * uiScale;
			if ( pos.y - height * uiScale < 0 )
				pos.y += (offset + height) * uiScale;
			else
				pos.y -= offset * uiScale;
			this.Pin( (int)(pos.x / uiScale), (int)(pos.y / uiScale), width, height, 0, 0 );
		}
	}

	public class TooltipSource : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
	{
		public Func<string> textGenerator;
		public string additionalText;
		public Sprite image;
		public Action<bool> onShow;
		public bool active;
		public int width;

		public void OnDestroy()
		{
			if ( active && onShow != null )
				onShow( false );
		}

        public void OnPointerEnter( PointerEventData eventData )
        {
			if ( focusOnDropdown )
				return;
			if ( onShow != null && !active )
				onShow( true );
			if ( textGenerator != null )
				tooltip.SetText( this, textGenerator(), image, additionalText, width:width );
			active = true;
        }

        public void OnPointerExit( PointerEventData eventData )
        {
			if ( tooltip.origin == this )
				tooltip.Clear();
			if ( onShow != null && active )
				onShow( false );
			active = false;
        }

		public void SetData( Func<string> textGenerator, Sprite image, string additionalText, Action<bool> onShow, int width )
		{
			this.textGenerator = textGenerator;
			this.image = image;
			this.additionalText = additionalText;
			this.onShow = onShow;
			this.width = width;

			if ( active )
				tooltip.SetText( this, textGenerator(), image, additionalText, width:width );
		}

		void Update()
		{
			if ( active && textGenerator != null )
				tooltip.SetText( this, textGenerator(), image, additionalText, width:width );
		}
    }

	public class HotkeyControl : MonoBehaviour
	{
		public Hotkey hotkey;
		public UIHelpers.Button button;

		public void Open( string name, KeyCode key, bool ctrl = false, bool alt = false, bool shift = false )
		{
			hotkey = new Hotkey( name, key, ctrl, alt, shift );
			button = gameObject.GetComponent<UIHelpers.Button>();
		}

		public void Update()
		{
			if ( button && hotkey != null && hotkey.IsPressed() )
				button.leftClickHandler();
		}
	}

	public class Panel : HiveCommon, IDragHandler, IBeginDragHandler, IPointerClickHandler
	{
		public HiveObject target;
		public bool followTarget = true;
		public Image frame;
		Image resizer;
		public bool escCloses = true;
		public bool disableDrag;
		public Vector2 offset = new Vector2( 100, 100 );
		public int borderWidth = 20;
		public bool noCloseButton;
		public bool noResize;
		public bool noPin;
		public bool reopen;
		public bool allowInSpectateMode;
		public Image pin;
		public bool pinned;
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
			if ( root.mainTeam == null && !allowInSpectateMode )
			{
				Close();
				return false;
			}

			if ( !(transform is RectTransform) )
				gameObject.AddComponent<RectTransform>();

			foreach ( var panel in root.panels )
			{
				if ( panel.pinned )
					continue;
				var r = IsTheSame( panel );
				if ( r != CompareResult.different )
					panel.Close();
				if ( r == CompareResult.same && !reopen )
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
			Prepare();
			this.target = target;
			this.Pin( x, y, xs, ys );
			UpdatePosition();
			return false;
		}

		public void Prepare()
		{
			if ( borderWidth != 0 )
			{
				frame = Frame( borderWidth ).Stretch( 0, 0, 0, 0 );
				frame.name = "Background";
			}
			if ( !noCloseButton )
			{
				var cb = Image( Icon.exit ).Pin( -borderWidth, 0, borderWidth, borderWidth, 1, 1 ).AddClickHandler( Close );
				cb.name = "Close button";
				cb.color = Color.red;
			}
			if ( !noPin )
			{
				pin = Image( Icon.pin ).Pin( -borderWidth * 2, 0, borderWidth, borderWidth, 1, 1 ).AddClickHandler( Pin );
				pin.color = Color.green.Dark();
			}
			if ( !noResize )
			{
				resizer = Image( Icon.resizer ).Pin( -iconSize, iconSize, iconSize, iconSize, 1, 0 );
				resizer.name = "Resizer";
			}
		}

		public void Clear()
		{
			foreach ( Transform c in transform )
				Destroy( c.gameObject );

			Prepare();
		}

		void Pin()
		{
			pinned = !pinned;
			pin.color = pinned ? Color.green : Color.green.Dark();
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
			if ( eye )
				eye.StopAutoChange();
		}

		public void SetSize( int x, int y )
		{
			if ( transform is RectTransform t )
				t.offsetMax = t.offsetMin + new Vector2( x * uiScale, y * uiScale );
		}

		public Image Image( Sprite picture = null )
		{
			return UIHelpers.Image( this, picture );
		}

		public Image Image( Icon icon )
		{
			return UIHelpers.Image( this, icon );
		}

		public UIHelpers.Button CheckBox( string text )
		{
			return UIHelpers.CheckBox( this, text );
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
			scroll.scrollSensitivity = 50;

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
			content.rectTransform.offsetMax = new Vector2( vertical ? (int)( uiScale * -20 ) : 0, horizontal ? (int)( uiScale * -20 ) : 0 );

			scroll.Clear();	// Just to create the background image

			return scroll;
		}

		public ItemImage ItemIcon( Item.Type type = Item.Type.unknown )
		{
			var i = ItemImage.Create().Open( type );
			i.name = "ItemImage";
			if ( type != Item.Type.unknown )
				i.picture.sprite = Item.sprites[(int)type];
			else
				i.picture.enabled = false;
			i.itemType = type;
			i.Stretch();
			i.transform.SetParent( transform );
			i.gameObject.AddComponent<Button>().onClick.AddListener( i.Track );
			return i;
		}

		public RectTransform RectTransform()
		{
			return new GameObject().AddComponent<RectTransform>();
		}

		public Image AreaIcon( Building building, Ground.Area area )
		{
			var bg = Image( Icon.smallFrame );
			bg.color = Color.grey;
			var i = Image( Icon.crosshair ).AddOutline();
			var a = i.gameObject.AddComponent<AreaControl>();
			a.Setup( building, area );
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

			var text = Text( null, fontSize );
			var d = text.gameObject.AddComponent<BuildingIconData>();
			d.building = building;
			d.Open( building );
			return text;
		}

		public class BuildingIconData : HiveObjectHandler
		{
			public Building building;
			public Text text;

			new void Update()
			{
				if ( text == null )
					text = gameObject.GetComponent<Text>();
				text.text = building.nick;
				if ( !building.construction.done )
					text.color = Color.grey;
				base.Update();
			}
		}

		public static void SelectBuilding( Building building )
		{
			building?.OnClicked( true );
		}

		public Image Button( string text )
		{
			Image i = Image( Resources.Load<Sprite>( "icons/button" ) );
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
			return UIHelpers.Text( this, text, fontSize );
		}

		public EditableText Editable( string text = "", int fontSize = 12 )
		{
			var t = new GameObject().AddComponent<EditableText>();
			t.AddClickHandler( t.Edit );
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

		public void UpdatePosition()
		{
			if ( target == null || !followTarget )
				return;

			MoveTo( target.location.GetPositionRelativeTo( eye.position ) );
		}

		public void MoveTo( Vector3 position )
		{
			Vector3 screenPosition = eye.cameraGrid.center.WorldToScreenPoint( position );
			screenPosition.x += offset.x;
			screenPosition.y += offset.y;
			if ( transform is RectTransform t )
			{
				float width = t.offsetMax.x - t.offsetMin.x;
				float height = t.offsetMax.y - t.offsetMin.y;

				if ( screenPosition.x + width > Screen.width )
					screenPosition.x -= width + 2 * offset.x;
				if ( screenPosition.y < height )
					screenPosition.y = height;
				if ( screenPosition.y > Screen.height )
					screenPosition.y = Screen.height;

				screenPosition.y -= Screen.height;
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
				OnResized();
			}
			else
			{
				t.offsetMin += eventData.delta;
				t.offsetMax += eventData.delta;
				followTarget = false;
				OnMoved();
			}
		}

		public virtual void OnResized()
		{
		}

		public virtual void OnMoved()
		{
		}

		public void OnPointerClick( PointerEventData eventData )
		{
			transform.SetAsLastSibling();
			if ( eventData.clickCount == 2 )
				OnDoubleClick();
		}

		public virtual void OnDoubleClick()
		{
			if ( target == null )
				return;

			eye.FocusOn( target );
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
				frame.color = Color.grey;

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

		public class AreaControl : MonoBehaviour, IInputHandler
		{
			public Building building;
			public Ground.Area area, originalArea;
			public Image image;

			public void Setup( Building building, Ground.Area area )
			{
				this.originalArea = area;
				this.area = new Ground.Area();
				this.area.center = area.center;
				this.area.radius = area.radius;
				this.building = building;
				image = gameObject.GetComponent<Image>();
				this.SetTooltip( "LMB Set new area\nShift+LMB Clear current area\nCtrl+LMB Focus camera on center", null, 
				"You can specify the area where this building is sending items or getting " +
				"items from. By default no area is specified, so items can arrive and go to " +
				"any place in the world. Note that the other side also has an option to " +
				"specify an area, if there is no union of these areas, items will not travel there.", Show );
				this.AddClickHandler( OnClick );
			}

			public bool pickGroundOnly { get { return true; } }

			public bool OnMovingOverNode( Node node )
			{
				if ( node )
					area.center = node;
				return true;
			}

			public bool OnNodeClicked( Node node )
			{
				if ( root.viewport.rightButton )
					return true;
					
				if ( eye.highlight.area == area )
					eye.highlight.TurnOff();
				oh.ScheduleChangeArea( building, originalArea, area.center, area.radius );
				return false;
			}

			public void OnClick()
			{
				if ( GetKey( KeyCode.LeftShift ) || GetKey( KeyCode.RightShift ) )
				{
					oh.ScheduleChangeArea( building, originalArea, null, 0 );
					if ( eye.highlight.area == area )
						eye.highlight.TurnOff();
					return;
				}
				if ( GetKey( KeyCode.LeftControl ) || GetKey( KeyCode.RightControl ) )
				{
					if ( area.center )
					{
						Transform panel = transform.parent;
						while ( panel )
						{
							var po = panel.GetComponent<Panel>();
							if ( po )
								po.followTarget = false;
							panel = panel.parent;
						}
						eye.FocusOn( area.center );
					}
					return;
				}
				area.center = ground.nodes[0];
				area.radius = 2;
				eye.highlight.HighlightArea( area, gameObject );
				root.viewport.inputHandler = this;
			}

			public void Show( bool show )
			{
				if ( show )
				{
					if ( originalArea.center == null )
						return;

					eye.highlight.HighlightArea( originalArea, gameObject );
				}
				else
				{
					if ( root.viewport.inputHandler != this as IInputHandler && eye.highlight.area == originalArea )
						eye.highlight.TurnOff();
				}
			}

			static public Hotkey increaseSizeHotkey = new Hotkey( "Area size increase", KeyCode.Period );
			static public Hotkey decreaseSizeHotkey = new Hotkey( "Area size decrease", KeyCode.Comma );

			public void Update()
			{
				image.color = originalArea.center != null ? Color.green : Color.white;
				if ( decreaseSizeHotkey.IsPressed() )
				{
					if ( area.radius > 1 )
						area.radius--;
				}
				if ( increaseSizeHotkey.IsPressed() )
				{
					if ( area.radius < 8 )
						area.radius++;
				}
			}

			public void OnLostInput()
			{
				if ( eye.highlight.area != area )
					return;
				eye.highlight.TurnOff();
			}

			public bool OnObjectClicked( HiveObject target )
			{
				return OnNodeClicked( target.location );
			}
		}

		public class EditableText : Text
		{
			public InputField editor;
			public Action onValueChanged;
			public void Edit()
			{
				var o = Instantiate( Resources.Load<GameObject>( "InputField" ) );
				editor = o.GetComponent<InputField>();
				editor.name = "InputField";
				editor.Link( this ).Stretch();
				editor.text = text;
				editor.onSubmit.AddListener( EditorSubmit );
				editor.onEndEdit.AddListener( delegate { EditorSubmit( text ); } );
				editor.Select();
			}

			void EditorSubmit( string newText )
			{
				if ( editor == null )
					return;

				text = newText;
				Destroy( editor.gameObject );
				if ( onValueChanged != null )
					onValueChanged();
			}
		}

		public class ItemImage : MonoBehaviour
		{
			public Item item;
			public Item.Type itemType = Item.Type.unknown;
			public string additionalTooltipText;
			public string additionalTooltip
			{
				set
				{
					additionalTooltipText = value;
					if ( itemType != Item.Type.unknown )
						this.SetTooltip( itemType.ToString().GetPrettyName(), Item.sprites[(int)itemType], additionalTooltipText, OnShowTooltip );
				}
			}
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

				inTransit = new GameObject( "Logistic indicator" ).AddComponent<Image>().Link( this );
				inTransit.sprite = Unit.arrowSprite;
				inTransit.rectTransform.anchorMin = new Vector2( 0.5f, 0.0f );
				inTransit.rectTransform.anchorMax = new Vector2( 1.0f, 0.5f );
				inTransit.rectTransform.offsetMin = inTransit.rectTransform.offsetMax = Vector2.zero;
				inTransit.gameObject.SetActive( false );

				if ( itemType != Item.Type.unknown )
					this.SetTooltip( itemType.ToString().GetPrettyName(), Item.sprites[(int)itemType], additionalTooltipText, OnShowTooltip );
				return this;
			}

			public void Track()
			{
				if ( item == null )
					return;

				ItemPanel.Create().Open( item );
			}

			public void SetType( Item.Type itemType, bool updateTooltip = true )
			{
				if ( this.itemType == itemType )
					return;

				Destroy( pathVisualization );
				pathVisualization = null;

				this.itemType = itemType;
				if ( itemType == Item.Type.unknown )
				{
					picture.enabled = false;
					if ( updateTooltip )
						this.RemoveTooltip();
					return;
				}
				picture.enabled = true;
				picture.sprite = Item.sprites[(int)itemType];
				if ( updateTooltip )
					this.SetTooltip( itemType.ToString(), Item.sprites[(int)itemType], additionalTooltipText, OnShowTooltip );
			}

			public void SetInTransit( bool show )
			{
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

			public void OnShowTooltip( bool show )
			{
				if ( show && pathVisualization == null && item )
					pathVisualization = PathVisualization.Create().Setup( item.path );
				if ( !show )
				{
					Destroy( pathVisualization?.gameObject );
					pathVisualization = null;
				}
			}

			void Update()
			{
				if ( itemType == Item.Type.unknown || picture.color.a < 0.5f )
					SetInTransit( false );
				if ( item?.path != null && inTransit )
				{
					if ( item.path.stepsLeft > 7 )
						inTransit.color = new Color( 1, 0, 0.15f );
					else if ( item.path.stepsLeft < 3 )
						inTransit.color = new Color( 0, 0.75f, 0.15f );
					else
						inTransit.color = new Color( 1, 0.75f, 0.15f );

				}
			}
		}
	}

	public class MessagePanel : Panel
	{
		static string latestText;
		float creationTime, autoCloseAfter;

		public static MessagePanel Create( string text, HiveObject location = null, float autoclose = float.MaxValue )
		{
			if ( latestText != text )
				root.requestUpdate = true;
			var result = new GameObject( "Message panel" ).AddComponent<MessagePanel>();
			result.Open( text, location, autoclose );
			return result;
		}

		public void Open( string text, HiveObject location, float autoClose )
		{
			autoCloseAfter = autoClose;
			creationTime = Time.unscaledTime;
			noResize = true;
			reopen = true;
			allowInSpectateMode = true;
			base.Open( location, 400, 60 );

			var t = Text( latestText = text ).Pin( borderWidth, -borderWidth, 400, 50 );
			SetSize( ((int)(t.preferredWidth/uiScale))+2*borderWidth, ((int)(t.preferredHeight/uiScale))+2*borderWidth );
			eye.FocusOn( location, true );
		}

		public new void Update()
		{
			if ( creationTime + autoCloseAfter < Time.unscaledTime )
				Close();

			base.Update();
		}
	}

	public class HiveObjectHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
	{
		public HiveObject hiveObject;
		public Image ring, arrow;

		public void Open( HiveObject hiveObject )
		{
			this.hiveObject = hiveObject;
		}

		public void OnPointerEnter( PointerEventData eventData )
		{
			Assert.global.IsNull( ring );				

			ring = new GameObject( $"Ring for {hiveObject}" ).AddComponent<Image>();
			ring.Link( root );
			ring.transform.SetAsFirstSibling();
			ring.sprite = iconTable.GetMediaData( Icon.ring );
			ring.color = new Color( 0, 1, 1 );

			arrow = new GameObject( $"Arrow for {hiveObject}" ).AddComponent<Image>();
			arrow.Link( root );
			arrow.sprite = iconTable.GetMediaData( Icon.rightArrow );
			arrow.color = new Color( 1, 0.75f, 0.15f );
			arrow.transform.localScale = new Vector3( 0.5f, 0.5f, 1 );
		}

		public void OnPointerExit( PointerEventData eventData )
		{
			Assert.global.IsNotNull( ring );
			Destroy( ring.gameObject );
			ring = null;
			Destroy( arrow.gameObject );
			arrow = null;
		}

		public void OnPointerClick( PointerEventData eventData )
		{
			hiveObject.OnClicked( true );
		}

		public void Update()
		{
			if ( ring == null )
				return;

			var c = HiveCommon.eye.cameraGrid.center;
			// A null reference crash happened here in map mode, so safety check
			if ( c == null || hiveObject == null || hiveObject.location == null )
				return;
			var p = c.WorldToScreenPoint( hiveObject.location.positionInViewport );

			ring.transform.position = p;
			float scale;
			if ( c.orthographic )
			{
				var f = c.WorldToScreenPoint( hiveObject.location.Neighbour( 0 ).positionInViewport );
				scale = ( p - f ).magnitude / 70;
			}
			else
				scale = 20 / p.z;
			ring.transform.localScale = Vector3.one * scale * uiScale;

			var offset = ( p - transform.position ).normalized;
			arrow.transform.position = transform.position + offset * 120;
			arrow.transform.rotation = Quaternion.Euler( 0, 0, 90 - (float)( 180 * Math.Atan2( offset.x, offset.y ) / Math.PI ) );
			var w = ( ( p - transform.position ).magnitude - Screen.height / 2 ) / Screen.height;
			if ( w < 0 ) 
				w = 0;
			if ( w > 1 )
				w = 1;
			arrow.color = Color.Lerp( Color.green, Color.red, w );
		}

		void OnDestroy()
		{
			Destroy( ring?.gameObject );
			Destroy( arrow?.gameObject );
		}
	}

	public static void RemoveBuilding( Building building )
	{
		if ( building == null )
			return;
		if ( building.flag.roadsStartingHereCount == 0 && building.flag.Buildings().Count == 1 )
			oh.ScheduleRemoveFlag( building.flag );
		else
			oh.ScheduleRemoveBuilding( building );
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
			if ( other is ConstructionPanel && !(this is ConstructionPanel) )	// TODO Not so nice, what if IsTheSame called on the construction panel
				return CompareResult.different;

			var p = other as BuildingPanel;
			if ( p == null )
				return CompareResult.different;

			if ( p.building == this.building )
				return CompareResult.same;

			return CompareResult.sameButDifferentTarget;
		}
	}

	public class BuildingMapWidget : MonoBehaviour
	{
		public Building building;
		public Watch contentWatch = new Watch();
		public Sprite utilization;
		public Material barMaterial, bgMaterial;
		static int progressShaderID, colorShaderID;
		static Shader spriteShader;
		static Mesh plane;

		public static BuildingMapWidget Create( Building building )
		{
			var obj = new GameObject( "Workshop Map Widget" ).AddComponent<BuildingMapWidget>();
			obj.Setup( building );
			return obj;
		}

		public static void Initialize()
		{
			progressShaderID = Shader.PropertyToID( "_Progress" );
			colorShaderID = Shader.PropertyToID( "_Color" );
			spriteShader = Resources.Load<Shader>( "shaders/Sprite" );

			plane = new Mesh();
			Vector3[] pos = new Vector3[4];
			pos[0] = new Vector3( -1, -1, 0 );
			pos[1] = new Vector3( -1, 1, 0 );
			pos[2] = new Vector3( 1, -1, 0 );
			pos[3] = new Vector3( 1, 1, 0 );
			plane.vertices = pos;
			int[] ind = new int[6];
			ind[0] = 0;
			ind[1] = 1;
			ind[2] = 2;
			ind[3] = 1;
			ind[4] = 3;
			ind[5] = 2;
			plane.triangles = ind;
		}

		public void Setup( Building building )
		{
			this.building = building;
			contentWatch.Attach( building.contentChange, false );

			var bg = gameObject.AddComponent<SpriteRenderer>();
			bg.sprite = iconTable.GetMediaData( Icon.box );
			bgMaterial = bg.material = new Material( spriteShader );
			bgMaterial.renderQueue = 4004;

			var c = gameObject.AddComponent<MeshCollider>();
			c.convex = true;
			c.sharedMesh = plane;

			transform.SetParent( building.transform, false );
			transform.localPosition = new Vector3( 0, 3, 0 );
			transform.localScale = new Vector3( 0.7f, 0.5f, 1 );
			if ( building is Workshop workshop )
				SetupForWorkshop( workshop );
			if ( building is Stock stock )
				SetupForStock( stock );
			if ( building is GuardHouse guardHouse )
				SetupForGuardHouse( guardHouse );
				
			World.SetLayerRecursive( gameObject, World.layerIndexMapOnly );
		}

		SpriteRenderer NewSprite( Sprite sprite, string name )
		{
			var renderer = new GameObject( name ).AddComponent<SpriteRenderer>();
			renderer.sprite = sprite;
			renderer.transform.SetParent( transform, false );
			renderer.material = new Material( spriteShader );
			renderer.material.renderQueue = 4004;
			return renderer;
		}

		SpriteRenderer NewSprite( Icon icon, string name ) => NewSprite( iconTable.GetMediaData( icon ), name );
		SpriteRenderer NewSprite( Item.Type itemType, string name ) => NewSprite( Item.sprites[(int)itemType], name );

		void SetupForWorkshop( Workshop workshop )
		{
			bgMaterial.SetColor( "_Color", new Color( 0.3f, 0.6f, 1) );
			if ( workshop.productionConfiguration.outputType < Item.Type.total && workshop.productionConfiguration.outputType >= 0 )
			{
				var output = NewSprite( workshop.productionConfiguration.outputType, "Output icon" );
				output.transform.localPosition = new Vector3( 0.6f, -0.4f, 0.1f );
				output.transform.localScale = new Vector3( 0.4f, 0.45f, 1 );
			}

			List<SpriteRenderer> inputs = new List<SpriteRenderer>();
			foreach ( var buffer in workshop.buffers )
			{
				var r = NewSprite( buffer.itemType, $"Input icon{inputs.Count}" );
				inputs.Add( r );
			}
			switch ( inputs.Count )
			{
				case 1:
				{
					inputs[0].transform.localPosition = new Vector3( -0.6f, -0.4f, 0.1f );
					break;
				}
				case 2:
				{
					inputs[0].transform.localPosition = new Vector3( -0.6f, -0.7f, 0.1f );
					inputs[1].transform.localPosition = new Vector3( -0.6f, -0.1f, 0.1f );
					break;
				}
				case 3:
				{
					inputs[0].transform.localPosition = new Vector3( -0.9f, -0.7f, 0.1f );
					inputs[1].transform.localPosition = new Vector3( -0.9f, -0.1f, 0.1f );
					inputs[2].transform.localPosition = new Vector3( -0.3f, -0.7f, 0.1f );
					break;
				}
				case 4:
				{
					inputs[0].transform.localPosition = new Vector3( -0.9f, -0.7f, 0.1f );
					inputs[1].transform.localPosition = new Vector3( -0.9f, -0.1f, 0.1f );
					inputs[2].transform.localPosition = new Vector3( -0.3f, -0.7f, 0.1f );
					inputs[3].transform.localPosition = new Vector3( -0.3f, -0.1f, 0.1f );
					break;
				}
			}
			if ( inputs.Count == 1 )
				inputs[0].transform.localScale = new Vector3( 0.4f, 0.45f, 1 );
			else
			{
				foreach ( var r in inputs )
					r.transform.localScale = new Vector3( 0.2f, 0.225f, 1 );
			}

			var arrow = NewSprite( Icon.rightArrow, "Arrow" );
			arrow.transform.localPosition = new Vector3( 0, -0.4f, 0.1f );
			arrow.transform.localScale = new Vector3( 0.4f, 0.45f, 1 );

			var bar = NewSprite( Icon.bar, "Utilization" );
			bar.transform.localPosition = new Vector3( 0, 0.65f, 0.1f );
			bar.transform.localScale = new Vector3( 1.5f, 1.5f, 1 );
			barMaterial = bar.material;
		}

		void SetupForStock( Stock stock )
		{
			bgMaterial.SetColor( "_Color", Color.green );
		}

		void SetupForGuardHouse( GuardHouse guardHouse )
		{
			bgMaterial.SetColor( "_Color", new Color( 1, 0.2f, 0.2f ) );
		}

		void Update()
		{
			transform.rotation = Quaternion.Euler( -90, (float)( eye.direction / Math.PI * 180 ), 0 );
			if ( barMaterial )
			{
				float progress = (building as Workshop).productivity.current;
				barMaterial.SetFloat( progressShaderID, progress );
				Color color;
				if ( progress < 0.5f )
					color = Color.Lerp( new Color( 1, 0.1f, 0 ), new Color( 1, 0.9f, 0.1f ), progress * 2 );
				else
					color = Color.Lerp( new Color( 1, 0.9f, 0.1f ), new Color( 0, 1, 0 ), progress * 2 - 1 );
				barMaterial.SetColor( colorShaderID, color );
			}
			if ( contentWatch.status )
			{
				if ( building is Stock stock )
				{
					foreach ( Transform c in transform )
						Destroy( c.gameObject );

					int slot = 0;
					for ( int i = 0; i < (int)Item.Type.total; i++ )
					{
						if ( stock.itemData[i].content == 0 )
							continue;

						var t = NewSprite( (Item.Type)i, $"Item {(Item.Type)i}" );
						t.transform.localPosition = new Vector3( -0.8f + 0.4f * (slot % 5), -0.7f + 0.5f * (slot / 5), 0.1f );
						t.transform.localScale = new Vector3( 0.25f, 0.28f, 1 );
						slot++;
					}
					World.SetLayerRecursive( gameObject, World.layerIndexMapOnly );
				}

				if ( building is GuardHouse gh )
				{
					foreach ( Transform c in transform )
						Destroy( c.gameObject );

					for ( int i = 0; i < gh.soldiers.Count; i++ )
					{
						var t = NewSprite( Item.Type.soldier, "Soldier" );
						t.transform.localPosition = new Vector3( -0.8f + 0.4f * (i % 5), -0.7f + 0.5f * (i / 5), 0.1f );
						t.transform.localScale = new Vector3( 0.25f, 0.28f, 1 );
					}
					World.SetLayerRecursive( gameObject, World.layerIndexMapOnly );
				}

			}
		}
	}

	public class WorkshopPanel : BuildingPanel
	{
		public Workshop workshop;
		public ProgressBar progressBar;
		public Image changeModeImage;
		public Text productivity;
		public Text itemsProduced;
		public EditableText title;
		public Text resourcesLeft;
		public Text status;

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
				string name = workshop.moniker;
				if ( name == null )
					name = workshop.type.ToString().GetPrettyName();
				title = Editable( name ).Pin( 20, row, 160, 20 );
				title.onValueChanged = Rename;
				title.name = "Title";
				title.SetTooltip( "LMB to rename" );
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
				progressBar = Progress().Pin( iconSize, row, iconSize + progressWidth, iconSize ).SetTooltip( ShowProgressBarTooltip );
				status = Text().Link( progressBar ).Stretch().AddOutline();
				status.alignment = TextAnchor.MiddleCenter;
				status.color = Color.white;
				row -= 25;

				if ( ( contentToShow & Content.itemsProduced ) > 0 )
				{
					itemsProduced = Text().Pin( 20, row, 200, 20 );
					productivity = Text().Pin( 150, -20, 50, 20 ).AddOutline().SetTooltip( "Current productivity of the building\nPress LMB for statistics of the past" );
					productivity.alignment = TextAnchor.MiddleRight;
					productivity.AddClickHandler( ShowPastStatuses );
					row -= 25;
				}
			}
			if ( workshop.gatherer && ( contentToShow & Content.resourcesLeft ) > 0 )
			{
				resourcesLeft = Text( "Resources left: 0" ).Pin( 20, row, 150, 20 );
				string tooltip = workshop.type switch
				{
					Workshop.Type.hunter => "Hunters needs to be build around wild animal spawners, otherwise they will not able to catch anything. Animal spawners are always spawning new animals, so this is an endless resource.",
					Workshop.Type.stonemason => "Rocks on the ground have multiple charges, but they are eventually running out. In that case the stonemason should be destroyed. Build stone mines if you need more stone.",
					Workshop.Type.woodcutter => "If the ground is good for planting trees (brown) build a forester nearby to replant trees, that way the woodcutter will be able to work forever.",
					Workshop.Type.forester => "",
					Workshop.Type.wheatFarm => "If there are enough free spots around the farm, the farmer will plant and create new fields, so it will never run out of grain.",
					Workshop.Type.cornFarm => "If there are enough free spots around the farm, the farmer will plant and create new fields, so it will never run out of corn.",
					Workshop.Type.fishingHut => "Fish willl never run out, it just needs time to respawn, so it is pointless to put too much fishing hut around a small lake.",
					_ => "When an ore is mined it becomes unavailable for a while, and need some time to recharge. This time is always the same, and ores are always recharging, so ore deposits are never running out."
				};
				resourcesLeft.SetTooltip( tooltip );
				if ( ( contentToShow & Content.controlIcons ) == 0 )
					row -= 25;
			}

			if ( ( contentToShow & Content.controlIcons ) != 0 )
			{
				Image( Icon.destroy ).Pin( 190, row ).AddClickHandler( Remove ).SetTooltip( "Remove the building" );
				Image( Icon.hauler ).Pin( 170, row ).AddClickHandler( ShowTinkerer ).SetTooltip( "Show the tinkerer of the building" );
				changeModeImage = Image( GetModeIcon() ).Pin( 150, row ).AddClickHandler( ChangeMode ).SetTooltip( "Current running mode of the building\nLMB to cycle throught possible modes", null, 
				"Clock (default) - Work when needed\n" +
				"Alarm - Work even if not needed\n" +
				"Bed - Don't work at all" );
				Image( Icon.buildings ).Pin( 130, row ).AddClickHandler( () => BuildingList.Create().Open( building.type ) ).SetTooltip( "Show a list of buildings with the same type" );
				changeModeImage.color = Color.black;
				row -= 25;
			}

			this.SetSize( 250, 15 - row );
			Update();
			if ( show )
				eye.FocusOn( workshop, true );
		}

		void ShowPastStatuses()
		{
			PastStatuses.Create().Open( workshop );
		}

		void Remove()
		{
			RemoveBuilding( workshop );
			Close();
		}

		void Rename()
		{
			workshop.moniker = title.text;
		}

		void ShowTinkerer()
		{
			UnitPanel.Create().Open( workshop.tinkerer, true );
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
				if ( workshop.resting.inProgress )
				{
					progressBar.progress = (float)( -workshop.resting.age ) / workshop.restTime;
					progressBar.color = Color.grey.Light();
				}
				else
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
				}
				if ( productivity )
					UpdateProductivity( productivity, workshop );
				if ( itemsProduced )
					itemsProduced.text = "Items produced: " + workshop.itemsProduced;
				if ( status )
					status.text = workshop.GetStatusText( workshop.currentStatus );
			}
			if ( resourcesLeft )
			{
				string text = workshop.type switch
				{
					Workshop.Type.woodcutter => $"Trees left: {workshop.ResourcesLeft()}",
					Workshop.Type.appleGatherer => $"Trees left: {workshop.ResourcesLeft()}",
					Workshop.Type.wheatFarm => $"Fields left: {workshop.ResourcesLeft()}",
					Workshop.Type.cornFarm => $"Fields left: {workshop.ResourcesLeft()}",
					Workshop.Type.forester => "",
					Workshop.Type.stonemason => $"Rock charges left: {workshop.ResourcesLeft()}",
					Workshop.Type.fishingHut => $"Fish left: {workshop.ResourcesLeft()}",
					Workshop.Type.hunter => $"Wild animals left: {workshop.ResourcesLeft()}",
					_ => $"Ore left: {workshop.ResourcesLeft()}"
				};
				resourcesLeft.text = text;
			}
			if ( changeModeImage )
				changeModeImage.sprite = GetModeIcon();
		}

		void ShowProgressBarTooltip( bool on )
		{
			if ( on )
			{
				// Recalculate the relax spots just in case it changed
				var r = workshop.relaxSpotCount;
				var percent = 100 * r / workshop.productionConfiguration.relaxSpotCountNeeded;
				if ( percent > 100 )
					percent = 100;

				var productionSec = workshop.productionConfiguration.productionTime * Time.fixedDeltaTime;
				var restSec = workshop.restTime * Time.fixedDeltaTime;
				progressBar.SetTooltip( 
					$"Maximum output: {workshop.CalculateMaxOutput().ToString( "n2" )}/min\n" +
					$"Time needed to produce a new item: {productionSec.ToString( "F2" )}s\n" +
					$"Resting needed between item productions: {restSec.ToString( "F2" )}s\n" +
					$"Relaxation spots around the house: {r}\nNeeded: {workshop.productionConfiguration.relaxSpotCountNeeded}, {percent}%", null,
					$"Resting time depends on the number of relaxing spots around the building. The more relaxing spots, the less resting time the building needs (ideally zero). ", ShowProgressBarTooltip );
			
				root.viewport.nodeInfoToShow = Viewport.OverlayInfoType.nodeRelaxSites;
				root.viewport.relaxCenter = workshop;
			}
			if ( !on && root.viewport.nodeInfoToShow == Viewport.OverlayInfoType.nodeRelaxSites && root.viewport.relaxCenter == workshop )
				root.viewport.nodeInfoToShow = Viewport.OverlayInfoType.none;
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
			Image disableIndicator, disableIcon;

			public void Setup( BuildingPanel boss, Item.Type itemType, int itemCount, int x, int y, int xi, Ground.Area area = null, bool input = true )
			{
				int itemsStartX = x - xi + iconSize;
				boss.Image( Item.sprites[(int)itemType] ).Pin( itemsStartX, y );
				items = new ItemImage[itemCount];
				this.boss = boss;
				this.itemType = itemType;
				for ( int i = 0; i < itemCount; i++ )
					items[i] = boss.ItemIcon( itemType ).PinSideways( xi - iconSize, y );
				int itemsEndX = UIHelpers.currentColumn;
				if ( itemCount > 0 )
					boss.Text( "?" ).PinSideways( 0, y, 15, 20 ).AddClickHandler( delegate { LogisticList.Create().Open( boss.building, itemType, input ? ItemDispatcher.Potential.Type.request : ItemDispatcher.Potential.Type.offer ); } ).SetTooltip( "Show a list of possible potentials for this item type" ).alignment = TextAnchor.MiddleCenter;
				if ( area != null )
					boss.AreaIcon( boss.building, area ).PinSideways( 0, y );
				if ( buffer != null && buffer.optional )
				{
					disableIcon = boss.Image( Icon.exit ).PinSideways( 0, y ).AddToggleHandler( SetDisabled, buffer.disabled );
					disableIndicator = boss.Image( Icon.emptyFrame ).Pin( itemsStartX, y - iconSize / 2 + 2, itemsEndX - itemsStartX + 4, 4 );
					disableIndicator.color = Color.Lerp( Color.red, Color.black, 0.5f );
				}
				boss.Image( Icon.buildings ).PinSideways( 0, y ).AddClickHandler( () => ShowProducers( itemType ) ).SetTooltip( "Show a list of buildings which produce this" );
			}

			void ShowProducers( Item.Type itemType )
			{
				for ( int i = 0; i < (int)Workshop.Type.total; i++ )
				{
					var config = Workshop.GetConfiguration( (Workshop.Type)i );
					if ( config != null && config.outputType == itemType )
						BuildingList.Create().Open( (Building.Type)i );
				}
			}

			void SetDisabled( bool disabled )
			{
				oh.ScheduleChangeBufferUsage( boss.building as Workshop, buffer, !disabled );
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
				if ( buffer != null && disableIndicator )
				{
					disableIcon.SetToggleState( buffer.disabled );
					disableIndicator.gameObject.SetActive( buffer.disabled );
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
			public int interval;
			public Color[] statusColors;
			public List<Workshop.Status> statusList;
			public Workshop workshop;
			public World.Timer autoRefresh = new World.Timer();
			public Image circle;
			const int autoRefreshInterval = Constants.World.normalSpeedPerSecond * 60;
			public List<Text> intervalTexts = new List<Text>();

			public static PastStatuses Create()
			{
				return new GameObject( "Past Statuses Panel").AddComponent<PastStatuses>();
			}

			public void Open( Workshop workshop )
			{
				if ( base.Open( workshop, 300, 150 ) )
					return;

				this.workshop = workshop;
				autoRefresh.Start( autoRefreshInterval );
				SetInterval( Constants.World.normalSpeedPerSecond * 60 * 10 );
			}
			
			public void Fill()
			{
				int[] ticksInStatus = new int[(int)Workshop.Status.total];
				int[] percentInStatus = new int[(int)Workshop.Status.total];
				int totalTicks = 0;
				void ProcessPastStatus( Workshop.PastStatus s )
				{
					if ( s.status == Workshop.Status.unknown )
						return;

					int start = Math.Max( time - interval, s.startTime );
					int end = s.startTime + s.length;
					int duration = end - start;

					if ( duration <= 0 )
						return;

					ticksInStatus[(int)s.status] += duration;
					totalTicks += duration;
				}
				ProcessPastStatus( new Workshop.PastStatus { status = workshop.currentStatus, startTime = workshop.statusDuration.reference, length = workshop.statusDuration.age } );
				foreach ( var s in workshop.statuses )
					ProcessPastStatus( s );
				if ( totalTicks == 0 )
				{
					Text( "Not enough data yet" ).Stretch( borderWidth, borderWidth, -borderWidth, -borderWidth ).alignment = TextAnchor.MiddleCenter;
					return;
				}

				statusList = new List<Workshop.Status>();
				for ( int i = 0; i < ticksInStatus.Length; i++ )
				{
					percentInStatus[i] = 100 * ticksInStatus[i] / totalTicks;
					for ( int j = 0; j < percentInStatus[i]; j++ )
						statusList.Add( (Workshop.Status)i );
				}
				while ( statusList.Count < 101 )
					statusList.Add( statusList[0] );

				statusColors = new Color[] { Color.green, Color.red, Color.yellow, Color.cyan, Color.magenta, Color.grey, Color.red.Light(), Color.blue.Light(), Color.Lerp( Color.green, Color.blue, 0.5f ).Light() };
				Assert.global.AreEqual( statusColors.Length, (int)Workshop.Status.total );

				Text( $"Last {totalTicks / 60 / Constants.World.normalSpeedPerSecond} minutes" ).Pin( -150, -borderWidth, 300, iconSize, 1, 1 );
				for ( int i = 0; i < (int)Workshop.Status.total; i++ )
				{
					if ( ticksInStatus[i] == 0 )
						continue;
					
					var e = Text( $"{percentInStatus[i]}% " + workshop.GetStatusText( (Workshop.Status)i ), 10 ).PinDownwards( -150, 0, 350, (int)( iconSize * 0.8f ), 1, 1 ).AddOutline();
					e.color = statusColors[i];
					e.name = $"Reason {i}";
				}

				circle = Image().Stretch( 20, 20, -170, -20 );
				circle.name = "Circle";
				FillCircle();

				intervalTexts.Clear();
				void AddIntervalText( string text, int interval )
				{
					var t = Text( text ).PinSideways( 0, 30, 30, iconSize, 1, 0 ).AddOutline();
					intervalTexts.Add( t );
					t.AddClickHandler( () => SetInterval( interval ) );
					t.name = interval.ToString();
					t.color = interval == this.interval ? Color.white : Color.grey;
				}
				UIHelpers.currentColumn = -180;
				AddIntervalText( "10h", Constants.World.normalSpeedPerSecond * 60 * 60 * 10 );
				AddIntervalText( "1h", Constants.World.normalSpeedPerSecond * 60 * 60 );
				AddIntervalText( "30m", Constants.World.normalSpeedPerSecond * 60 * 30 );
				AddIntervalText( "10m", Constants.World.normalSpeedPerSecond * 60 * 10 );
				AddIntervalText( "1m", Constants.World.normalSpeedPerSecond * 60 );
			}

			void FillCircle()
			{
				if ( circle == null )
					return;

				var t = new Texture2D( (int)circle.rectTransform.rect.width, (int)circle.rectTransform.rect.height );

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
				circle.sprite = Sprite.Create( t, new Rect( 0, 0, t.width, t.height ), Vector2.zero );

			}

			public void SetInterval( int interval )
			{
				this.interval = interval;
				Clear();
				Fill();
				foreach ( var t in intervalTexts )
					t.color = t.name == interval.ToString() ? Color.white : Color.grey;
			}

			override public void Update()
			{
				base.Update();
				if ( autoRefresh.inProgress )
					return;

				autoRefresh.Start( autoRefreshInterval );
				Clear();
				Fill();
			}

			public override void OnResized()
			{
				FillCircle();
			}
		}
	}

	public class GuardHousePanel : BuildingPanel
	{
		public GuardHouse guardHouse;
		public Attackable attackable;
		public Dropdown soldierCount;
		public Text defenders, attackers;
		public Image sendAttacker;
		public int attackerCount;
		public Watch attackedStatus = new Watch();

		public static GuardHousePanel Create()
		{
			return new GameObject().AddComponent<GuardHousePanel>();
		}

		public void Open( Attackable attackable, bool show = false )
		{
			this.attackable = attackable;
			this.guardHouse = attackable as GuardHouse;
			attackedStatus.Attach( attackable.attackedStatus, false );
			noResize = true;
			if ( base.Open( attackable, 220, 120 ) )
				return;
			name = "Guard House panel";
			if ( attackable.team == root.mainTeam )
			{
				Image( Icon.destroy ).PinCenter( -borderWidth-iconSize, iconSize, iconSize, iconSize, 1, 0 ).AddClickHandler( Remove );
				Text( "Defender count" ).Pin( borderWidth, -borderWidth, 200 );
				soldierCount = Dropdown().PinDownwards( borderWidth, 0, 160 );
				soldierCount.AddOptions( new List<string> { "1", "2", "3" } );
				soldierCount.value = guardHouse.soldiers.Count - 1;
				soldierCount.onValueChanged.AddListener( SoldierCountChanged );
			}
			else
			{
				defenders = Text().Pin( borderWidth, -borderWidth, 200 );
				sendAttacker = Button( "Send attacker" ).AddClickHandler( SendAttacker ).PinDownwards( 20, 0, 180 );
			}
			attackers = Text().PinDownwards( borderWidth, 0, 200, 2* iconSize );
			if ( show )
				eye.FocusOn( guardHouse, true );
			Image( Icon.buildings ).Pin( 145, 30, iconSize, iconSize, 0, 0 ).AddClickHandler( () => BuildingList.Create().Open( Building.Type.guardHouse ) ).SetTooltip( "Show a list of buildings with the same type" );
		}

		new void Update()
		{
			if ( defenders )
				 defenders.text = $"Defenders: {attackable.defenderCount}"; 
			if ( attackers && attackedStatus.status )
			{
				if ( attackable.attackerTeam )
					attackers.text = $"Under attack from team\n{attackable.attackerTeam.name} with {attackable.attackerCount} soldiers";
				else
					attackers.text = "Not under attack";
			}
			base.Update();
		}

		void Remove()
		{
			RemoveBuilding( guardHouse );
			Close();
		}

		void SendAttacker()
		{
			switch ( root.mainTeam.Attack( attackable, 1, true ) )
			{
				case Team.AttackStatus.noSoldiers:
					MessagePanel.Create( "Not enough soldiers", autoclose:3 );
					break;
				case Team.AttackStatus.tooFar:
					MessagePanel.Create( "No guard house nearby", autoclose:3 );
					break;
				case Team.AttackStatus.available:
					oh.ScheduleAttack( root.mainTeam, attackable, 1 );
					break;
			}
		}

		void SoldierCountChanged( int value )
		{
			oh.ScheduleChangeDefenderCount( guardHouse, value + 1 );
		}
	}

	public class StockPanel : BuildingPanel
	{
		public int currentStockCRC;
		public Stock stock;
		public Stock.Channel channel;
		public Text channelText;
		public string channelPattern;
		public Text[] counts = new Text[(int)Item.Type.total];
		public Text total;
		public Item.Type selectedItemType = Item.Type.log;
		public ItemImage selected;
		public Text inputMin, inputMax, outputMin, outputMax, cartInput, cartOutput;
		public RectTransform controls;
		public Image selectedInput, selectedOutput;
		new public EditableText name;
		public InputField renamer;

		float lastMouseXPosition;
		int min, max, currentValue = -1;

		public static StockPanel Create()
		{
			return new GameObject( "Stock panel").AddComponent<StockPanel>();
		}

		public void Open( Stock stock, bool show = false )
		{
			this.stock = stock;
			noResize = true;
			if ( base.Open( stock, 300, 400 ) )
				return;
			RecreateControls();
			if ( show )
				eye.FocusOn( stock, true );
		}

		void SelectItemType( Item.Type itemType )
		{
			selectedItemType = itemType;
			UpdateRouteIcons();
		}

		void UpdateRouteIcons()
		{
			bool cartInput = stock.itemData[(int)selectedItemType].cartInput > 0;
			selectedInput.gameObject.SetActive( cartInput );
			bool cartOutput = stock.itemData[(int)selectedItemType].cartOutput >= Constants.Stock.cartCapacity;
			selectedOutput.gameObject.SetActive( cartOutput );
		}

		int StockCRC()
		{
			int CRC = 0;
			foreach ( var itemData in stock.itemData )
				CRC += itemData.cartInput + itemData.cartOutput;
			return CRC;
		}

		void RecreateControls()
		{
			if ( controls )
				Destroy( controls.gameObject );
			controls = new GameObject( "Stock controls" ).AddComponent<RectTransform>();
			controls.Link( this ).Stretch();

			AreaIcon( stock, stock.inputArea ).Link( controls ).Pin( 30, -25, 30, 30 ).name = "Input area";
			AreaIcon( stock, stock.outputArea ).Link( controls ).Pin( 235, -25, 30, 30 ).name = "Output area";
			total = Text( "", 16 ).Link( controls ).Pin( 25, 75, 100, iconSize * 2, 0, 0 );
			total.name = "Total";
			total.SetTooltip( $"Total number of items in the stock, the maximum is {stock.maxItems}", null, 
			"The total number of items can exceed the max value temporarily, but the stock won't store more items if it is full." );
			name = Editable( stock.moniker ).Link( controls ).PinCenter( 0, -35, 160, iconSize, 0.5f, 1 );
			name.alignment = TextAnchor.MiddleCenter;
			if ( name.text == "" )
				name.text = "Stock";
			name.onValueChanged = OnRename;
			name.SetTooltip( "LMB to rename" );

			int row = -55;
			for ( int j = 0; j < (int)Item.Type.total; j++ )
			{
				int offset = j % 2 > 0 ? 140 : 0;
				var t = (Item.Type)j;
				var i = ItemIcon( (Item.Type)j ).Link( controls ).Pin( 20 + offset, row );
				string tooltip = "LMB Select item type\nRMB Popup menu";
				i.additionalTooltip = tooltip;

				if ( stock.itemData[j].cartInput > 0 )
					Image( Icon.rightArrow ).Link( i ).PinCenter( 0, 0, iconSize / 2, iconSize / 2, 0, 0.5f ).color = new Color( 1, 0.75f, 0.15f );

				if ( stock.itemData[j].cartOutput >= Constants.Stock.cartCapacity )
				{
					Image( Icon.rightArrow ).Link( i ).PinCenter( 0, 0, iconSize / 2, iconSize / 2, 1, 0.5f ).color = new Color( 1, 0.75f, 0.15f );
					offset += 10;
				}
				i.AddClickHandler( () => SelectItemType( t ) );
				var d = Dropdown().Link( i ).Pin( 0, 0, 100, 0, 0, 0 );
				d.ClearOptions();
				var options = new List<String> { "Select", "Show routes", "Show input potentials", "Show output potentials" };
#if DEBUG
				options.Add( "Clear" );
				options.Add( "Add one" );
#endif
				options.Add( "Cancel" );
				d.AddOptions( options );
				Item.Type y = (Item.Type)j;
				d.onValueChanged.AddListener( (x) => ItemTypeAction( i, y, x ) );
				i.AddClickHandler( () => PopupForItemType( d ), UIHelpers.ClickType.right );
				counts[j] = Text().Link( controls ).Pin( 44 + offset, row, 100 );
				if ( j % 2 > 0 )
					row -= iconSize + 5;
			}

			for ( int i = 0; i < counts.Length; i++ )
			{
				Item.Type j = (Item.Type)i;
				counts[i].AddClickHandler( () => SelectItemType( j ) );
			}

			var selectedItemArea = RectTransform().Link( controls ).PinCenter( 180, 70, 100, 40, 0, 0 );
			selectedItemArea.name = "Selected item area";
			selected = ItemIcon( selectedItemType ).Link( selectedItemArea ).PinCenter( 0, 0, 2 * iconSize, 2 * iconSize, 0.5f, 0.5f ).AddClickHandler( () => ShowRoutesFor( selectedItemType ) );
			selected.SetTooltip( "LMB to see a list of routes using this item type at this stock\nRMB to change cart orders for this item" ).name = "Selected item";;
			inputMin = Text().Link( selectedItemArea ).Pin( 0, 0, 40, iconSize, 0, 1 ).
			SetTooltip( "If this number is higher than the current content, the stock will request new items at high priority", null, "LMB+drag left/right to change" );
			inputMin.alignment = TextAnchor.MiddleCenter;
			inputMax = Text().Link( selectedItemArea ).Pin( -30, 0, 40, iconSize, 1, 1 ).
			SetTooltip( "If the stock has at least this many items, it will no longer accept surplus", null, "LMB+drag left/right to change" );
			inputMax.alignment = TextAnchor.MiddleCenter;
			outputMin = Text().Link( selectedItemArea ).Pin( 0, 20, 40, iconSize, 0, 0 ).
			SetTooltip( "The stock will only supply other buildings with the item if it has at least this many", null, "LMB+drag left/right to change" );
			outputMin.alignment = TextAnchor.MiddleCenter;
			outputMax = Text().Link( selectedItemArea ).Pin( -30, 20, 40, iconSize, 1, 0 ).
			SetTooltip( "If the stock has more items than this number, then it will send the surplus even to other stocks", null, "LMB+drag left/right to change" );
			outputMax.alignment = TextAnchor.MiddleCenter;
			Image( Icon.cart ).Link( selectedItemArea ).PinCenter( 20, 0, iconSize * 2, iconSize * 2, 1, 0.5f );
			cartInput = Text().Link( selectedItemArea ).Pin( 0, 0, 40, iconSize, 1, 1 ).AddClickHandler( () => StartCart( true ), UIHelpers.ClickType.right ).
			SetTooltip( "The stock will try to order items from other stocks by cart, if the number of items in this stock is less than this number", null, "LMB+drag left/right to change\nRMB to set a value which start the route" );
			cartInput.alignment = TextAnchor.MiddleCenter;
			cartInput.AddOutline().color = Color.white;
			cartOutput = Text().Link( selectedItemArea ).Pin( 0, 20, 40, iconSize, 1, 0 ).AddClickHandler( () => StartCart( false ), UIHelpers.ClickType.right ).
			SetTooltip( "If the stock has more items than this number, then the cart will distribute it to other stocks if needed", null, "LMB+drag left/right to change\nRMB to set a value which start the route" );
			cartOutput.alignment = TextAnchor.MiddleCenter;
			cartOutput.AddOutline().color = Color.white;
			selectedInput = Image( Icon.rightArrow ).Link( selected ).PinCenter( 0, 0, iconSize, iconSize, 0, 0.7f );
			selectedInput.color = new Color( 1, 0.75f, 0.15f );
			selectedOutput = Image( Icon.rightArrow ).Link( selected ).PinCenter( 0, 0, iconSize, iconSize, 1, 0.7f );
			selectedOutput.color = new Color( 1, 0.75f, 0.15f );

			Image( Icon.reset ).Link( controls ).Pin( 180, 40, iconSize, iconSize, 0, 0 ).AddClickHandler( stock.ClearSettings ).SetTooltip( "Reset all values to default" ).name = "Reset";
			Image( Icon.cart ).Link( controls ).Pin( 205, 40, iconSize, iconSize, 0, 0 ).AddClickHandler( ShowCart ).SetTooltip( "Show the cart of the stock", null, 
			$"Every stock has a cart which can transport {Constants.Stock.cartCapacity} items at the same time. " +
			"For optimal performance the cart should be used for long range transport, haulers should be restricted by areas only to short range local transport. " +
			"To utilize carts, you have to increase either the cart input or cart output numbers. Select an item type and look for the two numbers above the cart icon on the bottom." ).name = "Show cart";
			Image( Icon.destroy ).Link( controls ).Pin( 230, 40, iconSize, iconSize, 0, 0 ).AddClickHandler( Remove ).SetTooltip( "Remove the stock, all content will be lost!" ).name = "Remover";
			Image( Icon.buildings ).Link( controls ).Pin( 155, 40, iconSize, iconSize, 0, 0 ).AddClickHandler( () => BuildingList.Create().Open( Building.Type.stock ) ).SetTooltip( "Show a list of buildings with the same type" );
			UpdateRouteIcons();
			currentStockCRC = StockCRC();
		}

		void ItemTypeAction( ItemImage image, Item.Type itemType, int code )
		{
			switch ( code )
			{
				case 0:
				{
					SelectItemType( itemType );
					break;
				}
				case 1:
				{
					ShowRoutesFor( itemType );
					break;
				}
				case 2:
				{
					LogisticList.Create().Open( stock, itemType, ItemDispatcher.Potential.Type.request );
					break;
				}
				case 3:
				{
					LogisticList.Create().Open( stock, itemType, ItemDispatcher.Potential.Type.offer );
					break;
				}
				case 4:
				{
					stock.itemData[(int)itemType].content = 0;
					world.lastChecksum = 0;
					break;
				}
				case 5:
				{
					stock.itemData[(int)itemType].content++;
					world.lastChecksum = 0;
					break;
				}
			}
		}

		void PopupForItemType( Dropdown d )
		{
			d.value = d.options.Count - 1;
			d.Show();
		}

		void ShowCart()
		{
			if ( stock.cart.taskQueue.Count == 0 )
				return;

			UnitPanel.Create().Open( stock.cart, true );
		}

		void ShowRoutesFor( Item.Type itemType )
		{
			RouteList.Create().Open( stock, itemType, stock.itemData[(int)itemType].outputRoutes.Count > 0 ? true : false );
		}


		void Remove()
		{
			RemoveBuilding( stock );
			Close();
		}

		void OnRename()
		{
			stock.moniker = name.text;
		}

		public override void Update()
		{
			base.Update();
			if ( currentStockCRC != StockCRC() )
				RecreateControls();
			for ( int i = 0; i < (int)Item.Type.total; i++ )
			{
				Color c = Color.black;
				if ( stock.itemData[i].content < stock.itemData[i].inputMin )
					c = Color.red.Dark();
				if ( stock.itemData[i].content > stock.itemData[i].inputMax )
					c = Color.green.Dark();
				counts[i].color = c;
				counts[i].text = stock.itemData[i].content + " (+" + stock.itemData[i].onWay + ")";
			}
			total.text = stock.total + " => " + stock.totalTarget;
			selected?.SetType( selectedItemType, false );
			int t = (int)selectedItemType;
			if ( channelText != inputMin ) inputMin.text = stock.itemData[t].inputMin + "<";
			if ( channelText != outputMin ) outputMin.text = stock.itemData[t].outputMin + "<";
			if ( channelText != inputMax ) inputMax.text = "<" + stock.itemData[t].inputMax;
			if ( channelText != outputMax ) outputMax.text = "<" + stock.itemData[t].outputMax;
			if ( channelText != cartInput ) cartInput.text = stock.itemData[t].cartInput.ToString();
			if ( channelText != cartOutput ) cartOutput.text = stock.itemData[t].cartOutput.ToString();

			if ( GetKeyDown( KeyCode.Mouse0 ) )
			{
				lastMouseXPosition = Input.mousePosition.x;
				var g = GetUIElementUnderCursor();
				void CheckChannel( GameObject widget, Stock.Channel channel, int min, int max, string pattern, bool adjustInputMin = false )
				{
					if ( g == widget )
					{
						this.channel = channel;
						channelText = widget.GetComponent<Text>();
						channelPattern = pattern;
						currentValue = stock.itemData[t].ChannelValue( channel );
						this.min = min;
						this.max = max;
						disableDrag = true;
						var l = (int)(Constants.Stock.cartCapacity * 1.5);
						if ( adjustInputMin && stock.itemData[t].inputMax < l )
							oh.ScheduleStockAdjustment( stock, selectedItemType, Stock.Channel.inputMax, l );
					}
				}
				CheckChannel( inputMin.gameObject, Stock.Channel.inputMin, 0, stock.itemData[t].inputMax, "{0}<" );
				CheckChannel( inputMax.gameObject, Stock.Channel.inputMax, stock.itemData[t].inputMin, stock.maxItems, "<{0}" );
				CheckChannel( outputMin.gameObject, Stock.Channel.outputMin, 0, stock.itemData[t].outputMax, "{0}<" );
				CheckChannel( outputMax.gameObject, Stock.Channel.outputMax, stock.itemData[t].outputMin, stock.maxItems, "<{0}" );
				CheckChannel( cartInput.gameObject, Stock.Channel.cartInput, 0, Constants.Stock.defaultmaxItems, "{0}", true );
				CheckChannel( cartOutput.gameObject, Stock.Channel.cartOutput, 0, Constants.Stock.defaultmaxItems, "{0}", true );
			}

			if ( currentValue >= 0 )
			{
				if ( GetKey( KeyCode.Mouse0 ) )
				{
					currentValue += (int)( ( Input.mousePosition.x - lastMouseXPosition ) * 0.2f );
					if ( currentValue < min )
						currentValue = min;
					if ( currentValue > max )
						currentValue = max;
					channelText.text = String.Format( channelPattern, currentValue );
					lastMouseXPosition = Input.mousePosition.x;
				}
				else
				{
					oh.ScheduleStockAdjustment( stock, selectedItemType, channel, currentValue );
					disableDrag = false;
					currentValue = -1;
					channelText = null;
				}
			}
		}

		void StartCart( bool input )
		{
			var t = stock.itemData[(int)selectedItemType];
			var l = (int)( Constants.Stock.cartCapacity * 1.5 );
			oh.StartGroup( "Start a new cart route" );
			if ( input )
			{
				if ( t.cartInput < 5 )
					oh.ScheduleStockAdjustment( stock, selectedItemType, Stock.Channel.cartInput, 5, false );
			}
			else
			{
				if ( t.cartOutput < Constants.Stock.cartCapacity )
					oh.ScheduleStockAdjustment( stock, selectedItemType, Stock.Channel.cartOutput, Constants.Stock.cartCapacity, false );
			}

			if ( t.inputMax < l )
				oh.ScheduleStockAdjustment( stock, selectedItemType, Stock.Channel.inputMax, l, false );
		}
	}

	public class NodePanel : Panel
	{
		public Node node;

		public static NodePanel Create()
		{
			return new GameObject().AddComponent<NodePanel>();
		}

		public void Open( Node node, bool show = false )
		{
			noResize = true;
			base.Open( node, 0, 0, 380, 180 );
			this.node = node;
			name = "Node panel";
#if DEBUG
			BuildButton( 20, -60, "Tree", !node.block && node.CheckType( Node.Type.land ), AddTree );
			BuildButton( 20, -80, "Remove", node.block, Remove );
			BuildButton( 20, -100, "Raise", true, delegate { AlignHeight( 0.1f ); } );
			BuildButton( 20, -120, "Lower", true, delegate { AlignHeight( -0.1f ); } );
			BuildButton( 20, -140, "Cave", !node.block, AddCave );

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
				eye.FocusOn( node, true );
		}

		void BuildButton( int x, int y, string title, bool enabled, Action action )
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
			node.AddResourcePatch( resourceType, 3, 10, overwrite:true );
		}

		void AddTree()
		{
			Resource.Create().Setup( node, Resource.Type.tree )?.life.Start( -2 * Constants.Resource.treeGrowthTime );
		}

		void AddCave()
		{
			Resource.Create().Setup( node, Resource.Type.animalSpawner );
		}

		void Remove()
		{
			if ( node.resources.Count > 0 )
				node.resources[0].Remove();
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
			base.Open( null, 0, 0, 360, 460 );
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
					if ( workshop.type == type && workshop.team == root.mainTeam )
						c++;
				var b = BuildButton( i % 2 == 0 ? 20 : 180, row, $"{type.ToString().GetPrettyName()} ({c})", delegate { BuildWorkshop( type ); } );
				string tooltip = "";
				var o = Workshop.GetConfiguration( type );
				if ( o.inputs != null && o.inputs.Length > 0 )
				{
					tooltip += "Requires ";
					for ( int h = 0; h < o.inputs.Length; h++ )
					{
						if ( h > 0 )
						{
							if ( h == o.inputs.Length - 1 && o.inputs.Length > 1 )
							{
								if ( o.commonInputs )
									tooltip += " or ";
								else
									tooltip += " and ";
							}
							else
								tooltip += ", ";
						}
						tooltip += o.inputs[h].itemType.ToString().GetPrettyName( false );
					}
					tooltip += "\n";
				}
				if ( o.outputType != Item.Type.unknown )
					tooltip += $"Produces {( o.outputStackSize > 1 ? "2*" : "")}{o.outputType.ToString().GetPrettyName( false )}\n";
				tooltip += $"Production time {(o.productionTime * Time.fixedDeltaTime).ToString( "F2" )}s";

				string additionalTooltip = type switch 
				{
					Workshop.Type.barrack => "Final building in the production chain, produces soldiers.",
					Workshop.Type.bowMaker => "Huge building producing one of the weapons required for soldiers.",
					Workshop.Type.brewery => "Beer is needed by both the barracks to produce soldiers, and the butcher to produce pork.",
					Workshop.Type.butcher => "This building produces one type of the food for mines, pork. To have an optimal supply of mines with food, " +
						"both pork and pretzel should be produced.",
					Workshop.Type.coalMine => "Most important type of mine, accepts all kind of food.",
					Workshop.Type.wheatFarm => "Produces grain. Need free green space around.",
					Workshop.Type.cornFarm => "Produces corn. Need free green space around.",
					Workshop.Type.fishingHut => "Simpliest building to produce food. Salt mines only accept fish, so unavoidable there. Should be built close to water.",
					Workshop.Type.forester => "This building doesn't produce or need anything, just plants trees around the house in the brown area.",
					Workshop.Type.jeweler => "Jewelry is needed by the barrack to produce soldiers.",
					Workshop.Type.goldMine => "This type of mine does not accept fish, only pretzel or pork.",
					Workshop.Type.hunter => "The hunter captures and kills wild rabbits, and produces hide which is needed for bow. Should be built close to the wild " +
						" animal spawners (weird rock with bunnies around)",
					Workshop.Type.ironMine => "Iron is needed to produce weapons.",
					Workshop.Type.mill => "Can only be built on the high mountains.",
					Workshop.Type.saltMine => "Salt is needed to produce pretzel, and important food for other mines.",
					Workshop.Type.sawmill => "Planks are needed by constructions at the beginning, later for bows.",
					Workshop.Type.smelter => "The slowest building in the game, probably need two for a single weapon maker.",
					Workshop.Type.stonemason => "Gathers rock from the surface, which is needed for construction only. Should be built close to rocks.",
					Workshop.Type.stoneMine => "As stone is not needed for soldier production yet, this building can be skipped.",
					Workshop.Type.weaponMaker => "Weapons are needed by the barrack to produce soldiers.",
					Workshop.Type.well => "Can only be built next to water.",
					Workshop.Type.woodcutter => "Should be built close to trees and brown area, close to a forester, which keeps planting the trees.",
					Workshop.Type.bakery => "Pretzel is one of the important food types, needs a salt mine.",
					_ => null
				};

				b.SetTooltip( tooltip, null, additionalTooltip  );

				if ( i % 2 != 0 )
					row -= 20;
			}
			BuildButton( 20, -400, "Junction", AddFlag ).SetTooltip( "Junction without a building", null, "Junctions can be built separately from a building, which can be added later. Junctions are " +
			" exclusive for haulers, so a junction with multiple roads with high traffic might be inefficient." );
			BuildButton( 180, -400, "Crossing", AddCrossing ).SetTooltip( "Crossing", null, "Crossings are like junctions, but they are not exclusive to halulers, so they can manage high traffic, " +
			" but they cannot be used as an exit for buildings" );

			BuildButton( 20, -420, "Guardhouse", AddGuardHouse ).SetTooltip( "Guard houses are needed to extend the border of the empire.", null, $"Only if a soldier occupies a guard house it extends the border. " +
			$"As there is only {Constants.Stock.startSoldierCount} soldiers are available at start, soldier production should start after building the first {Constants.Stock.startSoldierCount} guardhouses." );
			BuildButton( 180, -420, "Stock", AddStock ).SetTooltip( "Stocks are used to store items temporarily", null, "They are also very important as starting and end point of routes, so there should be a stock close to every buildings. Stocks can be built on hills also." +
			"See the route list for more details." );
		}

		Image BuildButton( int x, int y, string title, Action action )
		{
			Image button = Button( title ).Pin( x, y, 160 ).AddClickHandler( action );
			if ( !enabled )
			{
				Text text = button.gameObject.GetComponentInChildren<Text>();
				if ( text )
					text.color = Color.red;
			}
			return button;
		}

		void AddFlag()
		{
			NewBuildingPanel.Create( NewBuildingPanel.Construct.flag );
			Close();
		}

		void AddCrossing()
		{
			NewBuildingPanel.Create( NewBuildingPanel.Construct.crossing );
			Close();
		}

		void AddStock()
		{
			NewBuildingPanel.Create( NewBuildingPanel.Construct.stock );
			Close();
		}

		void AddGuardHouse()
		{
			NewBuildingPanel.Create( NewBuildingPanel.Construct.guardHouse );
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
					if ( workshops[i].type == type && workshops[i].team == root.mainTeam )
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
			NewBuildingPanel.Create( NewBuildingPanel.Construct.workshop, type );
			Close();
		}
	}

	public class NewBuildingPanel : Panel, IInputHandler
	{
		public HiveObject currentBlueprint;
		public WorkshopPanel currentBlueprintPanel;
		public Construct constructionMode = Construct.nothing;
		public Workshop.Type workshopType;
		public static int currentFlagDirection = 1;    // 1 is a legacy value.
		public Text testResult;

		static public Hotkey showNearestPossibleHotkey = new Hotkey( "Show nearest construction site", KeyCode.Tab, true );
		static public Hotkey showNearestPossibleAnyDirectionHotkey = new Hotkey( "Show nearest construction site with any direction", KeyCode.Tab );
		static public Hotkey rotateCWHotkey = new Hotkey( "Rotate Construction CW", KeyCode.Period );
		static public Hotkey rotateCCWHotkey = new Hotkey( "Rotate Construction CCW", KeyCode.Comma );

		public enum Construct
		{
			nothing,
			workshop,
			stock,
			guardHouse,
			flag,
			crossing
		}

		public static NewBuildingPanel Create( Construct type, Workshop.Type workshopType = Workshop.Type.unknown )
		{
			if ( 
				workshopType == Workshop.Type.coalMine || 
				workshopType == Workshop.Type.saltMine || 
				workshopType == Workshop.Type.goldMine ||
				workshopType == Workshop.Type.ironMine ||
				workshopType == Workshop.Type.stoneMine )
				root.viewport.nodeInfoToShow = Viewport.OverlayInfoType.nodeUndergroundResources;
			else
				root.viewport.nodeInfoToShow = Viewport.OverlayInfoType.nodePossibleBuildings;
			var p = new GameObject( "New building panel" ).AddComponent<NewBuildingPanel>();
			p.Open( type, workshopType );
			return p;
		}

		void Open( Construct type, Workshop.Type workshopType = Workshop.Type.unknown )
		{
			constructionMode = type;
			this.workshopType = workshopType;

			reopen = true;
			base.Open( 500, 150 );
			this.Pin( 40, 200, 500, 150, 0, 0 );
			Text( $"Building a new {(type==Construct.workshop?workshopType.ToString():type.ToString()).GetPrettyName( false )}", 14 ).Pin( borderWidth, -borderWidth, 460, 30 );
			Text( $"Press {rotateCWHotkey.keyName} or {rotateCCWHotkey.keyName} to rotate" ).PinDownwards( borderWidth, 0, 460 );
			Text( $"Press {showNearestPossibleAnyDirectionHotkey.keyName} to see the nearest possible constructuion site" ).PinDownwards( borderWidth, 0, 460 );
			Text( $"or {showNearestPossibleHotkey.keyName} to see the nearest possible place with this facing" ).PinDownwards( borderWidth, 0, 460 );
			testResult = Text() .PinDownwards( borderWidth, 0, 460 );

			root.viewport.inputHandler = this;
		}

		public void CancelBlueprint()
		{
			currentBlueprint?.Remove();
			currentBlueprint = null;
			if ( currentBlueprintPanel )
				currentBlueprintPanel.Close();
			currentBlueprintPanel = null;
		}

        public void OnLostInput()
        {
			CancelBlueprint();
			Close();
        }

		new void OnDestroy()
		{
			root.viewport.nodeInfoToShow = Viewport.OverlayInfoType.none;
			root.viewport.ResetInputHandler();
			base.OnDestroy();
		}

		void ShowTestResult( SiteTestResult t )
		{
			testResult.text = t.code switch
			{
				SiteTestResult.Result.fit => "Location is good, press LMB to finalize",
				SiteTestResult.Result.blocked => "Location is blocked",
				SiteTestResult.Result.buildingTooClose => "Another building is too close",
				SiteTestResult.Result.crossingInTheWay => "There is a crossing where the junction should be",
				SiteTestResult.Result.flagTooClose => "Another junction is too close",
				SiteTestResult.Result.heightAlreadyFixed => "This area already has a fixed height, cannot be flatten",
				SiteTestResult.Result.outsideBorder => "Outside of empire border",
				SiteTestResult.Result.wrongGroundType => $"Ground type not found: {t.groundTypeMissing.ToString().GetPrettyName( false )}",
				SiteTestResult.Result.wrongGroundTypeAtEdge => $"Ground type not found at edge: {t.groundTypeMissing.ToString().GetPrettyName( false )}",
				_ => "Unknown"
			};
		}

		public bool pickGroundOnly { get { return true; } }

        public bool OnMovingOverNode( Node node )
        {
			if ( constructionMode == Construct.flag || constructionMode == Construct.crossing )
				root.viewport.SetCursorType( Viewport.CursorType.building );
			else
				root.viewport.SetCursorType( Viewport.CursorType.building, currentFlagDirection );
			if ( currentBlueprint && currentBlueprint.location != node )
				CancelBlueprint();
			if ( currentBlueprint )
				return true;
			switch ( constructionMode )
			{
				case Construct.workshop:
				{
					ShowTestResult( Workshop.IsNodeSuitable( node, root.mainTeam, Workshop.GetConfiguration( workshopType ), currentFlagDirection ) );
					var workshop = Workshop.Create().Setup( node, root.mainTeam, workshopType, currentFlagDirection, true, Resource.BlockHandling.ignore );
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
					ShowTestResult( Flag.IsNodeSuitable( node, root.mainTeam ) );
					currentBlueprint = Flag.Create().Setup( node, root.mainTeam, true );
					break;
				};
				case Construct.crossing:
				{
					ShowTestResult( Flag.IsNodeSuitable( node, root.mainTeam ) );
					currentBlueprint = Flag.Create().Setup( node, root.mainTeam, true, true );
					break;
				};
				case Construct.stock:
				{
					ShowTestResult( Stock.IsNodeSuitable( node, root.mainTeam, currentFlagDirection ) );
					currentBlueprint = Stock.Create().Setup( node, root.mainTeam, currentFlagDirection, true, Resource.BlockHandling.ignore );
					break;
				};
				case Construct.guardHouse:
				{
					ShowTestResult( GuardHouse.IsNodeSuitable( node, root.mainTeam, currentFlagDirection ) );
					currentBlueprint = GuardHouse.Create().Setup( node, root.mainTeam, currentFlagDirection, true, Resource.BlockHandling.ignore );
					break;
				};
			};
			return true;
        }

        public bool OnNodeClicked( Node node )
        {
			if ( !currentBlueprint || root.viewport.rightButton )
				return true;

			if ( !root.world.roadTutorialShowed )
				RoadTutorialPanel.Create();
			if ( currentBlueprint is Building building )
			{
				oh.StartGroup();
				if ( building.flag.blueprintOnly )
					oh.ScheduleCreateFlag( building.flag.node, root.mainTeam, false, false );
				oh.ScheduleCreateBuilding( building.node, building.flagDirection, building.type, root.mainTeam, false );
			}
			if ( currentBlueprint is Flag flag )
				oh.ScheduleCreateFlag( flag.node, root.mainTeam, flag.crossing );
			currentBlueprint = null;
			currentBlueprintPanel?.Close();
			currentBlueprintPanel = null;
			constructionMode = Construct.nothing;
			return false;
        }

        public bool OnObjectClicked(HiveObject target)
        {
			return true;
        }

		new void Update()
		{
			base.Update();
			if ( showNearestPossibleHotkey.IsPressed() )
				ShowNearestPossible( false );
			if ( showNearestPossibleAnyDirectionHotkey.IsPressed() )
				ShowNearestPossible( true );
			if ( rotateCWHotkey.IsPressed() )
			{
				if ( currentFlagDirection == 0 )
					currentFlagDirection = 5;
				else
					currentFlagDirection--;
				CancelBlueprint();
			}
			if ( rotateCCWHotkey.IsPressed() )
			{
				if ( currentFlagDirection == 5 )
					currentFlagDirection = 0;
				else
					currentFlagDirection++;
				CancelBlueprint();
			}
		}

		void ShowNearestPossible( bool anyDirection )
		{
			if ( root.viewport.currentNode == null )
				return;
				
			List<int> possibleDirections = new List<int>();
			if ( anyDirection )
			{
				for ( int i = 0; i < Constants.Node.neighbourCount; i++ )
					possibleDirections.Add( i );
			}
			else
				possibleDirections.Add( currentFlagDirection );

			Node bestSite = null;
			int bestDistance = int.MaxValue;
			int bestFlagDirection = -1;
			foreach ( var o in Ground.areas[Constants.Ground.maxArea - 1] )
			{
				Node node = root.viewport.currentNode + o;
				foreach ( int flagDirection in possibleDirections )
				{
					bool suitable = false;
					switch ( constructionMode )
					{
						case Construct.workshop:
						{
							suitable = Workshop.IsNodeSuitable( node, root.mainTeam, Workshop.GetConfiguration( workshopType ), flagDirection, true );
							break;
						}
						case Construct.stock:
						{
							suitable = Stock.IsNodeSuitable( node, root.mainTeam, flagDirection, true );
							break;
						}
						case Construct.guardHouse:
						{
							suitable = GuardHouse.IsNodeSuitable( node, root.mainTeam, flagDirection, true );
							break;
						}
						default:
						{
							break;
						}
					}
					if ( suitable )
					{
						int distance = node.DistanceFrom( root.viewport.currentNode );
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
				eye.FocusOn( bestSite, true, true );
				currentFlagDirection = bestFlagDirection;
			}
		}

    }

	public class RoadPanel : Panel, IInputHandler
	{
		public List<ItemImage> leftItems = new List<ItemImage>(), rightItems = new List<ItemImage>(), centerItems = new List<ItemImage>();
		public List<Text> leftNumbers = new List<Text>(), rightNumbers = new List<Text>(), centerDirections = new List<Text>();
		public Node node;
		public Dropdown targetHaulerCount;
		public Text jam;
		public Text units;
		public Image ring;
		public Text lastUsedText;

		const int itemsDisplayed = 3;

		public Road road { get { return node.road; } }

		public static RoadPanel Create()
		{
			return new GameObject().AddComponent<RoadPanel>();
		}

		public void Open( Road road, Node node )
		{
			borderWidth = 10;
			noResize = true;
			offset = new Vector2( 100, 0 );
			base.Open( node, 0, 0, 210, 185 );
			this.node = node;
			Image( Icon.hauler ).Pin( 170, -10 ).AddClickHandler( Hauler ).SetTooltip( "Show the hauler working on this road" );
			Image( Icon.destroy ).Pin( 150, -10 ).AddClickHandler( Remove ).SetTooltip( "Remove the road" );
			Image( Icon.junction ).Pin( 130, -10, 20, 20 ).AddClickHandler( Split ).SetTooltip( "Split the road by inserting a junction at the selected location" );
			jam = Text( "Jam" ).Pin( 12, -4, 120 );
			units = Text( "Hauler count" ).Pin( 12, -28, 120 );
			name = "Road panel";
			targetHaulerCount = Dropdown().Pin( 20, -44, 150, 25 ).SetTooltip( "Number of haluers working on the road", null, 
			"By default new haluers will automatically assigned to the road when needed (and retire when not needed any longer), but you can also specify the desired number of haulers on the road." );
			targetHaulerCount.AddOptions( new List<string> { "Auto", "1", "2", "3", "4" } );
			targetHaulerCount.value = road.targetHaulerCount;
			targetHaulerCount.onValueChanged.AddListener( TargetUnitCountChanged );

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
			lastUsedText = Text( "Not yet used" ).Pin( borderWidth, -155, 180 );
			ring = Image( Icon.ring ).Link( root );
			ring.transform.SetAsFirstSibling();
			ring.color = new Color( 0, 1, 1 );

			root.viewport.inputHandler = this;
		}

		void Remove()
		{
			if ( road )
				oh.ScheduleRemoveRoad( road );
			Close();
		}

		void Hauler()
		{
			if ( GetKey( KeyCode.LeftShift ) )
				road.CallNewHauler();
			else
				UnitPanel.Create().Open( road.haulers[0], true ); // TODO Make it possibe to view additional haulers
		}

		void Split()
		{
			oh.ScheduleCreateFlag( node, root.mainTeam );
			FlagPanel.Create().Open( null, node );
			Close();
			ValidateAll();
		}

		void TargetUnitCountChanged( int newValue )
		{
			if ( road && road.targetHaulerCount != newValue )
				oh.ScheduleChangeRoadHaulerCount( road, newValue );
		}

		new public void OnDestroy()
		{
			base.OnDestroy();
			Destroy( ring.gameObject );
		}

		public override void Update()
		{

			if ( road == null )
				return;

			base.Update();
			jam.text = "Items waiting: " + road.jam;
			units.text = "Hauler count: " + road.haulers.Count;

			bool reversed = false;
			var camera = eye.cameraGrid.center;
			float x0 = camera.WorldToScreenPoint( road.nodes[0].position ).x;
			float x1 = camera.WorldToScreenPoint( road.lastNode.position ).x;
			if ( x1 < x0 )
				reversed = true;

			for ( int j = 0; j < itemsDisplayed; j++ )
			{
				int i = itemsDisplayed - 1 - j;
				Unit hauler;
				if ( j < road.haulers.Count && (hauler = road.haulers[j]) && hauler.taskQueue.Count > 0 )
				{
					Item item = hauler.itemsInHands[0];	// TODO show the second item somehow	
					centerItems[i].SetItem( item );
					if ( item )
					{
						Flag flag = item.nextFlag;
						if ( flag == null && item.destination )
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
			{
				targetHaulerCount.value = road.targetHaulerCount;
				if ( !road.lastUsed.empty )
					lastUsedText.text = $"Last used {UIHelpers.TimeToString( road.lastUsed.age )} ago";
			}

			var c = HiveCommon.eye.cameraGrid.center;
			var p = c.WorldToScreenPoint( node.positionInViewport );
			ring.transform.position = p;
			float scale;
			if ( c.orthographic )
			{
				var f = c.WorldToScreenPoint( node.Neighbour( 0 ). positionInViewport );
				scale = ( p - f ).magnitude / 200;
			}
			else
				scale = 5 / p.z;
			ring.transform.localScale = Vector3.one * scale * uiScale;

			for ( int i = 0; i < Constants.Node.neighbourCount; i++ )
			{
				if ( !road.Move( road.nodes.IndexOf( node ), i, true ) )
					continue;

				var n = node.Neighbour( i );
				Graphics.DrawMesh( Viewport.plane, Matrix4x4.TRS( n.positionInViewport + Vector3.up * 0.2f, Quaternion.Euler( 0, 30 - i * 60, 0 ), Vector3.one * 0.6f ), Viewport.arrowMaterial, 0 );
			}
		}

        public bool OnMovingOverNode( Node node )
        {
			return keepGoing;
        }

        public bool OnNodeClicked( Node node )
        {
			if ( road )
			{
				var index = road.nodes.IndexOf( this.node );
				var dir = this.node.DirectionTo( node );
				if ( !root.viewport.rightButton && road.Move( index, dir, true ) )
				{
					oh.ScheduleMoveRoad( road, index, dir );
					target = this.node = node;
				}
			}

			if ( root.viewport.rightButton && node.road == road )
				target = this.node = node;
			return keepGoing;
        }

        public bool OnObjectClicked( HiveObject target )
        {
			return root.viewport.OnObjectClicked( target );
        }

        public void OnLostInput()
        {
			Close();
        }
    }

	public class FlagPanel : Panel, IInputHandler
	{
		public Flag flag;
		public Node node;
		public ItemImage[] items = new ItemImage[Constants.Flag.maxItems];
		public Image[] itemTimers = new Image[Constants.Flag.maxItems];
		public Image shovelingIcon, convertIcon;

		public static FlagPanel Create()
		{
			return new GameObject().AddComponent<FlagPanel>();
		}

		public void Open( Flag flag, Node node = null, bool show = false )
		{
#if DEBUG
			Selection.activeGameObject = flag?.gameObject;
#endif
			borderWidth = 10;
			noResize = true;
			if ( base.Open( flag, 0, 0, 250, 75 ) )
				return;

			this.flag = flag;
			this.node = node;
			int col = 16;
			Image( Icon.destroy ).Pin( 210, -45 ).AddClickHandler( Remove ).SetTooltip( "Remove the junction" );
			Image( Icon.newRoad ).Pin( 20, -45 ).AddClickHandler( StartRoad ).SetTooltip( "Connect this junction to another one using a road" );
			Image( Icon.magnet ).PinSideways( 0, -45 ).AddClickHandler( Capture ).SetTooltip( "Merge nearby roads to this junction" );
			shovelingIcon = Image( Icon.shovel ).PinSideways( 0, -45 ).AddClickHandler( Flatten ).SetTooltip( "Call a builder to flatten the area around this junction" );
			convertIcon = Image( Icon.crossing ).PinSideways( 0, -45 ).AddClickHandler( Convert ).SetTooltip( "Convert this junction to a crossing and vice versa", null, 
			"The difference between junctions and crossings is that only a single haluer can use a junction at a time, while crossings are not exclusive. Junctions in front of buildings cannot be crossings, and buildings cannot be built ar crossings." );

			for ( int i = 0; i < Constants.Flag.maxItems; i++ )
			{
				itemTimers[i] = Image().Pin( col, -8, iconSize, 3 );
				items[i] = ItemIcon().Pin( col, -13 );
				int j = i;
				items[i].name = "item " + i;
				col += iconSize+5;
			}
			name = "Flag panel";
			if ( show && flag )
				eye.FocusOn( flag, true );
			Update();

			root.viewport.inputHandler = this;
		}

		void Capture()
		{
			oh.ScheduleCaptureRoad( flag );
		}

		void Remove()
		{
			if ( flag && flag != root.mainTeam?.mainBuilding?.flag )
				oh.ScheduleRemoveFlag( flag );
			Close();
		}

		void StartRoad()
		{
			if ( flag && root.playerInCharge )
			{
				Road road = Road.Create().Setup( flag );
				root.viewport.inputHandler = road;
				root.viewport.showGridAtMouse = true;
				if ( !root.world.createRoadTutorialShowed )
				{
					root.world.createRoadTutorialShowed = true;
					tooltip.SetText( road, "Connect to another junction or middle of another road, or press Shift+LMB on an open spot to create a new junction" );
				}
			}
			Close();
		}

		void Convert()
		{
			oh.ScheduleChangeFlagType( flag );
		}

		void Flatten()
		{
			oh.ScheduleFlattenFlag( flag );
		}

		public override void Update()
		{
			if ( flag == null )
				flag = node.flag;
			base.Update();

			if ( flag == null )
				return;

			for ( int i = 0; i < Constants.Flag.maxItems; i++ )
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
						itemTimers[i].rectTransform.sizeDelta = new Vector2( Math.Min( iconSize, timeAtFlag / Constants.World.normalSpeedPerSecond / 60 ), 3 );
						itemTimers[i].color = Color.Lerp( Color.green, Color.red, timeAtFlag / Constants.World.normalSpeedPerSecond / 600f );
					}
					else
						items[i].SetInTransit( true );
				}
			}

			if ( flag.flattening != null )	// This should never be null unless after loaded old files.
				shovelingIcon.color = flag.flattening.builder ? Color.grey : Color.white;
			convertIcon.color = flag.crossing ? Color.red : Color.white;

			if ( flag.Buildings().Count > 0 )
				return;
			for ( int i = 0; i < Constants.Node.neighbourCount; i++ )
			{
				var n = flag.node.Neighbour( i );
				if ( !flag.Move( i, true ) )
					continue;
				Graphics.DrawMesh( Viewport.plane, Matrix4x4.TRS( n.positionInViewport + Vector3.up * 0.2f, Quaternion.Euler( 0, 30 - i * 60, 0 ), Vector3.one * 0.6f ), Viewport.arrowMaterial, 0 );
			}
		}

        public bool OnMovingOverNode( Node node )
        {
			return keepGoing;
        }

        public bool OnNodeClicked( Node node )
        {
			int i = flag.node.DirectionTo( node );
			if ( i >= 0 && !root.viewport.rightButton && flag.Move( i, true ) )
				oh.ScheduleMoveFlag( flag, i );
			else
				root.viewport.OnNodeClicked( node );
			return keepGoing;
        }

        public bool OnObjectClicked(HiveObject target)
        {
			return root.viewport.OnObjectClicked( target );
        }

        public void OnLostInput()
        {
			Close();
        }
    }

	public class UnitPanel : Panel, Eye.IDirector
	{
		public Unit unit;
		public Text itemCount;
		public Stock cartDestination;
		public PathVisualization cartPath;
		public Text status;
		public ItemImage statusImage0, statusImage1;
		public Unit.Task lastFirstTask;
		public bool uninitializedStatus;
		public HiveObject targetObject;

		public static UnitPanel Create()
		{
			return new GameObject().AddComponent<UnitPanel>();
		}

		public void Open( Unit unit, bool show )
		{
			borderWidth = 10;
			noResize = true;
			if ( base.Open( unit.node, 0, 0, 200, 95 ) )
				return;
			name = "Unit panel";
			this.unit = unit;
			status = Text().Pin( 20, -20, 200, 50 ).AddClickHandler( ShowTarget );
			statusImage0 = ItemIcon().Pin( 80, -20 );
			statusImage0.gameObject.SetActive( false );
			statusImage1 = ItemIcon().Pin( 130, -20 );
			statusImage1.gameObject.SetActive( false );
			itemCount = Text( "Items" ).Pin( 20, -70, 160 );
			uninitializedStatus = true;

			Image( Icon.home ).Pin( 160, 20, iconSize, iconSize, 0, 0 ).AddClickHandler( ShowHome ).SetTooltip( "Show the home of the unit" );

			if ( show )
				eye.GrabFocus( this );
#if DEBUG
			Selection.activeGameObject = unit.gameObject;
#endif
		}

		void ShowTarget()
		{
			if ( targetObject )
				eye.FocusOn( targetObject.location, true );
		}

		void ShowHome()
		{
			if ( unit.type == Unit.Type.tinkerer )
				unit.building.OnClicked( true );

			if ( unit.type == Unit.Type.cart )
				unit.building.OnClicked( true );

			if ( unit.type == Unit.Type.hauler )
				unit.road.OnClicked( true );

			if ( unit.type == Unit.Type.constructor )
				unit.team.mainBuilding.OnClicked( true );

			if ( unit.type == Unit.Type.soldier && unit.building )
				unit.building.OnClicked( true );
		}

		public override CompareResult IsTheSame( Panel other )
		{
			var p = other as UnitPanel;
			if ( p == null )
				return CompareResult.different;

			if ( p.unit == this.unit )
				return CompareResult.same;

			return CompareResult.sameButDifferentTarget;
		}

		public override void OnDoubleClick()
		{
			eye.GrabFocus( this );
		}

		public void SetCameraTarget( Eye eye )
		{
			eye.FocusOn( unit, approach:false );
		}

		public override void Update()
		{
			if ( unit == null )
			{
				Close();
				return;
			}
			base.Update();
			var cart = unit as Stock.Cart;

			Unit.Task firstTask = null;
			if ( unit.taskQueue.Count > 0 )
				firstTask = unit.taskQueue.First();
			if ( lastFirstTask != firstTask || uninitializedStatus )
			{
				lastFirstTask = firstTask;
				uninitializedStatus = false;
				targetObject = null;
				statusImage0.SetItem( null );
				statusImage1.SetItem( null );
				statusImage0.gameObject.SetActive( false );
				statusImage1.gameObject.SetActive( false );
				switch( unit.type )
				{
					case Unit.Type.hauler:
					{
						var pickup = unit.FindTaskInQueue<Unit.PickupItem>();
						if ( pickup != null )
						{
							status.text = "Picking up";
							statusImage0.SetItem( pickup.items[0] );
							statusImage0.gameObject.SetActive( true );
							targetObject = pickup.items[0].flag;
							break;
						}
						var deliver = unit.FindTaskInQueue<Unit.DeliverItem>();
						if ( deliver )
						{
							status.text = "Delivering";
							statusImage0.SetItem( deliver.items[0] );
							statusImage0.gameObject.SetActive( true );
							if ( deliver.items[1] )
							{
								status.text += "          and";
								statusImage1.SetItem( deliver.items[1] );
								statusImage1.gameObject.SetActive( true );
							}
							targetObject = (HiveObject)deliver.items[0].nextFlag ?? deliver.items[0].destination;
							break;
						}
						var startWorking = unit.FindTaskInQueue<Unit.StartWorkingOnRoad>();
						if ( startWorking )
						{
							status.text = "Going to a road to start working\nas a hauler";
							break;
						}
						status.text = "Waiting for something to do";
						break;
					}
					case Unit.Type.tinkerer:
					{
						var res = unit.FindTaskInQueue<Workshop.GetResource>();
						if ( res )
						{
							status.text = "Getting " + res.resource.type.ToString();
							targetObject = res.resource;
							break;
						}
						var plant = unit.FindTaskInQueue<Workshop.Plant>();
						if ( plant )
						{
							status.text = "Planting " + plant.resourceType.ToString();
							targetObject = plant.node;
							break;

						}
						var deliver = unit.FindTaskInQueue<Unit.DeliverItem>();
						if ( deliver )
						{
							if ( unit == unit.building.tinkerer )
								status.text = "Bringing             home";
							else
								status.text = "Releasing";
							statusImage0.SetItem( deliver.items[0] );
							statusImage0.gameObject.SetActive( true );
							break;
						}
						var step = unit.FindTaskInQueue<Unit.WalkToNeighbour>();
						if ( step )
						{
							status.text = "Going home";
							break;
						}

						status.text = "Waiting for something to do";
						break;
					}
					case Unit.Type.constructor:
					{
						var flattening = unit.FindTaskInQueue<Unit.Callback>();
						if ( flattening )
						{
							status.text = "Flattening land";
							break;
						}
						if ( unit.taskQueue.Count == 0 )
							status.text = "Constructing";
						else
							status.text = "Going to construction site";
						break;
					}
					case Unit.Type.soldier:
					{
						if ( unit.building )
						{
							if ( unit.team != unit.building.team )
								status.text = "Attacking";
							else if ( unit.building.type == (Building.Type)Workshop.Type.barrack )
								status.text = "Joining the army";
							else
								status.text = "Guarding";
						}
						else
							status.text = "Walking to the post";
						break;
					}
					case Unit.Type.cart:
					{
						var massDeliver = unit.FindTaskInQueue<Stock.DeliverStackTask>();
						if ( massDeliver )
						{
							status.text = $"Transporting {Constants.Stock.cartCapacity}";
							statusImage1.SetType( (unit as Stock.Cart).itemType );
							statusImage1.gameObject.SetActive( true );
							targetObject = massDeliver.stock;
							break;
						}
						status.text = "Returning home";
						if ( unit.taskQueue.Count == 0 )
							Close();
						break;
					}
					case Unit.Type.unemployed:
					{
						status.text = "Going back to the headquarters";
						break;
					}
				}
			}

if ( cart )
			{
				if ( cart.destination != cartDestination )
				{
					cartDestination = cart.destination;
					var path = cart.FindTaskInQueue<Unit.WalkToFlag>()?.path;
					Destroy( cartPath );
					cartPath = PathVisualization.Create().Setup( path );
				}
			}

			itemCount.text = "Items delivered: " + unit.itemsDelivered;

			if ( followTarget )
				MoveTo( unit.transform.position + Vector3.up * Constants.Node.size );
		}

		public new void OnDestroy()
		{
			base.OnDestroy();
			eye.ReleaseFocus( this );
			Destroy( cartPath );
		}
	}

	public class ConstructionPanel : BuildingPanel
	{
		public ProgressBar progress;
		public Building.Construction construction;
		public WorkshopPanel.Buffer planks;
		public WorkshopPanel.Buffer stones;
		public Text neededText, hereText, onWayText, missingText;

		public static ConstructionPanel Create()
		{
			return new GameObject( "Contruction panel" ).AddComponent<ConstructionPanel>();
		}

		public void Open( Building.Construction construction, bool show = false )
		{
			base.Open( construction.boss, 220, 250 );
			this.construction = construction;
			Image( Icon.house ).Pin( -60, 30, iconSize, iconSize, 1, 0 ).AddClickHandler( OpenFinalPanel ).SetTooltip( "Open the final panel which will become actual only after the construction has been finished" );
			Image( Icon.destroy ).PinSideways( 0, 30, iconSize, iconSize, 1, 0 ).AddClickHandler( Remove );

			Text( construction.boss.title ).Pin( 20, -20, 160 );
			Text( "Under construction", 10 ).PinDownwards( 20, 0, 160 );

			neededText = Text().PinDownwards( borderWidth, 0, 200, 25 );
			neededText.alignment = TextAnchor.MiddleLeft;

			hereText = Text().PinDownwards( borderWidth, 0, 200, 25 );
			hereText.alignment = TextAnchor.MiddleLeft;

			onWayText = Text().PinDownwards( borderWidth, 0, 200, 25 );
			onWayText.alignment = TextAnchor.MiddleLeft;

			missingText = Text().PinDownwards( borderWidth, 0, 200, 25 );
			missingText.alignment = TextAnchor.MiddleLeft;
			var row = UIHelpers.currentRow;

			ItemIcon( Item.Type.plank ).Link( neededText ).Pin( 60, 0 );
			ItemIcon( Item.Type.stone ).Link( neededText ).Pin( 120, 0 );
			ItemIcon( Item.Type.plank ).Link( hereText ).Pin( 57, 0 );
			ItemIcon( Item.Type.stone ).Link( hereText ).Pin( 117, 0 );
			ItemIcon( Item.Type.plank ).Link( onWayText ).Pin( 80, 0 );
			ItemIcon( Item.Type.stone ).Link( onWayText ).Pin( 140, 0 );
			ItemIcon( Item.Type.plank ).Link( missingText ).Pin( 60, 0 );
			ItemIcon( Item.Type.stone ).Link( missingText ).Pin( 120, 0 );

			planks = new WorkshopPanel.Buffer();
			planks.Setup( this, Item.Type.plank, construction.boss.configuration.plankNeeded, 20, row - 5, iconSize + 5 );
			stones = new WorkshopPanel.Buffer();
			stones.Setup( this, Item.Type.stone, construction.boss.configuration.stoneNeeded, 120, row - 5, iconSize + 5 );

			progress = Progress().Pin( 20, row - 30, ( iconSize + 5 ) * 4 );

			if ( show )
				eye.FocusOn( construction.boss, true );
		}

		public void OpenFinalPanel( )
		{
			if ( construction.boss is Stock s )
			{
				var p = StockPanel.Create();
				p.reopen = true;
				p.Open( s );

			}
			if ( construction.boss is Workshop w )
			{
				var p = WorkshopPanel.Create();
				p.reopen = true;
				p.Open( w );
			}
			Close();
		}

		public new void Update()
		{
			if ( construction.boss.destroyed )
			{
				Close();
				return;
			}
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

			neededText.text = $"Needed: {construction.boss.configuration.plankNeeded}         and {construction.boss.configuration.stoneNeeded}";
			hereText.text = $"Arrived: {construction.plankArrived}         and {construction.stoneArrived}";
			onWayText.text = $"On the way: {construction.plankOnTheWay}         and {construction.stoneOnTheWay}";
			missingText.text = $"Missing: {construction.plankMissing}         and {construction.stoneMissing}";
		}

		void Remove()
		{
			if ( construction != null )
				RemoveBuilding( construction.boss );
		}
	}

	public class ItemPanel : Panel, Eye.IDirector
	{
		public Item item;
		public PathVisualization route;
		public Text stats;
		GameObject mapIcon;
		Building destination;
		Text destinationText;

		static public ItemPanel Create()
		{
			return new GameObject().AddComponent<ItemPanel>();
		}

		public void Open( Item item )
		{
			this.item = item;

			noResize = true;
			if ( base.Open( null, 0, 0, 250, 150 ) )
				return;

			name = "Item panel";

			Text( item.type.ToString() ).Pin( 15, -15, 100 );
			stats = Text().Pin( 15, -35, 250 );
			Text( "Origin:" ).Pin( 15, -55, 170 );
			if ( item.origin )
				BuildingIcon( item.origin ).Pin( 100, -55, 120 );
			Text( "Destination:" ).Pin( 15, -75, 170 );

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
			eye.GrabFocus( this );
		}

		override public void Update()
		{
			base.Update();
			if ( item == null )
			{
				Close();
				return;
			}

			if ( item.destination != destination )
			{
				if ( destinationText )
					Destroy( destinationText.gameObject );
				destinationText = BuildingIcon( item.destination )?.Pin( 100, -75, 120 );
				destination = item.destination;
			}
			
			if ( item.flag )
				stats.text = "Age: " + item.life.age / Constants.World.normalSpeedPerSecond + " secs, at flag for " + item.atFlag.age / Constants.World.normalSpeedPerSecond + " secs";
			else
				stats.text = "Age: " + item.life.age / Constants.World.normalSpeedPerSecond + " secs";

			if ( item.destination && route == null )
				route = PathVisualization.Create().Setup( item.path );
			if ( item.flag )
				mapIcon.transform.position = item.flag.node.position + Vector3.up * 4;
			else
			{
				item.assert.IsNotNull( item.hauler );
				mapIcon.transform.position = item.hauler.transform.position + Vector3.up * 4;
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
				eye.FocusOn( item.flag.node );
			else
				eye.FocusOn( item.hauler );
		}

		public new void OnDestroy()
		{
			base.OnDestroy();
			eye.ReleaseFocus( this );
		}
	}

	public class RouteList : Panel
	{
		public Stock stock;
		public Item.Type itemType;
		public bool outputs;
		public ScrollRect scroll;
		public List<Stock.Route> list;
		public static Material arrowMaterialYellow, arrowMaterialGreen, arrowMaterialRed;
		public Text[] last, rate, total, status, priority;
		public Image[] cart;
		public List<Stock> stockOptions = new List<Stock>();
		public bool forceRefill;
		public Text direction;
		public Stock tempPickedStock;
		public Stock.Route toHighlight;

		static public RouteList Create()
		{
			return new GameObject( "Route list" ).AddComponent<RouteList>();
		}

		public RouteList Open( Stock stock, Item.Type itemType, bool outputs )
		{
			base.Open( null, 660, 350 );
			this.stock = stock;
			this.itemType = itemType;
			this.outputs = outputs;

			{
				var d = Dropdown().Pin( borderWidth, -borderWidth, 150, iconSize );
				int currentValue = 0;
				List<string> options = new List<string>();
				stockOptions.Clear();
				foreach ( var s in root.mainTeam.stocks )
				{
					if ( s == stock )
						currentValue = options.Count;
					options.Add( s.nick );
					stockOptions.Add( s );
				}
				if ( stock == null )
					currentValue = options.Count;
				options.Add( "All" );
				stockOptions.Add( null );
				d.AddOptions( options );
				d.value = currentValue;
				d.onValueChanged.AddListener( OnStockChanged );
				d.SetTooltip( "Selected stock", null, "You can select a stock here, " +
					"in which case only the routes starting or ending there will be listed. By selecting \"All\" every route for the " +
					"selected item type will be listed. Remember, each building can be renamed." );
			}

			{
				var d = Dropdown().PinSideways( 0, -borderWidth, 150, iconSize );
				List<string> options = new List<string>();
				for ( int i = 0; i < (int)Item.Type.total; i++ )
					options.Add( ((Item.Type)i).ToString().GetPrettyName() );
				d.AddOptions( options );
				d.value = (int)itemType;
				d.onValueChanged.AddListener( OnItemTypeChanged );
				d.SetTooltip( "Selected item type", null, "Only routes with the selected item type will be listed." );
			}

			direction = Text( outputs ? "Output" : "Input" ).PinSideways( 5, -borderWidth, 100, iconSize ).AddClickHandler( OnChangeDirecton );
			direction.alignment = TextAnchor.MiddleLeft;
			direction.SetTooltip( "Direction of the listed routes", null, "When a stock is selected, this will control if the output or input routes of that stock will be listed" );

			Text( "?", 20 ).PinSideways( 20, -borderWidth, 40, 40 ).SetTooltip( "This is a list of transfer routes", null, 
				"Transfer routes are used to transfer a lot of items to bigger distances. A transfer route is defined by " +
				$"the starting stock, a destination stock, and an item type. Each stock has a cart, which can carry {Constants.Stock.cartCapacity} " +
				$"items at the same time, these carts are moving items along routes. A cart will start to carry items to the destination of the route, if all the following are met:\n" +
				$"- The start stock has at least {Constants.Stock.cartCapacity} items stored\n" +
				$"- Destination stock is willing to accept at least {Constants.Stock.cartCapacity} items to store\n" +
				"- Cart of the start stock is free\n" +
				"- Junction in front of the staring stock is free\n" +
				$"- Destination has at least {Constants.Stock.cartCapacity} free space\n" +
				"Once the cart started moving it uses the roads to get to the destination. If a route is used by a cart right now, a cart icon will appear in the row of the route, " +
				"clicking that icon will let you follow the cart." );

			Text( "#" ).Pin( 20, -borderWidth - iconSize, 20, iconSize ).SetTooltip( "Priority" );
			Text( "Start" ).PinSideways( 0, -borderWidth - iconSize, 120, iconSize );
			Text( "End" ).PinSideways( 0, -borderWidth - iconSize, 120, iconSize );
			Text( "Distance" ).PinSideways( 0, -borderWidth - iconSize, 30, iconSize );
			Text( "Last" ).PinSideways( 0, -borderWidth - iconSize, 50, iconSize );
			Text( "Rate" ).PinSideways( 0, -borderWidth - iconSize, 50, iconSize );
			Text( "Total" ).PinSideways( 0, -borderWidth - iconSize, 50, iconSize );
			Text( "Status" ).PinSideways( 0, -borderWidth - iconSize, 100, iconSize );
			scroll = ScrollRect().Stretch( borderWidth, borderWidth, -borderWidth, -borderWidth * 3 );
			return this;
		}

		void OnStockChanged( int newStock )
		{
			stock = stockOptions[newStock];
			forceRefill = true;
		}

		void OnItemTypeChanged( int newType )
		{
			itemType = (Item.Type)newType;
			forceRefill = true;
		}

		void OnChangeDirecton()
		{
			outputs = !outputs;
			direction.text = outputs ? "Output" : "Input";
			forceRefill = true;
		}

		bool UpdateList() 
		{
			List<Stock.Route> currentList;
			if ( stock == null )
			{
				currentList = new List<Stock.Route>();
				foreach ( var stock in root.mainTeam.stocks )
				{
					foreach ( var r in stock.itemData[(int)itemType].outputRoutes )
						currentList.Add( r );
				}
			}
			else
			{
				currentList = new List<Stock.Route>( stock.itemData[(int)itemType].outputRoutes );
				if ( !outputs )
					currentList = stock.GetInputRoutes( itemType );
			}
			bool needRefill = list == null || currentList.Count != list.Count;

			if ( !needRefill )
				for ( int i = 0; i < list.Count; i++ )
					needRefill |= list[i] != currentList[i];
			
			list = currentList;
			return needRefill;
		}

		public void Fill()
		{
			forceRefill = false;
			scroll.Clear();

			last = new Text[list.Count];
			rate = new Text[list.Count];
			total = new Text[list.Count];
			cart = new Image[list.Count];
			status = new Text[list.Count];
			priority = new Text[list.Count];
			int row = 0;
			for ( int i = 0; i < list.Count; i++ )
			{
				var route = list[i];
				priority[i] = Text( route.priority.ToString() ).Link( scroll.content ).Pin( 0, row, 20, iconSize ).SetTooltip( "Priority" );
				BuildingIcon( route.start ).Link( scroll.content ).PinSideways( 0, row, 120, iconSize );
				BuildingIcon( route.end ).Link( scroll.content ).PinSideways( 0, row, 120, iconSize );
				Text( route.start.node.DistanceFrom( route.end.node ).ToString() ).Link( scroll.content ).PinSideways( 0, row, 30, iconSize );
				last[i] = Text().Link( scroll.content ).PinSideways( 0, row, 50, iconSize );
				rate[i] = Text().Link( scroll.content ).PinSideways( 0, row, 50, iconSize );
				total[i] = Text().Link( scroll.content ).PinSideways( 0, row, 50, iconSize );
				status[i] = Text( "", 8 ).Link( scroll.content ).PinSideways( 0, row, 100, 2 * iconSize );
				Image( Icon.rightArrow ).Link( scroll.content ).PinSideways( 0, row ).Rotate( 90 ).AddClickHandler( () => oh.ScheduleChangePriority( route, 1 ) ).SetTooltip( "Increase the priority of the route" ).color = new Color( 1, 0.75f, 0.15f );
				Image( Icon.rightArrow ).Link( scroll.content ).PinSideways( 0, row ).Rotate( -90 ).AddClickHandler( () => oh.ScheduleChangePriority( route, -1 ) ).SetTooltip( "Decrease the priority of the route" ).color = new Color( 1, 0.75f, 0.15f );
				cart[i] = Image( Icon.cart ).Link( scroll.content ).PinSideways( 0, row ).AddClickHandler( () => ShowCart( route ) ).SetTooltip( "Follow the cart which is currently working on the route" );
				row -= iconSize + 5;
			}
			scroll.SetContentSize( 0, -row );
		}

		void ShowCart( Stock.Route route )
		{
			if ( route.start.cart.currentRoute == route )
				UnitPanel.Create().Open( route.start.cart, true );
		}

		new void Update()
		{
			base.Update();

			if ( UpdateList() || forceRefill )
				Fill();

			for ( int i = 0; i < list.Count; i++ )
			{
				if ( list[i].lastDelivery > 0 )
				{
					int ticks = time - list[i].lastDelivery;
					last[i].text = $"{(int)(ticks/60*Time.fixedDeltaTime)}:{((int)(ticks*Time.fixedDeltaTime)%60).ToString( "D2" )} ago";
				}
				else
					last[i].text = "-";
				rate[i].text = $"~{(list[i].averageTransferRate*Constants.World.normalSpeedPerSecond*60).ToString( "F2" )}/m";
				total[i].text = list[i].itemsDelivered.ToString();
				cart[i].gameObject.SetActive( list[i].start.cart?.currentRoute == list[i] );
				priority[i].text = list[i].priority.ToString();
				status[i].text = list[i].state switch
				{
					Stock.Route.State.noSourceItems => "Not enough items at source",
					Stock.Route.State.destinationNotAccepting => "Destination has enough",
					Stock.Route.State.flagJammed => "Junction is not free at start",
					Stock.Route.State.inProgress => "In progress",
					Stock.Route.State.noFreeCart => "Cart is not free",
					Stock.Route.State.noFreeSpaceAtDestination => "Destination is full",
					_ => "Unknown"
				};
			}

			if ( arrowMaterialYellow == null )
			{
				arrowMaterialYellow = new Material( Resources.Load<Shader>( "shaders/Route" ) );
				arrowMaterialYellow.mainTexture = iconTable.GetMediaData( Icon.rightArrow ).texture;
				World.SetRenderMode( arrowMaterialYellow, World.BlendMode.Cutout );
				arrowMaterialYellow.color = new Color( 1, 0.75f, 0.15f );

				arrowMaterialGreen = new Material( Resources.Load<Shader>( "shaders/Route" ) );
				arrowMaterialGreen.mainTexture = iconTable.GetMediaData( Icon.rightArrow ).texture;
				World.SetRenderMode( arrowMaterialGreen, World.BlendMode.Cutout );
				arrowMaterialGreen.color = new Color( 0, 0.75f, 0.15f );

				arrowMaterialRed = new Material( Resources.Load<Shader>( "shaders/Route" ) );
				arrowMaterialRed.mainTexture = iconTable.GetMediaData( Icon.rightArrow ).texture;
				World.SetRenderMode( arrowMaterialRed, World.BlendMode.Cutout );
				arrowMaterialRed.color = new Color( 1, 0, 0 );
			}

			foreach ( var route in list )
			{
				var startPosition = route.start.node.positionInViewport;
				var endPosition = route.end.node.positionInViewport;
				var dif = endPosition-startPosition;
				var fullDistance = dif.magnitude;
				var normalizedDif = dif / fullDistance;
				materialUIPath.color = Color.green;
				const float steps = Constants.Node.size * 2;
				var distance = (float)( Time.time - Math.Floor( Time.time ) ) * steps;
				while ( distance < fullDistance )
				{
					var material = arrowMaterialYellow;
					if ( route.endData.content < route.endData.cartInput )
						material = arrowMaterialRed;
					if ( route.startData.content >= route.startData.cartOutput || route.state == Stock.Route.State.inProgress )
						material = arrowMaterialGreen;
					Graphics.DrawMesh( Viewport.plane, Matrix4x4.TRS( startPosition + distance * normalizedDif + Vector3.up * Constants.Node.size, Quaternion.Euler( 0, (float)( 180 + 180 * Math.Atan2( dif.x, dif.z ) / Math.PI ), 0 ), Vector3.one ), material, 0 );
					distance += steps;
				}
				materialUIPath.color = Color.white;
			}
		}
    }

	public class HotkeyList : Panel
	{
		public ScrollRect scroll;

		static public HotkeyList Create()
		{
			return new GameObject( "Hotkey List ").AddComponent<HotkeyList>();
		}

		public void Open()
		{
			allowInSpectateMode = true;
			base.Open( null, 0, 0, 500, 350 );
			Fill();
		}

		public void Fill()
		{
			scroll = ScrollRect().Stretch( borderWidth, borderWidth, -borderWidth, -borderWidth );
			int row = 0;
			Hotkey.instances.Sort( (a, b) => a.action.CompareTo( b.action ) );
			foreach ( var hotkey in Hotkey.instances )
			{
				Text( hotkey.action ).Link( scroll.content ).Pin( 0, row, 320, iconSize ).alignment = TextAnchor.UpperRight;
				Text( hotkey.keyName ).Link( scroll.content ).PinSideways( 10, row, 110, iconSize ).AddClickHandler( () => Editor.Create().Open( hotkey, this ) );
				row -= iconSize;
			}
			scroll.SetContentSize( -1, -row );
		}

		public void UpdateList()
		{
			var scrollPos = scroll.verticalNormalizedPosition;
			Clear();
			Fill();
			scroll.verticalNormalizedPosition = scrollPos;
		}

		public class Editor : Panel
		{
			public Hotkey hotkey;
			public HotkeyList boss;

			public static Editor Create()
			{
				return new GameObject( "Press new hotkey panel" ).AddComponent<Editor>();
			}

			public void Open( Hotkey hotkey, HotkeyList boss )
			{
				this.hotkey = hotkey;
				this.boss = boss;
				base.Open( 300, 100 );
				Listen();
			}

			void Listen()
			{
				Text( $"Press new hotkey for\n{hotkey.action}" ).PinCenter( 0, -borderWidth-iconSize, 260, 2 * iconSize, 0.5f, 1 ).alignment = TextAnchor.MiddleCenter;
				Button( "Cancel" ).PinCenter( 0, -70, 100, iconSize, 0.25f ).AddClickHandler( () => { Close(); } );
				Button( "Reset" ).PinCenter( 0, -70, 100, iconSize, 0.75f ).AddClickHandler( () => { ChangeTo( hotkey.original, false ); } );
			}

			void OnGUI()
			{
				if ( Event.current.type != EventType.KeyDown )
					return;
				var key = Event.current.keyCode;
				if ( key == KeyCode.LeftAlt || key == KeyCode.RightAlt )
					return;
				if ( key == KeyCode.LeftShift || key == KeyCode.RightShift )
					return;
				if ( key == KeyCode.LeftControl || key == KeyCode.RightControl )
					return;

				Event.current.Use();	// does it do anything?
				Interface.ignoreKey = key;

				Hotkey newHotkey = new Hotkey { alt = Event.current.alt, ctrl = Event.current.control, shift = Event.current.shift, key = key };
				ChangeTo( newHotkey );
			}

			public void ChangeTo( Hotkey newHotkey, bool offerAnotherTake = true )
			{
				foreach ( var hotkey in Hotkey.instances )
				{
					if ( hotkey == this.hotkey )
						continue;;

					if ( hotkey.ctrl != newHotkey.ctrl )
						continue;
					if ( hotkey.alt != newHotkey.alt )
						continue;
					if ( hotkey.shift != newHotkey.shift )
						continue;
					if ( hotkey.key == newHotkey.key )
					{
						Clear();
						Text( $"{newHotkey.keyName} is already\ntaken for {hotkey.action}" ).PinCenter( 0, -borderWidth-iconSize, 260, 2 * iconSize, 0.5f ).alignment = TextAnchor.MiddleCenter;
						Button( "Use anyway" ).PinCenter( 0, -70, 100, iconSize, 0.25f ).AddClickHandler( () => UseInstead( newHotkey, hotkey ) );
						if ( offerAnotherTake )
							Button( "Take another" ).PinCenter( 0, -70, 100, iconSize, 0.75f ).AddClickHandler( () => { Clear(); Listen(); } );
						else
							Button( "Cancel" ).PinCenter( 0, -70, 100, iconSize, 0.75f ).AddClickHandler( Close );
						return;
					}
				}

				hotkey.key = newHotkey.key;
				hotkey.ctrl = newHotkey.ctrl;
				hotkey.alt = newHotkey.alt;
				hotkey.shift = newHotkey.shift;
				DoneEditing();
			}

			void DoneEditing()
			{
				root.SaveHotkeys();
				boss.UpdateList();
				Close();
			}

			void UseInstead( Hotkey newHotkey, Hotkey old )
			{
				old.key = KeyCode.None;
				old.alt = old.ctrl = old.shift = false;
				hotkey.key = newHotkey.key;
				hotkey.ctrl = newHotkey.ctrl;
				hotkey.alt = newHotkey.alt;
				hotkey.shift = newHotkey.shift;
				DoneEditing();
			}
		}
	}

	public class BuildingList : Panel
	{
		ScrollRect scroll;
		List<Building> buildings = new List<Building>();
		List<Text> productivities = new List<Text>();
		List<Text> outputs = new List<Text>();
		static Building.Type filter = Building.Type.unknown;
		List<List<Text>> inputs = new List<List<Text>>();
		static bool reversed;
		static Comparison<Building> comparison = CompareTypes;
		Text summary;

		static public BuildingList Create()
		{
			return new GameObject( "Building list" ).AddComponent<BuildingList>();
		}

		public void Open( Building.Type buildingType = Building.Type.unknown ) 
		{
			base.Open( null, 0, 0, 500, 420 );

			if ( buildingType != Building.Type.unknown )
				filter = buildingType;

			Text( "Filter:" ).Pin( 20, -20, 150 );
			var d = Dropdown().Pin( 80, -20, 150 );
			List<string> options = new List<string>();
			for ( int j = 0; j < (int)Building.Type.total; j++ )
			{
				string typeName = BuildingTypeToString( (Building.Type)j );
				if ( typeName != null )
					options.Add( typeName );
			}
			options.Sort();
			options.Add( "All" );
			d.AddOptions( options );
			d.value = options.IndexOf( BuildingTypeToString( filter ) );

			d.onValueChanged.AddListener( delegate { SetFilter( d ); } );

			var t = Text( "type", 10 ).Pin( 20, -40, 150 ).AddClickHandler( delegate { ChangeComparison( CompareTypes ); } );
			var p = Text( "productivity", 10 ).Pin( 170, -40, 150 ).AddClickHandler( delegate { ChangeComparison( CompareProductivities ); } );
			var o = Text( "output", 10 ).Pin( 380, -40, 150 ).AddClickHandler( delegate { ChangeComparison( CompareOutputs ); } );
			var i = Text( "input", 10 ).Pin( 235, -40, 150 ).AddClickHandler( delegate { ChangeComparison( CompareInputs ); } );
			scroll = ScrollRect().Stretch( 20, 40, -20, -60 );

			summary = Text().Pin( 20, 40, 200, iconSize, 0, 0 );

			SetFilter( d );
		}

		public string BuildingTypeToString( Building.Type type )
		{
			if ( type == Building.Type.unknown )
				return "All";
			if ( type == (Building.Type)Workshop.Type._geologistObsolete )
				return null;
			string name = "";
			if ( type < Building.Type.stock )
				name = ((Workshop.Type)type).ToString();
			else
				name = type.ToString();
			return Nice( name );
		}

		void SetFilter( Dropdown d )
		{
			for ( int i = 0; i < (int)Building.Type.total; i++ )
				if ( BuildingTypeToString( (Building.Type)i ) == d.options[d.value].text )
					filter = (Building.Type)i;
			if ( d.options[d.value].text == "All" )
				filter = Building.Type.unknown;
			if ( filter == Building.Type.unknown )
				eye.highlight.TurnOff();
			else
				eye.highlight.HighlightBuildingTypes( filter, owner:gameObject );
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
			int constructionCount = 0;
			buildings = new List<Building>();
			foreach ( var building in Resources.FindObjectsOfTypeAll<Building>() )
			{
				Assert.global.IsFalse( building.destroyed );	// Triggered
				Assert.global.IsNotNull( building );
				if ( building.team != root.mainTeam || building.blueprintOnly || ( building.type != filter && filter != Building.Type.unknown ) )
					continue;
				buildings.Add( building );
			}

			buildings.Sort( comparison );
			if ( reversed )
				buildings.Reverse();
			productivities.Clear();
			outputs.Clear();
			inputs.Clear();

			scroll.Clear();

			for ( int i = 0; i < buildings.Count; i++ )
			{
				if ( !buildings[i].construction.done )
					constructionCount++;
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
			scroll.SetContentSize( -1, iconSize * buildings.Count );
			summary.text = $"Total: {buildings.Count}, under construction: {constructionCount}";
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

	public class Viewport : HiveCommon, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IInputHandler, IPointerDownHandler, IPointerUpHandler
	{
		public bool mouseOver;
		public GameObject cursor;
		IInputHandler inputHandlerData;
		readonly GameObject[] cursorTypes = new GameObject[(int)CursorType.total];
		public static Material greenCheckOnGround;
		public static Material redCrossOnGround;
		public static Mesh plane;
		public Vector3 lastMouseOnGround;
		static int gridMaskXID;
		static int gridMaskZID;
		public bool showGridAtMouse;
		public OverlayInfoType nodeInfoToShow;
		public Building relaxCenter;
		static readonly List<BuildPossibility> buildCategories = new List<BuildPossibility>();
		public Node currentNode;  // Node currently under the cursor
		static GameObject marker;
		public bool markEyePosition;
		public bool rightButton;
		public bool rightDrag;
		public static bool showGround = true;
		public Vector3 rightOffset, downOffset;
		public Vector3 lastMouse;

		public static Material arrowMaterial;

		public enum OverlayInfoType
		{
			none,
			nodePossibleBuildings,
			nodeUndergroundResources,
			nodeRelaxSites,
			stockContent
		}

		public bool showCursor;

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
				if ( inputHandlerData != null && ( inputHandlerData as MonoBehaviour ) != null )
					inputHandlerData.OnLostInput();
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
			image.color = new Color( 0, 0, 0, 0 );

			inputHandler = this;
			marker.transform.SetParent( transform );

			plane = new Mesh();
			plane.vertices = new Vector3[4] { 
				new Vector3( -0.5f, 0, -0.5f ), 
				new Vector3( -0.5f, 0, 0.5f ), 
				new Vector3( 0.5f, 0, -0.5f ),
				new Vector3( 0.5f, 0, 0.5f ) };
			plane.uv = new Vector2[4] { 
				new Vector2( 1, 0 ), 
				new Vector2( 0, 0 ), 
				new Vector2( 1, 1 ),
				new Vector2( 0, 1 ) };
			plane.triangles = new int[6] { 0, 1, 2, 1, 3, 2 };

			greenCheckOnGround = new Material( Resources.Load<Shader>( "shaders/relaxMarker" ) );
			greenCheckOnGround.mainTexture = Resources.Load<Texture>( "icons/greenCheck" );

			redCrossOnGround = new Material( Resources.Load<Shader>( "shaders/relaxMarker" ) );
			redCrossOnGround.mainTexture = Resources.Load<Texture>( "icons/redCross" );

			var showGridButton = this.Image( Icon.grid ).AddToggleHandler( (state) => showGridAtMouse = state ).Pin( -280, -10, iconSize * 2, iconSize * 2, 1 ).AddHotkey( "Show grid", KeyCode.F1 );
			showGridButton.SetTooltip( () => $"Show grid (hotkey: {showGridButton.GetHotkey().keyName})" );
			var showNodeButton = this.Image( Icon.cursor ).AddToggleHandler( (state) => showCursor = state ).PinSideways( 0, -10, iconSize * 2, iconSize * 2, 1 ).AddHotkey( "Show node at cursor", KeyCode.F2 );
			showNodeButton.SetTooltip( () => $"Show node at cursor (hotkey: {showNodeButton.GetHotkey().keyName})" );
			var showPossibleBuildingsButton = this.Image( Icon.buildings ).AddToggleHandler( ShowPossibleBuildings ).PinSideways( 0, -10, iconSize * 2, iconSize * 2, 1 ).AddHotkey( "Show possible buildings", KeyCode.F3 );
			showPossibleBuildingsButton.SetTooltip( () => $"Show possible buildings (hotkey: {showPossibleBuildingsButton.GetHotkey().keyName})" );
			var showUndergroundResourcesButton = this.Image( Icon.crate ).AddToggleHandler( ShowUndergroundResources ).PinSideways( 0, -10, iconSize * 2, iconSize * 2, 1 ).AddHotkey( "Show underground resources", KeyCode.F4 );
			showUndergroundResourcesButton.SetTooltip( () => $"Show underground resources (hotkey: {showUndergroundResourcesButton.GetHotkey().keyName})" );
			var showStockContentButton = this.Image( Icon.itemPile ).AddToggleHandler( ShowStockContent ).PinSideways( 0, -10, iconSize * 2, iconSize * 2, 1 ).AddHotkey( "Show stock content", KeyCode.F5 );
			showStockContentButton.SetTooltip( () => $"Show stock contents (hotkey: {showStockContentButton.GetHotkey().keyName})" );
			var showStocksButton = this.Image( Icon.stock ).AddToggleHandler( HighlightStocks ).PinSideways( 0, -10, iconSize * 2, iconSize * 2, 1 ).AddHotkey( "Highlight stocks", KeyCode.F6 );
			showStocksButton.SetTooltip( () => $"Show stocks (hotkey: {showStocksButton.GetHotkey().keyName})" );
			var toggleGroundButton = this.Image( Icon.ground ).AddToggleHandler( ToggleGround, true ).PinSideways( 0, -10, iconSize * 2, iconSize * 2, 1 ).AddHotkey( "Toggle ground on map", KeyCode.G, true );
			toggleGroundButton.SetTooltip( () => $"Toggle between ground and political display on map (hotkey: {toggleGroundButton.GetHotkey().keyName})" );
			root.LoadHotkeys();

			arrowMaterial = new Material( Resources.Load<Shader>( "shaders/relaxMarker" ) );
			arrowMaterial.mainTexture = iconTable.GetMediaData( Icon.rightArrow ).texture;
			arrowMaterial.color = new Color( 1, 0.75f, 0.15f );
		}

		void ShowPossibleBuildings( bool state )
		{
			nodeInfoToShow = state ? OverlayInfoType.nodePossibleBuildings : OverlayInfoType.none;
		}

		void ShowUndergroundResources( bool state )
		{
			nodeInfoToShow = state ? OverlayInfoType.nodeUndergroundResources : OverlayInfoType.none;
		}

		void ShowStockContent( bool state )
		{
			nodeInfoToShow = state ? OverlayInfoType.stockContent : OverlayInfoType.none;
		}

		void ToggleGround( bool ground )
		{
			Eye.CameraGrid grid = null;
			if ( eye.mapMode )
				grid = eye.cameraGrid;
			if ( Map.MapImage.instance )
				grid = Map.MapImage.instance.camera;

			if ( grid )
			{
				if ( ground )
					grid.cullingMask = grid.cullingMask | (1 << World.layerIndexWater) | (1 << World.layerIndexGround);
				else
					grid.cullingMask = grid.cullingMask & (int.MaxValue - (1 << World.layerIndexWater) - (1 << World.layerIndexGround) );
			}
			showGround = ground;
		}

		void HighlightStocks( bool state )
		{
			if ( state )
				eye.highlight.HighlightBuildingTypes( Building.Type.headquarters, Building.Type.stock, gameObject );
			else
			{
				if ( eye.highlight.owner == gameObject )
					eye.highlight.TurnOff();
			}
		}

		public bool ResetInputHandler()
		{
			if ( inputHandler == this as IInputHandler )
				return false;
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
				configuration = new Workshop.Configuration { huge = true },
				material = greenMaterial,
				mesh = Resources.Load<Mesh>( "meshes/groundSigns/bigHouse" ),
				scale = 1.5f
			} );
			buildCategories.Add( new BuildPossibility
			{
				configuration = new Workshop.Configuration(),
				material = blueMaterial,
				mesh = Resources.Load<Mesh>( "meshes/groundSigns/mediumHouse" ),
				scale = 1.5f
			} );
			buildCategories.Add( new BuildPossibility
			{
				configuration = new Workshop.Configuration { flatteningNeeded = false },
				material = yellowMaterial,
				mesh = Resources.Load<Mesh>( "meshes/groundSigns/smallHouse" ),
				scale = 1.5f
			} );
			buildCategories.Add( new BuildPossibility
			{
				configuration = new Workshop.Configuration { groundTypeNeeded = Node.Type.hill },
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

		public HiveObject FindObjectAt( Vector3 screenPosition )
		{
			var layers = eye.cameraGrid.cullingMask;
			layers &= int.MaxValue - (1 << World.layerIndexHighlightVolume);
			layers |= 1 << World.layerIndexGround;
			if ( inputHandler.pickGroundOnly )
				layers &= int.MaxValue - (1 << World.layerIndexBuildings) - (1 << World.layerIndexUnits) - (1 << World.layerIndexResources);
			RaycastHit hit = new RaycastHit();
			foreach ( var camera in eye.cameraGrid.cameras )
			{
				Ray ray = camera.ScreenPointToRay( screenPosition );
				if ( Physics.Raycast( ray, out hit, 1000, layers ) )
					break;
			}

			if ( hit.collider == null )
				return null;

			var hiveObject = hit.collider.GetComponent<HiveObject>();
			if ( hiveObject == null && hit.collider.transform.parent )
				hiveObject = hit.collider.transform.parent.GetComponent<HiveObject>();
			if ( hiveObject == null && hit.collider.transform.parent?.parent )
				hiveObject = hit.collider.transform.parent.parent.GetComponent<HiveObject>();
			if ( hiveObject == null )
				return root;

			var b = hiveObject as Building;
			if ( b && b.blueprintOnly )
				hiveObject = ground;

			if ( hiveObject is Ground.Block || hiveObject == ground )
			{
				Vector3 localPosition = ground.transform.InverseTransformPoint( hit.point );
				return Node.FromPosition( localPosition, ground );
			}
			
			return hiveObject;
		}

		public Node FindNodeAt( Vector3 screenPosition )
		{ 
			RaycastHit hit = new RaycastHit();

			foreach ( var camera in eye.cameraGrid.cameras )
			{
				Ray ray = camera.ScreenPointToRay( screenPosition );

				foreach ( var block in ground.blocks )
				{
					var c = block.collider;
					if ( c == null )
						continue;

					if ( c.Raycast( ray, out hit, 1000 ) ) // TODO How long the ray should really be?
						break;
				}

				if ( hit.collider != null )
					break;
			}

			if ( hit.collider == null )
				return null;

			lastMouseOnGround = ground.transform.InverseTransformPoint( hit.point );

			if ( showGridAtMouse )
			{
				ground.material.SetFloat( gridMaskXID, hit.point.x );
				ground.material.SetFloat( gridMaskZID, hit.point.z );
			}
			else
			{
				ground.material.SetFloat( gridMaskXID, 10000 );
				ground.material.SetFloat( gridMaskZID, 10000 );
			}
			return Node.FromPosition( lastMouseOnGround, ground );
		}

		public void OnPointerClick( PointerEventData eventData )
		{
			rightButton = eventData.button == PointerEventData.InputButton.Right;
			if ( inputHandler == null )
				inputHandler = this;
			var hiveObject = FindObjectAt( Input.mousePosition );
			if ( hiveObject == null )
			{
				UnityEngine.Debug.Log( "Clicked on nothing?" );
				return;
			}

			if ( hiveObject.team != root.mainTeam && !hiveObject.wantFoeClicks )
				return;
				
			var node = hiveObject as Node;
			if ( node )
			{
				if ( !inputHandler.OnNodeClicked( node ) )
					inputHandler = this;
			}
			else
				if ( !inputHandler.OnObjectClicked( hiveObject ) )
					inputHandler = this;
		}

		public void OnPointerDown( PointerEventData eventData )
		{
			if ( eventData.button != PointerEventData.InputButton.Right )
				return;

			rightDrag = true;
			var camera = eye.cameraGrid.center;	// TODO Only works with the center camera?
			Ray ray = camera.ScreenPointToRay( eventData.position );
			Physics.Raycast( ray, out RaycastHit hit, 1000, 1 << World.layerIndexGround );

			Vector3 center = camera.WorldToScreenPoint( hit.point );
			Vector3 centerWorld = camera.ScreenToWorldPoint( center );
			Vector3 rightWorld = camera.ScreenToWorldPoint( center - Vector3.right );
			Vector3 downWorld = camera.ScreenToWorldPoint( center - Vector3.up );
			rightOffset = rightWorld - centerWorld;
			downOffset = downWorld - centerWorld;
		}

		public void OnPointerUp( PointerEventData eventData )
		{
			if ( eventData.button == PointerEventData.InputButton.Right )
				rightDrag = false;
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
			if ( ground == null )
				return;

			if ( rightDrag )
			{
				var delta = Input.mousePosition - lastMouse;
				Vector3 offset = delta.x * rightOffset + delta.y * downOffset;
				eye.Move( 0, 0 );	// TODO Feels like a brutal hack
				eye.x += offset.x;
				eye.y += offset.z;
			}
			lastMouse = Input.mousePosition;

			if ( inputHandler == null || inputHandler.Equals( null ) )
				inputHandler = this;

			if ( markEyePosition && mouseOver )
			{
				marker.SetActive( true );
				marker.transform.position = eye.position + Vector3.up * ( ( float )( ground.GetHeightAt( eye.x, eye.y ) + 1.5f * Math.Sin( 2 * Time.time ) ) );
				marker.transform.rotation = Quaternion.Euler( 0, Time.time * 200, 0 );
			}
			else
				marker.SetActive( false );

			RenderOverlayInfo();

			if ( !mouseOver || world == null || eye == null )
				return;
			currentNode = FindNodeAt( Input.mousePosition );
			if ( cursor && currentNode )
			{
				cursor.transform.localPosition = currentNode.position;
				cursor.transform.SetParent( ground.transform, false );
			}
			if ( currentNode && !inputHandler.OnMovingOverNode( currentNode ) )
				inputHandler = this;
#if DEBUG
			if ( GetKeyDown( KeyCode.PageUp ) && currentNode )
				currentNode?.SetHeight( currentNode.height + 0.05f );
			if ( GetKeyDown( KeyCode.PageDown ) && currentNode )
				currentNode?.SetHeight( currentNode.height - 0.05f );
#endif
		}

		void RenderOverlayInfo()
		{
			if ( nodeInfoToShow != OverlayInfoType.none && currentNode )
			{
				foreach ( var o in Ground.areas[6] )
				{
					var n = currentNode + o;
					if ( nodeInfoToShow == OverlayInfoType.nodePossibleBuildings )
					{
						foreach ( var p in buildCategories )
						{
							if ( p.configuration != null )
							{
								if ( !Building.IsNodeSuitable( n, root.mainTeam, p.configuration, NewBuildingPanel.currentFlagDirection ) )
									continue;
							}
							else
							{
								if ( !Flag.IsNodeSuitable( n, root.mainTeam ) )
									continue;
							}

							Graphics.DrawMesh( p.mesh, Matrix4x4.TRS( n.positionInViewport , Quaternion.identity, new Vector3( p.scale, p.scale, p.scale ) ), p.material, 0 );
							break;
						}
					}
					if ( nodeInfoToShow == OverlayInfoType.nodeUndergroundResources )
					{
						foreach ( var resource in n.resources )
						{
						#if DEBUG
						#else
							if ( n.team != root.mainTeam )
								continue;
						#endif
							if ( !resource.underGround )
								continue;
							var itemType = Resource.ItemType( resource.type );
							var body = Item.looks.GetMediaData( itemType );
							var renderer = body.GetComponent<MeshRenderer>();
							var meshFilter = body.GetComponent<MeshFilter>();
							World.DrawObject( body, Matrix4x4.TRS( n.positionInViewport + Vector3.up * 0.2f, Quaternion.identity, Vector3.one * 0.3f ) );
							break;
						}
					}
				}
			}
			if ( nodeInfoToShow == OverlayInfoType.nodeRelaxSites )
			{
				foreach ( var o in Ground.areas[Constants.Workshop.relaxAreaSize] )
				{
					var n = relaxCenter.node + o;
					var material = Workshop.IsNodeGoodForRelax( n ) ? greenCheckOnGround : redCrossOnGround;
					Graphics.DrawMesh( plane, Matrix4x4.TRS( n.positionInViewport + Vector3.up * 0.2f, Quaternion.identity, Vector3.one * 0.8f ), material, 0 );
				}
			}
			if ( nodeInfoToShow == OverlayInfoType.stockContent && root.mainTeam )
			{
				foreach ( var stock in root.mainTeam.stocks )
				{
					float angle = Time.fixedTime;

					for ( int itemType = 0; itemType != (int)Item.Type.total; itemType++ )
					{
						if ( stock.itemData[itemType].content < 1 )
							continue;
						float height = stock.main ? 3f : 1.5f;
						World.DrawObject( Item.looks.GetMediaData( (Item.Type)itemType ), Matrix4x4.TRS( stock.node.positionInViewport + new Vector3( 0.5f * (float)Math.Sin( angle ), height, 0.5f * (float)Math.Cos( angle ) ), Quaternion.identity, Vector3.one * stock.itemData[itemType].content * 0.02f ) );
						angle += (float)( Math.PI * 2 / 7 );
					}
				}
			}
		}

		public bool OnMovingOverNode( Node node )
		{
			if ( node != null )
			{
				CursorType t = CursorType.nothing;
				Node flagNode = node.Neighbour( NewBuildingPanel.currentFlagDirection );
				bool hasFlagAround = false, hasFlagAroundFlag = false;
				foreach ( var o in Ground.areas[1] )
					if ( o && node.Add( o ).flag != null )
						hasFlagAround = true;
				foreach ( var o in Ground.areas[1] )
					if ( o && flagNode.Add( o ).flag != null )
						hasFlagAroundFlag = true;
				if ( !node.block && !hasFlagAround )
					t = CursorType.flag;
				if ( !node.block && !flagNode.block && !hasFlagAroundFlag )
					t = CursorType.building;
				SetCursorType( t );
				return true;
			}
			return true;
		}

		public void SetCursorType( CursorType cursorType, int roadDirection = -1 )
		{
			if ( !showCursor )
				cursorType = CursorType.invisible;
			if ( cursor == null )
			{
				cursor = Instantiate( Resources.Load<GameObject>( "prefabs/others/cursor" ) );
				cursor.transform.SetParent( ground.transform );
				for ( int i = 0; i < cursorTypes.Length; i++ )
				{
					cursorTypes[i] = World.FindChildRecursive( cursor.transform, ( (CursorType)i ).ToString() )?.gameObject;
					if ( i != (int)CursorType.invisible )
						Assert.global.IsNotNull( cursorTypes[i] );
				}
			}
			
			for ( int i = 0; i < cursorTypes.Length; i++ )
				cursorTypes[i]?.SetActive( i == (int)cursorType );
			if ( roadDirection >= 0 )
				cursorTypes[(int)CursorType.direction0 + roadDirection].SetActive( true );
		}

		public bool OnNodeClicked( Node node )
		{
			if ( rightButton )
				return true;

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
			if ( !rightButton )
				target.OnClicked();
			return true;
		}
	}

	public class RoadList : Panel
	{
		ScrollRect scroll;
		Team team;
		static Comparison<Road> comparison = CompareByLength;
		static bool reversed;

		public static RoadList Create( Team team )
		{
			var list = new GameObject( "Road list panel" ).AddComponent<RoadList>();
			list.Open( team );
			return list;
		}

		void Open( Team team )
		{
			this.team = team;
			if ( base.Open( 500, 500 ) )
				return;

			Text( "Length" ).Pin( borderWidth, -borderWidth, 100 ).AddClickHandler( () => ChangeComparison( CompareByLength ) );
			Text( "Worker count" ).PinSideways( 0, -borderWidth, 100 ).AddClickHandler( () => ChangeComparison( CompareByWorkers ) );
			Text( "Last used" ).PinSideways( 0, -borderWidth, 100 ).AddClickHandler( () => ChangeComparison( CompareByLastUsed ) );
			Text( "Jam" ).PinSideways( 0, -borderWidth, 100 ).AddClickHandler( () => ChangeComparison( CompareByJam ) );
			Image( Icon.reset ).Pin( -borderWidth-20, -borderWidth, iconSize, iconSize, 1 ).AddClickHandler( Fill );

			scroll = ScrollRect().Stretch( borderWidth, borderWidth, -borderWidth, -borderWidth-20 );
			Fill();
		}

		void ChangeComparison( Comparison<Road> newComparison )
		{
			if ( comparison == newComparison )
				reversed = !reversed;
			else
				reversed = true;
			comparison = newComparison;
			Fill();
		}

		void Fill()
		{
			scroll.Clear();

			List<Road> sortedRoads = new List<Road>();
			foreach ( var road in team.roads )
			{
				if ( road )
					sortedRoads.Add( road );
			}
			sortedRoads.Sort( comparison );
			if ( reversed )
				sortedRoads.Reverse();

			int row = 0;
			foreach ( var road in sortedRoads )	
			{
				Text( (road.nodes.Count - 1).ToString() ).Link( scroll.content ).Pin( 0, row, 100 ).AddHiveObjectHandler( road );
				Text( (road.haulers.Count).ToString() ).Link( scroll.content ).PinSideways( 0, row, 100 ).AddHiveObjectHandler( road );
				Text( road.lastUsed.age > 0 ? UIHelpers.TimeToString( road.lastUsed.age ) : "Never" ).Link( scroll.content ).PinSideways( 0, row, 100 ).AddHiveObjectHandler( road );
				Text( (road.jam).ToString() ).Link( scroll.content ).PinSideways( 0, row, 100 ).AddHiveObjectHandler( road );
				row -= iconSize;
			}
			scroll.SetContentSize( -1, sortedRoads.Count * iconSize );
		}

		static int CompareByLength( Road a, Road b )
		{
			return a.nodes.Count.CompareTo( b.nodes.Count );
		}

		static int CompareByWorkers( Road a, Road b )
		{
			return a.haulers.Count.CompareTo( b.haulers.Count );
		}

		static int CompareByLastUsed( Road a, Road b )
		{
			return a.lastUsed.ageinf.CompareTo( b.lastUsed.ageinf );
		}

		static int CompareByJam( Road a, Road b )
		{
			return a.jam.CompareTo( b.jam );
		}

	}

	public class ItemList : Panel
	{
		ScrollRect scroll;
		Team team;
		World.Speed speedToRestore;
		static Comparison<Item> comparison = CompareByAge;
		static bool reversed;

		public static ItemList Create()
		{
			return new GameObject().AddComponent<ItemList>();
		}

		public void Open( Team team )
		{
			if ( base.Open( null, 0, 0, 420, 320 ) )
				return;
			name = "Item list panel";
			this.team = team;
			speedToRestore = world.speed;
			world.SetSpeed( World.Speed.pause );

			Text( "Origin" ).Pin( 50, -20, 100 ).AddClickHandler( delegate { ChangeComparison( CompareByOrigin ); } );
			Text( "Destination" ).Pin( 150, -20, 100 ).AddClickHandler( delegate { ChangeComparison( CompareByDestination ); } );
			Text( "Age (sec)" ).Pin( 250, -20, 120 ).AddClickHandler( delegate { ChangeComparison( CompareByAge ); } );
			Text( "Distance" ).Pin( 320, -20, 100 ).AddClickHandler( delegate { ChangeComparison( CompareByPathLength ); } ).SetTooltip( "Number of roads for the whole travel from the original building to the current destination" );

			scroll = ScrollRect().Stretch( 20, 20, -20, -40 );
			Fill();
		}

		public override void Close()
		{
			base.Close();
			world.SetSpeed( speedToRestore );
		}

		void ChangeComparison( Comparison<Item> newComparison )
		{
			if ( comparison == newComparison )
				reversed = !reversed;
			else
				reversed = true;
			comparison = newComparison;
			Fill();
		}

		void Fill()
		{
			int row = 0;
			scroll.Clear();

			List<Item> sortedItems = new List<Item>();
			foreach ( var item in team.items )
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

				if ( item.origin )
					BuildingIcon( item.origin ).Link( scroll.content ).Pin( 30, row, 80 );
				if ( item.destination )
					BuildingIcon( item.destination ).Link( scroll.content ).Pin( 130, row, 80 );
				Text( ( item.life.age / 50 ).ToString() ).Link( scroll.content ).Pin( 230, row, 50 );
				if ( item.path != null )
					Text( item.path.roadPath.Count.ToString() ).Link( scroll.content ).Pin( 300, row, 30 );				
				row -= iconSize + 5;
			}
			scroll.SetContentSize( -1, sortedItems.Count * ( iconSize + 5 ) );
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
			if ( itemA.path != null )
				lA = itemA.path.roadPath.Count;
			if ( itemB.path != null )
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
			if ( itemA.destination == null )
				return 1;
			if ( itemB.destination == null )
				return -1;
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
			allowInSpectateMode = true;
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
				Text( resource.type.ToString().GetPrettyName() ).Link( scroll.content ).Pin( 30, row, 100 ).AddClickHandler( delegate { Node node = resource.node; NodePanel.Create().Open( node, true ); } );
				Text( ( resource.gathered.age / 50 ).ToString() ).Link( scroll.content ).Pin( 130, row, 50 );
				Text( resource.keepAway.inProgress ? "no" : "yes" ).Link( scroll.content ).Pin( 230, row, 30 );
				row -= iconSize + 5;
			}
			scroll.SetContentSize( -1, sortedResources.Count * ( iconSize + 5 ) );
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
		World.Speed speedToRestore;
		bool filled;

		public static LogisticList Create()
		{
			return new GameObject().AddComponent<LogisticList>();
		}

		public void Open( Building building, Item.Type itemType, ItemDispatcher.Potential.Type direction )
		{
			Assert.global.AreEqual( building.team, root.mainTeam );
			root.mainTeam.itemDispatcher.queryBuilding = this.building = building;
			root.mainTeam.itemDispatcher.queryItemType = this.itemType = itemType;
			root.mainTeam.itemDispatcher.queryType = this.direction = direction;
			root.mainTeam.itemDispatcher.fullTracking = true;
			speedToRestore = world.speed;
			if ( world.speed == World.Speed.pause )
				world.SetSpeed( World.Speed.normal );

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
			root.mainTeam.itemDispatcher.queryItemType = Item.Type.unknown;
			root.mainTeam.itemDispatcher.queryBuilding = null;
			root.mainTeam.itemDispatcher.fullTracking = false;
			world.SetSpeed( speedToRestore );
		}

		new public void Update()
		{
			base.Update();
			if ( root.mainTeam.itemDispatcher.results != null && !filled )
			{
				filled = true;
				Fill();
				root.mainTeam.itemDispatcher.fullTracking = false;
				root.mainTeam.itemDispatcher.queryBuilding = this.building = null;
				root.mainTeam.itemDispatcher.queryItemType = this.itemType = Item.Type.unknown;
				world.SetSpeed( World.Speed.pause );
			}
		}

		void Fill()
		{
			int row = 0;
			foreach ( Transform child in scroll.content )
				Destroy( child.gameObject );

			foreach ( var result in root.mainTeam.itemDispatcher.results )
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
					ItemDispatcher.Result.flagJam => "Jam at output junction",
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
						ItemDispatcher.Result.flagJam => "Jam at their output junction",
						ItemDispatcher.Result.noDispatcher => "Their dispatcher is not free",
						ItemDispatcher.Result.notInArea => "Their area excludes this",
						ItemDispatcher.Result.outOfItems => "They are out of items",
						_ => message
					};
				}
				if ( result.remote && !result.incoming && result.result == ItemDispatcher.Result.outOfItems )
					message = "Not needed";
				if ( !result.remote && result.result == ItemDispatcher.Result.outOfItems )
				{
					if ( direction == ItemDispatcher.Potential.Type.offer )
						message = "Out of items";
					else
						message = "Not needed";
				}
				Text( message ).Link( scroll.content ).Pin( 250, row, 200, 40 );
				row -= iconSize + 5;
			}
			scroll.SetContentSize( -1, root.mainTeam.itemDispatcher.results.Count * ( iconSize + 5 ) );
		}
	}

	public class ItemStats : Panel
	{
		ScrollRect scroll;
		Team team;
		static Comparison<int> currentComparison;
		static bool reverse = false;
		readonly Text[] inStock = new Text[(int)Item.Type.total];
		readonly Text[] onWay = new Text[(int)Item.Type.total];
		readonly Text[] surplus = new Text[(int)Item.Type.total];
		readonly Text[] production = new Text[(int)Item.Type.total];
		readonly Button[] stockButtons = new Button[(int)Item.Type.total];
		readonly ItemImage[] itemIcon = new ItemImage[(int)Item.Type.total];

		int[] inStockCount = new int[(int)Item.Type.total];
		int[] onWayCount = new int[(int)Item.Type.total];

		public static ItemStats Create()
		{
			return new GameObject().AddComponent<ItemStats>();
		}

		public void Open( Team team )
		{
			if ( base.Open( null, 0, 0, 320, 300 ) )
				return;

			name = "Item stats panel";
			this.team = team;
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

			scroll = ScrollRect().Stretch( 20, 40, -20, -40 );

			for ( int i = 0; i < inStock.Length; i++ )
			{
				int row = i * - ( iconSize + 5 );
				itemIcon[i] = ItemIcon( (Item.Type)i ).Link( scroll.content ).Pin( 0, row );
				inStock[i] = Text( "0" ).Link( scroll.content ).Pin( 30, row, 40, iconSize );
				stockButtons[i] = inStock[i].gameObject.AddComponent<Button>();
				onWay[i] = Text( "0" ).Link( scroll.content ).Pin( 80, row, 40 );
				surplus[i] = Text( "0" ).Link( scroll.content ).Pin( 130, row, 40 );
				production[i] = Text( "0" ).Link( scroll.content ).Pin( 180, row, 40 );
			}

			scroll.SetContentSize( -1, (int)Item.Type.total * ( iconSize + 5 ) );
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
			return team.surplus[a].CompareTo( team.surplus[b] );
		}

		int ComparePerMinute( int a, int b )
		{
			return team.itemProductivityHistory[a].production.CompareTo( team.itemProductivityHistory[b].production );
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
			foreach ( var stock in team.stocks )
			{
				for ( int i = 0; i < inStock.Length; i++ )
				{
					if ( stock.itemData[i].content > maxStockCount[i] )
					{
						maxStockCount[i] = stock.itemData[i].content;
						richestStock[i] = stock;
					}
					inStockCount[i] += stock.itemData[i].content;
				}
			}

			foreach ( var item in team.items )
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
				itemIcon[i].SetType( (Item.Type)order[i] );
				inStock[i].text = inStockCount[order[i]].ToString();
				stockButtons[i].onClick.RemoveAllListeners();
				Stock stock = richestStock[order[i]];
				stockButtons[i].onClick.AddListener( delegate { SelectBuilding( stock ); } );
				onWay[i].text = onWayCount[order[i]].ToString();
				surplus[i].text = team.surplus[order[i]].ToString();

				var itemData = team.itemProductivityHistory[order[i]];
				production[i].text = itemData.current.ToString( "n2" );
			};
		}
	}

	public class History : Panel
	{
		Item.Type selected;
		Team team;
		float lastProductivity;
		Image chart, itemFrame;
		Text record;
		public float scale = 1;

		public static History Create()
		{
			return new GameObject().AddComponent<History>();
		}

		public void Open( Team team )
		{
			this.team = team;

			if ( base.Open( null, 0, 0, 450, 300 ) )
				return;

			name = "History panel";
			for ( int i = 0; i < (int)Item.Type.total; i++ )
			{
				var t = (Item.Type)i;
				ItemIcon( (Item.Type)i ).Pin( 20 + i * iconSize, -20 ).AddClickHandler( delegate { selected = t; } );
			}
			itemFrame = Image( Icon.tinyFrame ).Pin( 17, -17, 26, 26 );
			chart = Image().Pin( 20, -40, 410, 240 );
			record = Text().Pin( 25, -45, 150 );
			record.color = Color.yellow;
			selected = Item.Type.soldier;
			lastProductivity = -1;
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
			var a = team.itemProductivityHistory[(int)selected];

			if ( chart.Contains( Input.mousePosition ) )
			{
				Vector3[] corners = new Vector3[4];
				chart.rectTransform.GetWorldCorners( corners );
				var cursorInsideChart = Input.mousePosition - corners[0];
				int ticks = Constants.Player.productivityAdvanceTime * (int)( corners[2].x - Input.mousePosition.x );
				var hours = ticks / 60 / 60 / Constants.World.normalSpeedPerSecond;
				string time = $"{(ticks/60/Constants.World.normalSpeedPerSecond)%60} minutes ago";
				if ( hours > 1 )
					time = $"{hours} hours and " + time;
				else if ( hours == 1 )
					time = "1 hour and " + time;
				chart.SetTooltip( $"{time}\n{PixelToPerMinute( (int)cursorInsideChart.y )} per minute" );
			}

			if ( lastProductivity == a.current )
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
			foreach ( var c in world.workshopConfigurations )
			{
				if ( c.outputType == selected && c.productionTime != 0 )
				{
					tickPerBuilding = c.productionTime / c.outputStackSize;
					foreach ( var w in Resources.FindObjectsOfTypeAll<Workshop>() ) 
					if ( w.team == root.mainTeam && w.type == c.type )
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
			int xh = t.width - ( time % World.hourTickCount ) / Constants.Player.productivityAdvanceTime;
			while ( xh >= 0 )
			{
				VerticalLine( xh, Color.grey );
				xh -= World.hourTickCount / Constants.Player.productivityAdvanceTime;
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
			lastProductivity = a.current;
		}
	}

	public class ChallengeList : Panel
	{
		public InputField manualSeed;

		public static ChallengeList Create( bool defeat = false )
		{
			var p = new GameObject( "Challenge list" ).AddComponent<ChallengeList>();
			p.Open( defeat );
			return p;
		}

		void Open( bool defeat )
		{
			allowInSpectateMode = true;
			base.Open( 500, 300 + (defeat ? (int)( iconSize * 1.5 ) : 0) );
			int titleRow = -borderWidth;
			if ( defeat )
			{
				Text( "You are defeated!", 18 ).PinCenter( 250, -30, 200 ).color = Color.red;
				titleRow -= (int)( iconSize * 1.5 );
			}
			var scroll = ScrollRect().Stretch( borderWidth, borderWidth + iconSize * 3, -borderWidth, -borderWidth + titleRow );
			Text( "Manual seed:" ).Pin( -330, borderWidth + iconSize, 150, iconSize, 1, 0 );
			manualSeed = InputField( new System.Random().Next().ToString() ).PinSideways( 0, borderWidth + iconSize, 150, iconSize, 1, 0 );
			Text( "Challenge name" ).Pin( borderWidth, titleRow, 140, iconSize );
			Text( "Time limit" ).PinSideways( 0, titleRow, 70, iconSize );
			Text( "World size" ).PinSideways( 0, titleRow, 70, iconSize );
			Text( "Best solution" ).PinSideways( 50, titleRow, 100, iconSize );
			Text( "The game can be continued no matter if the current challenge is lost or won, so you can play with any of these as a free game" ).PinCenter( 0, 3 * iconSize, 400, 2 * iconSize, 0.5f, 0 ).alignment = TextAnchor.MiddleCenter;
			var view = scroll.content;

			int row = 0;
			foreach ( var challenge in root.challenges )
			{
				Text( challenge.title ).Pin( 0, row, 140, iconSize ).Link( view ).SetTooltip( challenge.description );
				Text( challenge.timeLimit > 0 ? UIHelpers.TimeToString( challenge.timeLimit ) : "none" ).Link( view ).PinSideways( 0, row, 70, iconSize );
				Text( challenge.worldSize switch { 24 => "small", 32 => "medium", 48 => "big", _ => "unknown" } ).Link( view ).PinSideways( 0, row, 70, iconSize );
				Button( "Begin" ).Link( view ).PinSideways( 0, row, 40, iconSize ).AddClickHandler( () => StartChallenge( challenge ) );
				Text( challenge.bestSolutionLevel.ToString() ).Link( view ).PinSideways( 10, row, 60, iconSize );
				if ( challenge.bestSolutionLevel != World.Goal.none )
					Button( "Replay" ).Link( view ).PinSideways( 0, row, 40, iconSize ).AddClickHandler( () => root.LoadReplay( challenge.bestSolutionReplayFileName ) );
				row -= iconSize;
			}
			scroll.SetContentSize( 0, -row );
		}

		void StartChallenge( World.Challenge challenge )
		{
			if ( !challenge.fixedSeed )
				challenge.seed = int.Parse( manualSeed.text );
			root.NewGame( challenge );
			Close();
		}
	}

	public class ReplayPanel : Panel
	{
		public static ReplayPanel Create()
		{
			var p = new GameObject( "Replay Panel" ).AddComponent<ReplayPanel>();
			p.Open();
			return p;
		}

		void Open()
		{
			base.Open( 300, 100 );
			CheckBox( "Show next action" ).AddToggleHandler( value => root.showReplayAction = value, root.showReplayAction ).Pin( borderWidth, -borderWidth, 150, iconSize );
			Button( "Cancel" ).AddClickHandler( () => oh.CancelReplay() ).PinDownwards( borderWidth, -5, 80, iconSize );
		}

		new void Update()
		{
			 if ( root.playerInCharge )
			 	Close();
			base.Update();
		}
	}

	public class ChallengePanel : Panel
	{
		Text worldTime, maintain, timeLeft, conditions, currentChallenge;

		ProgressBar progress;
		World.Speed originalSpeed = (World.Speed)(-1);
		bool worldStopped;

		public static ChallengePanel Create()
		{
			return new GameObject( "Challenge Progress Panel" ).AddComponent<ChallengePanel>();
		}

		public void Open( World.Goal reached = World.Goal.none )
		{
			var challenge = world.challenge;
			worldStopped = reached != World.Goal.none;
			noResize = true;
			noPin = true;
			if ( reached != World.Goal.none )
				reopen = true;
			if ( base.Open( 400, 220 ) )
				return;
			this.Pin( -200, -110, 200, 110, 0.5f, 0.5f );
			UIHelpers.currentRow = -30;
			if ( worldStopped )
			{
				Assert.global.IsNotNull( root.mainTeam );
				var t = Text();
				t.PinDownwards( -100, 0, 200, 30, 0.5f ).AddOutline();
				t.alignment = TextAnchor.MiddleCenter;
				if ( reached == World.Goal.gold )
				{
					t.color = Color.yellow;
					t.text = "VICTORY!";
				}
				else if (reached == World.Goal.silver )
				{
					t.color = Color.grey;
					t.text = "Silver level reached";
				}
				else
				{
					Assert.global.AreEqual( reached, World.Goal.bronze );
					t.color = Color.yellow.Dark();
					t.text = "Bronze level reached";
				}
				originalSpeed = root.world.speed;
				eye.FocusOn( root.mainTeam.mainBuilding.flag.node, true );
				root.world.SetSpeed( World.Speed.pause );
			}
			worldTime = Text().PinDownwards( -200, 0, 400, 30, 0.5f );
			worldTime.alignment = TextAnchor.MiddleCenter;
			
			currentChallenge = Text().PinDownwards( borderWidth, 0, 400, iconSize );
			Text( challenge.description, 10 ).PinDownwards( borderWidth, 0, 300, 2 * iconSize );
			conditions = Text( "", 10 ).PinDownwards( borderWidth, 0, 300, 3 * iconSize );
			if ( world.challenge.maintain > 0 && world.challenge.reachedLevel < World.Goal.gold )
			{
				maintain = Text().PinDownwards( -200, 0, 400, iconSize, 0.5f );
				maintain.alignment = TextAnchor.MiddleCenter;
			}
			if ( world.challenge.timeLimit > 0 && world.challenge.reachedLevel < World.Goal.gold )
			{
				timeLeft = Text().PinDownwards( -200, 0, 400, iconSize, 0.5f );
				timeLeft.alignment = TextAnchor.MiddleCenter;
			}
			progress = Progress().PinDownwards( -60, 0, 120, iconSize, 0.5f );
			var row = UIHelpers.currentRow - iconSize / 2 - 10;
			Button( "Restart" ).PinCenter( 0, row, 100, 25, 0.25f ).AddClickHandler( () => Restart( false ) );
			Button( "Restart with different seed" ).PinCenter( 0, row, 150, 25, 0.75f ).AddClickHandler( () => Restart( true ) );
			
			this.SetSize( 400, -row + 30 );
		}

		void Restart( bool randomizeSeed )
		{
			var c = World.Challenge.Create().Setup( world.challenge );
			if ( randomizeSeed && !c.fixedSeed )
				c.seed = World.NextRnd( OperationHandler.Event.CodeLocation.challengePanelRestart );
			root.NewGame( c );
		}

		new public void Update()
		{
			var m = root.mainTeam.itemProductivityHistory[(int)Item.Type.soldier];
			var challenge = world.challenge;
			worldTime.text = $"World time: {UIHelpers.TimeToString( time )}";
			conditions.text = challenge.conditionsText;
			if ( maintain )
			{
				World.Goal level = World.Goal.none;
				int time = 0;
				void CheckLevel( World.Goal levelToCheck, World.Timer timer )
				{
					if ( timer.inProgress )
					{
						level = levelToCheck;
						time = -timer.age;
					}
				}
				CheckLevel( World.Goal.bronze, challenge.maintainBronze );
				CheckLevel( World.Goal.silver, challenge.maintainSilver );
				CheckLevel( World.Goal.gold, challenge.maintainGold );
				if ( level != World.Goal.none )
					maintain.text = $"Maintain {level} level for {UIHelpers.TimeToString( time )} more!";
				else
					maintain.text = "No appraisable level reached yet";
			}
			if ( timeLeft )
			{
				if ( challenge.allowTimeLeftLevels )
				{
					void GoalLeft( World.Goal goal, float multiplier )
					{
						var left = (int)(challenge.timeLimit * multiplier - challenge.life.age);
						if ( left >= 0 )
							timeLeft.text = $"Time left: {UIHelpers.TimeToString( left )} ({goal})";
					}
					timeLeft.text = "Out of time";
					GoalLeft( World.Goal.bronze, 2 );
					GoalLeft( World.Goal.silver, 4f/3 );
					GoalLeft( World.Goal.gold, 1 );
				}
				else
				{
					int left = (int)(challenge.timeLimit - challenge.life.age);
					if ( left > 0 )
						timeLeft.text = $"Time left: {UIHelpers.TimeToString( left )}";
					else
						timeLeft.text = "Out of time";
				}
			}
			progress.progress = challenge.progress;
			currentChallenge.text =  $"Current challenge: {challenge.title}, level reached yet: {challenge.reachedLevel}";
			base.Update();
		}

		new public void OnDestroy()
		{
			if ( originalSpeed > 0 )
				root.world.SetSpeed( originalSpeed );
			if ( worldStopped )
				eye?.ReleaseFocus( null, true );
			base.OnDestroy();
		}
	}

	public class PlayerSelectorPanel : Panel
	{
		public InputField newName;
		public Text newNameTitle;
		public Dropdown selector;
		public Text selectorTitle;
		public bool createNewPlayer;
		public Dropdown control;

		public static PlayerSelectorPanel Create( bool createNewPlayer )
		{
			var p = new GameObject( "Player Selector Panel" ).AddComponent<PlayerSelectorPanel>();
			p.Open( createNewPlayer );
			return p;
		}

		void Open( bool createNewPlayer )
		{
			noResize = true;
			noPin = true;
			reopen = true;
			allowInSpectateMode = true;
			base.Open( 300, 200 );
			this.createNewPlayer = createNewPlayer;

			UIHelpers.currentRow = -borderWidth;
			if ( createNewPlayer )
			{
				newNameTitle = Text( "Name of new player" ).PinDownwards( borderWidth, -borderWidth, 200 );
				newName = InputField( Constants.Player.names.Random() ).PinDownwards( borderWidth, 0, 200 );
			}

			selectorTitle = Text( createNewPlayer ? "Select team" : "Select player" ).PinDownwards( borderWidth, 0, 200 );
			selector = Dropdown().PinDownwards( borderWidth, 0, 260 );
			List<string> items = new List<string>();
			int currentPlayer = 0;
			if ( createNewPlayer )
			{
				foreach( var team in world.teams )
					items.Add( team.name );
			}
			else
			{
				foreach ( var player in world.players )
				{
					if ( player == root.mainPlayer )
						currentPlayer = items.Count;
					items.Add( $"{player.name} (team {player.team.name})" );
				}
			}
			items.Add( "New" );
			selector.AddOptions( items );
			selector.value = currentPlayer;
			selector.onValueChanged.AddListener( Selected );

			if ( createNewPlayer )
				Button( "Create" ).PinDownwards( 100, 0, 80 ).AddClickHandler( CreatePlayer );
			
			var line = UIHelpers.currentRow;
			Text( "Control:" ).Pin( borderWidth, line, 100 );
			control = Dropdown().Pin( borderWidth+100, line, 150 );
			control.AddOptions( new List<String>{ "manual", "automatic", "demo" } );
			control.onValueChanged.AddListener( OnControlChanged );
		}

		void OnControlChanged( int index )
		{
			if ( createNewPlayer )
				return;

			if ( root.mainPlayer is Simpleton simpleton )
			{
				if ( index == 0 )
					simpleton.active = false;
				else
				{
					simpleton.active = true;
					simpleton.showActions = index == 2;
				}
			}
		}

		void Selected( int index )
		{
			if ( createNewPlayer )
				return;

			if ( index >= world.players.Count )
				Create( true );
			else
				root.mainPlayer = world.players[index];

			Close();
		}

		void CreatePlayer()
		{
			string team = Constants.Player.teamNames.Random();	// TODO No control over the team name?
			if ( selector.value < world.teams.Count )
				team = world.teams[selector.value].name;
			oh.ScheduleCreatePlayer( newName.text, team );
			Close();
		}

		new void Update()
		{
			base.Update();
			if ( root.mainPlayer is Simpleton simpleton && simpleton.active )
			{
				if ( simpleton.showActions )
					control.value = 2;
				else
					control.value = 1;

			}
			else
				control.value = 0;
		}
	}

	public class MainPanel : Panel
	{
		InputField seed;
		InputField saveName;
		Dropdown loadNames;
		FileSystemWatcher watcher;
		bool loadNamesRefreshNeeded = true;
		Eye grabbedEye;
		Dropdown networkJoinDestinationDropdown;
		InputField networkJoinDestinationInputField;

		public static MainPanel Create()
		{
			return new GameObject( "Main Panel", typeof( RectTransform ) ).AddComponent<MainPanel>();
		}

		public void Open( bool focusOnMainBuilding = false )
		{
			noCloseButton = true;
			noResize = true;
			noPin = true;
			allowInSpectateMode = true;
			bool demoMode = world.fileName.Contains( "demolevel" );
			Open( null, 0, 0, 300, 250 );
			this.PinCenter( 0, 0, 300, 250, 0.5f, 0.3f );

			UIHelpers.currentRow = -borderWidth;

			if ( !demoMode )
			{
				Button( "Continue" ).PinDownwards( 0, 0, 100, 25, 0.5f, 1, true ).AddClickHandler( Close );
				Image().PinDownwards( 0, 0, 260, 1, 0.5f, 1, true ).color = Color.black;
			}

			Button( "View Challenges" ).PinDownwards( 0, 0, 120, 25, 0.5f, 1, true ).AddClickHandler( () => { ChallengeList.Create(); Close(); } );
			Image().PinDownwards( 0, 0, 260, 1, 0.5f, 1, true ).color = Color.black;

			if ( !demoMode )
			{
				var loadRow = UIHelpers.currentRow;
				Button( "Load" ).PinDownwards( 20, 0, 60, 25 ).AddClickHandler( Load );
				UIHelpers.currentRow = loadRow;
				loadNames = Dropdown().PinDownwards( 80, 0, 200, 25 );
				Image().PinDownwards( 20, 0, 260, 1 ).color = Color.black;

				var saveRow = UIHelpers.currentRow;
				Button( "Save" ).PinDownwards( 20, 0, 60, 25 ).AddClickHandler( Save );
				UIHelpers.currentRow = saveRow;
				saveName = InputField().PinDownwards( 80, 0, 200, 25 );
				saveName.text = world.nextSaveFileName;
			}

			watcher = new FileSystemWatcher( Application.persistentDataPath + "/Saves" );
			watcher.Created += SaveFolderChanged;
			watcher.Deleted += SaveFolderChanged;
			watcher.EnableRaisingEvents = true;

			var replayRow = UIHelpers.currentRow;
			Button( "Load replay" ).PinDownwards( 0, 0, 100, 25, 0.25f, 1, true ).AddClickHandler( () => Replay( true ) );
			UIHelpers.currentRow = replayRow;
			Button( "Save replay" ).PinDownwards( 0, 0, 100, 25, 0.75f, 1, true ).AddClickHandler( () => Replay( false ) );

			var joinRow = UIHelpers.currentRow;
			Button( "Join" ).Pin( borderWidth, joinRow, 50, 25 ).AddClickHandler( Join );
			networkJoinDestinationDropdown = Dropdown().Pin( borderWidth, joinRow - 50, 250, 0 );
			networkJoinDestinationDropdown.onValueChanged.AddListener( JoinDropFinished );
			networkJoinDestinationDropdown.AddClickHandler( JoinDrop );
			Button( "Browse LAN" ).Pin( -100 - borderWidth, joinRow, 100, 25, 1 ).AddClickHandler( JoinDrop );
			networkJoinDestinationInputField = InputField().Pin( borderWidth, joinRow -25, 250, 25 );
			networkJoinDestinationInputField.onSubmit.AddListener( (string address) => Join() );

			Button( "Exit" ).PinDownwards( 0, 0, 100, 25, 0.5f, 1, true ).AddClickHandler( Application.Quit );

			if ( focusOnMainBuilding && root.mainPlayer )
			{
				grabbedEye = eye;
				grabbedEye.FocusOn( root.mainTeam.mainBuilding?.flag?.node, true, approach:false );
				grabbedEye.autoMove = new Vector2( 0.8f, 0.17f );
				escCloses = false;
			}

			SetSize( 300, -UIHelpers.currentRow + 20 );
		}

		public void Join()
		{
			var s = networkJoinDestinationInputField.text.Split( ':' );
			world.Join( s[0], int.Parse( s[1] ) );
			MessagePanel.Create( "Requesting game state from server" );
			Close();
		}

		void JoinDrop()
		{
			networkJoinDestinationDropdown.ClearOptions();
			List<string> options = new List<string>();
			var current = networkJoinDestinationInputField.text;
			if ( current != "" && !network.localDestinations.Contains( current ) )
				options.Add( current );
			foreach ( var destination in network.localDestinations )
				options.Add( destination );
			networkJoinDestinationDropdown.AddOptions( options );
			networkJoinDestinationDropdown.Show();
		}

		void JoinDropFinished( int value )
		{
			if ( value != -1 ) 
				networkJoinDestinationInputField.text = networkJoinDestinationDropdown.options[value].text; 
		}

		public new void OnDestroy()
		{
			base.OnDestroy();
			if ( grabbedEye == eye )
				root?.world?.eye?.ReleaseFocus( null, true );
		}

		public new void Update()
		{
			base.Update();
			if ( loadNamesRefreshNeeded )
				UpdateLoadNames();
			if ( networkJoinDestinationInputField.text == "" && network.localDestinations.Count > 0 )
				networkJoinDestinationInputField.text = network.localDestinations[0];
		}

		void Replay( bool load )
		{
			if ( load )
			{
				List<string> files = new List<string>();
				var directory = new DirectoryInfo( Application.persistentDataPath+"/Replays" );
				if ( !directory.Exists )
					return;

				var replayFiles = directory.GetFiles( "*.json" ).OrderByDescending( f => f.LastWriteTime );

				root.LoadReplay( replayFiles.First().FullName );
			}
			else
			{
				root.SaveReplay( Application.persistentDataPath + $"/Replays/{new System.Random().Next()}.json" );
			}
		}

		void Load()
		{
			if ( loadNames.options.Count <= loadNames.value )
				return;
			root.Load( Application.persistentDataPath + "/Saves/" + loadNames.options[loadNames.value].text );
			Close();
		}

		void Save()
		{
			root.Save( Application.persistentDataPath + "/Saves/" + saveName.text + ".json", true );
			saveName.text = world.nextSaveFileName;
		}

		void SaveFolderChanged( object sender, FileSystemEventArgs args )
		{
			loadNamesRefreshNeeded = true;
		}

		void UpdateLoadNames()
		{
			loadNamesRefreshNeeded = false;
			if ( loadNames == null )
				return;
			loadNames.ClearOptions();

			List<string> files = new List<string>();
			var directory = new DirectoryInfo( Application.persistentDataPath+"/Saves" );
			if ( !directory.Exists )
				return;

			var saveGameFiles = directory.GetFiles( "*.json" ).OrderByDescending( f => f.LastWriteTime );
			foreach ( var f in saveGameFiles )
				files.Add( f.Name );

			loadNames.AddOptions( files );
		}
	}

	public class WelcomePanel : Panel	// These two panels (WelcomePanel and RoadTutorialPanel) should simply be MessagePanel
	{
		public static WelcomePanel Create()
		{
			var p = new GameObject( "Welcome panel" ).AddComponent<WelcomePanel>();
			p.Open();
			return p;
		}

		void Open()
		{
			noResize = noPin = true;
			base.Open( 300, 200 );
			this.PinCenter( 0, 0, 300, 200, 0.5f, 0.5f );
			Text( $"Your goal in this game is to complete challenges. " +
				$"To see an update on this, open the challenge progress dialog (hotkey: {root.worldProgressButton.GetHotkey().keyName}). " +
				$"The only building you got at the beginning is your headquarters. It behaves like a stock, but you cannot destroy or move it. It also has " +
				$"somewhat higher capacity than a normal stock (can store {Constants.Stock.defaultmaxItemsForMain} items instead of {Constants.Stock.defaultmaxItems}). " +
				$"First thing you need to do is build more buildings, open the build panel (hotkey: {root.buildButton.GetHotkey().keyName})" ).Stretch( borderWidth, borderWidth, -borderWidth, -borderWidth );
		}

		new void Update()
		{
			base.Update();
			if ( root.panels.Count > 3 )
				Close();
		}
	}

	public class RoadTutorialPanel : Panel
	{
		public static RoadTutorialPanel Create()
		{
			var p = new GameObject( "Road tutorial" ).AddComponent<RoadTutorialPanel>();
			p.Open();
			return p;
		}

		void Open()
		{
			noResize = noPin = true;
			base.Open( 350, 250 );
			this.PinCenter( 0, 0, 350, 250, 0.5f, 0.5f );
			root.world.roadTutorialShowed = true;

			Text( "Every building has a junction in front of it, that is where the building is connected to the economy. On the other hand a junction might have zero " +
				"buildings using it as an exit, but can also have multiple ones, if the buildings are facing different directions. " +
				$"Junctions act as a temporary storage of items, a junction can have {Constants.Flag.maxItems} items placed at it. Junctions need to be connected with roads. " +
				$"A road is always connecting two junctions. Roads cannot cross each other. Every road has at least one hauler assigned to it, who is carrying items between the two " +
				$"ends of the road, but never leaves the road itself. Roads are free and has no limit on their length, haulers are also free. Roads are also " +
				$"instantly built. To build a road select a junction, and press the road button." ).Stretch( borderWidth, borderWidth, -borderWidth, -borderWidth );
		}
	}

	public interface IInputHandler
	{
		bool OnMovingOverNode( Node node );
		bool OnNodeClicked( Node node );
		bool OnObjectClicked( HiveObject target );
		void OnLostInput();
		bool pickGroundOnly { get { return false; } }
	}

	protected const bool keepGoing = true;	// possible return values for IInputHandler functions
	protected const bool finished = false;

	public static int FirstUnusedIndex( string path, string pattern, int limit = 1000 )
	{
		var files = Directory.GetFiles( path ).Select(f => System.IO.Path.GetFileName( f ) );
		var sortedFiles = files.ToList();
		sortedFiles.Sort();

		for ( int index = 0; index < limit; index++ )
		{
			if ( sortedFiles.BinarySearch( String.Format( pattern, index ) ) < 0 )
				return index;
		}
		return -1;
	}
}


public static class UIHelpers
{
	public static int currentRow = 0, currentColumn = 0;

	public static Image Image( this Component panel, Sprite picture = null )
	{
		Image i = new GameObject().AddComponent<Image>();
		i.name = "Image";
		i.sprite = picture;
		i.transform.SetParent( panel.transform );
		return i;
	}

	public static Image Image( this Component panel, Interface.Icon icon )
	{
		return panel.Image( Interface.iconTable.GetMediaData( icon ) );
	}

	public static Button CheckBox( this Component panel, string text )
	{
		Button b = new GameObject( "Checkbox" ).AddComponent<Button>();
		b.transform.SetParent( panel.transform );
		var i = new GameObject( "Checkbox image" ).AddComponent<Image>();
		i.Link( b ).Pin( 0, 0, Interface.iconSize, Interface.iconSize ).AddOutline();
		void UpdateCheckboxLook( bool on )
		{
			i.sprite = Interface.iconTable.GetMediaData( on ? Interface.Icon.yes : Interface.Icon.no );
		}
		b.visualizer = UpdateCheckboxLook;
		Text( b, text ).Link( b ).Pin( Interface.iconSize, 0, 200, Interface.iconSize ).alignment = TextAnchor.MiddleLeft;
		return b;
	}

	public static Text Text( this Component panel, string text = "", int fontSize = 12 )
	{
		Text t = new GameObject().AddComponent<Text>();
		t.name = "Text";
		t.transform.SetParent( panel.transform );
		t.font = Interface.font;
		t.fontSize = (int)( fontSize * Interface.uiScale );
		t.color = Color.black;
		t.text = text;
		return t;
	}

	public static UIElement Pin<UIElement>( this UIElement g, int x, int y, int xs = Interface.iconSize, int ys = Interface.iconSize, float xa = 0, float ya = 1, bool center = false ) where UIElement : Component
	{
		if ( center )
		{
			x -= xs / 2;
			y += ys / 2;
		}
		currentColumn = x + xs;
		currentRow = y - ys;
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
		return g.Pin( x, y, xs, ys, xa, ya, true );
	}

	public static UIElement PinDownwards<UIElement>( this UIElement g, int x, int y, int xs = Interface.iconSize, int ys = Interface.iconSize, float xa = 0, float ya = 1, bool center = false ) where UIElement : Component
	{
		g.Pin( x, y + currentRow - (center ? ys / 2 : 0), xs, ys, xa, ya, center );
		return g;
	}

	public static UIElement PinSideways<UIElement>( this UIElement g, int x, int y, int xs = Interface.iconSize, int ys = Interface.iconSize, float xa = 0, float ya = 1, bool center = false ) where UIElement : Component
	{
		g.Pin( x + currentColumn + (center ? xs / 2 : 0), y, xs, ys, xa, ya, center );
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

	public static UIElement Rotate<UIElement>( this UIElement g, float angle ) where UIElement : Component
	{
		g.transform.rotation = Quaternion.Euler( 0, 0, angle );
		return g;
	}

	[RequireComponent( typeof( RectTransform ) )]
	public class Button : MonoBehaviour, IPointerClickHandler
	{
		public Action leftClickHandler, rightClickHandler, middleClickHandler;
		public Action<bool> toggleHandler;
		public Action<bool> visualizer;
		public bool toggleState;

        public void OnPointerClick( PointerEventData eventData )
        {
			if ( eventData.button == PointerEventData.InputButton.Left && leftClickHandler != null )
				leftClickHandler();
			if ( eventData.button == PointerEventData.InputButton.Right && rightClickHandler != null )
				rightClickHandler();
			if ( eventData.button == PointerEventData.InputButton.Middle && middleClickHandler != null )
				middleClickHandler();
        }

		public void Toggle()
		{
			SetToggleState( !toggleState );
			if ( toggleHandler != null )
				toggleHandler( toggleState );
		}

		public void SetToggleState( bool state )
		{
			if ( toggleState == state )
				return;
			toggleState = state;
			if ( visualizer == null )
				visualizer = UpdateLook;
			visualizer( toggleState );
		}

		public void UpdateLook( bool on )
		{
			var i = GetComponent<Image>();
			if ( i )
				i.color = on ? Color.white : Color.grey;
		}
    }

	public enum ClickType
	{
		left,
		right,
		middle
	}

	public static UIElement AddHiveObjectHandler<UIElement>( this UIElement g, HiveObject hiveObject ) where UIElement : Component
	{
		var hoh = g.gameObject.AddComponent<Interface.HiveObjectHandler>();
		hoh.Open( hiveObject );
		return g;
	}

	public static UIElement AddClickHandler<UIElement>( this UIElement g, Action callBack, ClickType type = ClickType.left ) where UIElement : Component
	{
		var b = g.gameObject.GetComponent<Button>();
		if ( b == null )
			b = g.gameObject.AddComponent<Button>();
		if ( type == ClickType.left )
			b.leftClickHandler = callBack;
		if ( type == ClickType.right )
			b.rightClickHandler = callBack;
		if ( type == ClickType.middle )
			b.middleClickHandler = callBack;
		return g;
	}

	public static UIElement AddToggleHandler<UIElement>( this UIElement g, Action<bool> callBack, bool initialState = false ) where UIElement : Component
	{
		var b = g.gameObject.GetComponent<Button>();
		if ( b == null )
			b = g.gameObject.AddComponent<Button>();

		b.leftClickHandler = b.Toggle;
		b.toggleHandler = callBack;
		b.toggleState = initialState;
		if ( b.visualizer == null )
			b.visualizer = b.UpdateLook;
		b.visualizer( initialState );

		return g;
	}

	public static UIElement SetToggleState<UIElement>( this UIElement g, bool state ) where UIElement : Component
	{
		var b = g.gameObject.GetComponent<Button>();
		b?.SetToggleState( state );
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
	
	public static UIElement SetTooltip<UIElement>( this UIElement g, Func<string> textGenerator, Sprite image = null, string additionalText = "", Action<bool> onShow = null, int width = 300 ) where UIElement : Component
	{
			Assert.global.IsTrue( textGenerator != null || onShow != null );
			var s = g.gameObject.GetComponent<Interface.TooltipSource>();
			if ( s == null )
				s = g.gameObject.AddComponent<Interface.TooltipSource>();
			s.SetData( textGenerator, image, additionalText, onShow, width );
			foreach ( Transform t in g.transform )
				t.SetTooltip( textGenerator, image, additionalText, onShow );
			return g;
	}

	public static UIElement SetTooltip<UIElement>( this UIElement g, string text, Sprite image = null, string additionalText = "", Action<bool> onShow = null ) where UIElement : Component
	{
		return SetTooltip( g, text == null ? null as Func<string> : () => text, image, additionalText, onShow );
	}

	public static UIElement SetTooltip<UIElement>( this UIElement g, Action<bool> onShow ) where UIElement : Component
	{
		return SetTooltip( g, "", null, null, onShow );
	}

	public static UIElement RemoveTooltip<UIElement>( this UIElement g ) where UIElement : Component
	{
		var s = g.gameObject.GetComponent<Interface.TooltipSource>();
		if ( s )
			UnityEngine.Object.Destroy( s );
		foreach ( Transform t in g.transform )
			t.RemoveTooltip();
		return g;
	}

	public static UIElement AddHotkey<UIElement>( this UIElement g, string name, KeyCode key, bool ctrl = false, bool alt = false, bool shift = false ) where UIElement : Component
	{
		var h = g.gameObject.AddComponent<Interface.HotkeyControl>();
		h.Open( name, key, ctrl, alt, shift );
		g.name = name;
		return g;
	}

	public static Interface.Hotkey GetHotkey( this Component g )
	{
		return g.gameObject.GetComponent<Interface.HotkeyControl>()?.hotkey;
	}

	public static string GetPrettyName( this string name, bool capitalize = true )
	{
		bool beginWord = true;
		string result = "";
		foreach ( char c in name )
		{
			if ( Char.IsUpper( c ) )
			{
				beginWord = true;
				result += " ";
			}
			if ( beginWord && capitalize )
				result += Char.ToUpper( c );
			else
				result += Char.ToLower( c );

			beginWord = false;
		}
		return result;
	}

	public static ScrollRect Clear( this ScrollRect s )
	{
		foreach ( Transform child in s.content )
			MonoBehaviour.Destroy( child.gameObject );

		var bg = new GameObject().AddComponent<Image>().Link( s.content ).Stretch();
		bg.color = new Color( 0, 0, 0, 0 );	// emptry transparent background image for picking, otherwise mouse wheel is not srolling when not over a child element
		bg.name = "Background";
		return s;
	}

	public static ScrollRect SetContentSize( this ScrollRect scroll, int x = -1, int y = -1 )
	{
		var t = scroll.content.transform as RectTransform;
		var m = t.offsetMax;
		if ( y != -1 )
			m.y = (int)( Interface.uiScale * y );
		if ( x != -1 )
			m.x = (int)( Interface.uiScale * x );
		t.offsetMax = m;
		t.offsetMin = Vector2.zero;
		scroll.verticalNormalizedPosition = 1;
		return scroll;
	}

	public static string TimeToString( int time )
	{
		string result = "";
		bool hasHours = false, hasDays = false;
		if ( time >= 24*60*60*Constants.World.normalSpeedPerSecond )
		{
			result = $"{time/24/60/60/Constants.World.normalSpeedPerSecond}:";
			hasDays = true;
		}
		if ( time >= Constants.World.normalSpeedPerSecond*60*60 )
		{
			result += $"{((time/Constants.World.normalSpeedPerSecond/60/60)%24).ToString( hasDays ? "d2" : "d1" )}:";
			hasHours = true;
		}
		result += $"{((time/Constants.World.normalSpeedPerSecond/60)%60).ToString( hasHours ? "d2" : "d1" )}";
		if ( !hasDays )
			result += $":{((time/Constants.World.normalSpeedPerSecond)%60).ToString( "d2" )}";
		return result;
	}

	public static List<byte> Add( this List<byte> packet, int value )
	{
		var valueBytes = BitConverter.GetBytes( value );
		foreach ( var b in valueBytes )
			packet.Add( b );
		return packet;
	}

	public static List<byte> Extract( this List<byte> packet, ref int value )
	{
		var size = BitConverter.GetBytes( value ).Length;
		value = BitConverter.ToInt32( packet.GetRange( 0, size ).ToArray(), 0 );
		packet.RemoveRange( 0, size );
		return packet;
	}

	public static List<byte> Add( this List<byte> packet, bool value )
	{
		var valueBytes = BitConverter.GetBytes( value );
		foreach ( var b in valueBytes )
			packet.Add( b );
		return packet;
	}

	public static List<byte> Extract( this List<byte> packet, ref bool value )
	{
		var size = BitConverter.GetBytes( value ).Length;
		value = BitConverter.ToBoolean( packet.GetRange( 0, size ).ToArray(), 0 );
		packet.RemoveRange( 0, size );
		return packet;
	}

	public static List<byte> Add<enumType>( this List<byte> packet, enumType value )
	{
		return packet.Add( (int)(object)value );
	}

	public static List<byte> Extract<enumType>( this List<byte> packet, ref enumType value ) where enumType : System.Enum
	{
		int data = 0;
		packet.Extract( ref data );
			value = (enumType)(object)data;
		return packet;
	}

	public static List<byte> Add( this List<byte> packet, List<int> array )
	{
		packet.Add( array.Count );
		foreach ( var value in array )
			packet.Add( value );
		return packet;
	}

	public static List<byte> Extract( this List<byte> packet, ref List<int> array )
	{
		array.Clear();
		int size = 0;
		packet.Extract( ref size );
		for ( int i = 0; i < size; i++ )
		{
			int value = 0;
			packet.Extract( ref value );
			array.Add( value );
		}
		return packet;
	}

	public static Type Random<Type>( this Type[] array )
	{
		return array[new System.Random().Next( array.Length )];
	}
}



