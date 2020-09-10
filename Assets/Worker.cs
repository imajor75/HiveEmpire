using System;
using UnityEngine;
using UnityEngine.Assertions;

public class Worker : MonoBehaviour
{
	public bool debug;
	public TaskType task;
	public Ground ground;
	public Road currentRoad;
	[HideInInspector]
	public GroundNode walkFrom;
	[HideInInspector]
	public GroundNode walkTo;
	public float walkProgress;
	public Flag targetFlag;
	public GroundNode targetNode;
	public GroundNode currentNode;
	public PathFinder path;
	public int pathProgress;
	public Type type;

	public Item item;
	public bool handsFull = false;

	public Building construction;

	public Road road;
	public int roadPointTarget;
	public int currentPoint;
	public int wishedPoint = -1;
	public Building offroadToBuilding;
	public bool atRoad;

	public Building building;
	public bool inside; // Used only for building workers

	public Animator animator;
	public static GameObject templateWoman;
	public static GameObject templateMan;
	public static GameObject templateBoy;
	public static RuntimeAnimatorController idleController, walkingController;

	public enum TaskType
	{
		nothing,
		doOneStep,
		reachRoadPoint,
		pickUpItem,
		dropItem,
		reachFlag,
		reachNode
	}

	public enum Type
	{
		haluer,
		tinkerer,
		constructor,
		idle
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
		this.road = road;
		// TODO Select the end closer to the starting point
		Building main = road.ground.mainBuilding;
		SetPosition( main.node, road.GetEnd( 0 ) );
		StepTo( main.flag.node );
		currentPoint = 0;
		ground = road.ground;
		return this;
	}

	public Worker SetupForBuilding( Building building )
	{
		type = Type.tinkerer;
		this.building = building;
		currentNode = building.node;
		inside = false;
		Building main = building.ground.mainBuilding;
		if ( building != main )
		{
			SetPosition( main.node, building.flag );
			StepTo( main.flag.node );
		}
		else
			inside = true;
		ground = building.ground;
		return this;
	}

	public Worker SetupForConstruction( Building building )
	{
		type = Type.constructor;
		construction = building;
		Building main = building.ground.mainBuilding;
		SetPosition( main.node, building.flag );
		StepTo( main.flag.node );
		ground = building.ground;
		return this;
	}

	// Start is called before the first frame update
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

	// Update is called once per frame
	void FixedUpdate()
	{
		// If worker is between two nodes, simply advancing it
		if ( walkTo != null )
		{
			walkProgress += 0.015f*ground.speedModifier; // TODO Speed should depend on the steepness of the road
			if ( walkProgress >= 1 )
			{
				if ( building && walkTo == building.node )
					inside = true;
				if ( construction && walkTo == construction.node )
					inside = true;
				walkTo = walkFrom = null;
				walkProgress -= 1;
			}
		}
		if ( walkTo == null )
		{
			if ( task == TaskType.nothing )
				FindTask();
		}
		if ( walkTo == null )
			ExecuteCurrentTask();
		UpdateBody();
	}

	public void ExecuteCurrentTask()
	{
		if ( task == TaskType.nothing )
			return;

		if ( task == TaskType.reachRoadPoint )
		{
			if ( currentPoint == roadPointTarget )
			{
				task = TaskType.nothing;
				FindTask();
			}
			else
				NextStep();
			return;
		}
		if ( task == TaskType.reachFlag )
		{
			if ( currentNode.flag == targetFlag )
			{
				task = TaskType.nothing;
				path = null;
				targetFlag = null;
				FindTask();
				return;
			}
			if ( path == null )
			{
				path = new PathFinder();
				if ( !path.FindPathBetween( currentNode, targetFlag.node, PathFinder.Mode.onRoad ) )
				{
					path = null;
					task = TaskType.nothing;
					WalkToNode( targetFlag.node );
					return;
				}
				pathProgress = 0;
				currentPoint = roadPointTarget = 0;
			}
			if ( currentPoint == roadPointTarget )
			{
				Road next = path.roadPath[pathProgress++];
				Flag start = next.GetEnd( 0 ), end = next.GetEnd( 1 );
				if ( start == currentNode.flag )
				{
					currentPoint = 0;
					roadPointTarget = next.nodes.Count - 1;
					currentNode = end.node;
				}
				else
				{
					Assert.AreEqual( end, currentNode.flag );
					currentPoint = next.nodes.Count - 1;
					roadPointTarget = 0;
					currentNode = start.node;
				}
				currentRoad = next;
			}
			NextStep( false );
			return;
		}
		if ( task == TaskType.reachNode )
		{
			if ( currentNode == targetNode )
			{
				task = TaskType.nothing;
				targetNode = null;
				path = null;
				FindTask();
				return;
			}
			if ( path == null )
			{
				path = new PathFinder();
				if ( !path.FindPathBetween( currentNode, targetNode, PathFinder.Mode.avoidObjects ) )
				{
					path = null;
					return;
				}
				pathProgress = 1;
			}
			StepTo( path.path[pathProgress++] );
			task = TaskType.reachNode;	// HACK Need task queue
			return;
		}
		if ( task == TaskType.doOneStep )
		{
			task = TaskType.nothing;
			return;
		}

		Assert.IsTrue( false );
	}

	public bool NextStep( bool exclusive = true )
	{
		if ( road && atRoad )
			Assert.AreEqual( road.workerAtNodes[currentPoint], this );
		Assert.IsNull( walkTo );

		if ( currentPoint == roadPointTarget )
			return false;

		int nextPoint;
		if ( currentPoint < roadPointTarget )
			nextPoint = currentPoint + 1;
		else
			nextPoint = currentPoint - 1;

		if ( exclusive )
		{
			Flag flag = currentRoad.nodes[nextPoint].flag;
			if ( flag )
			{
				if ( flag.user && flag.user.wishedPoint != currentPoint )
					return false;
				flag.user = this;
			}
			currentRoad.workerAtNodes[currentPoint] = null;
			if ( currentRoad.workerAtNodes[nextPoint] != null )
			{
				var otherWorker = currentRoad.workerAtNodes[nextPoint];
				if ( otherWorker.wishedPoint == currentPoint )
				{
					// TODO Workers should avoid each other
					bool coming = otherWorker.NextStep();
					Assert.IsTrue( coming );
				}
				else
				{
					road.workerAtNodes[currentPoint] = this;
					wishedPoint = nextPoint;
					return false;
				}
			}
		}

		wishedPoint = -1;
		walkFrom = currentRoad.nodes[currentPoint];
		currentNode = walkTo = currentRoad.nodes[nextPoint];
		if ( walkFrom.flag && walkFrom.flag.user == this )
			walkFrom.flag.user = null;
		currentPoint = nextPoint;
		if ( exclusive )
			road.workerAtNodes[currentPoint] = this;
		return true;
	}

	public void Remove()
	{
		if ( handsFull )
		{
			item.Remove();
			handsFull = false;
		}
		else
		{
			if ( item )
			{
				Assert.AreEqual( this, item.worker );
				item.worker = null;
				item = null;
			}
		}
		if ( road != null && atRoad )
		{
			GroundNode point = road.nodes[currentPoint];
			Assert.AreEqual( road.workerAtNodes[currentPoint], this );
			road.workerAtNodes[currentPoint] = null;
			Flag flag = road.nodes[currentPoint].flag;
			if ( flag )
			{
				Assert.AreEqual( flag.user, this );
				flag.user = null;
			}
			road.workers.Remove( this );
			targetNode = road.nodes[0];
		}
		currentRoad = road = null;
		building = null;
		construction = null;
		type = Type.idle;
		task = TaskType.nothing;
	}

	public void StepTo( GroundNode target )
	{
		Assert.IsTrue( currentNode.DirectionTo( target ) >= 0, "Target node " + target.x + ", " + target.y + " is not adjacent" );
		walkTo = target;
		walkFrom = currentNode;
		currentNode = target;
		task = TaskType.doOneStep;
	}

	public void FindTask()
	{
		if ( debug )
		{
			int h = 9;
		}

		Assert.AreEqual( task, TaskType.nothing );
		if ( targetNode != null )
		{
			WalkToNode( targetNode );
			return;
		}
		if ( targetFlag != null )
		{
			if ( currentNode.flag == null )
			{
				WalkToNode( targetFlag.node );
				targetFlag = null;
			}
			else
				WalkToFlag( targetFlag, currentNode.flag );
			return;
		}
		if ( road && !atRoad )
		{
			int i = road.NodeIndex( currentNode );
			Assert.IsTrue( i >= 0 );
			if ( road.workerAtNodes[i] == null )
			{
				road.workerAtNodes[i] = this;
				atRoad = true;
			}
			return;
		}
		if ( handsFull )
		{
			if ( road != null && offroadToBuilding == null && item.AtFinalFlag() )
			{
				StepTo( item.destination.node );
				offroadToBuilding = item.destination;
				return;
			}
			Flag flag;
			if ( building )
				flag = building.flag;
			else
				flag = road.GetEnd( currentPoint );
			Assert.IsNotNull( flag );
			item.ArrivedAt( flag );
			item = null;
			handsFull = false;
		}
		if ( offroadToBuilding )
		{
			Assert.IsNotNull( road );
			StepTo( road.nodes[currentPoint] );
			offroadToBuilding = null;
			return;
		}
		if ( building && !inside )
		{
			StepTo( building.node );
			return;
		}
		if ( construction && !inside )
		{
			StepTo( construction.node );
			return;
		}
		if ( item != null )
		{
			// Picking up item
			Assert.AreEqual( road.GetEnd( currentPoint ), item.flag );
			item.flag.ReleaseItem( item );
			handsFull = true;
			if ( currentPoint == 0 )
				WalkToRoadPoint( road.nodes.Count - 1 );
			else
				WalkToRoadPoint( 0 );
			return;
		}

		// TODO Pick the most important item rather than the first available

		if ( road != null )
		{
			foreach ( var item in road.GetEnd( 0 ).items )
				CheckItem( item );
			foreach ( var item in road.GetEnd( 1 ).items )
				CheckItem( item );
			if ( item != null )
				return;
		}

		if ( road != null && currentPoint != road.nodes.Count / 2 && road.workers.Count == 1 )
		{
			WalkToRoadPoint( road.nodes.Count / 2 );
			return;
		}

		if ( type == Type.idle )
		{
			if ( currentNode == ground.mainBuilding.node )
			{
				Destroy( this );
				return;
			}
			if ( currentNode == ground.mainBuilding.flag.node )
			{
				StepTo( ground.mainBuilding.node );
				return;
			}
			targetFlag = ground.mainBuilding.flag;
			if ( inside )
			{
				for ( int i = 0; i < GroundNode.neighbourCount; i++ )
				{
					Flag flag = currentNode.Neighbour( i ).flag;
					if ( flag != null )
					{
						StepTo( flag.node );
						inside = false;
						return;
					}
				}
			}
		}

		return;
	}

	public void CheckItem( Item item )
	{
		if ( this.item )
			return;
		if ( item == null || item.worker || item.destination == null )
			return;

		if ( item.path == null || item.NextRoad() != road )
			return;
		CarryItem( item );
	}

	public void CarryItem( Item item, GroundNode destination = null )
	{
		Assert.IsFalse( handsFull );
		Assert.IsTrue( item.flag != null || building != null );
		item.worker = this;
		this.item = item;
		if ( destination == null )
		{
			//Assert.IsNotNull( item.flag );
			WalkToRoadPoint( road.NodeIndex( item.flag.node ) );
		}
		else
		{
			Assert.IsNull( item.flag );
			StepTo( destination );
			handsFull = true;
		}
		inside = false;
	}

	public void WalkToRoadPoint( int index )
	{
		Assert.AreEqual( task, TaskType.nothing );
		Assert.IsTrue( index >= 0 && index <= road.nodes.Count );
		roadPointTarget = index;
		walkProgress = 0;
		task = TaskType.reachRoadPoint;
		currentRoad = road;
		NextStep();
	}

	public void WalkToFlag( Flag flag, Flag from )
	{
		Assert.AreEqual( task, TaskType.nothing );
		task = TaskType.reachFlag;
		targetFlag = flag;
		currentNode = from.node;
	}

	public void WalkToNode( GroundNode node )
	{
		Assert.AreEqual( task, TaskType.nothing );
		task = TaskType.reachNode;
		targetNode = node;
	}

	public void SetPosition( GroundNode position, Flag target )
	{
		currentNode = position;
		targetFlag = target;
	}

	static float[] angles = new float[6] { 210, 150, 90, 30, 330, 270 };
	public void UpdateBody()
	{
		if ( walkTo == null )
		{
			if ( animator != null && animator.runtimeAnimatorController == walkingController )
				animator.runtimeAnimatorController = idleController;

			if ( currentNode != null )
				transform.localPosition = currentNode.Position();
			return;
		}
		else
			if ( animator != null && animator.runtimeAnimatorController == idleController )
			animator.runtimeAnimatorController = walkingController;

		if ( item && handsFull )
			item.UpdateLook();

		transform.localPosition = Vector3.Lerp( walkFrom.Position(), walkTo.Position(), walkProgress );
		int direction = walkFrom.DirectionTo( walkTo );
		Assert.IsTrue( direction >= 0 );
		transform.rotation = Quaternion.Euler( Vector3.up * angles[direction] );
	}

	public void Validate()
	{
		Assert.IsTrue( road || building );
		Assert.IsTrue( road == null || building == null );
		Assert.IsTrue( !handsFull || item );
		if ( item )
		{
			Assert.AreEqual( item.worker, this );
			if ( handsFull )
				Assert.IsNull( item.flag );
			else
				Assert.IsNotNull( item.flag );
			item.Validate();
		}
		if ( road )
		{
			Assert.IsTrue( road.workers.Contains( this ) );
			if ( atRoad )
			{
				Assert.IsTrue( currentPoint >= 0 && currentPoint < road.nodes.Count );
				int t = 0;
				for ( int i = 0; i < road.workerAtNodes.Length; i++ )
				{
					if ( road.workerAtNodes[i] == this )
					{
						t++;
						Assert.AreEqual( i, currentPoint );
					}
				}
				Assert.AreEqual( t, 1 );

				Assert.IsTrue( roadPointTarget >= 0 && roadPointTarget < road.nodes.Count );
				if ( wishedPoint >= 0 )
				{
					Assert.IsTrue( wishedPoint <= road.nodes.Count );
					Assert.AreEqual( Math.Abs( wishedPoint - currentPoint ), 1 );
				}
			}
		}
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

