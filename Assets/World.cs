using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class World : ScriptableObject
{
	public Ground ground;
	[JsonIgnore]
	public float speedModifier = 1;
	public Stock mainBuilding;
	public GroundNode zero;
	static public System.Random rnd;
	public List<Player> players = new List<Player>();
	public Player mainPlayer;
	static public World instance;
	public Eye eye;
	static public int soundMaxDistance = 15;

	World()
	{
		Assert.IsNull( instance );
		instance = this;
	}

	static public AudioSource CreateSoundSource( Component component )
	{
		var soundSource = component.gameObject.AddComponent<AudioSource>();
		soundSource.spatialBlend = 1;
		soundSource.minDistance = 1;
		soundSource.maxDistance = GroundNode.size * World.soundMaxDistance;
		return soundSource;
	}

	public void NewGame( int seed )
	{
		Debug.Log( "Starting new game with seed " + seed );
		Clear();
		Prepare( true );

		eye = Eye.Create().Setup( this );
		players.Add( mainPlayer = ScriptableObject.CreateInstance<Player>() );
		ground = Ground.Create().Setup( this, seed );
	}

	public void Load( string fileName )
	{
		Clear();
		Prepare( true );

		using ( var sw = new StreamReader( fileName ) )
		using ( var reader = new JsonTextReader( sw ) )
		{
			var serializer = new Serializer( reader );
			World world = serializer.Deserialize<World>( reader );
			Assert.AreEqual( world, this );
		}

		ground.FinishLayout();
		players.Clear();
		players.Add( mainPlayer = mainBuilding.owner );
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
			serializer.Serialize( writer, this );
		}
	}

	public void Prepare( bool force = false )
	{
		if ( Object.FindObjectOfType<ItemDispatcher>() == null || force )
		{
			var itemDispatcherObject = new GameObject();
			itemDispatcherObject.AddComponent<ItemDispatcher>();
		}
		if ( Object.FindObjectOfType<Light>() == null || force )
		{
			var lightObject = new GameObject();
			var light = lightObject.AddComponent<Light>();
			light.type = UnityEngine.LightType.Directional;
			lightObject.name = "Sun";
			light.transform.Rotate( new Vector3( 40, -60, 0 ) );
			light.shadows = LightShadows.Soft;
			light.color = new Color( 0.6f, 0.6f, 0.6f );
		}
		if ( Object.FindObjectOfType<EventSystem>() == null || force )
		{
			var esObject = new GameObject();
			esObject.name = "Event System";	
			esObject.AddComponent<EventSystem>();
			esObject.AddComponent<StandaloneInputModule>();
		}
	}

	public void Clear()
	{
		players.Clear();
		mainPlayer = null;
		mainBuilding = null;
		eye = null;
		foreach ( GameObject o in Object.FindObjectsOfType<GameObject>() )
		{
			if ( o.transform.root.GetComponent<Interface>() != null )
				continue;

			o.name += " - DESTROYED";
			Destroy( o );
		}
		Interface.instance.Clear();
	}
}
