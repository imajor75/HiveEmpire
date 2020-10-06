﻿using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;

public class World : MonoBehaviour
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

	public static World Create()
	{
		return new GameObject().AddComponent<World>();
	}

	public World Setup()
	{
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
		players.Add( mainPlayer = Player.Create().Setup() );
		ground = Ground.Create().Setup( this, seed );
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
		//	var list = Resources.FindObjectsOfTypeAll<Item>();
		//	foreach ( var o in list )
		//	{
		//		Worker w = o.worker;
		//		if ( w == null )
		//			continue;
		//		GroundNode n = w.node;
		//		for ( int i = 0; i < w.taskQueue.Count; i++ )
		//		{
		//			var d = w.taskQueue[i] as Worker.DeliverItem;
		//			if ( d != null )
		//				break;

		//			var p = w.taskQueue[i] as Worker.WalkToRoadPoint;
		//			if ( p != null )
		//				n = w.road.nodes[p.targetPoint];

		//			var k = w.taskQueue[i] as Worker.WalkToNode;
		//			if ( k != null )
		//				n = k.target;

		//			var v = w.taskQueue[i] as Worker.WalkToNeighbour;
		//			if ( v != null )
		//				n = v.target;

		//			Assert.global.IsTrue( i != w.taskQueue.Count - 1 );
		//		}
		//		if ( o.destination == null || n != o.destination.node )
		//			n.flag.ReserveItem( o );
		//	}
		//}

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
	}

	public void Clear()
	{
		players.Clear();
		mainPlayer = null;
		mainBuilding = null;
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

	public void Validate()
	{
		ground.Validate();
	}
}
