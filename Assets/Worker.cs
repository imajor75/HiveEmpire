using JetBrains.Annotations;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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
	[JsonIgnore]
	public Flag reservation;
	public int look;
	public Resource origin;
	public float currentSpeed;
	public Flag exclusiveFlag;
	public int itemsDelivered;
	static public MediaTable<AudioClip, Resource.Type> resourceGetSounds;
	[JsonIgnore]
	public AudioSource soundSource;
	[JsonIgnore]
	public GameObject mapObject;
	Material mapMaterial;

	public Road road;
	public bool atRoad;

	public Building building;

	Animator animator;
	static public List<GameObject> templates = new List<GameObject>();
	static public RuntimeAnimatorController animationController;
	static public int walkingID, pickupID, putdownID;

	public List<Task> taskQueue = new List<Task>();
	GameObject body;
	GameObject box;
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

	public class WalkToFlag : Task
	{
		public Flag target;
		public Path path;

		public void Setup( Worker boss, Flag target )
		{
			base.Setup( boss );
			this.target = target;
		}
		public override bool ExecuteFrame()
		{
			if ( boss.node.flag == target )
				return true;

			if ( path == null )
			{
				path = Path.Between( boss.node, target.node, PathFinder.Mode.onRoad );
				if ( path == null )
				{
					var instance = ScriptableObject.CreateInstance<WalkToNode>();
					instance.Setup( boss, target.node );
					ReplaceThisWith( instance );
					return false;
				}
			}
			int point = 0;
			if ( boss.node.flag == path.Road.GetEnd( 0 ) )
				point = path.Road.nodes.Count - 1;
			boss.ScheduleWalkToRoadPoint( path.NextRoad(), point, false, true );
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
				path = Path.Between( boss.node, target, PathFinder.Mode.avoidObjects, ignoreFinalObstacle );
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

			if ( currentPoint == -1 )
				currentPoint = road.NodeIndex( boss.node );
			if ( currentPoint == -1 )
				return true;	// Task failed
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
				if ( other && !other.Call( road, currentPoint ) )
				{
					// As a last resort to make space is to simply remove the other hauler
					if ( other.atRoad && other.road != road && other.road.workers.Count > 1 && other.IsIdle() )
						other.Remove();
					else
						return false;
				}
			}

			wishedPoint = -1;
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
		static public int pickupTimeStart = 100;
		public Item item;
		public Building destnation;
		public int pickupTimer = pickupTimeStart;

		public void Setup( Worker boss, Item item )
		{
			base.Setup( boss );
			destnation = item.destination;
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
			if ( pickupTimer == pickupTimeStart )
			{
				boss.animator.ResetTrigger( putdownID );
				boss.animator.SetTrigger( pickupID );
				if ( boss.itemTable )
					boss.itemTable.material = Item.materials[(int)item.type];
				boss.box?.SetActive( true );
			}
			if ( ( pickupTimer -= (int)World.instance.timeFactor ) > 0 )
				return false;

			if ( destnation != item.destination )
			{
				boss.assert.AreEqual( boss.type, Type.haluer );
				return ResetBoss();
			}

			item.flag?.ReleaseItem( item );
			boss.itemInHands = item;
			boss.assert.IsTrue( item.worker == boss || item.worker == null );
			item.worker = boss;
			if ( item.worker.type == Type.haluer )
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
		static public int putdownTimeStart = 100;
		public Item item;
		public int putdownTimer = putdownTimeStart;

		public void Setup( Worker boss, Item item )
		{
			base.Setup( boss );
			this.item = item;
		}
		public override void Cancel()
		{
			if ( item != null && item.nextFlag != null )
			{
				item.nextFlag.CancelItem( item );
				item.nextFlag = null;
				item.assert.IsNotSelected();
			}
			base.Cancel();
		}
		public override bool ExecuteFrame()
		{
			if ( putdownTimer == putdownTimeStart )
			{
				boss.animator.ResetTrigger( pickupID );
				boss.animator.SetTrigger( putdownID );
			}
			if ( ( putdownTimer -= (int)World.instance.timeFactor ) > 0 )
				return false;

			boss.itemsDelivered++;
			boss.box?.SetActive( false );
			boss.assert.AreEqual( item, boss.itemInHands );
			if ( item.destination?.node == boss.node )
				item.Arrived();
			else
			{
				Flag flag = boss.node.flag;
				if ( flag )
					item.ArrivedAt( flag );
				else
					item.Remove();	// This happens when the previous walk tasks failed, and the worker couldn't reach the target
			}
			boss.itemInHands = null;

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
				return true;	// Task failed
			boss.assert.IsFalse( boss.atRoad );
			if ( road.workerAtNodes[i] == null )
			{
				road.workerAtNodes[i] = boss;
				boss.atRoad = true;
				return true;
			}
			return false;
		}
	}

	public enum Type
	{
		haluer,
		tinkerer,
		constructor,
		soldier,
		wildAnimal,
		unemployed
	}

	public static void Initialize()
	{
		templates.Add( (GameObject)Resources.Load( "Polytope Studio/Lowpoly Medieval Characters/Prefabs/PT_Medieval_Female_Peasant_01_a" ) );
		templates.Add( (GameObject)Resources.Load( "Polytope Studio/Lowpoly Medieval Characters/Prefabs/PT_Medieval_Male_Peasant_01_a" ) );
		templates.Add( (GameObject)Resources.Load( "Polytope Studio/Lowpoly Medieval Characters/Prefabs/PT_Medieval_Boy_Peasant_01_a" ) );
		templates.Add( (GameObject)Resources.Load( "FootmanPBRHPPolyart/Prefabs/footman_Blue_HP" ) );
		templates.Add( (GameObject)Resources.Load( "Rabbits/Prefabs/Rabbit 1" ) );

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
			"Mines/pickaxe", Resource.Type.stone };
		resourceGetSounds.Fill( sounds );
	}

	static public Worker Create()
	{
		GameObject workerBody = new GameObject();
		Worker worker = workerBody.AddComponent<Worker>();
		return worker;
	}

	public Worker SetupForRoad( Road road )
	{
		type = Type.haluer;
		owner = road.owner;
		look = 2;
		ground = road.ground;
		Building main = road.owner.mainBuilding;
		node = main.node;
		this.road = road;
		atRoad = false;
		ScheduleWalkToNeighbour( main.flag.node );
		ScheduleWalkToFlag( road.GetEnd( 0 ) ); // TODO Pick the end closest to the main building
		ScheduleWalkToRoadNode( road, road.CenterNode(), false );
		ScheduleStartWorkingOnRoad( road );
		return this;
	}

	public Worker SetupForBuilding( Building building )
	{
		type = Type.tinkerer;
		look = 1;
		return SetupForBuildingSite( building );
	}

	public Worker SetupForConstruction( Building building )
	{
		type = Type.constructor;
		look = 0;
		return SetupForBuildingSite( building );
	}

	public Worker SetupAsSoldier( Building building )
	{
		type = Type.soldier;
		look = 3;
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
		type = Type.wildAnimal;
		look = 4;
		this.node = node;
		this.origin = origin;
		this.ground = node.ground;
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

		body = (GameObject)GameObject.Instantiate( templates[look], transform );
		Transform hand = World.FindChildRecursive( body.transform, "RightHand" );
		if ( hand != null )
		{
			if ( type == Type.haluer )
				box = (GameObject)GameObject.Instantiate( boxTemplateBoy, hand );
			else
				box = (GameObject)GameObject.Instantiate( boxTemplateMan, hand );
			box.SetActive( false );
			itemTable = World.FindChildRecursive( box.transform, "ItemTable" ).GetComponent<MeshRenderer>();
			assert.IsNotNull( itemTable );
		}
		animator = body.GetComponent<Animator>();
		animator.runtimeAnimatorController = animationController;
		animator.applyRootMotion = false;
		UpdateBody();
		switch ( type )
		{
			case Type.soldier:
				name = "Soldier";
				break;
			case Type.wildAnimal:
				name = "Bunny";
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
		mapObject.transform.localScale = Vector3.one * 0.3f;
		mapObject.GetComponent<MeshRenderer>().material = mapMaterial = new Material( World.defaultShader );
	}

	public static float SpeedBetween( GroundNode a, GroundNode b )
	{
		float heightDifference = Math.Abs( a.height - b.height );
		float time = 2f + heightDifference * 4f;
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
		// If worker is between two nodes, simply advancing it
		if ( walkTo != null )
		{
			walkProgress += currentSpeed * ground.world.timeFactor; // TODO Speed should depend on the steepness of the road
			if ( walkProgress >= 1 )
			{
				walkTo = walkFrom = null;
				walkBase = null;
				walkProgress -= 1;
			}
		}
		if ( itemInHands && itemInHands.nextFlag == null && itemInHands.destination == null )   // Item trip was cancelled during the haluer trying to finish the path into a building
			Reset();

		if ( walkTo == null )
		{
			if ( taskQueue.Count > 0 )
			{
				// We need to remember the task, because during the call to ExecuteFrame the task might be removed from the queue
				Task task = taskQueue[0];
				if ( task.ExecuteFrame() )
					taskQueue.Remove( task );
			}
		}
		if ( IsIdle() )
			FindTask();
		UpdateBody();
		UpdateOnMap();
	}

	void UpdateOnMap()
	{
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
			case Type.haluer:
				if ( !atRoad )
				{
					mapMaterial.color = Color.grey;
					break;
				}
				if ( IsIdle() )
					mapMaterial.color = Color.green;
				else
					mapMaterial.color = Color.white;
				break;
		}
	}

	public bool Remove( bool returnToMainBuilding = true )
	{
		Reset();
		if ( origin != null )
		{
			assert.AreEqual( type, Type.wildAnimal );
			assert.AreEqual( origin.type, Resource.Type.animalSpawner );
			origin.animals.Remove( this );
		}
		if ( road != null && atRoad )
		{
			int currentPoint = road.NodeIndex( node );
			if ( currentPoint < 0 )
			{
				// There was a building at node, but it was already destroyed
				currentPoint = road.NodeIndex( node.Add( Building.flagOffset ) );
			}
			assert.AreEqual( road.workerAtNodes[currentPoint], this );
			road.workerAtNodes[currentPoint] = null;
			if ( exclusiveFlag )
			{
				assert.AreEqual( exclusiveFlag.user, this );
				exclusiveFlag.user = null;
			}
			atRoad = false;
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
		type = Type.unemployed;
		return true;
	}

	public void FindTask()
	{
		assert.IsNotSelected();
		assert.IsTrue( IsIdle() );
		if ( road != null )
		{
			if ( !atRoad )
			{
				ScheduleWalkToNode( road.CenterNode(), false );
				ScheduleStartWorkingOnRoad( road );
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
					return;
				}
				itemInHands.Remove();
				animator.SetTrigger( putdownID );
				itemInHands = null;
			}
			Item bestItem = null;
			float bestScore = 0;
			for ( int c = 0; c < 2; c++ )
			{
				Flag flag = road.GetEnd( c );
				foreach ( var item in flag.items )
				{
					if ( item == null || item.flag == null )	// It can be nextFlag as well
						continue;
					float score = CheckItem( item );
					if ( score > bestScore )
					{
						bestScore = score;
						bestItem = item;
					}
				}
			}
			if ( bestItem != null )
			{
				CarryItem( bestItem );
				return;
			}

			if ( node != road.CenterNode() && road.workers.Count == 1 )
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
				if ( t.building || t.resource )
					continue;
				if ( t.DistanceFrom( origin.node ) > 8 )
					continue;
				ScheduleTask( ScriptableObject.CreateInstance<Workshop.Pasturing>().Setup( this ) );
				Walk( t );
				return;
			}
		}

		if ( building != null && node != building.node )
		{
			if ( node.flag )	// TODO Do something if the worker can't get home
				ScheduleWalkToFlag( building.flag );
			else
				ScheduleWalkToNode( building.flag.node );
			ScheduleWalkToNeighbour( building.node );
			return;
		}

		if ( type == Type.soldier && building == null )
			type = Type.unemployed;

		if ( type == Type.unemployed )
		{
			if ( node == owner.mainBuilding.node )
			{
				if ( walkTo == null )
					Destroy( gameObject );
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

	public float CheckItem( Item item )
	{
		if ( item.worker || item.destination == null )
			return 0;

		if ( item.path == null )
			return 0;

		if ( !item.path.IsFinished && item.path.Road != road )
			return 0;

		Flag target = road.GetEnd( 0 );
		if ( target == item.flag )
			target = road.GetEnd( 1 );
		if ( target.FreeSpace() == 0 && item.path.StepsLeft != 1 )
			return 0;

		float value = road.owner.itemHaulPriorities[(int)item.type];

		// TODO Better prioritization of items
		if ( item.flag.node == node )
			value *= 2;

		return value;
	}

	public void ScheduleWalkToNeighbour( GroundNode target, bool first = false )
	{
		var instance = ScriptableObject.CreateInstance<WalkToNeighbour>();
		instance.Setup( this, target );
		if ( first )
			taskQueue.Insert( 0, instance );
		else
			taskQueue.Add( instance );
	}

	public void ScheduleWalkToNode( GroundNode target, bool ignoreFinalObstacle = false, bool first = false )
	{
		var instance = ScriptableObject.CreateInstance<WalkToNode>();
		instance.Setup( this, target, ignoreFinalObstacle );
		if ( first )
			taskQueue.Insert( 0, instance );
		else
			taskQueue.Add( instance );
	}

	public void ScheduleWalkToFlag( Flag target, bool first = false )
	{
		var instance = ScriptableObject.CreateInstance<WalkToFlag>();
		instance.Setup( this, target );
		if ( first )
			taskQueue.Insert( 0, instance );
		else
			taskQueue.Add( instance );
	}

	public void ScheduleWalkToRoadPoint( Road road, int target, bool exclusive = true, bool first = false )
	{
		var instance = ScriptableObject.CreateInstance<WalkToRoadPoint>();
		instance.Setup( this, road, target, exclusive );
		if ( first )
			taskQueue.Insert( 0, instance );
		else
			taskQueue.Add( instance );
	}

	public void ScheduleWalkToRoadNode( Road road, GroundNode target, bool exclusive = true, bool first = false )
	{
		var instance = ScriptableObject.CreateInstance<WalkToRoadPoint>();
		instance.Setup( this, road, road.NodeIndex( target ), exclusive );
		if ( first )
			taskQueue.Insert( 0, instance );
		else
			taskQueue.Add( instance );
	}

	public void SchedulePickupItem( Item item, bool first = false )
	{
		var instance = ScriptableObject.CreateInstance<PickupItem>();
		instance.Setup( this, item );
		if ( first )
			taskQueue.Insert( 0, instance );
		else
			taskQueue.Add( instance );
	}

	public void ScheduleDeliverItem( Item item = null, bool first = false )
	{
		var instance = ScriptableObject.CreateInstance<DeliverItem>();
		instance.Setup( this, item );
		if ( first )
			taskQueue.Insert( 0, instance );
		else
			taskQueue.Add( instance );
	}

	public void ScheduleStartWorkingOnRoad( Road road, bool first = false )
	{
		var instance = ScriptableObject.CreateInstance<StartWorkingOnRoad>();
		instance.Setup( this, road );
		if ( first )
			taskQueue.Insert( 0, instance );
		else
			taskQueue.Add( instance );
	}

	public void ScheduleTask( Task task, bool first = false )
	{
		if ( first )
			taskQueue.Insert( 0, task );
		else
			taskQueue.Add( task );
	}

	public void CarryItem( Item item )
	{
		item.assert.IsNotSelected();
		assert.IsNotNull( road );
		if ( !item.path.IsFinished )
			assert.AreEqual( road, item.path.Road );
		int itemPoint = road.NodeIndex( item.flag.node ), otherPoint = 0;
		if ( itemPoint == 0 )
			otherPoint = road.nodes.Count - 1;
		Flag other = road.GetEnd( otherPoint );

		ScheduleWalkToRoadPoint( road, itemPoint );
		SchedulePickupItem( item );
		if ( !item.path.IsFinished )
			ScheduleWalkToRoadPoint( road, otherPoint );

		if ( item.path.StepsLeft <= 1 )
		{
			var destination = item.destination;
			ScheduleWalkToNeighbour( destination.node );
			ScheduleDeliverItem( item );
			ScheduleWalkToNeighbour( destination.flag.node );
		}
		else
		{
			assert.IsTrue( other.FreeSpace() > 0 );
			other.ReserveItem( item );
			ScheduleDeliverItem( item );
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
			transform.localPosition = node.Position();
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
		animator.SetBool( walkingID, true );

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
			transform.localPosition = Vector3.Lerp( walkFrom.Position(), walkTo.Position(), walkProgress ) + Vector3.up * GroundNode.size * Road.height;
			int direction = walkFrom.DirectionTo( walkTo );
			assert.IsTrue( direction >= 0 );
			transform.rotation = Quaternion.Euler( Vector3.up * angles[direction] );
		}
	}

	public bool IsIdle( bool inBuilding = false )
	{
		if ( taskQueue.Count != 0 || walkTo != null )
			return false;
		if ( !inBuilding )
			return true;
		assert.IsNotNull( building );
		Workshop workshop = building as Workshop;
		if ( workshop && workshop.working )
			return false;
		return node == building.node;
	}

	public bool Call( Road road, int point )
	{
		if ( this.road != road || !atRoad )
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

	public void Validate()
	{
		if ( type == Type.wildAnimal )
		{
			assert.IsNotNull( origin );
			assert.AreEqual( origin.type, Resource.Type.animalSpawner );
		}
		else
			assert.IsNull( origin );
		assert.IsTrue( road == null || building == null );
		if ( road )
		{
			assert.IsTrue( road.workers.Contains( this ) );
			int point = road.NodeIndex( node );
			if ( point < 0 )
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
			assert.AreEqual( itemInHands.worker, this );	
			itemInHands.Validate();
		}
		foreach ( Task task in taskQueue )
			task.Validate();
		if ( exclusiveFlag )
		{
			assert.AreEqual( type, Type.haluer );
			assert.IsTrue( atRoad );
			assert.IsNotNull( road );
			assert.AreEqual( exclusiveFlag.user, this, "Flag exclusivity mismatch" );
		}
	}
}