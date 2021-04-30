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
	public World.Timer exposed;
	public int charges = 1;
	GameObject body;
	public World.Timer keepAway;
	public World.Timer spawn;
	public Worker hunter;
	public List<Worker> animals = new List<Worker>();
	public static int treeGrowthMax = 15000;    // 5 minutes
	public static int cornfieldGrowthMax = 8000;
	public static int exposeMax = 50000;
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
		soil,
		total,
		unknown = -1	
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

		"Ores/coaltable_final" , Type.coal,
		"Ores/goldtable_final" , Type.gold,
		"Ores/irontable_final" , Type.iron,
		"Ores/salttable_final" , Type.salt,
		"Ores/stonetable_final" , Type.stone,
		"AnimalCave/animRock(Clone)", Type.animalSpawner,
		"grainfield_final", Type.cornfield };
		Resource.meshes.Fill( meshes );

		object[] sounds = {
			"bird1", 3000, Type.tree,
			"bird2", 3000, Type.tree,
			"bird3", 3000, Type.tree,
			"bird4", 3000, Type.tree };
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
			DestroyThis();
			return null;
		}

		if ( node.building || node.flag || node.resource )
		{
			DestroyThis();
			return null;
		}

		node.resource = this;
		this.type = type;
		this.charges = charges;
		this.node = node;
		life.Start();
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
    public void Start()
    {
		transform.SetParent( World.resources.transform );
		transform.localPosition = node.position;

		name = type.ToString();
		GameObject prefab = meshes.GetMediaData( type );
		if ( prefab )
			body = Instantiate( prefab );
		if ( type == Type.pasturingAnimal )
			name = "Pasturing Animal Resource";
		if ( body != null )
		{
			if ( type == Type.tree || type == Type.rock )
				body.transform.Rotate( Vector3.up * World.rnd.Next( 360 ) );
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
		if ( underGround )
			body?.SetActive( !exposed.done && !exposed.empty );
		if ( type == Type.animalSpawner && ( spawn.done || spawn.empty ) )
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
					assert.Fail();
			}
			spawn.Start( 1000 );
		}
		if ( silence.done )
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
		assert.AreEqual( this, node.resource );
		node.resource = null;
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

	public override GroundNode Node { get { return node; } }

	public override void Validate( bool chain )
	{
		if ( type == Type.animalSpawner )
			foreach ( var w in animals )
				assert.IsNotNull( w );
		if ( type == Type.pasturingAnimal )
			assert.AreEqual( animals.Count, 1 );
	}
}
