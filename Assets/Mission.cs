using Newtonsoft.Json;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Mission : MonoBehaviour
{
	public Ground ground;

    void Start()
    {
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
		var serializer = new Serializer();

		using ( var sw = new StreamReader( fileName ) )
		using ( var reader = new JsonTextReader( sw ) )
		{
			ground = serializer.Deserialize<Ground>( reader );
		}

		ground.FinishLayout();

		PrepareScene( true );
	}

	public void Save( string fileName )
	{
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
