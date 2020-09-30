using JetBrains.Annotations;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;

[SelectionBase]
public class Worker : MonoBehaviour
{
	public Type type;
	public Ground ground;
	public GroundNode walkFrom = zero;
	public GroundNode walkTo = zero;
	public Road walkBase;
	public int walkBlock;
	public bool walkBackward;
	public float walkProgress;
	public GroundNode node;
	public Item itemInHands;
	public Flag reservation;
	public int look;
	public Resource origin;
	public float currentSpeed;
	public Flag exclusiveFlag;
	static public MediaTable<AudioClip, Resource.Type> resourceGetSounds;
	[JsonIgnore]
	public AudioSource soundSource;

	public Road road;
	public bool atRoad;

	public Building building;

	Animator animator;
	static public List<GameObject> templates = new List<GameObject>();
	static public RuntimeAnimatorController animationController;
	static public int walkingID, pickupID, putdownID;
	public static GroundNode zero = new GroundNode();	// HACK This is a big fat hack, to stop Unity editor from crashing

	public List<Task> taskQueue = new List<Task>();
	GameObject body;
	GameObject box;
	static GameObject boxTemplate;

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
			Assert.AreEqual( this, boss.taskQueue[0] );
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

		public virtual void Validate()
		{
			Assert.IsTrue( boss.taskQueue.Contains( this ) );
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
			if ( boss.node.flag == path.Road().GetEnd( 0 ) )
				point = path.Road().nodes.Count - 1;
			boss.ScheduleWalkToRoadPoint( path.NextRoad(), point, true, false );
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
			base.Setup( boss );
			this.road = road;
			this.exclusive = exclusive;
			targetPoint = point;
		}

		public override bool ExecuteFrame()
		{
			if ( road == null )
			{
				boss.Reset();
				return false;
			}

			if ( currentPoint == -1 )
				currentPoint = road.NodeIndex( boss.node );
			Assert.IsTrue( currentPoint >= 0 && currentPoint < road.nodes.Count );
			if ( currentPoint == targetPoint )
				return true;
			else
				NextStep();
			return false;
		}

		public bool NextStep()
		{
			if ( exclusive )
				Assert.AreEqual( road.workerAtNodes[currentPoint], boss );
			Assert.AreEqual( boss.walkTo, zero );

			if ( currentPoint == targetPoint )
				return false;

			int nextPoint;
			if ( currentPoint < targetPoint )
				nextPoint = currentPoint + 1;
			else
				nextPoint = currentPoint - 1;

			if ( exclusive )
			{
				Flag flag = road.nodes[nextPoint].flag;
				if ( flag )
				{
					if ( flag.user )
					{
						if ( flag.user.road != road )
							return false;
						if ( flag.user.taskQueue.Count > 0 )
						{
							var otherTask = flag.user.taskQueue[0] as WalkToRoadPoint;
							if ( otherTask != null && otherTask.wishedPoint != currentPoint )
								return false;
						}
					}
				}
				road.workerAtNodes[currentPoint] = null;
				if ( road.workerAtNodes[nextPoint] != null )
				{
					bool coming = false;
					var otherWorker = road.workerAtNodes[nextPoint];
					if ( otherWorker.taskQueue.Count > 0 )
					{
						var otherTask = otherWorker.taskQueue[0] as WalkToRoadPoint;
						if ( otherTask && otherTask.wishedPoint == currentPoint )
						{
							// TODO Workers should avoid each other
							coming = otherTask.NextStep();
						}
					}
					if ( !coming )
					{
						road.workerAtNodes[currentPoint] = boss;
						wishedPoint = nextPoint;
						return false;
					}
				}
			}

			wishedPoint = -1;

			Assert.AreEqual( boss.node, road.nodes[currentPoint] );
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
					boss.walkTo.flag.user = boss;
					boss.exclusiveFlag = boss.walkTo.flag;
				}
				if ( boss.walkFrom.flag && boss.walkFrom.flag.user == boss )
					boss.walkFrom.flag.user = null;
			}
			return true;
		}

		public override void Validate()
		{
			base.Validate();
			if ( this != boss.taskQueue[0] )
				return;
				
			Assert.IsTrue( currentPoint >= -1 && currentPoint < road.nodes.Count );
			if ( exclusive )
			{
				int t = 0;
				for ( int i = 0; i < road.workerAtNodes.Count; i++ )
				{
					if ( road.workerAtNodes[i] == boss )
					{
						t++;
						Assert.AreEqual( i, currentPoint );
					}
				}
				Assert.AreEqual( t, 1 );
			}

			Assert.IsTrue( targetPoint >= 0 && targetPoint < road.nodes.Count );
			if ( wishedPoint >= 0 )
			{
				Assert.IsTrue( wishedPoint <= road.nodes.Count );
				Assert.AreEqual( Math.Abs( wishedPoint - currentPoint ), 1 );
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
			boss.Walk( target );
			return true;
		}
	}

	public class PickupItem : Task
	{
		static public int pickupTimeStart = 100;
		public Item item;
		public int pickupTimer = pickupTimeStart; 

		public void Setup( Worker boss, Item item )
		{
			base.Setup( boss );
			this.item = item;

		}
		public override void Cancel()
		{
			Assert.AreEqual( boss, item.worker );
			item.worker = null;
			base.Cancel();
		}
		public override bool ExecuteFrame()
		{
			if ( pickupTimer == pickupTimeStart )
			{
				boss.animator.SetTrigger( pickupID );
				boss.box?.SetActive( true );
			}
			if ( (pickupTimer -= (int)World.instance.speedModifier) > 0 )
				return false;

			if ( item.flag != null )
				item.flag.ReleaseItem( item );
			boss.itemInHands = item;
			Assert.IsTrue( item.worker == boss || item.worker == null );
			item.worker = boss;
			if ( item.worker.type == Type.haluer )
				item.path.NextRoad();
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
		public override bool ExecuteFrame()
		{
			if ( putdownTimer == putdownTimeStart )
				boss.animator.SetTrigger( putdownID );
			if ( ( putdownTimer -= ( int )World.instance.speedModifier) > 0 )
				return false;

			boss.box?.SetActive( false );
    		Assert.AreEqual( item, boss.itemInHands );
			if ( item.destination?.node == boss.node )
				item.Arrived();
			else
			{
				Flag flag = boss.node.flag;
				Assert.IsNotNull( flag, "Trying to deliver an item at a location where there is no flag" );
				item.ArrivedAt( flag );
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
			int i = road.NodeIndex( boss.node );
			Assert.IsTrue( i >= 0 );
			Assert.IsFalse( boss.atRoad );
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

		boxTemplate = (GameObject)Resources.Load( "Tresure_box/tresure_box_inhands" );
		Assert.IsNotNull( boxTemplate );

		animationController = (RuntimeAnimatorController)Resources.Load( "Crafting Mecanim Animation Pack FREE/Prefabs/Crafter Animation Controller FREE" );
		Assert.IsNotNull( animationController );
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
		look = 2;
		ground = road.ground;
		Building main = road.ground.world.mainBuilding;
		node = main.node;
		this.road = road;
		atRoad = false;
		ScheduleWalkToNeighbour( main.flag.node );
		ScheduleWalkToFlag( road.GetEnd( 0 ) ); // TODO Pick the end closest to the main building
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
		this.building = building;
		Building main = ground.world.mainBuilding;
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
		if ( hand != null && type == Type.haluer )
		{
			box = (GameObject)GameObject.Instantiate( boxTemplate, hand );
			box.SetActive( false );
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
	}

	public static float SpeedBetween( GroundNode a, GroundNode b )
	{
		float heightDifference = Math.Abs( a.height - b.height );
		float time = 2f + heightDifference * 4f;
		return 1 / time / 50;
	}

	public void Walk( GroundNode target )
	{
		Assert.IsTrue( node.DirectionTo( target ) >= 0, "Trying to walk to a distant node" );
		currentSpeed = SpeedBetween( target, node );
		walkFrom = node;
		node = walkTo = target;
	}

	// Update is called once per frame
	void FixedUpdate()
	{
		// If worker is between two nodes, simply advancing it
		if ( walkTo != zero )
		{
			walkProgress += currentSpeed*ground.world.speedModifier; // TODO Speed should depend on the steepness of the road
			if ( walkProgress >= 1 )
			{
				walkTo = walkFrom = zero;
				walkBase = null;
				walkProgress -= 1;
			}
		}
		if ( walkTo == zero )
		{
			if ( taskQueue.Count > 0 && taskQueue[0].ExecuteFrame() )
				taskQueue.RemoveAt( 0 );
		}
		if ( walkTo == zero && taskQueue.Count == 0 )
			FindTask();
		UpdateBody();
	}

	public bool Remove( bool returnToMainBuilding = true )
	{
		Reset();
		if ( !returnToMainBuilding )
		{
			Destroy( gameObject );
			return true;
		}
		if ( road != null && atRoad )
		{
			int currentPoint = road.NodeIndex( node );
			Assert.AreEqual( road.workerAtNodes[currentPoint], this );
			road.workerAtNodes[currentPoint] = null;
			if ( exclusiveFlag )
			{
				Assert.AreEqual( exclusiveFlag.user, this );
				exclusiveFlag.user = null;
			}
			road.workers.Remove( this );
			// TODO Pick the closer end
			ScheduleWalkToRoadPoint( road, 0 );
		}
		road = null;
		building = null;
		type = Type.unemployed;
		return true;
	}

	public void FindTask()
	{
		Assert.AreEqual( taskQueue.Count, 0 );
		if ( road != null )
		{
			if ( !atRoad )
			{
				ScheduleWalkToNode( road.nodes[0] );
				ScheduleStartWorkingOnRoad( road );
				return;
			}
			Assert.IsNotSelected( this );
			Item bestItem = null;
			float bestScore = 0;
			for ( int c = 0; c < 2; c++ )
			{
				foreach ( var item in road.GetEnd( c ).items )
				{
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
		}

		if ( road != null && node != road.CenterNode() && road.workers.Count == 1 )
		{
			ScheduleWalkToRoadPoint( road, road.nodes.Count / 2 );
			return;
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
			if ( node.flag )
				ScheduleWalkToFlag( building.flag );
			else
				ScheduleWalkToNode( building.flag.node );
			ScheduleWalkToNeighbour( building.node );
			return;
		}

		if ( type == Type.unemployed )
		{
			if ( node == ground.world.mainBuilding.node )
			{
				if ( walkTo == zero )
					Destroy( gameObject );
				return;
			}
			if ( node == ground.world.mainBuilding.flag.node )
			{
				ScheduleWalkToNeighbour( ground.world.mainBuilding.node );
				return;
			}
			if ( node.flag )
				ScheduleWalkToFlag( ground.world.mainBuilding.flag );
			else
				ScheduleWalkToNode( ground.world.mainBuilding.flag.node );
		}
	}

	public float CheckItem( Item item )
	{
		if ( item == null || item.worker || item.destination == null )
			return 0;

		if ( item.path == null || item.path.Road() != road )
			return 0;

		Flag target = road.GetEnd( 0 );
		if ( target == item.flag )
			target = road.GetEnd( 1 );
		if ( target.FreeSpace() == 0 )
			return 0;

		// TODO Better prioritization of items
		if ( item.flag.node == node )
			return 2;

		return 1;
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

	public void ScheduleWalkToRoadPoint( Road road, int target, bool first = false, bool exclusive = true )
	{
		var instance = ScriptableObject.CreateInstance<WalkToRoadPoint>();
		instance.Setup( this, road, target, exclusive );
		if ( first )
			taskQueue.Insert( 0, instance );
		else
			taskQueue.Add( instance );
	}

	public void ScheduleWalkToRoadNode( Road road, GroundNode target, bool first = false )
	{
		var instance = ScriptableObject.CreateInstance<WalkToRoadPoint>();
		instance.Setup( this, road, road.NodeIndex( target ), true );
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
		Assert.IsNotNull( road );
		Assert.AreEqual( road, item.path.Road() );
		int itemPoint = road.NodeIndex( item.flag.node ), otherPoint = 0;
		if ( itemPoint == 0 )
			otherPoint = road.nodes.Count - 1;
		Flag other = road.GetEnd( otherPoint );

		ScheduleWalkToRoadPoint( road, itemPoint );
		SchedulePickupItem( item );
		ScheduleWalkToRoadPoint( road, otherPoint );

		if ( item.path.StepsLeft() == 1 )
		{
			var destination = item.destination;
			ScheduleWalkToNeighbour( destination.node );
			ScheduleDeliverItem( item );
			ScheduleWalkToNeighbour( destination.flag.node );
		}
		else
		{
			Assert.IsTrue( other.FreeSpace() > 0 );
			other.reserved++;
			Assert.IsNull( reservation );
			reservation = other;
			ScheduleDeliverItem( item );
		}
		item.worker = this;
	}

	public void Reset()
	{
		if ( origin != null )
		{
			Assert.AreEqual( type, Type.wildAnimal );
			Assert.AreEqual( origin.type, Resource.Type.animalSpawner );
			origin.animals.Remove( this );
		}
		if ( reservation )
		{
			reservation.reserved--;
			reservation = null;
		}
		foreach ( var task in taskQueue )
			task.Cancel();
		taskQueue.Clear();

		if ( itemInHands )
		{
			itemInHands.Remove();
			animator.SetTrigger( putdownID );
			itemInHands = null;
		}
	}

	static float[] angles = new float[6] { 210, 150, 90, 30, 330, 270 };
	public void UpdateBody()
	{
		if ( walkTo == zero )
		{
			animator?.SetBool( walkingID, false );
			transform.localPosition = node.Position();
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
			Assert.IsTrue( direction >= 0 );
			transform.rotation = Quaternion.Euler( Vector3.up * angles[direction] );
		}
	}

	public bool IsIdleInBuilding()
	{
		Assert.IsNotNull( building );
		return node == building.node && walkTo == zero && taskQueue.Count == 0;
	}

	public void Validate()
	{
		if ( type == Type.wildAnimal )
		{
			Assert.IsNotNull( origin );
			Assert.AreEqual( origin.type, Resource.Type.animalSpawner );
		}
		else
			Assert.IsNull( origin );
		Assert.IsTrue( road == null || building == null );
		if ( road )
		{
			Assert.IsTrue( road.workers.Contains( this ) );
			int point = road.NodeIndex( node );
			Assert.AreEqual( road.workerAtNodes[point], this );
		}
		if ( itemInHands )
		{
			Assert.AreEqual( itemInHands.worker, this );	
			Assert.IsNull( itemInHands.flag );
			itemInHands.Validate();
		}
		foreach ( Task task in taskQueue )
			task.Validate();
		if ( exclusiveFlag )
		{
			Assert.AreEqual( type, Type.haluer );
			Assert.IsTrue( atRoad );
			Assert.IsNotNull( road );
			Assert.AreEqual( exclusiveFlag.user, this );
		}
	}
}