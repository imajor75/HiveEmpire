using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using UnityEngine;

public class Worker : MonoBehaviour
{
	public Type type;
	public Ground ground;
	public GroundNode walkFrom = zero;
	public GroundNode walkTo = zero;
	public float walkProgress;
	public GroundNode node;
	public Item itemInHands;
	public Flag reservation;
	public int look;
	public Resource origin;

	public Building construction;

	public Road road;
	public bool atRoad;

	public Building building;

	public Animator animator;
	public static List<GameObject> templates = new List<GameObject>();
	public static RuntimeAnimatorController idleController, walkingController;
	public static GroundNode zero = new GroundNode();	// HACK This is a big fat hack, to stop Unity editor from crashing

	public List<Task> taskQueue = new List<Task>();
	GameObject body;

	public class Task : ScriptableObject
	{
		public Worker boss;

		public void Setup( Worker boss )
		{
			this.boss = boss;
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
				return false;

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
					if ( flag.user && flag.user.taskQueue.Count > 0 )
					{
						var otherTask = flag.user.taskQueue[0] as WalkToRoadPoint;
						if ( otherTask != null && otherTask.wishedPoint != currentPoint )
							return false;
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
			currentPoint = nextPoint;
			if ( exclusive )
			{
				road.workerAtNodes[currentPoint] = boss;
				if ( boss.walkTo.flag )
					boss.walkTo.flag.user = boss;
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
		public Item item;

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
		public Item item;

		public void Setup( Worker boss, Item item )
		{
			base.Setup( boss );
			this.item = item;
		}
		public override bool ExecuteFrame()
		{
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
			if ( road.workerAtNodes[i] == null )
			{
				road.workerAtNodes[i] = boss;
				boss.road = road;
				return true;
			}
			return false;
		}
	}

	public class Pasture : Task
	{
		public Resource resource;
		public int timer;
		public override bool ExecuteFrame()
		{
			if ( resource == null )
			{
				resource = Resource.Create().SetupAsPrey( boss );
				timer = 100;
				return false;
			}
			if ( timer-- > 0 )
				return false;

			if ( resource.hunter == null )
			{
				resource.animals.Clear();
				resource.Remove();
				return true;
			}
			return false;

		}

		public override void Cancel()
		{
			if ( resource )
			{
				resource.animals.Clear();
				resource.Remove();
			}
			base.Cancel();
		}

		public override void Validate()
		{
			if ( resource )
			{
				Assert.AreEqual( resource.type, Resource.Type.pasturingAnimal );
				Assert.AreEqual( resource.node, boss.node );
			}
			base.Validate();
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
		
		idleController = (RuntimeAnimatorController)Resources.Load( "Kevin Iglesias/Basic Motions Pack/AnimationControllers/BasicMotions@Idle" );
		Assert.IsNotNull( idleController );
		walkingController = (RuntimeAnimatorController)Resources.Load( "Kevin Iglesias/Basic Motions Pack/AnimationControllers/BasicMotions@Walk" );
		Assert.IsNotNull( walkingController );
	}

	static public Worker Create()
	{
		GameObject workerBody = new GameObject();
		workerBody.name = "Worker";
		Worker worker = workerBody.AddComponent<Worker>();
		return worker;
	}

	public Worker SetupForRoad( Road road )
	{
		type = Type.haluer;
		look = 2;
		ground = road.ground;
		Building main = road.ground.mainBuilding;
		node = main.node;
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
		Building main = ground.mainBuilding;
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
		animator = body.GetComponent<Animator>();
		animator.runtimeAnimatorController = idleController;
		UpdateBody();
	}

	public void Walk( GroundNode target )
	{
		Assert.IsTrue( node.DirectionTo( target ) >= 0, "Trying to walk to a distant node" );
		walkFrom = node;
		node = walkTo = target;

		if ( walkFrom.flag && walkFrom.flag.user == this )
			walkFrom.flag.user = null;
	}

	// Update is called once per frame
	void FixedUpdate()
	{
		// If worker is between two nodes, simply advancing it
		if ( walkTo != zero )
		{
			walkProgress += 0.015f*ground.speedModifier; // TODO Speed should depend on the steepness of the road
			if ( walkProgress >= 1 )
			{
				walkTo = walkFrom = zero;
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

	public void Remove()
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
			itemInHands = null;
		}
		if ( road != null && atRoad )
		{
			int currentPoint = road.NodeIndex( node );
			Assert.AreEqual( road.workerAtNodes[currentPoint], this );
			road.workerAtNodes[currentPoint] = null;
			Flag flag = node.flag;
			if ( flag )
			{
				Assert.AreEqual( flag.user, this );
				flag.user = null;
			}
			road.workers.Remove( this );
			// TODO Pick the closer end
			ScheduleWalkToRoadPoint( road, 0 );
		}
		road = null;
		building = null;
		construction = null;
		type = Type.unemployed;
	}

	public void FindTask()
	{
		Assert.AreEqual( taskQueue.Count, 0 );
		if ( road != null )
		{
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
			int r = Ground.rnd.Next( 6 );
			var d = Ground.areas[1];
			for ( int i = 0; i < d.Count; i++ )
			{
				GroundNode t = node.Add( d[(i+r)%d.Count] );
				if ( t.building || t.resource )
					continue;
				if ( t.DistanceFrom( origin.node ) > 8 )
					continue;
				SchedulePasture();
				Walk( t );
				return;
			}
		}

		if ( type == Type.unemployed )
		{
			if ( node == ground.mainBuilding.node )
			{
				if ( walkTo == zero )
					Destroy( gameObject );
				return;
			}
			if ( node == ground.mainBuilding.flag.node )
			{
				ScheduleWalkToNeighbour( ground.mainBuilding.node );
				return;
			}
			ScheduleWalkToFlag( ground.mainBuilding.flag );
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

	public void SchedulePasture( bool first = false )
	{
		var instance = ScriptableObject.CreateInstance<Pasture>();
		instance.Setup( this );
		if ( first )
			taskQueue.Insert( 0, instance );
		else
			taskQueue.Add( instance );
	}

	public void ScheduleTask( Task task )
	{
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

	static float[] angles = new float[6] { 210, 150, 90, 30, 330, 270 };
	public void UpdateBody()
	{
		if ( walkTo == zero )
		{
			if ( animator != null && animator.runtimeAnimatorController == walkingController )
				animator.runtimeAnimatorController = idleController;

			transform.localPosition = node.Position();
			return;
		}
		else
			if ( animator != null && animator.runtimeAnimatorController == idleController )
			animator.runtimeAnimatorController = walkingController;

		if ( itemInHands )
			itemInHands.UpdateLook();

		transform.localPosition = Vector3.Lerp( walkFrom.Position(), walkTo.Position(), walkProgress ) + Vector3.up * GroundNode.size * Road.height;
		int direction = walkFrom.DirectionTo( walkTo );
		Assert.IsTrue( direction >= 0 );
		transform.rotation = Quaternion.Euler( Vector3.up * angles[direction] );
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
			Assert.IsTrue( road.workers.Contains( this ) );
		if ( itemInHands )
		{
			Assert.AreEqual( itemInHands.worker, this );
			Assert.IsNull( itemInHands.flag );
			itemInHands.Validate();
		}
		foreach ( Task task in taskQueue )
			task.Validate();
	}
}