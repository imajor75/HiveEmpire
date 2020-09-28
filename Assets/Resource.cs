﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[SelectionBase]
public class Resource : MonoBehaviour
{
	public GroundNode node;
	public bool underGround;
	public Type type;
	public int growth = 0;
	public int exposed = 0;
	public int charges = 1;
	GameObject body;
	public int keepAwayTimer = 0;
	public int spawnTimer = 0;
	public Worker hunter;
	public List<Worker> animals = new List<Worker>();
	static List<GameObject> templateTree = new List<GameObject>();
	static List<GameObject> templateRock = new List<GameObject>();
	static List<GameObject> templateTable = new List<GameObject>();
	static GameObject templateAnimalRock;
	static Material cornfieldMaterial;
	public static int treeGrowthMax = 15000;    // 5 minutes
	public static int cornfieldGrowthMax = 6000;
	public static int exposeMax = 9000;
	public int silenceTimer;
	public AudioClip nextSound;
	static public MediaTable<AudioClip, Type> ambientSounds;
	public AudioSource soundSource;

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
		total
	}

	public static void Initialize()
	{
		templateTree.Add( (GameObject)Resources.Load( "BrokenVector/LowPolyTreePack/Prefabs/Tree Type0 05" ) );
		templateTree.Add( (GameObject)Resources.Load( "BrokenVector/LowPolyTreePack/Prefabs/Tree Type3 02" ) );
		templateTree.Add( (GameObject)Resources.Load( "BrokenVector/LowPolyTreePack/Prefabs/Tree Type3 05" ) );
		templateTree.Add( (GameObject)Resources.Load( "BrokenVector/LowPolyTreePack/Prefabs/Tree Type5 04" ) );
		templateTree.Add( (GameObject)Resources.Load( "BrokenVector/LowPolyTreePack/Prefabs/Tree Type0 02" ) );

		templateRock.Add( (GameObject)Resources.Load( "LowPoly Rocks/Prefabs/Rock1" ) );
		templateRock.Add( (GameObject)Resources.Load( "LowPoly Rocks/Prefabs/Rock3" ) );
		templateRock.Add( (GameObject)Resources.Load( "LowPoly Rocks/Prefabs/Rock9" ) );
		templateRock.Add( (GameObject)Resources.Load( "Rocks pack Lite/Prefabs/Rock1" ) );
		templateRock.Add( (GameObject)Resources.Load( "Rocks pack Lite/Prefabs/Rock2" ) );
		templateRock.Add( (GameObject)Resources.Load( "Rocks pack Lite/Prefabs/Rock3" ) );

		templateTable.Add( (GameObject)Resources.Load( "Ores/coaltable_final" ) );
		templateTable.Add( (GameObject)Resources.Load( "Ores/goldtable_final" ) );
		templateTable.Add( (GameObject)Resources.Load( "Ores/irontable_final" ) );
		templateTable.Add( (GameObject)Resources.Load( "Ores/salttable_final" ) );
		templateTable.Add( (GameObject)Resources.Load( "Ores/stonetable_final" ) );

		cornfieldMaterial = Resources.Load<Material>( "cornfield" );

		templateAnimalRock = (GameObject)Resources.Load( "AnimalCave/animRock(Clone)" );

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

	public Resource Setup( GroundNode node, Type type, int charges = 1 )
	{
		underGround = IsUnderGround( type );

		if ( charges < 1 )
		{
			if ( underGround )
				charges = -1;
			else
				charges = 1;
		}

		if ( ( underGround && node.type != GroundNode.Type.hill ) || ( !underGround && node.type != GroundNode.Type.grass ) )
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
		transform.SetParent( node.ground.ResourcesGameObject().transform );
		transform.localPosition = node.Position();

		name = type.ToString();

		if ( type == Type.tree )
		{
			name = "Tree";
			body = GameObject.Instantiate( templateTree[World.rnd.Next( templateTree.Count )] );
			body.transform.Rotate( Vector3.up * World.rnd.Next( 360 ) );
			body.transform.localScale = Vector3.one * 0.3f;
		}
		if ( type == Type.rock )
		{
			name = "Rock";
			body = GameObject.Instantiate( templateRock[World.rnd.Next( templateRock.Count )] );
			body.transform.Rotate( Vector3.up * World.rnd.Next( 360 ) );
		}
		if ( type == Type.cornfield )
		{
			name = "Cornfield";
			body = GameObject.CreatePrimitive( PrimitiveType.Capsule );
			body.transform.localScale = new Vector3( 0.5f, 0, 0.5f );
			body.GetComponent<MeshRenderer>().material = cornfieldMaterial;
		}
		if ( type == Type.animalSpawner )
		{
			name = "Cave";
			body = GameObject.Instantiate( templateAnimalRock );
		}
		if ( type == Type.pasturingAnimal )
			name = "Pasturing Animal Resource";
		if ( type == Type.coal )
			body = GameObject.Instantiate( templateTable[0] );
		if ( type == Type.gold )
			body = GameObject.Instantiate( templateTable[1] );
		if ( type == Type.iron )
			body = GameObject.Instantiate( templateTable[2] );
		if ( type == Type.salt )
			body = GameObject.Instantiate( templateTable[3] );
		if ( type == Type.stone )
			body = GameObject.Instantiate( templateTable[4] );
		if ( body != null )
		{
			body.transform.SetParent( transform );
			body.transform.localPosition = Vector3.zero;
		}

		soundSource = World.CreateSoundSource( this );
	}

	void Update()
	{
		if ( type == Type.cornfield )
		{
			float growth = (float)this.growth / cornfieldGrowthMax;
			transform.localScale = new Vector3( 0.5f, growth / 2, 0.5f );
		}
		if ( type == Type.tree )
		{
			float size = (float)growth/treeGrowthMax;
			size = Math.Max( size, 0.1f );
			size = Math.Min( size, 1 );
			transform.localScale = Vector3.one * size;
		}
	}

	void FixedUpdate()
	{
		growth += (int)node.ground.world.speedModifier;
		keepAwayTimer -= (int)node.ground.world.speedModifier;
		exposed -= (int)node.ground.world.speedModifier;
		if ( underGround )
			body?.SetActive( exposed > 0 );
		if ( type == Type.animalSpawner && spawnTimer-- <= 0 )
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
					Assert.IsTrue( false );
			}
			spawnTimer = 1000;
		}
		silenceTimer--;
		if ( silenceTimer < 0 )
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
					silenceTimer = 1500;
				else
				{
					silenceTimer = (int)( World.rnd.NextDouble() * m.intData * 50 );
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

	public void Remove()
	{
		if ( type == Type.pasturingAnimal && animals.Count > 0 )
		{
			animals[0].taskQueue.Clear();
			animals[0].Remove( false );
		}
		Assert.AreEqual( this, node.resource );
		node.resource = null;
		Destroy( gameObject );
	}

	public bool IsReadyToBeHarvested()
	{
		if ( keepAwayTimer > 0 )
			return false;
		if ( type == Type.tree )
			return growth > treeGrowthMax;
		if ( type == Type.cornfield )
			return growth > cornfieldGrowthMax;
		return true;
	}

	public void UpdateBody()
	{
		transform.localPosition = node.Position();
	}

	public void Validate()
	{
		//Assert.IsNotNull( body );
		if ( type == Type.animalSpawner )
			foreach ( var w in animals )
				Assert.IsNotNull( w );
		if ( type == Type.pasturingAnimal )
			Assert.AreEqual( animals.Count, 1 );
	}
}
