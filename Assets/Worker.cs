using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;

public class Worker : MonoBehaviour
{
	public Type type;
	public Ground ground;
	public GroundNode walkFrom = zero;
	public GroundNode walkTo = zero;
	public float walkProgress;
	public GroundNode node;
	public Item itemInHands;

	public Building construction;

	public Road road;
	public bool atRoad;

	public Building building;

	public Animator animator;
	public static GameObject templateWoman;
	public static GameObject templateMan;
	public static GameObject templateBoy;
	public static RuntimeAnimatorController idleController, walkingController;
	public static GroundNode zero = new GroundNode();	// HACK This is a big fat hack, to stop Unity editor from crashing

	public List<Task> taskQueue = new List<Task>();

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
		public void AddTask( Task newTask, bool asFirst )
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
			boss.ScheduleWalkToRoadPoint( path.Road(), point, true, false );
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

		public void Setup( Worker boss, GroundNode target )
		{
			base.Setup( boss );
			this.target = target;
		}
		public override bool ExecuteFrame()
		{
			if ( boss.node == target )
				return true;

			if ( path == null )
			{
				path = Path.Between( boss.node, target, PathFinder.Mode.avoidObjects );
				if ( path == null )
					return false;
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
						var otherTask = flag.user.taskQueue[0] as WalkToRoadPoint;
						if ( otherTask != null && otherTask.wishedPoint != currentPoint )
							return false;
					}
					flag.user = boss;
				}
				road.workerAtNodes[currentPoint] = null;
				if ( road.workerAtNodes[nextPoint] != null )
				{
					var otherWorker = road.workerAtNodes[nextPoint];
					var otherTask = otherWorker.taskQueue[0] as WalkToRoadPoint;
					if ( otherTask && otherTask.wishedPoint == currentPoint )
					{
						// TODO Workers should avoid each other
						bool coming = otherTask.NextStep();
						Assert.IsTrue( coming, "Other worker is not coming" );
					}
					else
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
				road.workerAtNodes[currentPoint] = boss;
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
				for ( int i = 0; i < road.workerAtNodes.Length; i++ )
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
			if ( item.destination.node == boss.node )
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

	public enum Type
	{
		haluer,
		tinkerer,
		constructor,
		unemployed
	}

	public static void Initialize()
	{
		templateWoman = (GameObject)Resources.Load( "Polytope Studio/Lowpoly Medieval Characters/Prefabs/PT_Medieval_Female_Peasant_01_a" );
		Assert.IsNotNull( templateWoman );
		templateMan = (GameObject)Resources.Load( "Polytope Studio/Lowpoly Medieval Characters/Prefabs/PT_Medieval_Male_Peasant_01_a" );
		Assert.IsNotNull( templateMan );
		templateBoy = (GameObject)Resources.Load( "Polytope Studio/Lowpoly Medieval Characters/Prefabs/PT_Medieval_Boy_Peasant_01_a" );
		Assert.IsNotNull( templateBoy );
		templateWoman = (GameObject)Resources.Load( "Polytope Studio/Lowpoly Medieval Characters/Prefabs/PT_Medieval_Female_Peasant_01_a" );
		Assert.IsNotNull( templateWoman );
		idleController = (RuntimeAnimatorController)Resources.Load( "Kevin Iglesias/Basic Motions Pack/AnimationControllers/BasicMotions@Idle" );
		Assert.IsNotNull( idleController );
		walkingController = (RuntimeAnimatorController)Resources.Load( "Kevin Iglesias/Basic Motions Pack/AnimationControllers/BasicMotions@Walk" );
		Assert.IsNotNull( walkingController );
	}

	static public Worker Create()
	{
		GameObject workerBody = (GameObject)GameObject.Instantiate( templateMan );
		workerBody.name = "Worker";
		Worker worker = workerBody.AddComponent<Worker>();
		worker.transform.localScale *= 0.35f;
		return worker;
	}

	public Worker SetupForRoad( Road road )
	{
		type = Type.haluer;
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
		return SetupForBuildingSite( building );
	}

	public Worker SetupForConstruction( Building building )
	{
		type = Type.constructor;
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

	void Start()
	{
		if ( road != null )
			transform.SetParent( road.ground.transform );
		if ( building != null )
			transform.SetParent( building.ground.transform );

		animator = GetComponent<Animator>();
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
			if ( taskQueue.Count == 0 )
				FindTask();
		}
		UpdateBody();
	}

	public void Remove()
	{
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
		// TODO Pick the most important item rather than the first available

		if ( road != null )
		{
			foreach ( var item in road.GetEnd( 0 ).items )
				CheckItem( item );
			foreach ( var item in road.GetEnd( 1 ).items )
				CheckItem( item );
			if ( taskQueue.Count != 0 )
				return;
		}

		if ( road != null && node != road.CenterNode() && road.workers.Count == 1 )
		{
			ScheduleWalkToRoadPoint( road, road.nodes.Count / 2 );
			return;
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

	public void CheckItem( Item item )
	{
		if ( taskQueue.Count != 0 )
			return;
		if ( item == null || item.worker || item.destination == null )
			return;

		if ( item.path == null || item.path.Road() != road )
			return;
		CarryItem( item );
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

	public void ScheduleWalkToNode( GroundNode target, bool first = false )
	{
		var instance = ScriptableObject.CreateInstance<WalkToNode>();
		instance.Setup( this, target );
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

	public void ScheduleDeliverItem( Item item, bool first = false )
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

	public void CarryItem( Item item )
	{
		Assert.IsNotNull( road );
		Assert.AreEqual( road, item.path.Road() );
		int itemPoint = road.NodeIndex( item.flag.node );
		ScheduleWalkToRoadPoint( road, itemPoint );
		SchedulePickupItem( item );
		if ( itemPoint > 0 )
			ScheduleWalkToRoadPoint( road, 0 );
		else
			ScheduleWalkToRoadPoint( road, road.nodes.Count - 1 );

		if ( item.path.StepsLeft() == 1 )
		{
			var destination = item.destination;
			ScheduleWalkToNeighbour( destination.node );
			ScheduleDeliverItem( item );
			ScheduleWalkToNeighbour( destination.flag.node );
		}
		else
			ScheduleDeliverItem( item );
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

		transform.localPosition = Vector3.Lerp( walkFrom.Position(), walkTo.Position(), walkProgress );
		int direction = walkFrom.DirectionTo( walkTo );
		Assert.IsTrue( direction >= 0 );
		transform.rotation = Quaternion.Euler( Vector3.up * angles[direction] );
	}

	public bool IsIdleInBuilding()
	{
		Assert.IsNotNull( building );
		return node == building.node && walkTo == zero;
	}

	public void Validate()
	{
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

public class WorkerMan : Worker
{
	new static public Worker Create()
	{
		GameObject workerBody = (GameObject)GameObject.Instantiate( templateMan );
		workerBody.name = "Worker";
		Worker worker = workerBody.AddComponent<WorkerMan>();
		worker.transform.localScale *= 0.35f;
		return worker;
	}
}

public class WorkerWoman : Worker
{
	new static public Worker Create()
	{
		GameObject workerBody = (GameObject)GameObject.Instantiate( templateWoman );
		workerBody.name = "Worker";
		Worker worker = workerBody.AddComponent<WorkerWoman>();
		worker.transform.localScale *= 0.35f;
		return worker;
	}
}

public class WorkerBoy : Worker
{
	new static public Worker Create()
	{
		GameObject workerBody = (GameObject)GameObject.Instantiate( templateBoy );
		workerBody.name = "Worker";
		Worker worker = workerBody.AddComponent<WorkerBoy>();
		worker.transform.localScale *= 0.35f;
		return worker;
	}
}

