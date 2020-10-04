using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class World : ScriptableObject
{
	public Ground ground;
	[JsonIgnore]
	public float speedModifier = 1;
	public Stock mainBuilding;
	static public System.Random rnd;
	public List<Player> players = new List<Player>();
	public Player mainPlayer;
	static public World instance;
	public Eye eye;
	static public int soundMaxDistance = 15;
	[JsonIgnore]
	static public int layerIndexNotOnMap;
	[JsonIgnore]
	static public int layerIndexMapOnly;
	[JsonIgnore]
	static public Shader defaultShader;

	public static void Initialize()
	{
		layerIndexNotOnMap = LayerMask.NameToLayer( "Not on map" );
		layerIndexMapOnly = LayerMask.NameToLayer( "Map only" );
		Assert.global.IsTrue( layerIndexMapOnly != -1 && layerIndexNotOnMap != -1 );
		defaultShader = Shader.Find( "Standard" );
	}

	World()
	{
		Assert.global.IsNull( instance );
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
		players.Add( mainPlayer = Player.Create().Setup() );
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
			Assert.global.AreEqual( world, this );
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

	public static Transform FindChildRecursive( Transform parent, string substring )
	{
		foreach ( Transform child in parent )
		{
			if ( child.name.Contains( substring ) )
				return child;
			Transform grandChild = FindChildRecursive( child, substring );
			if ( grandChild )
				return grandChild;
		}

		return null;
	}

	public static void SetLayerRecursive( GameObject gameObject, int layer )
	{
		gameObject.layer = layer;
		foreach ( Transform child in gameObject.transform )
			SetLayerRecursive( child.gameObject, layer );
	}
	public enum BlendMode
	{
		Opaque,
		Cutout,
		Fade,
		Transparent
	}

	public static void SetRenderMode( Material standardShaderMaterial, BlendMode blendMode )
	{
		switch ( blendMode )
		{
			case BlendMode.Opaque:
				standardShaderMaterial.SetInt( "_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One );
				standardShaderMaterial.SetInt( "_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero );
				standardShaderMaterial.SetInt( "_ZWrite", 1 );
				standardShaderMaterial.DisableKeyword( "_ALPHATEST_ON" );
				standardShaderMaterial.DisableKeyword( "_ALPHABLEND_ON" );
				standardShaderMaterial.DisableKeyword( "_ALPHAPREMULTIPLY_ON" );
				standardShaderMaterial.renderQueue = -1;
				break;
			case BlendMode.Cutout:
				standardShaderMaterial.SetInt( "_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One );
				standardShaderMaterial.SetInt( "_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero );
				standardShaderMaterial.SetInt( "_ZWrite", 1 );
				standardShaderMaterial.EnableKeyword( "_ALPHATEST_ON" );
				standardShaderMaterial.DisableKeyword( "_ALPHABLEND_ON" );
				standardShaderMaterial.DisableKeyword( "_ALPHAPREMULTIPLY_ON" );
				standardShaderMaterial.renderQueue = 2450;
				break;
			case BlendMode.Fade:
				standardShaderMaterial.SetInt( "_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha );
				standardShaderMaterial.SetInt( "_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha );
				standardShaderMaterial.SetInt( "_ZWrite", 0 );
				standardShaderMaterial.DisableKeyword( "_ALPHATEST_ON" );
				standardShaderMaterial.EnableKeyword( "_ALPHABLEND_ON" );
				standardShaderMaterial.DisableKeyword( "_ALPHAPREMULTIPLY_ON" );
				standardShaderMaterial.renderQueue = 3000;
				break;
			case BlendMode.Transparent:
				standardShaderMaterial.SetInt( "_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One );
				standardShaderMaterial.SetInt( "_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha );
				standardShaderMaterial.SetInt( "_ZWrite", 0 );
				standardShaderMaterial.DisableKeyword( "_ALPHATEST_ON" );
				standardShaderMaterial.DisableKeyword( "_ALPHABLEND_ON" );
				standardShaderMaterial.EnableKeyword( "_ALPHAPREMULTIPLY_ON" );
				standardShaderMaterial.renderQueue = 3000;
				break;
		}
	}

	public void Validate()
	{
		ground.Validate();
	}
}
