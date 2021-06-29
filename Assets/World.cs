using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
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
	static public int soundMaxDistance = 7;
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
	public int randomSeed;
	public int overseas = 2;
	public bool victory;

	static public Water water;
	static public GameObject nodes;
	static public GameObject itemsJustCreated;

	[Obsolete( "Compatibility with old files", true )]
	float maxHeight;
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

	[System.Serializable]
	public class Ore
	{
		public Resource.Type resourceType;
		public float idealRatio;
		public int resourceCount;
		public float missing
		{
			get
			{
				int total = World.instance.oreCount;
				if ( resourceCount == 0 )
					return 1000f;
				return idealRatio - ( (float)resourceCount / total );
			}

		}
	}

	public int oreCount;
	public List<Ore> ores = new List<Ore>();
	public int animalSpawnerCount;

	public class Settings : HeightMap.Settings
	{
		[Range(16, 128)]
		public int size = 48;

		public float maxHeight = 6;
		[Range(0.0f, 1.0f)]
		public float waterLevel = 0.25f;
		[Range(0.0f, 1.0f)]
		public float hillLevel = 0.6f;
		[Range(0.0f, 1.0f)]
		public float mountainLevel = 0.8f;
		[Range(0.0f, 1.0f)]
		public float forestGroundChance = 0.45f;

		public float forestChance = 0.006f;
		public float rocksChance = 0.002f;
		public float animalSpawnerChance = 0.001f;
		public float oreChance = 0.1f;
		public float ironRatio = 4;
		public float coalRatio = 8;
		public float goldRatio = 3;
		public float saltRatio = 2;
		public float stoneRatio = 1;

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

		[JsonIgnore]
		public bool apply;  // For debug purposes only
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
		soundSource.pitch = instance.timeFactor;
		soundSource.maxDistance = GroundNode.size * World.soundMaxDistance;
		return soundSource;
	}

	void FixedUpdate()
	{
		if ( settings.apply )
		{
			settings.apply = false;
			instance.NewGame( instance.currentSeed, true );
			Interface.root.mainPlayer = instance.players[0];
		}
		massDestroy = false;
		time += (int)timeFactor;
		foreach ( var player in players )
			player.FixedUpdate();
	}
		
	public void NewGame( int seed, bool keepCameraLocation = false )
	{
		SetTimeFactor( 1 );
		overseas = 2;
		var oldEye = eye;
		victory = false;
		time = 0;

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
		Interface.ValidateAll();

		eye = Eye.Create().Setup( this );
		ground = Ground.Create().Setup( this, heightMap, forestMap, settings.size );
		GenerateResources();
		water = Water.Create().Setup( ground );
		var mainPlayer = Player.Create().Setup();
		if ( mainPlayer )
			players.Add( mainPlayer );
		ground.RecalculateOwnership();
		gameInProgress = true;

		water.transform.localPosition = Vector3.up * waterLevel;

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
		Interface.ValidateAll();
	}

	void Start()
	{
		name = "World";
		foreach ( var player in players )
			player.Start();
		water.transform.localPosition = Vector3.up * waterLevel;
	}

    public void Load( string fileName )
	{
   		Clear();
		Prepare();
		Interface.ValidateAll();

		World world = Serializer.Read<World>( fileName );
		Assert.global.AreEqual( world, this );

		rnd = new System.Random( randomSeed );

		foreach ( var player in players )
			player.Start();

		if ( water == null )
			water = Water.Create().Setup( ground );
		water.transform.localPosition = Vector3.up * waterLevel;

		{
			var list = Resources.FindObjectsOfTypeAll<Road>();
			foreach ( var o in list )
			{
				if ( !o.ready )
					o.Remove( false );
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
				if ( o.owner == null && players.Count > 0 )
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
				t?.CreateMissingArrays();
			}
		}
		{
			var list = Resources.FindObjectsOfTypeAll<Resource>();
			foreach ( var o in list )
			{
				if ( o.charges > 1000 )
					o.infinite = true;
				if ( o.life.empty )
					o.life.reference = instance.time - 15000;
			}
		}
		{
			var list = Resources.FindObjectsOfTypeAll<Workshop.GetResource>();
			foreach ( var o in list )
			{
#pragma warning disable 0618
				if ( o.node != null )
				{
					foreach ( var resource in o.node.resources )
						if ( resource.type == o.resourceType )
							o.resource = resource;
				}
#pragma warning restore 0618
			}
		}
		{
			var list = Resources.FindObjectsOfTypeAll<Flag>();
			foreach ( var f in list )
				if ( f.flattening == null )
					f.flattening = new Building.Flattening();
		}

		{
			var list = Resources.FindObjectsOfTypeAll<ItemDispatcher>();
			foreach ( var d in list )
			{
				while ( d.markets.Count < (int)Item.Type.total )
				{
					var market = ScriptableObject.CreateInstance<ItemDispatcher.Market>();
					market.Setup( d, (Item.Type)d.markets.Count );
					d.markets.Add( market );
				}
			}
		}

		{
			foreach ( var node in ground.nodes )
				node.AlignType();
		}
		gameInProgress = true;
		SetTimeFactor( timeFactor );    // Just for the animators and sound

		Interface.ValidateAll();
	}

	public void Save( string fileName )
	{
		randomSeed = rnd.Next();
		rnd = new System.Random( randomSeed );

		Serializer.Write( fileName, this, false );
	}

	public void Prepare()
	{
		var lightObject = new GameObject { layer = World.layerIndexPPVolume };
		var light = lightObject.AddComponent<Light>();
		light.type = UnityEngine.LightType.Directional;
		lightObject.name = "Sun";
		light.transform.Rotate( new Vector3( 60, 0, 0 ) );
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
			var esObject = new GameObject( "Event System" );
			esObject.AddComponent<EventSystem>();
			esObject.AddComponent<StandaloneInputModule>();
		}

		nodes = new GameObject( "Nodes" );
		nodes.transform.SetParent( transform );

		itemsJustCreated = new GameObject( "Items just created" );		// Temporary parent for items until they enter the logistic network. If they are just root in the scene, World.Clear would not destroy them.
		itemsJustCreated.transform.SetParent( transform );
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

	public static void DrawObject( GameObject prefab, Matrix4x4 transform )
	{
		MeshFilter filter = prefab.GetComponent<MeshFilter>();
		MeshRenderer renderer = prefab.GetComponent<MeshRenderer>();
		var local = Matrix4x4.TRS( prefab.transform.localPosition, prefab.transform.localRotation, prefab.transform.localScale );
		if ( filter && renderer )
			Graphics.DrawMesh( filter.sharedMesh, transform * local, renderer.sharedMaterial, 0 );
		foreach ( Transform c in prefab.transform )
			DrawObject( c.gameObject, transform * local );
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
		ores.Clear();
		oreCount = 0;
		animalSpawnerCount = 0;
		ores.Add( new Ore{ resourceType = Resource.Type.coal, idealRatio = settings.coalRatio } );
		ores.Add( new Ore{ resourceType = Resource.Type.iron, idealRatio = settings.ironRatio } );
		ores.Add( new Ore{ resourceType = Resource.Type.gold, idealRatio = settings.goldRatio } );
		ores.Add( new Ore{ resourceType = Resource.Type.salt, idealRatio = settings.saltRatio } );
		ores.Add( new Ore{ resourceType = Resource.Type.stone, idealRatio = settings.stoneRatio } );

		foreach ( var node in ground.nodes )
		{
			var r = new System.Random( rnd.Next() );
			if ( r.NextDouble() < settings.forestChance )
				node.AddResourcePatch( Resource.Type.tree, 8, 0.6f );
			if ( r.NextDouble() < settings.rocksChance )
				node.AddResourcePatch( Resource.Type.rock, 5, 0.5f );
			bool hasOre = false;
			foreach ( var resource in node.resources )
				if ( resource.underGround )
					hasOre = true;
			if ( r.NextDouble() < settings.oreChance && node.type == GroundNode.Type.hill && !hasOre )
			{
				float totalMissing = 0;
				foreach ( var ore in ores )
					totalMissing += ore.missing;
				float type = (float)( r.NextDouble() * totalMissing );
				
				foreach ( var ore in ores )
				{
					if ( type < ore.missing )
					{
						var resourceCount = node.AddResourcePatch( ore.resourceType, settings.size / 6, 10 );
						ore.resourceCount += resourceCount;
						oreCount += resourceCount;
						break;
					}
					else
						type -= ore.missing;
				}
			}

			if ( node.CheckType( GroundNode.Type.land ) )
			{
				foreach ( var o in Ground.areas[1] )
				{
					if ( node.Add( o ).type == GroundNode.Type.underWater )
					{
						node.AddResource( Resource.Type.fish );
						break;
					}
				}
			}
		}

		int idealAnimalSpawnerCount = (int)( settings.size * settings.size * settings.animalSpawnerChance );
		if ( idealAnimalSpawnerCount == 0 )
			idealAnimalSpawnerCount = 1;
		while ( animalSpawnerCount != idealAnimalSpawnerCount )
		{
			var location = ground.GetNode( rnd.Next( settings.size ), rnd.Next( settings.size ) );
			if ( Resource.Create().Setup( location, Resource.Type.animalSpawner ) != null )
				animalSpawnerCount++;
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
#if UNITY_EDITOR
			if ( PrefabUtility.IsPartOfAnyPrefab( o ) )
				continue;
#endif
			var mainModule = o.main;
			mainModule.simulationSpeed = factor;
		}
		var list3 = Resources.FindObjectsOfTypeAll<AudioSource>();
		foreach ( var o in list3 )
			o.pitch = factor;
	}

	public float productivityGoal
	{
		get
		{
			var groundSize = ground.dimension * ground.dimension;
			return groundSize / 768.0f;
		}
	}

	public static int hourTickCount { get { return (int)( 60 * 60 / UnityEngine.Time.fixedDeltaTime ); } }

	public float waterLevel
	{
		get
		{	
			return settings.waterLevel * settings.maxHeight;
		}

		[Obsolete( "Compatibility with old files", true )]
		set
		{
			// Compatibility with old files
			settings.waterLevel = value;
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
		[SerializeField]
		public int age
		{
			get
			{
				if ( empty )
					return 0;
				return instance.time - reference;
			}
		}
		public bool done { get { return !empty && age >= 0; } }
		[SerializeField]
		public bool empty { get { return reference == 0; } }
		public bool inProgress { get { return !empty && !done; } }
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
