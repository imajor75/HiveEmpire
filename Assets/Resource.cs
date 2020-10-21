using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[SelectionBase]
public class Resource : Assert.Base
{
	public GroundNode node;
	public bool underGround;
	public Type type;
	public World.Timer life;
	public World.Timer exposed;
	public int charges = 1;
	GameObject body;
	public World.Timer keepAway;
	public World.Timer spawn;
	public Worker hunter;
	public List<Worker> animals = new List<Worker>();
	public static int treeGrowthMax = 15000;    // 5 minutes
	public static int cornfieldGrowthMax = 6000;
	public static int exposeMax = 39000;
	public World.Timer silence;
	AudioClip nextSound;
	static public MediaTable<AudioClip, Type> ambientSounds;
	AudioSource soundSource;
	static public MediaTable<GameObject, Type> meshes;

	public enum Type
	{
		tree,
		rock,
		fish,
		cornfield,
		animalSpawner,
		pasturingAnimal,
		salt,
		coal,
		iron,
		gold,
		stone,
		expose,
		total,
		unknown = -1	
	}

	public static void Initialize()
	{
		object[] meshes = {
		"BrokenVector/LowPolyTreePack/Prefabs/Tree Type0 05", Type.tree,
		"BrokenVector/LowPolyTreePack/Prefabs/Tree Type3 02", Type.tree,
		"BrokenVector/LowPolyTreePack/Prefabs/Tree Type3 05", Type.tree,
		"BrokenVector/LowPolyTreePack/Prefabs/Tree Type5 04", Type.tree,
		"BrokenVector/LowPolyTreePack/Prefabs/Tree Type0 02", Type.tree,

		"LowPoly Rocks/Prefabs/Rock1", Type.rock,
		"LowPoly Rocks/Prefabs/Rock3", Type.rock,
		"LowPoly Rocks/Prefabs/Rock9", Type.rock,
		"Rocks pack Lite/Prefabs/Rock2", Type.rock,
		"Rocks pack Lite/Prefabs/Rock1", Type.rock,
		"Rocks pack Lite/Prefabs/Rock3", Type.rock,

		"Ores/coaltable_final" , Type.coal,
		"Ores/goldtable_final" , Type.gold,
		"Ores/irontable_final" , Type.iron,
		"Ores/salttable_final" , Type.salt,
		"Ores/stonetable_final" , Type.stone,
		"AnimalCave/animRock(Clone)", Type.animalSpawner,
		"grainfield_final", Type.cornfield };
		Resource.meshes.Fill( meshes );

		object[] sounds = {
			"bird1", 1000, Type.tree,
			"bird2", 1000, Type.tree,
			"bird3", 1000, Type.tree,
			"bird4", 1000, Type.tree };
		ambientSounds.Fill( sounds );
	}

	static public Resource Create()
	{
		GameObject obj = new GameObject();
		return obj.AddComponent<Resource>();
	}

	public static bool IsUnderGround( Type type )
	{
		return type == Type.coal || type == Type.iron || type == Type.stone || type == Type.gold || type == Type.salt;
	}

	public Resource Setup( GroundNode node, Type type, int charges = -1 )
	{
		underGround = IsUnderGround( type );

		if ( charges < 1 )
		{
			if ( underGround )
				charges = int.MaxValue;
			else
				charges = 1;
		}

		if ( ( underGround && node.type != GroundNode.Type.hill ) || ( !underGround && !node.CheckType( GroundNode.Type.land ) ) )
		{
			Destroy( gameObject );
			return null;
		}

		if ( node.building || node.flag || node.resource )
		{
			Destroy( gameObject );
			return null;
		}

		node.resource = this;
		this.type = type;
		this.charges = charges;
		this.node = node;
		return this;
	}

	public Resource SetupAsPrey( Worker animal )
	{
		if ( Setup( animal.node, Type.pasturingAnimal ) == null )
			return null;

		animals.Add( animal );
		return this;
	}

    // Start is called before the first frame update
    void Start()
    {
		transform.SetParent( World.resources.transform );
		transform.localPosition = node.Position();

		name = type.ToString();
		GameObject prefab = meshes.GetMediaData( type );
		if ( prefab )
			body = GameObject.Instantiate( prefab );
		if ( type == Type.pasturingAnimal )
			name = "Pasturing Animal Resource";
		if ( body != null )
		{
			if ( type == Type.tree || type == Type.rock )
				body.transform.Rotate( Vector3.up * World.rnd.Next( 360 ) );
			body.transform.SetParent( transform );
			body.transform.localPosition = Vector3.zero;
		}

		soundSource = World.CreateSoundSource( this );
	}

	void Update()
	{
		if ( type == Type.cornfield )
		{
			float growth = (float)this.life.Age / cornfieldGrowthMax;
			if ( node.type != GroundNode.Type.grass )
				growth /= 2;
			if ( growth > 1 )
				growth = 1;
			transform.localScale = new Vector3( 1, growth, 1 );
		}
		if ( type == Type.tree )
		{
			float size = (float)life.Age/treeGrowthMax;
			if ( node.type != GroundNode.Type.forest )
				size /= 2;
			size = Math.Max( size, 0.1f );
			size = Math.Min( size, 1 );
			transform.localScale = Vector3.one * size;
		}
	}

	void FixedUpdate()
	{
		if ( underGround )
			body?.SetActive( !exposed.Done );
		if ( type == Type.animalSpawner && spawn.Done )
		{
			foreach ( var o in Ground.areas[1] )
			{
				GroundNode n = node.Add( o );
				if ( n.building != null || n.resource != null )
					continue;
				if ( animals.Count >= 3 )
					continue;
				var animal = Worker.Create().SetupAsAnimal( this, n );
				if ( animal != null )
				{
					animals.Add( animal );
					break;
				}
				else
					assert.IsTrue( false );
			}
			spawn.Start( 1000 );
		}
		if ( silence.Done )
		{
			if ( nextSound )
			{
				soundSource.clip = nextSound;
				soundSource.loop = false;
				soundSource.Play();
				nextSound = null;
			}
			else
			{
				var m = ambientSounds.GetMedia( type );
				if ( m == null )
					silence.Start( 1500 );
				else
				{
					silence.Start( (int)( World.rnd.NextDouble() * m.intData * 50 ) );
					nextSound = m.data;
				}
			}
		}
	}

	static public Item.Type ItemType( Type type )
	{
		switch ( type )
		{
			case Type.tree:
				return Item.Type.log;
			case Type.rock:
				return Item.Type.stone;
			case Type.fish:
				return Item.Type.fish;
			case Type.cornfield:
				return Item.Type.grain;
			case Type.pasturingAnimal:
				return Item.Type.hide;
			case Type.salt:
				return Item.Type.salt;
			case Type.iron:
				return Item.Type.iron;
			case Type.gold:
				return Item.Type.gold;
			case Type.coal:
				return Item.Type.coal;
			case Type.stone:
				return Item.Type.stone;
			default:
				return Item.Type.unknown;
		}
	}

	public bool Remove()
	{
		if ( type == Type.pasturingAnimal && animals.Count > 0 )
		{
			animals[0].taskQueue.Clear();
			animals[0].Remove( false );
		}
		assert.AreEqual( this, node.resource );
		node.resource = null;
		Destroy( gameObject );
		return this;
	}

	public bool IsReadyToBeHarvested()
	{
		if ( !keepAway.Done )
			return false;
		if ( type == Type.tree )
		{
			if ( node.type == GroundNode.Type.forest )
				return life.Age > treeGrowthMax;
			return life.Age > treeGrowthMax * 2;
		}
		if ( type == Type.cornfield )
		{
			if ( node.type == GroundNode.Type.grass )
				return life.Age > cornfieldGrowthMax;
			return life.Age > cornfieldGrowthMax * 2;
		}
		return true;
	}

	public void UpdateBody()
	{
		transform.localPosition = node.Position();
	}

	public void Validate()
	{
		//assert.IsNotNull( body );
		if ( type == Type.animalSpawner )
			foreach ( var w in animals )
				assert.IsNotNull( w );
		if ( type == Type.pasturingAnimal )
			assert.AreEqual( animals.Count, 1 );
	}
}
