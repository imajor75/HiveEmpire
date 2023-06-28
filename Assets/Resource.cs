using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

[SelectionBase]
public class Resource : VisibleHiveObject
{
	public Node node;
	public Type type;
	public Game.Timer life = new ();
	public Game.Timer apple = new ();
	public Game.Timer scaleUpdate = new ();
	public int charges = 1;
	public bool infinite;
	public int bodyRandom;	// Just a random number. We cannot generate a random number in Start otherwise CRC would break
	public Game.Timer gathered = new ();
	public Game.Timer keepAway = new ();
	public Game.Timer spawn = new ();
	public Unit hunter;
	public List<Unit> animals = new ();
	public Unit origin;		// Only valid for a prey, references the bunny
	public Game.Timer silence = new ();
	static public MediaTable<Sprite, Type> functionalSprites, sprites;

	override public string textId => base.textId + $" ({type})";

	static Material bark, leaves;
	GameObject body;
	static public MediaTable<AudioClip, Type> ambientSounds;
	AudioSource soundSource;
	static public MediaTable<GameObject, Type> meshes;
	override public World.UpdateStage updateMode => World.UpdateStage.turtle;
	public Node.Block block
	{
		get
		{
			return new Node.Block( type switch
			{
				Type.tree => Node.Block.Type.unitsAndBuildings,
				Type.rock => Node.Block.Type.unitsAndBuildings,
				Type.fish => Node.Block.Type.none,
				Type.cornField => Node.Block.Type.buildings,
				Type.wheatField => Node.Block.Type.buildings,
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
	Game.Timer exposed { set {} }
	[Obsolete( "Compatibility with old files", true )]
	float strength { set {} }

	public enum Type
	{
		tree,
		rock,
		fish,
		cornField,
		wheatField,
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
		silver,
		copper,
		dung,
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
		List<Resource> toRemove = new ();
		foreach ( var resource in node.resources )
		{
			if ( resource.type == Resource.Type.tree || resource.type == Resource.Type.rock || resource.type == Resource.Type.cornField || resource.type == Resource.Type.wheatField )
				toRemove.Add( resource );
		}
		foreach ( var resource in toRemove )
			resource.Remove();
	}

	public void StartRest()
	{
		if ( underGround )
			keepAway.Start( (int)( Constants.Workshop.mineOreRestTime ) );
		if ( type == Type.fish )
			keepAway.Start( Constants.Workshop.fishRestTime );
		if ( type == Type.dung )
			keepAway.Start( Constants.Workshop.dungRestTime );
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

		"prefabs/others/apples", Type.apple,

		"prefabs/others/cave", Type.animalSpawner,
		"prefabs/others/field", Type.cornField,
		"prefabs/others/wheatField", Type.wheatField,
		null, Type.apple };
		Resource.meshes.Fill( meshes, false );

		object[] mapSpriteData  = {
			"textures/tree_from_above", Type.tree };
		functionalSprites.Fill( mapSpriteData, false );

		object[] spriteData = {
		"sprites/other/tree00", Type.tree,
		"sprites/other/rockInGrass00", Type.rock,
		"sprites/other/rockInGrass01", Type.rock,
		"sprites/other/rockInGrass02", Type.rock };
		sprites.Fill( spriteData );
		sprites.fileNamePrefix = "sprites/other/";

		object[] sounds = {
			"bird1", Constants.Resource.treeSoundTime, Type.tree,
			"bird2", Constants.Resource.treeSoundTime, Type.tree,
			"bird3", Constants.Resource.treeSoundTime, Type.tree,
			"bird4", Constants.Resource.treeSoundTime, Type.tree };
		ambientSounds.fileNamePrefix = "soundEffects/";
		ambientSounds.Fill( sounds, false );

		bark = Resources.Load<Material>( "treeBark" );
		leaves = Resources.Load<Material>( "treeLeaf" );
	}

	static public Resource Create()
	{
		return new GameObject().AddComponent<Resource>();
	}

	public static bool IsUnderGround( Type type )
	{
		return type == Type.coal || type == Type.iron || type == Type.stone || type == Type.gold || type == Type.salt || type == Type.copper || type == Type.silver;
	}

	public Resource Setup( Node node, Type type, int charges = 1, bool allowBlocking = false )
	{
		this.type = type;

		Node.Type needed = Node.Type.aboveWater;
		if ( underGround )
			needed = Node.Type.high;
		if ( type == Type.cornField || type == Type.wheatField )
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

		bodyRandom = Interface.rnd.Next();

		if ( type == Type.cornField || type == Type.wheatField )
		{
			node.avoidGrass = true;
			ground.SetDirty( node );
		}

		base.Setup( node.world );

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
		world.ground.Link( this );
		transform.localPosition = node.position;

		name = type.ToString();
		GameObject prefab = meshes.GetMediaData( type, bodyRandom );
		if ( prefab )
		{
			body = Instantiate( prefab );
			if ( type == Type.tree )
				World.SetLayerRecursive( body, LayerMask.NameToLayer( "Trees" ) );
			else
				World.SetLayerRecursive( body, Constants.World.layerIndexResources );
			Tree treeCreator;
			if ( body.TryGetComponent<Tree>( out treeCreator ) )
			{
				Eradicate( treeCreator );
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
			body.transform.SetParent( transform, false );
			body.transform.localPosition = Vector3.zero;

			// Align field to ground
			if ( type == Type.cornField || type == Type.wheatField )
			{
				foreach ( Transform c in body.transform )
				{
					float h = ground.GetHeightAt( c.position.x, c.position.z ) - node.height;
					c.position += Vector3.up * h;
				}
			}
			if ( type == Type.apple )
			{
				// Change the height of the individual apples to nicely lay on the ground
				foreach ( Transform apple in body.transform )
				{
					var positionInGround = world.ground.transform.InverseTransformPoint( apple.position );
					positionInGround.y = world.ground.GetHeightAt( positionInGround.x, positionInGround.z );
					apple.position = world.ground.transform.TransformPoint( positionInGround );
				}
			}
		}

		base.Start();
	}

	override public Sprite GetVisualSprite( VisualType visualType )
	{
		return visualType switch
		{
			VisualType.nice2D => sprites.GetMediaData( type, bodyRandom ),
			VisualType.functional => functionalSprites.GetMediaData( type, bodyRandom ),
			_ => null
		};
	}

	override public GameObject CreateVisual( VisualType visualType )
	{
		var sprite = base.CreateVisual( visualType );
		if ( visualType != VisualType.functional && type == Type.tree )
		{
			var spriteMaterial = sprite.GetComponent<SpriteRenderer>().material;
			spriteMaterial.SetFloat( "_Peek", 1 );
		}
		return sprite;
	}


	new public void Update()
	{
		if ( scaleUpdate.inProgress )
		{
			base.Update();
			return;
		}

		// TODO Adjusting the local scale as the plant grows is slow, we could update that only once a second or so
		if ( type == Type.cornField || type == Type.wheatField )
		{
			float growth = (float)life.age;
			if ( type == Type.cornField )
				growth /= Constants.Resource.cornFieldGrowthTime;
			else
				growth /= Constants.Resource.wheatFieldGrowthTime;

			if ( growth > 1 )
				growth = 1;
			foreach ( Transform plant in body.transform )
				plant.localScale = new Vector3( growth, growth, growth );
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
		scaleUpdate.Start( Constants.Resource.scaleUpdatePeriod );
		base.Update();
	}

	public override void GameLogicUpdate( UpdateStage stage )
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
			var m = ambientSounds.GetMedia( type, game.NextRnd( OperationHandler.Event.CodeLocation.resourceSound ) );
			if ( m == null || m.data == null )
			{
				silence.Start( 1500 );
				assert.AreNotEqual( type, Type.tree );
			}
			else
			{
				if ( !soundSource )
					soundSource = World.CreateSoundSource( this );

				soundSource.clip = m.data;
				soundSource.loop = false;
				
				if ( !silence.empty )
					soundSource.Play();

				silence.Start( (int)( game.NextFloatRnd( OperationHandler.Event.CodeLocation.resourceSilence ) * m.intData ) );
			}
		}
		if ( type == Type.tree && life.age > Constants.Resource.treeGrowthTime && !apple.inProgress )
		{
			bool hasApple = false;
			foreach ( var resource in node.resources )
				if ( resource.type == Type.apple )
					hasApple = true;
			if ( !hasApple )
				Create().Setup( node, Type.apple, world.appleFactor, true );
			apple.Start( Constants.Workshop.appleGrowTime );
		}
	}

	static public Item.Type ItemType( Type type )
	{
		return type switch
		{
			Type.tree => Item.Type.log,
			Type.rock => Item.Type.stone,
			Type.fish => Item.Type.fish,
			Type.cornField => Item.Type.corn,
			Type.wheatField => Item.Type.grain,
			Type.pasturingAnimal => Item.Type.hide,
			Type.salt => Item.Type.salt,
			Type.iron => Item.Type.iron,
			Type.copper => Item.Type.copper,
			Type.gold => Item.Type.gold,
			Type.silver => Item.Type.silver,
			Type.coal => Item.Type.coal,
			Type.stone => Item.Type.stone,
			Type.apple => Item.Type.apple,
			Type.dung => Item.Type.dung,
			_ => Item.Type.unknown,
		};
	}

	public override void Remove()
	{
		if ( destroyed )
			return;
		destroyed = true;
		
		RemoveElements( animals );
		if ( origin )
			origin.Remove();

		assert.IsTrue( node.resources.Contains( this ) );
		node.avoidGrass = false;
		if ( type == Type.cornField || type == Type.wheatField )
			ground.SetDirty( node );
		node.resources.Remove( this );
		base.Remove();
	}

	public bool IsReadyToBeHarvested()
	{
		if ( !keepAway.done && !keepAway.empty )
			return false;
		if ( charges <= 0 && !infinite )
			return false;
		if ( type == Type.tree )
		{
			if ( node.type == Node.Type.forest )
				return life.age > Constants.Resource.treeGrowthTime;
			return life.age > Constants.Resource.treeGrowthTime * 2;
		}
		if ( type == Type.cornField )
			return life.age > Constants.Resource.cornFieldGrowthTime;
		if ( type == Type.wheatField )
			return life.age > Constants.Resource.wheatFieldGrowthTime;
		return true;
	}

	public void UpdateBody()
	{
		transform.localPosition = node.position;
	}

	public override void Reset()
	{
		keepAway.Start();
		if ( type == Type.cornField || type == Type.wheatField )
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
		assert.IsTrue( charges > 0 || infinite );
		base.Validate( chain );
	}
}
