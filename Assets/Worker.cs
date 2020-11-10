using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;

[SelectionBase]
public class Worker : Assert.Base
{
	public Type type;
	public Ground ground;
	public Player owner;
	public GroundNode walkFrom;
	public GroundNode walkTo;
	public Road walkBase;
	public int walkBlock;
	public bool walkBackward;
	public float walkProgress;
	public GroundNode node;
	public Item itemInHands;
	public Type look;
	public Resource origin;
	public float currentSpeed;
	public Flag exclusiveFlag;
	public int itemsDelivered;
	static public MediaTable<AudioClip, Resource.Type> resourceGetSounds;
	static public MediaTable<AudioClip, Type> walkSounds;
	[JsonIgnore]
	public AudioSource soundSource;
	[JsonIgnore]
	public GameObject mapObject;
	Material mapMaterial;
	GameObject arrowObject;
	SpriteRenderer itemOnMap;
	static Sprite arrowSprite;
	Material shirtMaterial;
	[JsonIgnore]
	public bool debugReset;
	static public int stuckTimeout = 3000;


	static MediaTable<GameObject, Type> looks;

	public Road road;
	public bool onRoad;

	public Building building;

	Animator animator;
	static public List<GameObject> templates = new List<GameObject>();
	static public RuntimeAnimatorController animationController;
	static public int walkingID, pickupID, putdownID;

	public List<Task> taskQueue = new List<Task>();
	GameObject body;
	GameObject box;
	GameObject[] wheels = new GameObject[2];
	MeshRenderer itemTable;
	static GameObject boxTemplateBoy;
	static GameObject boxTemplateMan;

	public class Task : ScriptableObject
	{
		public Worker boss;

		public Task Setup( Worker boss )
		{
			this.boss = boss;
			return this;
		}
		public virtual void Prepare() { }
		public virtual bool ExecuteFrame() { return false; }
		public virtual void Cancel() { }
		public void ReplaceThisWith( Task another )
		{
			boss.assert.AreEqual( this, boss.taskQueue[0] );
			boss.taskQueue.RemoveAt( 0 );
			boss.taskQueue.Insert( 0, another );
		}
		public void AddTask( Task newTask, bool asFirst = false )
		{
			if ( asFirst )
				boss.taskQueue.Insert( 0, newTask );
			else
				boss.taskQueue.Add( newTask );
		}

		public bool ResetBoss()
		{
			boss.Reset();
			return true;
		}

		public virtual void Validate()
		{
			boss.assert.IsTrue( boss.taskQueue.Contains( this ) );
		}
	}

	public class Callback : Task
	{
		public interface IHandler
		{
			void Callback( Worker worker );
		}
		public IHandler handler;

		public void Setup( Worker boss, IHandler handler )
		{
			base.Setup( boss );
			this.handler = handler;
		}

		public override bool ExecuteFrame()
		{
			handler.Callback( boss );
			return true;
		}
	}

	public class WalkToFlag : Task
	{
		public Flag target;
		public Path path;
		public bool exclusive;

		public void Setup( Worker boss, Flag target, bool exclusive = false )
		{
			base.Setup( boss );
			this.target = target;
			this.exclusive = exclusive;
		}
		public override bool ExecuteFrame()
		{
			if ( boss.node.flag == target )
				return true;

			if ( path == null || path.Road == null )
			{
				path = Path.Between( boss.node, target.node, PathFinder.Mode.onRoad, boss );
				if ( path == null )
				{
					var instance = CreateInstance<WalkToNode>();
					instance.Setup( boss, target.node );
					ReplaceThisWith( instance );
					return false;
				}
			}
			int point = 0;
			Road road = path.NextRoad();
			if ( boss.node.flag == road.GetEnd( 0 ) )
				point = road.nodes.Count - 1;
			if ( exclusive )
			{
				if ( boss.road )
				{
					int index = boss.road.NodeIndex( boss.node );
					boss.assert.IsTrue( index >= 0 );
					boss.assert.AreEqual( boss.road.workerAtNodes[index], boss );
					boss.road.workerAtNodes[index] = null;
				}
				boss.assert.AreEqual( boss.node.flag.user, boss );
				road.workerAtNodes[road.NodeIndex( boss.node )] = boss;
				boss.road = road;
			}
			boss.ScheduleWalkToRoadPoint( road, point, exclusive, true );
			return false;
		}

		public override void Validate()
		{
			base.Validate();
		}
	}

	public class WalkToNode : Task
	{
		public GroundNode target;
		public Path path;
		public bool ignoreFinalObstacle;

		public void Setup( Worker boss, GroundNode target, bool ignoreFinalObstacle = false )
		{
			base.Setup( boss );
			this.target = target;
			this.ignoreFinalObstacle = ignoreFinalObstacle;
		}
		public override bool ExecuteFrame()
		{
			if ( boss.node == target )
				return true;

			if ( path == null )
			{
				path = Path.Between( boss.node, target, PathFinder.Mode.avoidObjects, boss, ignoreFinalObstacle );
				if ( path == null )
				{
					Debug.Log( "Worker failed to go to " + target.x + ", " + target.y );
					return true;
				}
			}
			boss.Walk( path.NextNode() );
			return false;
		}
	}

	public class WalkToRoadPoint : Task
	{
		// TODO Select the end closer to the starting point
		public Road road;
		public int currentPoint = -1;
		public int targetPoint;
		public int wishedPoint = -1;
		public bool exclusive;
		public World.Timer stuck;

		public void Setup( Worker boss, Road road, int point, bool exclusive )
		{
			boss.assert.IsTrue( point >= 0 && point < road.nodes.Count, "Invalid road point (" + point + ", " + road.nodes.Count + ")" );
			base.Setup( boss );
			this.road = road;
			this.exclusive = exclusive;
			targetPoint = point;
		}

		public override bool ExecuteFrame()
		{
			if ( road == null )
				return ResetBoss();

			if ( stuck.Done && boss.type == Type.hauler && road.ActiveWorkerCount > 1 )
			{
				boss.Remove();
				return true;
			}
			if ( currentPoint == -1 )
				currentPoint = road.NodeIndex( boss.node );
			if ( currentPoint == -1 )
				return true;    // Task failed
			boss.assert.IsTrue( currentPoint >= 0 && currentPoint < road.nodes.Count );
			if ( currentPoint == targetPoint )
				return true;
			else
				NextStep();
			return false;
		}

		public bool NextStep( bool ignoreOtherWorkers = false )
		{
			boss.assert.IsTrue( targetPoint >= 0 && targetPoint < road.nodes.Count );
			if ( exclusive )
				boss.assert.AreEqual( road.workerAtNodes[currentPoint], boss );
			boss.assert.IsNull( boss.walkTo );

			if ( currentPoint == targetPoint )
				return false;

			int nextPoint;
			if ( currentPoint < targetPoint )
				nextPoint = currentPoint + 1;
			else
				nextPoint = currentPoint - 1;
			wishedPoint = nextPoint;

			if ( exclusive && !ignoreOtherWorkers )
			{
				Worker other = road.workerAtNodes[nextPoint];
				Flag flag = road.nodes[nextPoint].flag;
				if ( flag )
				{
					boss.assert.IsTrue( other == null || other == flag.user );
					other = flag.user;
				}
				other?.assert?.IsNotSelected();
				if ( other && !other.Call( road, currentPoint ) )
				{
					// As a last resort to make space is to simply remove the other hauler
					if ( other.onRoad && other.type == Type.hauler && other.road != road && other.road.ActiveWorkerCount > 1 && other.IsIdle() )
						other.Remove();
					else
					{
						if ( stuck.Empty )
							stuck.Start( stuckTimeout );
						return false;
					}
				}
			}

			wishedPoint = -1;
			boss.assert.IsTrue( currentPoint >= 0 && currentPoint < road.workerAtNodes.Count );	// TODO Triggered, happens when a road starts and ends at the same flag
			if ( road.workerAtNodes[currentPoint] == boss ) // it is possible that the other worker already took the place, so it must be checked
				road.workerAtNodes[currentPoint] = null;

			boss.assert.AreEqual( boss.node, road.nodes[currentPoint] );
			boss.Walk( road.nodes[nextPoint] );
			boss.walkBase = road;
			if ( nextPoint > currentPoint )
			{
				boss.walkBlock = currentPoint;
				boss.walkBackward = false;
			}
			else
			{
				boss.walkBlock = nextPoint;
				boss.walkBackward = true;
			}
			currentPoint = nextPoint;
			if ( exclusive )
			{
				road.workerAtNodes[currentPoint] = boss;
				if ( boss.walkTo.flag )
				{
					if ( !ignoreOtherWorkers )
						boss.assert.IsNull( boss.walkTo.flag.user, "Worker still in way at flag." );
					boss.walkTo.flag.user = boss;
					boss.exclusiveFlag = boss.walkTo.flag;
				}
				if ( boss.walkFrom.flag )
				{
					if ( boss.walkFrom.flag.user == boss )
						boss.walkFrom.flag.user = null;
					boss.assert.AreEqual( boss.walkFrom.flag, boss.exclusiveFlag );
					boss.exclusiveFlag = null;
				}
			}
			stuck.Reset();	
			return true;
		}

		public override void Validate()
		{
			base.Validate();
			if ( this != boss.taskQueue[0] )
				return;

			boss.assert.IsTrue( currentPoint >= -1 && currentPoint < road.nodes.Count );
			int cp = road.NodeIndex( boss.node );
			if ( exclusive )
			{
				int t = 0;
				for ( int i = 0; i < road.workerAtNodes.Count; i++ )
				{
					if ( road.workerAtNodes[i] == boss )
					{
						t++;
						boss.assert.AreEqual( i, cp );
					}
				}
				boss.assert.AreEqual( t, 1 );
			}

			boss.assert.IsTrue( targetPoint >= 0 && targetPoint < road.nodes.Count );
			if ( wishedPoint >= 0 )
			{
				boss.assert.IsTrue( wishedPoint <= road.nodes.Count );
				boss.assert.AreEqual( Math.Abs( wishedPoint - cp ), 1 );
			}
		}
	}

	public class WalkToNeighbour : Task
	{
		public GroundNode target;
		public void Setup( Worker boss, GroundNode target )
		{
			base.Setup( boss );
			this.target = target;
		}

		public override bool ExecuteFrame()
		{
			if ( boss.node.DirectionTo( target ) >= 0 ) // This is not true, when previous walk tasks are failed
				boss.Walk( target );
			return true;
		}
	}

	public class PickupItem : Task
	{
		static public int pickupTimeStart = 60;
		public Item item;
		public Path path;
		public World.Timer timer;

		public void Setup( Worker boss, Item item )
		{
			base.Setup( boss );
			path = item.path;
			this.item = item;

		}
		public override void Cancel()
		{
			boss.assert.AreEqual( boss, item.worker );
			item.worker = null;
			base.Cancel();
		}
		public override bool ExecuteFrame()
		{
			boss.assert.AreEqual( item.worker, boss );
			if ( item.buddy )
				boss.assert.AreEqual( item.buddy.worker, boss );
			if ( boss.type == Type.hauler )
				boss.assert.IsNull( boss.itemInHands );
			if ( timer.Empty )
			{
				timer.Start( pickupTimeStart );
				boss.animator?.ResetTrigger( putdownID );
				boss.animator?.SetTrigger( pickupID );   // TODO Animation phase is not saved in file
				if ( boss.itemTable )
					boss.itemTable.material = Item.materials[(int)item.type];
				boss.box?.SetActive( true );
			}
			if ( !timer.Done )
				return false;

			if ( path != item.path )
			{
				// This block can run for tinkerers too, if the item lost destination before the tinkerer would pick it up
				if ( boss.type == Type.hauler )
					boss.assert.AreEqual( boss.road, path.Road );
				return ResetBoss();
			}

			item.flag?.ReleaseItem( item );
			boss.itemInHands = item;
			boss.assert.IsTrue( item.worker == boss || item.worker == null );
			if ( item.worker.type == Type.hauler )
			{
				if ( item.path.IsFinished )
					boss.assert.AreEqual( item.destination.flag.node, boss.node );
				else
					item.path.NextRoad();
			}
			return true;
		}
	}

	public class DeliverItem : Task
	{
		static public int putdownTimeStart = 60;
		public Item item;
		public 
			World.Timer timer;

		public void Setup( Worker boss, Item item )
		{
			base.Setup( boss );
			this.item = item;
		}
		public override void Cancel()
		{
			if ( item != null && item.nextFlag != null )
			{
				if ( item.buddy?.worker )
				{
					// If two items are swapped, the second one has no assigned PickupItem task, the DeliverItem task will handle
					// the pickup related tasks. So the cancel of this DeliverItem task needs to cancel the same things the PickupTask.Cancel would do.
					boss.assert.AreEqual( item.buddy.worker, boss );
					item.buddy.worker = null;
				}
				item.nextFlag.CancelItem( item );
			}
			base.Cancel();
		}
		public override bool ExecuteFrame()
		{
			if ( timer.Empty )
			{
				timer.Start( putdownTimeStart );
				if ( item.buddy )
				{
					if ( boss.itemTable )
						boss.itemTable.material = Item.materials[(int)item.buddy.type];
					timer.reference -= 30;
				}
				else
				{
					boss.animator?.ResetTrigger( pickupID );
					boss.animator?.SetTrigger( putdownID );
				}
			}
			if ( !timer.Done )
				return false;

			boss.itemsDelivered++;
			boss.box?.SetActive( item.buddy != null );
			boss.assert.AreEqual( item, boss.itemInHands );
			if ( item.destination?.node == boss.node )
			{
				item.Arrived();
				boss.itemInHands = null;
			}
			else
			{
				if ( item.nextFlag && boss.node.flag == item.nextFlag )
				{
					Item change = boss.itemInHands = item.ArrivedAt( item.nextFlag );
					if ( change )
					{
						if ( boss.road && change.Road == boss.road )
							change.path.NextRoad();
						else
							change.CancelTrip();
					}
				}
				else
					return ResetBoss(); // This happens when the previous walk tasks failed, and the worker couldn't reach the target
			}

			return true;
		}
	}

	public class StartWorkingOnRoad : Task
	{
		public Road road;

		public void Setup( Worker boss, Road road )
		{
			base.Setup( boss );
			this.road = road;
		}

		public override bool ExecuteFrame()
		{
			if ( road == null )
			{
				boss.Remove();
				return true;    // Task failed
			}
			int i = road.NodeIndex( boss.node );
			if ( i < 0 || boss.node.flag != null )
				return true;    // Task failed
			boss.assert.IsFalse( boss.onRoad );
			if ( road.workerAtNodes[i] == null )
			{
				road.workerAtNodes[i] = boss;
				boss.onRoad = true;
				if ( boss.shirtMaterial )
					boss.shirtMaterial.color = Color.yellow;
				return true;
			}
			return false;
		}
	}

	public class Wait : Task
	{
		public int time;
		public World.Timer timer;

		public void Setup( Worker boss, int time )
		{
			base.Setup( boss );
			this.time = time;
		}

		public override bool ExecuteFrame()
		{
			if ( timer.Empty )
				timer.Start( time );

			return !timer.Done;
		}
	}

	public enum Type
	{
		hauler,
		tinkerer,
		constructor,
		soldier,
		wildAnimal,
		unemployed,
		cart
	}

	public static void Initialize()
	{
		object[] lookData = {
		"Polytope Studio/Lowpoly Medieval Characters/Prefabs/PT_Medieval_Female_Peasant_01_a", Type.constructor,
		"Polytope Studio/Lowpoly Medieval Characters/Prefabs/PT_Medieval_Male_Peasant_01_a", Type.tinkerer,
		"Polytope Studio/Lowpoly Medieval Characters/Prefabs/PT_Medieval_Boy_Peasant_01_a", Type.hauler,
		"FootmanPBRHPPolyart/Prefabs/footman_Blue_HP", Type.soldier,
		"Rabbits/Prefabs/Rabbit 1", Type.wildAnimal,
		"Medieval village/Cart/PREFABs/cartRoot", Type.cart };

		looks.Fill( lookData );

		boxTemplateBoy = (GameObject)Resources.Load( "Tresure_box/tresure_box_inhands_boy" );
		Assert.global.IsNotNull( boxTemplateBoy );
		boxTemplateMan = (GameObject)Resources.Load( "Tresure_box/tresure_box_inhands_man" );
		Assert.global.IsNotNull( boxTemplateMan );

		animationController = (RuntimeAnimatorController)Resources.Load( "Crafting Mecanim Animation Pack FREE/Prefabs/Crafter Animation Controller FREE" );
		Assert.global.IsNotNull( animationController );
		walkingID = Animator.StringToHash( "Moving" );
		pickupID = Animator.StringToHash( "CarryPickupTrigger" );
		putdownID = Animator.StringToHash( "CarryPutdownTrigger" );

		object[] sounds = {
			"Mines/pickaxe_deep", Resource.Type.coal, Resource.Type.iron, Resource.Type.gold, Resource.Type.stone, Resource.Type.salt,
			"Forest/treecut", Resource.Type.tree,
			"Mines/pickaxe", Resource.Type.rock };
		resourceGetSounds.Fill( sounds );
		object[] walk = {
			"cart", Type.cart };
		walkSounds.Fill( walk );

		var tex = Resources.Load<Texture2D>( "arrow" );
		arrowSprite = Sprite.Create( tex, new Rect( 0.0f, 0.0f, tex.width, tex.height ), new Vector2( 0.5f, 0.5f ) );
	}

	static public Worker Create()
	{
		GameObject workerBody = new GameObject();
		Worker worker = workerBody.AddComponent<Worker>();
		return worker;
	}

	public Worker SetupForRoad( Road road )
	{
		look = type = Type.hauler;
		name = "Hauler";
		owner = road.owner;
		ground = road.ground;
		Building main = road.owner.mainBuilding;
		node = main.node;
		this.road = road;
		onRoad = false;
		ScheduleWalkToNeighbour( main.flag.node );
		ScheduleWalkToFlag( road.GetEnd( 0 ) ); // TODO Pick the end closest to the main building
		ScheduleWalkToRoadNode( road, road.CenterNode(), false );
		ScheduleStartWorkingOnRoad( road );
		return this;
	}

	public Worker SetupForBuilding( Building building )
	{
		look = type = Type.tinkerer;
		name = "Tinkerer";
		return SetupForBuildingSite( building );
	}

	public Worker SetupForConstruction( Building building )
	{
		look = type = Type.constructor;
		name = "Builder";
		return SetupForBuildingSite( building );
	}

	public Worker SetupAsSoldier( Building building )
	{
		look = type = Type.soldier;
		name = "Soldier";
		return SetupForBuildingSite( building );
	}

	Worker SetupForBuildingSite( Building building )
	{
		ground = building.ground;
		owner = building.owner;
		this.building = building;
		Building main = owner.mainBuilding;
		node = main.node;
		if ( building != main )
		{
			ScheduleWalkToNeighbour( main.flag.node );
			ScheduleWalkToFlag( building.flag );
			ScheduleWalkToNeighbour( building.node );
		}
		return this;
	}

	public Worker SetupAsAnimal( Resource origin, GroundNode node )
	{
		look = type = Type.wildAnimal;
		this.node = node;
		this.origin = origin;
		this.ground = node.ground;
		return this;
	}

	public Worker SetupAsCart( Stock stock )
	{
		look = type = Type.cart;
		building = stock;
		node = stock.node;
		ground = stock.ground;
		owner = stock.owner;
		return this;
	}

	void Start()
	{
		if ( road != null )
			transform.SetParent( road.ground.transform );
		if ( building != null )
			transform.SetParent( building.ground.transform );
		if ( node != null )
			transform.SetParent( node.ground.transform );

		body = Instantiate( looks.GetMediaData( look ), transform );
		Transform hand = World.FindChildRecursive( body.transform, "RightHand" );
		if ( hand != null )
		{
			if ( type == Type.hauler )
				box = Instantiate( boxTemplateBoy, hand );
			else
				box = Instantiate( boxTemplateMan, hand );
			box.SetActive( false );
			itemTable = World.FindChildRecursive( box.transform, "ItemTable" ).GetComponent<MeshRenderer>();
			assert.IsNotNull( itemTable );
		}
		Transform shirt = World.FindChildRecursive( body.transform, "PT_Medieval_Boy_Peasant_01_upper" );
		if ( shirt )
		{
			var skinnedMeshRenderer = shirt.GetComponent<SkinnedMeshRenderer>();
			if ( skinnedMeshRenderer )
			{
				Material[] materials = skinnedMeshRenderer.materials;
				materials[0] = shirtMaterial = new Material( World.defaultShader );
				skinnedMeshRenderer.materials = materials;
				if ( type == Type.hauler )
				{
					if ( onRoad )
						shirtMaterial.color = Color.yellow;
					else
						shirtMaterial.color = Color.grey;
				}
				else
					shirtMaterial.color = Color.black;
			}
		}
		animator = body.GetComponent<Animator>();
		if ( animator )
		{
			animator.runtimeAnimatorController = animationController;
			animator.applyRootMotion = false;
		}
		else
			animator = null;
		wheels[0] = World.FindChildRecursive( body.transform, "cart_wheels_front" )?.gameObject;
		wheels[1] = World.FindChildRecursive( body.transform, "cart_wheels_back" )?.gameObject;

		UpdateBody();
		switch ( type )
		{
			case Type.soldier:
				name = "Soldier";
				break;
			case Type.wildAnimal:
				name = "Bunny";
				break;
			case Type.hauler:
				name = "Hauler";
				break;
			case Type.constructor:
				name = "Builder";
				break;
			case Type.tinkerer:
				name = "Tinkerer";
				break;
			default:
				name = "Worker";
				break;
		}
		soundSource = World.CreateSoundSource( this );
		World.SetLayerRecursive( gameObject, World.layerIndexNotOnMap );

		mapObject = GameObject.CreatePrimitive( PrimitiveType.Sphere );
		World.SetLayerRecursive( mapObject, World.layerIndexMapOnly );
		mapObject.transform.SetParent( transform );
		mapObject.transform.localPosition = Vector3.up * 2;
		mapObject.transform.localScale = Vector3.one * ( type == Type.cart ? 0.5f : 0.3f );
		var r = mapObject.GetComponent<MeshRenderer>();
		r.material = mapMaterial = new Material( World.defaultShader );
		r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
		mapMaterial.renderQueue = 4002;

		arrowObject = new GameObject();
		World.SetLayerRecursive( arrowObject, World.layerIndexMapOnly );
		arrowObject.transform.SetParent( transform, false );
		arrowObject.AddComponent<SpriteRenderer>().sprite = arrowSprite;
		arrowObject.transform.localScale = Vector3.one * 0.4f;

		itemOnMap = new GameObject().AddComponent<SpriteRenderer>();
		itemOnMap.name = "Item on map";
		itemOnMap.transform.SetParent( transform, false );
		itemOnMap.transform.localScale = Vector3.one * 0.15f;
		itemOnMap.transform.localPosition = Vector3.up * 3;
		itemOnMap.transform.rotation = Quaternion.Euler( 90, 0, 0 );
		itemOnMap.gameObject.layer = World.layerIndexMapOnly;
		itemOnMap.material = Instantiate( itemOnMap.material );
		itemOnMap.material.renderQueue = 4003;
	}

	// Distance the worker is taking in a single frame (0.02 sec)
	public static float SpeedBetween( GroundNode a, GroundNode b )
	{
		float heightDifference = Math.Abs( a.height - b.height );
		float time = 2f + heightDifference * 4f;	// Number of seconds it takes for the worker to reach the other node
		return 1 / time / 50;
	}

	public void Walk( GroundNode target )
	{
		assert.IsTrue( node.DirectionTo( target ) >= 0, "Trying to walk to a distant node" );
		currentSpeed = SpeedBetween( target, node );
		walkFrom = node;
		node = walkTo = target;
	}

	// Update is called once per frame
	void FixedUpdate()
	{
		if ( debugReset )
		{
			Reset();
			debugReset = false;
			return;
		}
		// If worker is between two nodes, simply advancing it
		if ( walkTo != null )
		{
			walkProgress += currentSpeed * ground.world.timeFactor;
			if ( walkProgress >= 1 )
			{
				walkTo = walkFrom = null;
				walkBase = null;
				walkProgress -= 1;
			}
		}

		if ( walkTo == null )
		{
			Profiler.BeginSample( "Tasks" );
			if ( taskQueue.Count > 0 )
			{
				// We need to remember the task, because during the call to ExecuteFrame the task might be removed from the queue
				Task task = taskQueue[0];
				if ( task.ExecuteFrame() )
					taskQueue.Remove( task );
			}
			Profiler.EndSample();
		}
		if ( IsIdle() )
		{
			Profiler.BeginSample( "FindTask" );
			FindTask();
			Profiler.EndSample();
		}
		UpdateBody();
		UpdateOnMap();
	}

	void UpdateOnMap()
	{
		arrowObject.SetActive( false );
		switch ( type )
		{
			case Type.unemployed:
				mapMaterial.color = Color.yellow;
				break;
			case Type.soldier:
				mapMaterial.color = Color.red;
				break;
			case Type.constructor:
				mapMaterial.color = Color.blue;
				break;
			case Type.tinkerer:
				mapMaterial.color = Color.cyan;
				break;
			case Type.hauler:
			case Type.cart:
				if ( !onRoad )
				{
					mapMaterial.color = Color.grey;
					break;
				}
				if ( IsIdle() )
					mapMaterial.color = Color.green;
				else
				{
					mapMaterial.color = Color.white;
					if ( taskQueue.Count > 0 )
					{
						var t = taskQueue[0] as WalkToRoadPoint;
						if ( t && t.wishedPoint >= 0 )
						{
							var wp = t.road.nodes[t.wishedPoint].Position;
							arrowObject.SetActive( true );
							var dir = wp - node.Position;
							arrowObject.transform.rotation = Quaternion.LookRotation( dir ) * Quaternion.Euler( 0, -90, 0 ) * Quaternion.Euler( 90, 0, 0 );
							arrowObject.transform.position = node.Position + Vector3.up * 4 + 0.5f * dir;
						}
					}
				}
				break;
		}

		{
			var t = Item.Type.unknown;
			if ( itemInHands )
				t = itemInHands.type;
			var c = this as Stock.Cart;
			if ( c && c.itemQuantity > 0 )
				t = c.itemType;
			if ( t != Item.Type.unknown )
			{
				itemOnMap.sprite = Item.sprites[(int)t];
				itemOnMap.transform.rotation = Quaternion.Euler( 90, 0, 0 );
				itemOnMap.enabled = true;
			}
			else
				itemOnMap.enabled = false;
		}
	}

	public bool Remove( bool returnToMainBuilding = true )
	{
		assert.IsTrue( type != Type.cart || building == null );
		Reset();
		if ( origin != null )
		{
			assert.AreEqual( type, Type.wildAnimal );
			assert.AreEqual( origin.type, Resource.Type.animalSpawner );
			origin.animals.Remove( this );
		}
		if ( road != null && onRoad )
		{
			int currentPoint = road.NodeIndex( node );
			if ( currentPoint < 0 )
			{
				// There was a building at node, but it was already destroyed
				currentPoint = road.NodeIndex( node.Add( Building.flagOffset ) );
			}
			assert.AreEqual( road.workerAtNodes[currentPoint], this );
			road.workerAtNodes[currentPoint] = null;
			onRoad = false;
		}
		if ( exclusiveFlag )
		{
			assert.AreEqual( exclusiveFlag.user, this );
			exclusiveFlag.user = null;
			exclusiveFlag = null;
		}
		if ( road != null )
		{
			road.workers.Remove( this );
			road = null;
		}
		if ( !returnToMainBuilding )
		{
			Destroy( gameObject );
			return true;
		}

		// Try to get to a flag, so that we could walk on the road network to the main building
		// In case of haulers, node.road should be nonzero, except for the ends, but those already 
		// has a flag
		// TODO Pick the closer end
		if ( node.road != null )
			ScheduleWalkToRoadPoint( node.road, 0, false );

		road = null;
		building = null;
		if ( shirtMaterial )
			shirtMaterial.color = Color.black;
		type = Type.unemployed;
		return true;
	}

	public void FindTask()
	{
		assert.IsTrue( IsIdle() );
		if ( type == Type.hauler )
		{
			Profiler.BeginSample( "Road" );
			if ( !onRoad )
			{
				Profiler.BeginSample( "BackToRoad" );
				ScheduleWalkToNode( road.CenterNode(), false );
				ScheduleStartWorkingOnRoad( road );
				Profiler.EndSample();
				return;
			}
			if ( itemInHands )
			{
				for ( int i = 0; i < 2; i++ )
				{
					Flag flag = road.GetEnd( i );
					if ( flag.FreeSpace() == 0 )
						continue;

					flag.ReserveItem( itemInHands );
					if ( road.NodeIndex( node ) == -1 ) // Not on the road, it was stepping into a building
						ScheduleWalkToNeighbour( node.Add( Building.flagOffset ) );	// It is possible, that the building is not there anymore
					ScheduleWalkToRoadPoint( road, i * ( road.nodes.Count - 1 ) );
					ScheduleDeliverItem( itemInHands );

					// The item is expecting the hauler to deliver it to nextFlag, but the hauled is delivering it to whichever flag has space
					// By calling CancelTrip, this expectation is eliminated, and won't cause an assert fail.
					itemInHands.CancelTrip();
					return;
				}
				ScheduleWait( 50 );
				return;
			}

			if ( FindItemToCarry() )
				return;

			if ( node != road.CenterNode() && road.ActiveWorkerCount == 1 )
			{
				ScheduleWalkToRoadPoint( road, road.nodes.Count / 2 );
				return;
			}
		}

		if ( type == Type.wildAnimal )
		{
			int r = World.rnd.Next( 6 );
			var d = Ground.areas[1];
			for ( int i = 0; i < d.Count; i++ )
			{
				GroundNode t = node.Add( d[(i+r)%d.Count] );
				if ( t.building || t.resource || t.type == GroundNode.Type.underWater )
					continue;
				if ( t.DistanceFrom( origin.node ) > 8 )
					continue;
				ScheduleTask( ScriptableObject.CreateInstance<Workshop.Pasturing>().Setup( this ) );
				Walk( t );
				return;
			}
		}

		if ( type == Type.cart )
		{
			assert.IsNotNull( owner );
			var stock = building as Stock;
			if ( stock == null )
			{
				type = Type.unemployed;
				return;
			}
			if ( exclusiveFlag )
			{
				exclusiveFlag.user = null;
				exclusiveFlag = null;
			}
			if ( road )
			{
				int index = road.NodeIndex( node );
				if ( index < 0 )
					index = road.NodeIndex( node.Add( Building.flagOffset ) );
				assert.AreEqual( road.workerAtNodes[index], this );
				road.workerAtNodes[index] = null;
				road = null;
			}

			if ( node == stock.node )
				return;

			assert.IsNotNull( stock );
			if ( node.flag )
				ScheduleWalkToFlag( building.flag );
			else
				ScheduleWalkToNode( building.flag.node );
			ScheduleWalkToNeighbour( building.node );
			var task = ScriptableObject.CreateInstance<Stock.DeliverStackTask>();
			task.Setup( this, stock );
			ScheduleTask( task );
			return;
		}

		if ( type != Type.unemployed && building != null && node != building.node )
		{
			ScheduleWait( 300 );
			if ( node.flag )	// TODO Do something if the worker can't get home
				ScheduleWalkToFlag( building.flag );
			else
				ScheduleWalkToNode( building.flag.node );
			bool failedDelivery = false;
			if ( itemInHands )
			{
				if ( building.flag.FreeSpace() > 0 )
				{
					building.flag.ReserveItem( itemInHands );
					ScheduleDeliverItem( itemInHands );
				}
				else
					failedDelivery = true;
			}
			if ( !failedDelivery )
				ScheduleWalkToNeighbour( building.node );

			return;
		}

		if ( type == Type.soldier && building == null )
			type = Type.unemployed;

		if ( type == Type.unemployed )
		{
			if ( this as Stock.Cart )
				assert.IsNull( building as Stock );
			if ( node == owner.mainBuilding.node )
			{
				if ( walkTo == null )
				{
					if ( itemInHands )
					{
						owner.mainBuilding.ItemOnTheWay( itemInHands );
						owner.mainBuilding.ItemArrived( itemInHands );
						itemInHands.Remove();
						itemInHands = null;
					}
					Destroy( gameObject );
				}
				return;
			}
			if ( node == owner.mainBuilding.flag.node )
			{
				ScheduleWalkToNeighbour( owner.mainBuilding.node );
				return;
			}
			if ( node.flag )
				ScheduleWalkToFlag( owner.mainBuilding.flag );
			else
				ScheduleWalkToNode( owner.mainBuilding.flag.node );
		}
	}

	bool FindItemToCarry()
	{
		Item bestItem = null;
		Item[] bestItemOnSide = { null, null };
		float bestScore = 0;
		float[] bestScoreOnSide = { 0, 0 };
		for ( int c = 0; c < 2; c++ )
		{
			Flag flag = road.GetEnd( c );
			foreach ( var item in flag.items )
			{
				if ( item == null || item.flag == null )	// It can be nextFlag as well
					continue;
				var score = CheckItem( item );
				if ( score.Item2 )
				{
					if ( score.Item1 > bestScoreOnSide[c] )
					{
						bestScoreOnSide[c] = score.Item1;
						bestItemOnSide[c] = item;
					}
				}
				else
				{
					if ( score.Item1 > bestScore )
					{
						bestScore = score.Item1;
						bestItem = item;
					}
				}
			}
		}

		if ( bestItem != null )
		{
			CarryItem( bestItem );
			return true;
		}
		else
		{
			if ( bestItemOnSide[0] && bestItemOnSide[1] )
			{
				if ( bestScoreOnSide[0] > bestScoreOnSide[1] )
					CarryItem( bestItemOnSide[0], bestItemOnSide[1] );
				else
					CarryItem( bestItemOnSide[1], bestItemOnSide[0] );
				return true;
			}
		}
		return false;
	}

	public Tuple<float, bool> CheckItem( Item item )
	{
		float value = road.owner.itemHaulPriorities[(int)item.type];

		// TODO Better prioritization of items
		if ( item.flag.node == node )
			value *= 2;

		if ( item.worker || item.destination == null )
			return Tuple.Create( 0f, false );

		if ( item.buddy )
			return Tuple.Create( 0f, false );

		if ( item.path == null )
			return Tuple.Create( 0f, false );

		if ( !item.path.IsFinished && item.path.Road != road )
			return Tuple.Create( 0f, false );
		
		Flag target = road.GetEnd( 0 );
		if ( target == item.flag )
			target = road.GetEnd( 1 );

		if ( target.FreeSpace() == 0 && item.path.StepsLeft != 1 )
		{
			if ( item.path.StepsLeft <= 1 )
				return Tuple.Create( 0f, false );
			return Tuple.Create( value, true );
		}

		return Tuple.Create( value, false );
	}

	public void ScheduleCall( Callback.IHandler handler )
	{
		assert.IsNotNull( handler );
		var instance = ScriptableObject.CreateInstance<Callback>();
		instance.Setup( this, handler );
		ScheduleTask( instance );
	}

	public void ScheduleWalkToNeighbour( GroundNode target, bool first = false )
	{
		var instance = ScriptableObject.CreateInstance<WalkToNeighbour>();
		instance.Setup( this, target );
		ScheduleTask( instance, first );
	}

	public void ScheduleWalkToNode( GroundNode target, bool ignoreFinalObstacle = false, bool first = false )
	{
		var instance = ScriptableObject.CreateInstance<WalkToNode>();
		instance.Setup( this, target, ignoreFinalObstacle );
		ScheduleTask( instance, first );
	}

	public void ScheduleWalkToFlag( Flag target, bool exclusive = false, bool first = false )
	{
		var instance = ScriptableObject.CreateInstance<WalkToFlag>();
		instance.Setup( this, target, exclusive );
		ScheduleTask( instance, first );
	}

	public void ScheduleWalkToRoadPoint( Road road, int target, bool exclusive = true, bool first = false )
	{
		var instance = ScriptableObject.CreateInstance<WalkToRoadPoint>();
		instance.Setup( this, road, target, exclusive );
		ScheduleTask( instance, first );
	}

	public void ScheduleWalkToRoadNode( Road road, GroundNode target, bool exclusive = true, bool first = false )
	{
		var instance = ScriptableObject.CreateInstance<WalkToRoadPoint>();
		instance.Setup( this, road, road.NodeIndex( target ), exclusive );
		ScheduleTask( instance, first );
	}

	public void SchedulePickupItem( Item item, bool first = false )
	{
		var instance = ScriptableObject.CreateInstance<PickupItem>();
		instance.Setup( this, item );
		ScheduleTask( instance, first );
	}

	public void ScheduleDeliverItem( Item item = null, bool first = false )
	{
		var instance = ScriptableObject.CreateInstance<DeliverItem>();
		instance.Setup( this, item );
		ScheduleTask( instance, first );
	}

	public void ScheduleStartWorkingOnRoad( Road road, bool first = false )
	{
		var instance = ScriptableObject.CreateInstance<StartWorkingOnRoad>();
		instance.Setup( this, road );
		ScheduleTask( instance, first );
	}

	public void ScheduleWait( int time, bool first = false )
	{
		var instance = ScriptableObject.CreateInstance<Wait>();
		instance.Setup( this, time );
		ScheduleTask( instance, first );
	}

	public void ScheduleTask( Task task, bool first = false )
	{
		if ( first )
			taskQueue.Insert( 0, task );
		else
			taskQueue.Add( task );
	}

	public void CarryItem( Item item, Item replace = null )
	{
		assert.IsNotNull( road );
		if ( !item.path.IsFinished )
			assert.AreEqual( road, item.path.Road );
		int itemPoint = road.NodeIndex( item.flag.node ), otherPoint = 0;
		if ( itemPoint == 0 )
			otherPoint = road.nodes.Count - 1;
		Flag other = road.GetEnd( otherPoint );

		if ( replace )
			assert.AreEqual( replace.flag, other );

		if ( item.buddy == null )
		{
			ScheduleWalkToRoadPoint( road, itemPoint );
			SchedulePickupItem( item );
		}

		if ( !item.path.IsFinished )
			ScheduleWalkToRoadPoint( road, otherPoint );

		if ( item.path.StepsLeft <= 1 )
		{
			assert.IsNull( replace );
			var destination = item.destination;
			ScheduleWalkToNeighbour( destination.node );
			ScheduleDeliverItem( item );
			ScheduleWalkToNeighbour( destination.flag.node );
		}
		else
		{
			if ( replace == null )
				assert.IsTrue( other.FreeSpace() > 0 );
			other.ReserveItem( item, replace );
			ScheduleDeliverItem( item );
			if ( replace && item.buddy == null )
				CarryItem( replace, item );
		}
		item.worker = this;
	}

	public void Reset()
	{
		foreach ( var task in taskQueue )
			task.Cancel();
		taskQueue.Clear();
	}

	static float[] angles = new float[6] { 210, 150, 90, 30, 330, 270 };
	public void UpdateBody()
	{
		if ( walkTo == null )
		{
			animator?.SetBool( walkingID, false );
			soundSource?.Stop();
			transform.localPosition = node.Position;
			if ( taskQueue.Count > 0 )
			{
				WalkToRoadPoint task = taskQueue[0] as WalkToRoadPoint;
				if ( task == null || task.wishedPoint < 0 )
					return;

				int direction = node.DirectionTo( task.road.nodes[task.wishedPoint] );
				assert.IsTrue( direction >= 0 );
				transform.rotation = Quaternion.Euler( Vector3.up * angles[direction] );
			}
			return;
		}
		animator?.SetBool( walkingID, true );

		if ( itemInHands )
			itemInHands.UpdateLook();

		if ( walkBase != null )
		{
			float progress = walkProgress;
			if ( walkBackward )
				progress = 1 - progress;
			transform.localPosition = walkBase.PositionAt( walkBlock, progress );
			Vector3 direction = walkBase.DirectionAt( walkBlock, progress );
			direction.y = 0;
			if ( walkBackward )
				direction = -direction;
			transform.rotation = Quaternion.LookRotation( direction );
		}
		else
		{
			transform.localPosition = Vector3.Lerp( walkFrom.Position, walkTo.Position, walkProgress ) + Vector3.up * GroundNode.size * Road.height;
			int direction = walkFrom.DirectionTo( walkTo );
			assert.IsTrue( direction >= 0 );
			transform.rotation = Quaternion.Euler( Vector3.up * angles[direction] );
		}

		if ( walkTo )
		{
			if ( soundSource && !soundSource.isPlaying )
			{
				soundSource.clip = walkSounds.GetMediaData( type );
				soundSource.Play();
			}
			if ( type == Type.cart )
			{
				foreach ( var g in wheels )
				{
					if ( g == null )
						continue;
					g.transform.Rotate( 0, World.instance.timeFactor * currentSpeed * 300, 0 );
				}
				body.transform.localRotation = Quaternion.Euler( ( walkTo.height - walkFrom.height ) / GroundNode.size * -50, 0, 0 );
			}
		}
	}

	public bool IsIdle( bool inBuilding = false )
	{
		if ( taskQueue.Count != 0 || walkTo != null )
			return false;
		if ( !inBuilding || building as Workshop == null )
			return true;
		Workshop workshop = building as Workshop;
		if ( workshop && workshop.working )
			return false;
		return node == building.node;
	}

	public bool Call( Road road, int point )
	{
		if ( this.road != road || !onRoad )
			return false;
		if ( IsIdle() )
		{
			// Give an order to the worker to change position with us. This
			// will not immediately move him away, but during the next calls the
			// worker will eventually come.
			ScheduleWalkToRoadPoint( road, point );
			return false;
		}
		if ( walkTo != null )
			return false;
		var currentTask = taskQueue[0] as WalkToRoadPoint;
		if ( currentTask == null )
			return false;
		if ( currentTask.wishedPoint != point )
			return false;

		currentTask.NextStep( true );
		return true;			
	}

	public void OnClicked()
	{
		Interface.WorkerPanel.Create().Open( this, false );
	}

	public T FindTaskInQueue<T>() where T : class
	{
		foreach ( var task in taskQueue )
		{
			T result = task as T;
			if ( result != null )
				return result;
		}
		return null;
	}

	public void Validate()
	{
		if ( type == Type.wildAnimal )
		{
			assert.IsNotNull( origin );
			assert.AreEqual( origin.type, Resource.Type.animalSpawner );
		}
		else
			assert.IsNull( origin );
		if ( type == Type.hauler )
			assert.IsTrue( road == null || building == null );
		if ( road && onRoad )
		{
			if ( type == Type.hauler )
				assert.IsTrue( road.workers.Contains( this ) );
			int point = road.NodeIndex( node );
			if ( point < 0 && type == Type.hauler )
			{
				if ( itemInHands )
					assert.IsTrue( node.building || itemInHands.tripCancelled );	// It is possible, that the item destination was destroyed during the last step
				else
					assert.IsTrue( node.building );
				if ( node.building )
				{
					point = road.NodeIndex( node.building.flag.node );
					assert.IsTrue( point >= 0 );
				}
			}
			if ( point >= 0 )
				assert.AreEqual( road.workerAtNodes[point], this );
		}
		if ( itemInHands )
		{
			assert.AreEqual( itemInHands.worker, this, "Unknown worker " + itemInHands.worker );	
			itemInHands.Validate();
		}
		foreach ( Task task in taskQueue )
			task.Validate();
		if ( exclusiveFlag )
		{
			assert.IsTrue( type == Type.hauler || type == Type.cart );
			if ( type != Type.cart )
			{
				assert.IsTrue( onRoad );
				assert.IsNotNull( road );
			}
			assert.AreEqual( exclusiveFlag.user, this, "Flag exclusivity mismatch" );
		}
		if ( type == Type.cart )
		{
			assert.IsNotNull( building as Stock );
			if ( road )
			{
				int index = road.NodeIndex( node );
				if ( index < 0 )
					index = road.NodeIndex( node.Add( Building.flagOffset ) );
				assert.IsTrue( index >= 0 );
				assert.AreEqual( road.workerAtNodes[index], this );
			}
		}
	}
}