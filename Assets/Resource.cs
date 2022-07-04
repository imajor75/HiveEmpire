using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

[SelectionBase]
public class Resource : HiveObject
{
	public Node node;
	public Type type;
	public World.Timer life = new World.Timer();
	public int charges = 1;
	public float strength = 1;
	public bool infinite;
	public int bodyRandom;	// Just a random number. We cannot generate a random number in Start otherwise CRC would break
	public World.Timer gathered = new World.Timer();
	public World.Timer keepAway = new World.Timer();
	public World.Timer spawn = new World.Timer();
	public Unit hunter;
	public List<Unit> animals = new List<Unit>();
	public Unit origin;		// Only valid for a prey, references the bunny
	public World.Timer silence = new World.Timer();

	static Material bark, leaves;
	GameObject body;
	static public MediaTable<AudioClip, Type> ambientSounds;
	AudioSource soundSource;
	static public MediaTable<GameObject, Type> meshes;
	public Node.Block block
	{
		get
		{
			return new Node.Block( type switch
			{
				Type.tree => Node.Block.Type.unitsAndBuildings,
				Type.rock => Node.Block.Type.unitsAndBuildings,
				Type.fish => Node.Block.Type.none,
				Type.cornfield => Node.Block.Type.buildings,
				Type.animalSpawner => Node.Block.Type.all,
				Type.pasturingAnimal => Node.Block.Type.buildings,
				Type.salt => Node.Block.Type.none,
				Type.coal => Node.Block.Type.none,
				Type.iron => Node.Block.Type.none,
				Type.gold => Node.Block.Type.none,
				Type.stone => Node.Block.Type.none,
				Type.expose => Node.Block.Type.none,
				_ => Node.Block.Type.none
			} ); 
		}
	}
	public bool underGround
	{
		get
		{
			return IsUnderGround( type );
		}
		[Obsolete( "Compatibility with old files", true )]
		set {}
	}

	[Obsolete( "Compatibility with old files", true )]
	World.Timer exposed { set {} }

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
		apple,
		total,
		unknown = -1	
	}

	public enum BlockHandling
	{
		block,
		ignore,
		remove
	}

	public static void RemoveFromGround( Node node )
	{
		List<Resource> toRemove = new List<Resource>();
		foreach ( var resource in node.resources )
		{
			if ( resource.type == Resource.Type.tree || resource.type == Resource.Type.rock || resource.type == Resource.Type.cornfield )
				toRemove.Add( resource );
		}
		foreach ( var resource in toRemove )
			resource.Remove();
	}

	public static void Initialize()
	{
		object[] meshes = {
		"prefabs/trees/Tree0", Type.tree,
		"prefabs/trees/Tree1", Type.tree,
		"prefabs/trees/Tree2", Type.tree,

		"prefabs/rocks/rock01", Type.rock,
		"prefabs/rocks/rock02", Type.rock,
		"prefabs/rocks/rock03", Type.rock,
		"prefabs/rocks/rock04", Type.rock,

		"prefabs/others/cave", Type.animalSpawner,
		"prefabs/others/field", Type.cornfield,
		null, Type.apple };
		Resource.meshes.Fill( meshes );

		object[] sounds = {
			"bird1", Constants.Resource.treeSoundTime, Type.tree,
			"bird2", Constants.Resource.treeSoundTime, Type.tree,
			"bird3", Constants.Resource.treeSoundTime, Type.tree,
			"bird4", Constants.Resource.treeSoundTime, Type.tree };
		ambientSounds.fileNamePrefix = "effects/";
		ambientSounds.Fill( sounds );

		bark = Resources.Load<Material>( "treeBark" );
		leaves = Resources.Load<Material>( "treeLeaf" );
	}

	static public Resource Create()
	{
		return new GameObject().AddComponent<Resource>();
	}

	public static bool IsUnderGround( Type type )
	{
		return type == Type.coal || type == Type.iron || type == Type.stone || type == Type.gold || type == Type.salt;
	}

	public Resource Setup( Node node, Type type, int charges = -1, float strength = 1, bool allowBlocking = false )
	{
		this.type = type;
		this.strength = strength;
		if ( charges < 1 )
		{
			if ( underGround || type == Type.fish || type == Type.apple )
				charges = int.MaxValue;
			else
			{
				if ( type == Type.rock )
					charges = Constants.Resource.rockCharges;
				else
					charges = 1;
			}
		}

		Node.Type needed = Node.Type.aboveWater;
		if ( underGround )
			needed = Node.Type.high;
		if ( type == Type.cornfield )
			needed = Node.Type.grass;
		if ( type == Type.tree )
			needed = Node.Type.land;

		if ( !node.CheckType( needed ) )
		{
			base.Remove();
			return null;
		}

		if ( !allowBlocking && node.block )
		{
			base.Remove();
			return null;
		}

		node.resources.Add( this );
		if ( charges != int.MaxValue )
		{
			this.charges = charges;
			this.infinite = false;
		}
		else
			this.infinite = true;
		this.node = node;
		life.Start();
		bodyRandom = World.NextRnd( OperationHandler.Event.CodeLocation.resourceSetup );

		if ( type == Type.cornfield )
		{
			node.avoidGrass = true;
			ground.SetDirty( node );
		}
		if ( type == Type.tree )
			Create().Setup( node, Type.apple, allowBlocking:true );

		base.Setup();

		return this;
	}

	public Resource SetupAsPrey( Unit animal )
	{
		if ( Setup( animal.node, Type.pasturingAnimal ) == null )
			return null;
		origin = animal;

		return this;
	}

    // Start is called before the first frame update
    new public void Start()
    {
		ground.Link( this );
		transform.localPosition = node.position;

		name = type.ToString();
		GameObject prefab = meshes.GetMediaData( type, bodyRandom );
		if ( prefab )
		{
			body = Instantiate( prefab );
			World.SetLayerRecursive( body, World.layerIndexResources );
			Tree treeCreator;
			if ( body.TryGetComponent<Tree>( out treeCreator ) )
			{
				Destroy( treeCreator );
				var renderer = body.GetComponent<MeshRenderer>();
				var materials = new Material[2];
				materials[0] = bark;
				materials[1] = leaves;			
				renderer.materials = materials;
			}
		}
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
				foreach ( Transform c in body.transform )
				{
					float h = ground.GetHeightAt( c.position.x, c.position.z ) - node.height;
					c.position = c.position + Vector3.up * h;
				}
			}
		}

		soundSource = World.CreateSoundSource( this );
		base.Start();
	}

	new public void Update()
	{
		if ( type == Type.cornfield )
		{
			float growth = (float)life.age / Constants.Resource.cornfieldGrowthTime;
			if ( node.type != Node.Type.grass )
				growth /= 2;
			if ( growth > 1 )
				growth = 1;
			var p = node.position;
			transform.localPosition = node.position + Vector3.up * ( -0.4f + 0.4f * growth );
		}
		if ( type == Type.tree )
		{
			float size = (float)life.age / Constants.Resource.treeGrowthTime;
			if ( node.type != Node.Type.forest )
				size /= 2;
			size = Math.Max( size, 0.1f );
			size = Math.Min( size, 1 );
			transform.localScale = Vector3.one * size;
		}
		base.Update();
	}

	public override void GameLogicUpdate()
	{
		if ( type == Type.animalSpawner && ( spawn.done || spawn.empty ) )
		{
			foreach ( var o in Ground.areas[1] )
			{
				if ( !o )
					continue;
				Node n = node.Add( o );
				if ( n.block.IsBlocking( Node.Block.Type.units ) )
					continue;
				if ( animals.Count >= 3 )
					continue;
				var animal = Unit.Create().SetupAsAnimal( this, n );
				if ( animal != null )
				{
					animals.Add( animal );
					break;
				}
				else
					assert.Fail();
			}
			spawn.Start( Constants.Resource.animalSpawnTime );
		}
		if ( silence.done || silence.empty )
		{
			var m = ambientSounds.GetMedia( type, World.NextRnd( OperationHandler.Event.CodeLocation.resourceSound ) );
			if ( m == null || m.data == null )
			{
				silence.Start( 1500 );
				assert.AreNotEqual( type, Type.tree );
			}
			else if ( soundSource )
			{
				if ( !silence.empty )
					soundSource.Play();
				silence.Start( (int)( World.NextFloatRnd( OperationHandler.Event.CodeLocation.resourceSilence ) * m.intData ) );
				soundSource.clip = m.data;
				soundSource.loop = false;
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
			Type.apple => Item.Type.apple,
			_ => Item.Type.unknown,
		};
	}

	public override void Remove()
	{
		if ( destroyed )
			return;
		destroyed = true;
		
		if ( type == Type.tree )
		{
			foreach ( var r in node.resources )
			{
				if ( r.type == Type.apple )
				{
					r.Remove();
					break;
				}
			}
		}
		RemoveElements( animals );
		if ( origin )
			origin.Remove();

		assert.IsTrue( node.resources.Contains( this ) );
		node.avoidGrass = false;
		if ( type == Type.cornfield )
			ground.SetDirty( node );
		node.resources.Remove( this );
		base.Remove();
	}

	public bool IsReadyToBeHarvested()
	{
		if ( !keepAway.done && !keepAway.empty )
			return false;
		if ( type == Type.tree )
		{
			if ( node.type == Node.Type.forest )
				return life.age > Constants.Resource.treeGrowthTime;
			return life.age > Constants.Resource.treeGrowthTime * 2;
		}
		if ( type == Type.cornfield )
		{
			if ( node.type == Node.Type.grass )
				return life.age > Constants.Resource.cornfieldGrowthTime;
			return life.age > Constants.Resource.cornfieldGrowthTime * 2;
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
			Remove();
	}

	public override Node location { get { return node; } }

	public override void Validate( bool chain )
	{
		if ( type != Type.animalSpawner )
			assert.AreEqual( animals.Count, 0 );
		if ( type == Type.pasturingAnimal )
			assert.IsNotNull( origin );
		else
			assert.IsNull( origin );
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
