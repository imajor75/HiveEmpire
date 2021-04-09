using Newtonsoft.Json;
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

	public float maxHeight = 8;
	public float waterLevel = 0.3f;
	public float hillLevel = 0.6f;
	public float mountainLevel = 0.8f;
	public float forestGroundChance = 0.45f;

	static public GameObject water;
	static public GameObject resources;
	static public GameObject buoys;
	static public GameObject nodes;

	public static float forestChance = 0.006f;
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
		time += (int)timeFactor;
		foreach ( var player in players )
			player.FixedUpdate();
	}

	public void LateUpdate()
	{
		foreach ( var player in players )
			player.LateUpdate();
	}

	public void NewGame( int seed, int size = 64 )
	{
		Debug.Log( "Starting new game with seed " + seed );

		rnd = new System.Random( seed );
		currentSeed = seed;

		Clear();
		Prepare();

		eye = Eye.Create().Setup( this );
		ground = Ground.Create().Setup( this, seed, size, size );
		GenerateResources();
		var mainPlayer = Player.Create().Setup();
		if ( mainPlayer )
			players.Add( mainPlayer );
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
		water.transform.localPosition = Vector3.up * waterLevel * maxHeight;
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

		{
			var list = Resources.FindObjectsOfTypeAll<Path>();
			foreach ( var o in list )
			{
				o.Validate();
			}
		}
		{
			var list = Resources.FindObjectsOfTypeAll<Workshop.GetResource>();
			foreach ( var o in list )
			{
#pragma warning disable CS0612 // Type or member is obsolete
				if ( o.item )
				{
					o.item.nextFlag.CancelItem( o.item );
					o.item?.Remove( false );
					o.item = null;
				}
#pragma warning restore CS0612 // Type or member is obsolete
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
				if ( o.taskQueue.Count > 0 && o.type == Worker.Type.tinkerer && o.itemInHands != null && o.itemInHands.destination == null )
					o.itemInHands.SetRawTarget( o.building );
				o.Validate();
			}
		}
		{
			var list = Resources.FindObjectsOfTypeAll<Building>();
			foreach ( var o in list )
			{
				var s = o as Workshop;
				if ( s && s.working && s.worker.node == s.node && s.worker.taskQueue.Count == 0 && s.worker.walkTo && s.Gatherer )
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

				o.Validate();
			}
		}
		{
			var list = Resources.FindObjectsOfTypeAll<Item>();
			foreach ( var o in list )
			{
				if ( o.index > -1 )
					o.Validate();
			}
		}
		{
			var list = Resources.FindObjectsOfTypeAll<Resource>();
			foreach ( var o in list )
			{
				if ( o.life.Empty )
					o.life.reference = World.instance.time - 15000;
				o.Validate();
			}
		}
		{
			var list = Resources.FindObjectsOfTypeAll<Road>();
			foreach ( var o in list )
				o.Validate();
		}
		gameInProgress = true;
		SetTimeFactor( timeFactor );    // Just for the animators
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
		var lightObject = new GameObject();
		lightObject.layer = World.layerIndexPPVolume;
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

		maxHeight = 8;
		waterLevel = 0.3f;
		hillLevel = 0.6f;
		mountainLevel = 0.8f;
		forestGroundChance = 0.45f;
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

		Validate();
	}

	public void GenerateResources()
	{
		foreach ( var node in ground.nodes )
		{
			var r = new System.Random( World.rnd.Next() );
			if ( r.NextDouble() < forestChance )
				node.AddResourcePatch( Resource.Type.tree, 8, 0.6f );
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

	public void Validate()
	{
		ground.Validate();
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
