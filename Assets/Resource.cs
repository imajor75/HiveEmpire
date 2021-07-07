using System;
using System.Collections.Generic;
using UnityEngine;

[SelectionBase]
public class Resource : HiveObject
{
	public GroundNode node;
	public bool underGround;
	public Type type;
	public World.Timer life;
	[Obsolete( "Compatibility with old files", true )]
	World.Timer exposed;
	public int charges = 1;
	public bool infinite;
	GameObject body;
	public int bodyRandom;	// Just a random number. We cannot generate a random number in Start otherwise CRC would break
	public World.Timer gathered;
	public World.Timer keepAway;
	public World.Timer spawn;
	public Worker hunter;
	public List<Worker> animals = new List<Worker>();
	public const int treeGrowthMax = 15000;    // 5 minutes
	public const int cornfieldGrowthMax = 20000;
	public const int animalSpawnTime = 1000;
	public World.Timer silence;
	static public MediaTable<AudioClip, Type> ambientSounds;
	AudioSource soundSource;
	static public MediaTable<GameObject, Type> meshes;
	public Blocking isBlocking
	{
		get
		{
			return type switch
			{
				Type.tree => Blocking.all,
				Type.rock => Blocking.all,
				Type.fish => Blocking.none,
				Type.cornfield => Blocking.everythingButWorkers,
				Type.animalSpawner => Blocking.all,
				Type.pasturingAnimal => Blocking.everythingButWorkers,
				Type.salt => Blocking.none,
				Type.coal => Blocking.none,
				Type.iron => Blocking.none,
				Type.gold => Blocking.none,
				Type.stone => Blocking.none,
				Type.expose => Blocking.none,
				_ => Blocking.none
			};
		}
	}

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
		expose,	// Obsolete, kept only to be able to load old saves
		soil,
		total,
		unknown = -1	
	}

	public enum Blocking
	{
		none,
		everythingButWorkers,
		all
	}

	public static void Initialize()
	{
		object[] meshes = {
		//"prefabs/trees/large01", Type.tree,
		//"prefabs/trees/large02", Type.tree,
		"prefabs/trees/round01", Type.tree,
		"prefabs/trees/round02", Type.tree,
		"prefabs/trees/round03", Type.tree,
		"prefabs/trees/round04", Type.tree,
		"prefabs/trees/thin01", Type.tree,
		"prefabs/trees/thin02", Type.tree,
		"prefabs/trees/thin03", Type.tree,
		"prefabs/trees/thin04", Type.tree,
		"prefabs/trees/thin05", Type.tree,
		"prefabs/trees/thin06", Type.tree,

		"prefabs/rocks/rock01", Type.rock,
		"prefabs/rocks/rock02", Type.rock,
		"prefabs/rocks/rock03", Type.rock,
		"prefabs/rocks/rock04", Type.rock,

		"AnimalCave/animRock(Clone)", Type.animalSpawner,
		"grainfield_final", Type.cornfield };
		Resource.meshes.Fill( meshes );

		object[] sounds = {
			"bird1", 60000, Type.tree,
			"bird2", 60000, Type.tree,
			"bird3", 60000, Type.tree,
			"bird4", 60000, Type.tree };
		ambientSounds.fileNamePrefix = "effects/";
		ambientSounds.Fill( sounds );
	}

	static public Resource Create()
	{
		return new GameObject().AddComponent<Resource>();
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
			if ( underGround || type == Type.fish )
				charges = int.MaxValue;
			else
			{
				if ( type == Type.rock )
					charges = 6;
				else
					charges = 1;
			}
		}

		if ( ( underGround && node.type != GroundNode.Type.hill ) || ( !underGround && !node.CheckType( GroundNode.Type.land ) ) )
		{
			DestroyThis();
			return null;
		}

		if ( node.IsBlocking() )
		{
			DestroyThis();
			return null;
		}

		node.resources.Add( this );
		this.type = type;
		if ( charges != int.MaxValue )
		{
			this.charges = charges;
			this.infinite = false;
		}
		else
			this.infinite = true;
		this.node = node;
		life.Start();
		bodyRandom = World.rnd.Next();
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
    new public void Start()
    {
		node.ground.Link( this );
		transform.localPosition = node.position;

		name = type.ToString();
		GameObject prefab = meshes.GetMediaData( type, bodyRandom );
		if ( prefab )
			body = Instantiate( prefab );
		if ( type == Type.pasturingAnimal )
			name = "Pasturing Animal Resource";
		if ( body != null )
		{
			if ( type == Type.tree || type == Type.rock )
				body.transform.Rotate( Vector3.up * ( bodyRandom % 360 ) );
			body.transform.SetParent( transform );
			body.transform.localPosition = Vector3.zero;

			// Align cornfield to ground
			if ( type == Type.cornfield )
			{
				Mesh mesh = body.GetComponent<MeshFilter>().mesh;
				var positions = mesh.vertices;
				for ( int i = 0; i < positions.Length; i++ )
				{
					var worldPos = body.transform.TransformPoint( positions[i] );
					float h = node.ground.GetHeightAt( worldPos.x, worldPos.z ) - node.height;
					positions[i] = body.transform.InverseTransformPoint( worldPos + Vector3.up * h );
				}
				mesh.vertices = positions;
			}

		}

		soundSource = World.CreateSoundSource( this );
		base.Start();
	}

	public void Update()
	{
		if ( type == Type.cornfield )
		{
			float growth = (float)life.age / cornfieldGrowthMax;
			if ( node.type != GroundNode.Type.grass )
				growth /= 2;
			if ( growth > 1 )
				growth = 1;
			var p = node.position;
			transform.localPosition = node.position + Vector3.up * ( -0.4f + 0.4f * growth );
		}
		if ( type == Type.tree )
		{
			float size = (float)life.age / treeGrowthMax;
			if ( node.type != GroundNode.Type.forest )
				size /= 2;
			size = Math.Max( size, 0.1f );
			size = Math.Min( size, 1 );
			transform.localScale = Vector3.one * size;
		}
	}

	public void FixedUpdate()
	{
		if ( type == Type.animalSpawner && ( spawn.done || spawn.empty ) )
		{
			foreach ( var o in Ground.areas[1] )
			{
				GroundNode n = node.Add( o );
				if ( n.IsBlocking() )
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
					assert.Fail();
			}
			spawn.Start( animalSpawnTime );
		}
		if ( silence.done || silence.empty )
		{
			var m = ambientSounds.GetMedia( type, World.rnd.Next() );
			if ( m == null || m.data == null )
			{
				silence.Start( 1500 );
				assert.AreNotEqual( type, Type.tree );
			}
			else
			{
				silence.Start( (int)( World.rnd.NextDouble() * m.intData ) );
				soundSource.clip = m.data;
				soundSource.loop = false;
				soundSource.Play();
			}
		}
	}

	static public Item.Type ItemType( Type type )
	{
		return type switch
		{
			Type.tree => Item.Type.log,
			Type.rock => Item.Type.stone,
			Type.fish => Item.Type.fish,
			Type.cornfield => Item.Type.grain,
			Type.pasturingAnimal => Item.Type.hide,
			Type.salt => Item.Type.salt,
			Type.iron => Item.Type.iron,
			Type.gold => Item.Type.gold,
			Type.coal => Item.Type.coal,
			Type.stone => Item.Type.stone,
			_ => Item.Type.unknown,
		};
	}

	public override bool Remove( bool takeYourTime )
	{
		if ( type == Type.pasturingAnimal && animals.Count > 0 )
		{
			animals[0].taskQueue.Clear();
			animals[0].Remove( false );
		}
		assert.IsTrue( node.resources.Contains( this ) );
		node.resources.Remove( this );
		DestroyThis();
		return true;
	}

	public bool IsReadyToBeHarvested()
	{
		if ( !keepAway.done && !keepAway.empty )
			return false;
		if ( type == Type.tree )
		{
			if ( node.type == GroundNode.Type.forest )
				return life.age > treeGrowthMax;
			return life.age > treeGrowthMax * 2;
		}
		if ( type == Type.cornfield )
		{
			if ( node.type == GroundNode.Type.grass )
				return life.age > cornfieldGrowthMax;
			return life.age > cornfieldGrowthMax * 2;
		}
		return true;
	}

	public void UpdateBody()
	{
		transform.localPosition = node.position;
	}

	public override void Reset()
	{
		keepAway.Start();
		if ( type == Type.cornfield )
			Remove( false );
	}

	public override GroundNode location { get { return node; } }

	public override void Validate( bool chain )
	{
		if ( type == Type.animalSpawner )
			foreach ( var w in animals )
				assert.IsNotNull( w );
		if ( type == Type.pasturingAnimal )
			assert.AreEqual( animals.Count, 1 );
		if ( hunter )
		{
			var hunterTask = hunter.FindTaskInQueue<Workshop.GetResource>();
			assert.IsNotNull( hunterTask );
			assert.AreEqual( node, hunterTask.resource.node );
		}
		if ( node )
			assert.IsTrue( node.resources.Contains( this ) );
	}
}
