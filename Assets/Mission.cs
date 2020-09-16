	using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Mission : MonoBehaviour
{
	public Ground ground;
	public List<Player> players = new List<Player>();
	public Player mainPlayer;

    void Start()
    {
		players.Add( mainPlayer = ScriptableObject.CreateInstance<Player>() );
		Item.Initialize();
		Panel.Initialize();
		Building.Initialize();
		Road.Initialize();
		Worker.Initialize();
		Flag.Initialize();
		Resource.Initialize();
		NewGame();
	}

	public void PrepareScene( bool force = false )
	{
		if ( Object.FindObjectOfType<ItemDispatcher>() == null || force )
		{
			var itemDispatcherObject = new GameObject();
			itemDispatcherObject.AddComponent<ItemDispatcher>();
		}
		if ( Object.FindObjectOfType<Eye>() == null || force )
		{
			Eye.Create();
		}
		if ( Object.FindObjectOfType<Light>() == null || force )
		{
			var lightObject = new GameObject();
			var light = lightObject.AddComponent<Light>();
			light.type = UnityEngine.LightType.Directional;
			lightObject.name = "Sun";
			light.transform.Rotate( new Vector3( 40, -60, 0 ) );
			light.shadows = LightShadows.Soft;
			light.color = new Color( 0.7f, 0.7f, 0.7f );
		}
		if ( Object.FindObjectOfType<Canvas>() == null || force )
		{
			var canvasObject = new GameObject();
			canvasObject.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
			canvasObject.AddComponent<GraphicRaycaster>();
			canvasObject.name = "Canvas";
		}
		if ( Object.FindObjectOfType<EventSystem>() == null || force )
		{
			var esObject = new GameObject();
			esObject.name = "Event System";
			esObject.AddComponent<EventSystem>();
			esObject.AddComponent<StandaloneInputModule>();
		}
	}

	public void ClearScene()
	{
		foreach ( GameObject o in Object.FindObjectsOfType<GameObject>() )
		{
			if ( o.GetComponent<Mission>() != null )
				continue;

			o.name += " - DESTROYED";
			Destroy( o );
		}
	}

	public void NewGame()
	{
		ClearScene();
		PrepareScene( true );

		ground = Ground.Create().Setup();
	}

	public void Load( string fileName )
	{
		ClearScene();
		PrepareScene( true );

		using ( var sw = new StreamReader( fileName ) )
		using ( var reader = new JsonTextReader( sw ) )
		{
			var serializer = new Serializer( reader );
			ground = serializer.Deserialize<Ground>( reader );
		}

		ground.FinishLayout();
		players.Clear();
		players.Add( mainPlayer = ground.mainBuilding.owner );

		Camera.main.transform.localPosition = new Vector3( ground.eyeX, ground.eyeY, ground.eyeZ );
		Camera.main.transform.rotation = new Quaternion( ground.eyeDX, ground.eyeDY, ground.eyeDZ, ground.eyeDW );
		Object.FindObjectOfType<Eye>().altitude = ground.eyeAltitude;
	}

	public void Save( string fileName )
	{
		ground.eyeX = Camera.main.transform.localPosition.x;
		ground.eyeY = Camera.main.transform.localPosition.y;
		ground.eyeZ = Camera.main.transform.localPosition.z;
		ground.eyeDX = Camera.main.transform.rotation.x;
		ground.eyeDY = Camera.main.transform.rotation.y;
		ground.eyeDZ = Camera.main.transform.rotation.z;
		ground.eyeDW = Camera.main.transform.rotation.w;
		ground.eyeAltitude = Object.FindObjectOfType<Eye>().altitude;

		JsonSerializerSettings jsonSettings = new JsonSerializerSettings();
		jsonSettings.TypeNameHandling = TypeNameHandling.Auto;
		jsonSettings.PreserveReferencesHandling = PreserveReferencesHandling.Objects;
		jsonSettings.ContractResolver = Serializer.SkipUnityContractResolver.Instance;
		var serializer = JsonSerializer.Create( jsonSettings );

		using ( var sw = new StreamWriter( fileName ) )
		using ( JsonTextWriter writer = new JsonTextWriter( sw ) )
		{
			writer.Formatting = Formatting.Indented;
			serializer.Serialize( writer, ground );
		}
	}

	void Update()
    {
		if ( Input.GetKeyDown( KeyCode.P ) )
		{
			string fileName = "test.json";
			Save( fileName );
			Debug.Log( fileName + " is saved" );
		}
		if ( Input.GetKeyDown( KeyCode.L ) )
		{
			string fileName = "test.json";
			Load( "test.json" );
			Debug.Log( fileName + " is loaded" );
		}
		if ( Input.GetKeyDown( KeyCode.N ) )
		{
			NewGame();
			Debug.Log( "New game created" );
		}
	}
}
