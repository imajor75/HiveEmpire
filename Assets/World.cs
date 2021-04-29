using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.PostProcessing;

public class World : MonoBehaviour
{
	public Ground ground;
	public int currentSeed;

	[JsonProperty]
	public float timeFactor = 1;
	public static bool massDestroy;
	static public System.Random rnd;
	public List<Player> players = new List<Player>();
	static public World instance;
	public Eye eye;
	static public int soundMaxDistance = 15;
	static public int layerIndexNotOnMap;
	static public int layerIndexMapOnly;
	static public int layerIndexPickable;
	static public int layerIndexPPVolume;
	static public Shader defaultShader;
	static public Shader defaultColorShader;
	static public Shader defaultMapShader;
	static public Shader defaultTextureShader;
	public bool gameInProgress;
	public int time;

	static public GameObject water;
	static public GameObject resources;
	static public GameObject buoys;
	static public GameObject nodes;

	[Obsolete( "Compatibility with old files", true )]
	float maxHeight;
	[Obsolete( "Compatibility with old files", true )]
	float waterLevel;
	[Obsolete( "Compatibility with old files", true )]
	float hillLevel;
	[Obsolete( "Compatibility with old files", true )]
	float mountainLevel;
	[Obsolete( "Compatibility with old files", true )]
	float forestGroundChance;
	[Obsolete( "Compatibility with old files", true )]
	float rockChance;
	[Obsolete( "Compatibility with old files", true )]
	float animalSpawnerChance;
	[Obsolete( "Compatibility with old files", true )]
	float ironChance;
	[Obsolete( "Compatibility with old files", true )]
	float coalChance;
	[Obsolete( "Compatibility with old files", true )]
	float stoneChance;
	[Obsolete( "Compatibility with old files", true )]
	float saltChance;
	[Obsolete( "Compatibility with old files", true )]
	float goldChance;

	public Settings settings;

	public class Settings : HeightMap.Settings
	{
		[Range(16, 128)]
		public int size = 48;

		public float maxHeight = 6;
		[Range(0.0f, 1.0f)]
		public float waterLevel = 0.25f;
		[Range(0.0f, 1.0f)]
		public float hillLevel = 0.55f;
		[Range(0.0f, 1.0f)]
		public float mountainLevel = 0.8f;
		[Range(0.0f, 1.0f)]
		public float forestGroundChance = 0.45f;

		public float forestChance = 0.006f;
		public float rocksChance = 0.002f;
		public float animalSpawnerChance = 0.001f;
		public float ironChance = 0.04f;
		public float coalChance = 0.04f;
		public float stoneChance = 0.02f;
		public float saltChance = 0.02f;
		public float goldChance = 0.02f;

		[JsonIgnore]
		public bool apply;  // For debug purposes only

		public void OnValidate()
		{
			if ( apply )
			{
				apply = false;
				instance.NewGame( instance.currentSeed, true );
			}
		}
	}

	public static void Initialize()
	{
		layerIndexNotOnMap = LayerMask.NameToLayer( "Not on map" );
		layerIndexMapOnly = LayerMask.NameToLayer( "Map only" );
		layerIndexPickable = LayerMask.NameToLayer( "Pickable" );
		layerIndexPPVolume = LayerMask.NameToLayer( "PPVolume" );
		Assert.global.IsTrue( layerIndexMapOnly != -1 && layerIndexNotOnMap != -1 );
		defaultShader = Shader.Find( "Standard" );
		defaultColorShader = Shader.Find( "Unlit/Color" );
		defaultTextureShader = Shader.Find( "Unlit/Texture" );
		defaultMapShader = Resources.Load<Shader>( "Map" );
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

	public void Awake()
	{
		settings = ScriptableObject.CreateInstance<Settings>();
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
		massDestroy = false;
		time += (int)timeFactor;
		foreach ( var player in players )
			player.FixedUpdate();
	}

	public void LateUpdate()
	{
		foreach ( var player in players )
			player.LateUpdate();
	}

	public void NewGame( int seed, bool keepCameraLocation = false )
	{
		var oldEye = ( eye.x, eye.y, eye.direction, eye.altitude, eye.targetAltitude, eye.viewDistance );

		Debug.Log( "Starting new game with seed " + seed );

		rnd = new System.Random( seed );
		currentSeed = seed;

		var heightMap = HeightMap.Create();
		heightMap.Setup( settings, rnd.Next() );
		heightMap.Fill();

		var forestMap = HeightMap.Create();
		forestMap.Setup( settings, rnd.Next() );
		forestMap.Fill();

#if DEBUG
		heightMap.SavePNG( "height.png" );
		forestMap.SavePNG( "forest.png" );
#endif

		Clear();
		Prepare();

		eye = Eye.Create().Setup( this );
		ground = Ground.Create().Setup( this, heightMap, forestMap, settings.size, settings.size );
		GenerateResources();
		var mainPlayer = Player.Create().Setup();
		if ( mainPlayer )
			players.Add( mainPlayer );
		ground.RecalculateOwnership();
		gameInProgress = true;

		foreach ( var player in players )
			player.Start();

		if ( keepCameraLocation )
		{
			eye.x = oldEye.x;
			eye.y = oldEye.y;
			eye.altitude = oldEye.altitude;
			eye.targetAltitude = oldEye.targetAltitude;
			eye.direction = oldEye.direction;
			eye.viewDistance = oldEye.viewDistance;
		}
	}

	void Start()
	{
		name = "World";
		foreach ( var player in players )
			player.Start();
		water.transform.localPosition = Vector3.up * settings.waterLevel * settings.maxHeight;
	}

	public void Load( string fileName )
	{
		Clear();
		Prepare();
		Interface.ValidateAll();

		using ( var sw = new StreamReader( fileName ) )
		using ( var reader = new JsonTextReader( sw ) )
		{
			var serializer = new Serializer( reader );
			World world = serializer.Deserialize<World>( reader );
			Assert.global.AreEqual( world, this );
		}

		foreach ( var player in players )
			player.Start();

		{
			var list = Resources.FindObjectsOfTypeAll<Road>();
			foreach ( var o in list )
			{
				o.ends[0] = o.nodes[0].flag;
				o.ends[1] = o.lastNode.flag;
			}
		}

		{
			var list = Resources.FindObjectsOfTypeAll<Worker>();
			foreach ( var o in list )
			{
				if ( o.type == Worker.Type.tinkerer )
					o.currentColor = Color.cyan;
				if ( o.type == Worker.Type.cart )
					o.currentColor = Color.white;
				if ( o.owner == null )
					o.owner = players[0];
				if ( o.taskQueue.Count > 0 && o.type == Worker.Type.tinkerer && o.itemsInHands[0] != null && o.itemsInHands[0].destination == null )
					o.itemsInHands[0].SetRawTarget( o.building );
			}
		}
		{
			var list = Resources.FindObjectsOfTypeAll<Building>();
			foreach ( var o in list )
			{
				if ( o.configuration == null )
					o.configuration = new Building.Configuration();
				if ( o.dispenser == null )
					o.dispenser = o.workerMate ?? o.worker;
				if ( o.construction.done )
					o.construction.worker = null;
				o.flagDirection = o.node.DirectionTo( o.flag.node );
				var s = o as Workshop;
				if ( s && s.working && s.worker.node == s.node && s.worker.taskQueue.Count == 0 && s.worker.walkTo && s.gatherer )
					s.working = false;
				if ( s && s.outputPriority == ItemDispatcher.Priority.stock )
					s.outputPriority = ItemDispatcher.Priority.low;
				if ( s && s.mode == Workshop.Mode.unknown )
				{
					if ( s.outputPriority == ItemDispatcher.Priority.low )
						s.mode = Workshop.Mode.whenNeeded;
					if ( s.outputPriority == ItemDispatcher.Priority.high )
						s.mode = Workshop.Mode.always;
				}

				var t = o as Stock;
				if ( t && t.cart == null )
					t.cart = Stock.Cart.Create().SetupAsCart( t ) as Stock.Cart;
			}
		}
		{
			var list = Resources.FindObjectsOfTypeAll<Resource>();
			foreach ( var o in list )
			{
				if ( o.life.Empty )
					o.life.reference = instance.time - 15000;
			}
		}
		{
			var list = Resources.FindObjectsOfTypeAll<Flag>();
			foreach ( var f in list )
				if ( f.flattening == null )
					f.flattening = new Building.Flattening();
		}


		{
			foreach ( var node in ground.nodes )
				node.AlignType();
		}
		gameInProgress = true;
		SetTimeFactor( timeFactor );    // Just for the animators

		Interface.ValidateAll();
	}

	public void Save( string fileName )
	{
		JsonSerializerSettings jsonSettings = new JsonSerializerSettings
		{
			
			TypeNameHandling = TypeNameHandling.Auto,
			PreserveReferencesHandling = PreserveReferencesHandling.Objects,
			ContractResolver = Serializer.SkipUnityContractResolver.Instance
		};
		var serializer = JsonSerializer.Create( jsonSettings );

		using var sw = new StreamWriter( fileName );
		using JsonTextWriter writer = new JsonTextWriter( sw );
		//writer.Formatting = Formatting.Indented;
		serializer.Serialize( writer, this );
	}

	public void Prepare()
	{
		var lightObject = new GameObject { layer = World.layerIndexPPVolume };
		var light = lightObject.AddComponent<Light>();
		light.type = UnityEngine.LightType.Directional;
		lightObject.name = "Sun";
		light.transform.Rotate( new Vector3( 40, -60, 0 ) );
		light.shadows = LightShadows.Soft;
		light.color = new Color( 1, 1, 1 );
		light.transform.SetParent( transform );
		var ppv = lightObject.AddComponent<PostProcessVolume>();
		ppv.profile = Resources.Load<PostProcessProfile>( "Post-processing Profile" );
		ppv.isGlobal = true;
		Assert.global.IsNotNull( ppv.profile );

		{
			// HACK The event system needs to be recreated after the main camera is destroyed,
			// otherwise there is a crash in unity
			Destroy( GameObject.FindObjectOfType<EventSystem>().gameObject );
			var esObject = new GameObject
			{
				name = "Event System"
			};
			esObject.AddComponent<EventSystem>();
			esObject.AddComponent<StandaloneInputModule>();
		}

		water = GameObject.CreatePrimitive( PrimitiveType.Plane );
		water.transform.SetParent( transform );
		water.GetComponent<MeshRenderer>().material = Resources.Load<Material>( "Water" );
		water.name = "Water";
		water.transform.localPosition = Vector3.up * settings.waterLevel * settings.maxHeight;
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
			Destroy( o.gameObject );

		massDestroy = true;
		Interface.root.Clear();
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

	public static void SetMaterialRecursive( GameObject gameObject, Material material )
	{
		var renderer = gameObject.GetComponent<MeshRenderer>();
		if ( renderer )
		{
			Material[] materials = new Material[renderer.materials.Length];
			for ( int i = 0; i < materials.Length; i++ )
				materials[i] = material;
			renderer.materials = materials;
		}
		foreach ( Transform child in gameObject.transform )
			SetMaterialRecursive( child.gameObject, material );
	}

	public static void CollectRenderersRecursive( GameObject gameObject, List<MeshRenderer> list )
	{
		var renderer = gameObject.GetComponent<MeshRenderer>();
		if ( renderer != null )
			list.Add( renderer );
		foreach ( Transform child in gameObject.transform )
			CollectRenderersRecursive( child.gameObject, list );
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

	public void Reset()
	{
		ground.Reset();
		foreach ( var player in players )
			Assert.global.AreEqual( player.firstPossibleEmptyItemSlot, 0 );

		Validate( true );
	}

	public void GenerateResources()
	{
		foreach ( var node in ground.nodes )
		{
			var r = new System.Random( World.rnd.Next() );
			if ( r.NextDouble() < settings.forestChance )
				node.AddResourcePatch( Resource.Type.tree, 8, 0.6f );
			if ( r.NextDouble() < settings.rocksChance )
				node.AddResourcePatch( Resource.Type.rock, 5, 0.5f );
			if ( r.NextDouble() < settings.animalSpawnerChance )
				node.AddResource( Resource.Type.animalSpawner );
			if ( r.NextDouble() < settings.ironChance )
				node.AddResourcePatch( Resource.Type.iron, 5, 10 );
			if ( r.NextDouble() < settings.coalChance )
				node.AddResourcePatch( Resource.Type.coal, 5, 10 );
			if ( r.NextDouble() < settings.stoneChance )
				node.AddResourcePatch( Resource.Type.stone, 3, 10 );
			if ( r.NextDouble() < settings.saltChance )
				node.AddResourcePatch( Resource.Type.salt, 3, 10 );
			if ( r.NextDouble() < settings.goldChance )
				node.AddResourcePatch( Resource.Type.gold, 3, 10 );
		}
	}

	public void SetTimeFactor( float factor )
	{
		timeFactor = factor;
		var list1 = Resources.FindObjectsOfTypeAll<Animator>();
		foreach ( var o in list1 )
			o.speed = factor;
		var list2 = Resources.FindObjectsOfTypeAll<ParticleSystem>();
		foreach ( var o in list2 )
		{
			var mainModule = o.main;
			mainModule.simulationSpeed = factor;
		}
	}

	public void Validate( bool chain )
	{
		if ( !chain )
			return;
		ground.Validate( true );
		foreach ( var player in players )
			player.Validate();
	}

	[System.Serializable]
	public struct Timer
	{
		public int reference;

		public void Start( int delta = 0 )
		{
			reference = instance.time + delta;
		}
		public void Reset()
		{
			reference = 0;
		}
		[JsonIgnore, SerializeField]
		public int Age
		{
			get
			{
				if ( Empty )
					return 0;
				return instance.time - reference;
			}
		}
		[JsonIgnore]
		public bool Done { get { return !Empty && Age >= 0; } }
		[JsonIgnore]
		public bool Empty { get { return reference == 0; } }
		[JsonIgnore]
		public bool InProgress { get { return !Empty && !Done; } }
	}
}

[System.Serializable]
public struct SerializableColor
{
	public float r, g, b, a;
	public static implicit operator SerializableColor( Color unityColor )
	{
		SerializableColor s;
		s.r = unityColor.r;
		s.g = unityColor.g;
		s.b = unityColor.b;
		s.a = unityColor.a;
		return s;
	}
	public static implicit operator Color( SerializableColor serializableColor )
	{
		Color s;
		s.r = serializableColor.r;
		s.g = serializableColor.g;
		s.b = serializableColor.b;
		s.a = serializableColor.a;
		return s;
	}
}
