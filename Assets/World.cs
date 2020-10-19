using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

public class World : MonoBehaviour
{
	public Ground ground;
	public float timeFactor = 1;
	static public System.Random rnd;
	public List<Player> players = new List<Player>();
	static public World instance;
	public Eye eye;
	static public int soundMaxDistance = 15;
	[JsonIgnore]
	static public int layerIndexNotOnMap;
	[JsonIgnore]
	static public int layerIndexMapOnly;
	[JsonIgnore]
	static public Shader defaultShader;
	static public Shader defaultColorShader;
	static public Shader defaultTextureShader;
	public bool gameInProgress;
	public int time;

	public static float maxHeight = 20;
	public static float waterLevel = 0.40f;
	public static float hillLevel = 0.55f;
	public static float mountainLevel = 0.6f;

	static public GameObject water;
	static public GameObject resources;
	static public GameObject buoys;
	static public GameObject nodes;

	public static float forestChance = 0.004f;
	public static float rocksChance = 0.002f;
	public static float animalSpawnerChance = 0.001f;
	public static float ironChance = 0.04f;
	public static float coalChance = 0.04f;
	public static float stoneChance = 0.02f;
	public static float saltChance = 0.02f;
	public static float goldChance = 0.02f;

	public static void Initialize()
	{
		layerIndexNotOnMap = LayerMask.NameToLayer( "Not on map" );
		layerIndexMapOnly = LayerMask.NameToLayer( "Map only" );
		Assert.global.IsTrue( layerIndexMapOnly != -1 && layerIndexNotOnMap != -1 );
		defaultShader = Shader.Find( "Standard" );
		defaultColorShader = Shader.Find( "Unlit/Color" );
		defaultTextureShader = Shader.Find( "Unlit/Texture" );
	}

	World()
	{
		Assert.global.IsNull( instance );
		instance = this;
	}

	public static World Create()
	{
		return new GameObject().AddComponent<World>();
	}

	public World Setup()
	{
		rnd = new System.Random();
		return this;
	}

	static public AudioSource CreateSoundSource( Component component )
	{
		var soundSource = component.gameObject.AddComponent<AudioSource>();
		soundSource.spatialBlend = 1;
		soundSource.minDistance = 1;
		soundSource.maxDistance = GroundNode.size * World.soundMaxDistance;
		return soundSource;
	}

	void FixedUpdate()
	{
		time++;
		foreach ( var player in players )
			player.FixedUpdate();
	}

	public void LateUpdate()
	{
		foreach( var player in players )
			player.LateUpdate();
	}

	public void NewGame( int seed )
	{
		Debug.Log( "Starting new game with seed " + seed );
		Clear();
		Prepare();

		eye = Eye.Create().Setup( this );
		ground = Ground.Create().Setup( this, seed );
		GenerateResources();
		players.Add( Player.Create().Setup() );
		ground.RecalculateOwnership();
		gameInProgress = true;

		foreach ( var player in players )
			player.Start();
	}

	void Start()
	{
		name = "World";
		foreach ( var player in players )
			player.Start();
	}

	public void Load( string fileName )
	{
		Clear();
		Prepare();

		using ( var sw = new StreamReader( fileName ) )
		using ( var reader = new JsonTextReader( sw ) )
		{
			var serializer = new Serializer( reader );
			World world = serializer.Deserialize<World>( reader );
			Assert.global.AreEqual( world, this );
		}

		foreach ( var player in players )
			player.Start();

		//{
		//	var list = Resources.FindObjectsOfTypeAll<Stock>();
		//	foreach ( var o in list )
		//	{
		//		o.owner.RegisterStock( o );
		//	}
		//}
		gameInProgress = true;
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
			//writer.Formatting = Formatting.Indented;
			serializer.Serialize( writer, this );
		}
	}

	public void Prepare()
	{
		var lightObject = new GameObject();
		var light = lightObject.AddComponent<Light>();
		light.type = UnityEngine.LightType.Directional;
		lightObject.name = "Sun";
		light.transform.Rotate( new Vector3( 40, -60, 0 ) );
		light.shadows = LightShadows.Soft;
		light.color = new Color( 0.6f, 0.6f, 0.6f );
		light.transform.SetParent( transform );

		{
			// HACK The event system needs to be recreated after the main camera is destroyed,
			// otherwise there is a crash in unity
			Destroy( GameObject.FindObjectOfType<EventSystem>().gameObject );
			var esObject = new GameObject();
			esObject.name = "Event System";
			esObject.AddComponent<EventSystem>();
			esObject.AddComponent<StandaloneInputModule>();
		}

		water = GameObject.CreatePrimitive( PrimitiveType.Plane );
		water.transform.SetParent( transform );
		water.GetComponent<MeshRenderer>().material = Resources.Load<Material>( "Water" );
		water.name = "Water";
		water.transform.localPosition = Vector3.up * waterLevel * maxHeight;
		water.transform.localScale = Vector3.one * 1000 * GroundNode.size;

		resources = new GameObject();
		resources.transform.SetParent( transform );
		resources.name = "Resources";

		buoys = new GameObject();
		buoys.transform.SetParent( transform );
		buoys.name = "Buoys";

		nodes = new GameObject();
		nodes.transform.SetParent( transform );
		nodes.name = "Nodes";
	}

	public void Clear()
	{
		gameInProgress = false;
		players.Clear();
		eye = null;
		foreach ( Transform o in transform )
		{
			o.name += " - DESTROYED";
			Destroy( o.gameObject );
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

	public void GenerateResources()
	{
		foreach ( var node in ground.nodes )
		{
			var r = new System.Random( World.rnd.Next() );
			if ( r.NextDouble() < forestChance )
				node.AddResourcePatch( Resource.Type.tree, 7, 0.5f );
			if ( r.NextDouble() < rocksChance )
				node.AddResourcePatch( Resource.Type.rock, 5, 0.5f );
			if ( r.NextDouble() < animalSpawnerChance )
				node.AddResource( Resource.Type.animalSpawner );
			if ( r.NextDouble() < ironChance )
				node.AddResourcePatch( Resource.Type.iron, 5, 10 );
			if ( r.NextDouble() < coalChance )
				node.AddResourcePatch( Resource.Type.coal, 5, 10 );
			if ( r.NextDouble() < stoneChance )
				node.AddResourcePatch( Resource.Type.stone, 3, 10 );
			if ( r.NextDouble() < saltChance )
				node.AddResourcePatch( Resource.Type.salt, 3, 10 );
			if ( r.NextDouble() < goldChance )
				node.AddResourcePatch( Resource.Type.gold, 3, 10 );
		}
	}

	public void SetTimeFactor( float factor )
	{
		timeFactor = factor;
		var list = Resources.FindObjectsOfTypeAll<Animator>();
		foreach ( var o in list )
			o.speed = factor;
	}

	static public int TimeStack
	{
		get
		{
			return (int)instance.timeFactor;
		}
	}

	public void Validate()
	{
		ground.Validate();
		foreach ( var player in players )
			player.Validate();
	}
}
