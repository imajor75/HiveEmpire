using System;
using UnityEngine;
using UnityEngine.Assertions;

public class Worker : MonoBehaviour
{
	public Road road;
	public Building building;
	public bool inside;	// Used only for building workers
	public int roadPointGoal;
	public int currentPoint;
	public GroundNode walkFrom;
	public GroundNode walkTo;
	public float walkProgress;
	public Item item;
	public bool handsFull = false;
	public int wishedPoint = -1;
	public static GameObject templateWoman;
	public static GameObject templateMan;
	public static GameObject templateBoy;
	public Animator animator;
	static RuntimeAnimatorController idleController, walkingController;
	public Building offroadToBuilding;

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

	public Worker SetupForRoad( Road road = null )
	{
		int i = -1;
		i = road.nodes.Count / 2;
		while ( i < road.nodes.Count - 1 && road.workerAtNodes[i] != null )
			i++;
		if ( road.workerAtNodes[i] != null )
		{
			Destroy( gameObject );
			return null;
		}
		
		this.road = road;
		currentPoint = roadPointGoal = i;
		road.workerAtNodes[i] = this;
		UpdateBody();	//??
		return this;
	}

	public Worker SetupForBuilding( Building building )
	{
		this.building = building;
		inside = true;
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
			walkProgress += 0.015f; // TODO Speed should depend on the steepness of the road
			if ( walkProgress >= 1 )
			{
				if ( building && walkTo == building.node )
					inside = true;
				walkTo = walkFrom = null;
				walkProgress -= 1;
			}
		}
		if ( walkTo == null )
		{
			if ( building && !inside )
			{
				StepTo( building.node );
			}
			if ( currentPoint == roadPointGoal )
			{
				if ( !offroadToBuilding && item != null && handsFull && item.pathProgress == item.path.roadPath.Count - 1 )
				{
					StepTo( item.destination.node );
					offroadToBuilding = item.destination;
				}
				else
				{
					if ( !FindGoal() && road != null && road.workers.Count > 1 )
						Remove();
				}
			}
			else
				NextStep();
		}
		UpdateBody();
	}

	public bool NextStep()
	{
		Assert.AreEqual( road.workerAtNodes[currentPoint], this );
		if ( offroadToBuilding )
		{
			StepTo( road.nodes[currentPoint] );
			offroadToBuilding = null;
			return true;
		}
		if ( currentPoint == roadPointGoal )
			return false;

		int nextPoint;
		if ( currentPoint < roadPointGoal )
			nextPoint = currentPoint + 1;
		else
			nextPoint = currentPoint - 1;

		Flag flag = road.nodes[nextPoint].flag;
		if ( flag )
		{
			if ( flag.user && flag.user.wishedPoint != currentPoint )
				return false;
			flag.user = this;
		}
		road.workerAtNodes[currentPoint] = null;
		if ( road.workerAtNodes[nextPoint] != null )
		{
			var otherWorker = road.workerAtNodes[nextPoint];
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

		wishedPoint = -1;
		walkFrom = road.nodes[currentPoint];
		walkTo = road.nodes[nextPoint];
		if ( walkFrom.flag && walkFrom.flag.user == this )
			walkFrom.flag.user = null;
		currentPoint = nextPoint;
		road.workerAtNodes[currentPoint] = this;
		return true;
	}

	public void Remove()
	{
		GroundNode point = road.nodes[currentPoint];
		Assert.AreEqual( road.workerAtNodes[currentPoint], this );
		if ( handsFull )
			item.Remove();
		road.workerAtNodes[currentPoint] = null;
		Flag flag = road.nodes[currentPoint].flag;
		if ( flag )
		{
			Assert.AreEqual( flag.user, this );
			flag.user = null;
		}
		road.workers.Remove( this );
		Destroy( gameObject );
	}

	void StepTo( GroundNode target )
	{
		GroundNode current;
		if ( building != null )
		{
			if ( inside )
				current = building.node;
			else
				current = building.flag.node;
		}
		else
		{
			if ( offroadToBuilding )
				current = offroadToBuilding.node;
			else
				current = road.nodes[currentPoint];
		}

		Assert.IsTrue( current.DirectionTo( target ) >= 0 );
		walkTo = target;
		walkFrom = current;
	}

	public bool FindGoal()
	{
		if ( item != null )
		{
			if ( handsFull )
			{
				Flag flag;
				if ( building )
					flag = building.flag;
				else
					flag = road.GetEnd( currentPoint );
				Assert.IsNotNull( flag );
				item.ArrivedAt( flag );
				item = null;
				handsFull = false;
				if ( !FindGoal() && road != null )
					WalkToRoadPoint( road.nodes.Count / 2 );
			}
			else
			{
				// Picking up item
				Assert.AreEqual( road.GetEnd( currentPoint ), item.flag );
				item.flag.ReleaseItem( item );
				handsFull = true;
				if ( currentPoint == 0 )
					WalkToRoadPoint( road.nodes.Count - 1 );
				else
					WalkToRoadPoint( 0 );
			}
			return true;
		}

		// TODO Pick the most important item rather than the first available

		if ( road != null )
		{
			foreach ( var item in road.GetEnd( 0 ).items )
				CheckItem( item );
			foreach ( var item in road.GetEnd( 1 ).items )
				CheckItem( item );
			return item != null;
		}
		if ( building != null && !inside )
		{
			StepTo( building.node );
			return true;
		}
		return false;
	}

	public void CheckItem( Item item )
	{
		if ( this.item )
			return;
		if ( item == null || item.worker )
			return;

		if ( item.path == null || item.path.roadPath[item.pathProgress] != road )
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
			Assert.IsNotNull( item.flag );
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
		Assert.IsTrue( index >= 0 && index <= road.nodes.Count );
		roadPointGoal = index;
		walkProgress = 0;
		NextStep();
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
		Assert.IsTrue( road.workers.Contains( this ) );
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

		Assert.IsTrue( roadPointGoal >= 0 && roadPointGoal < road.nodes.Count );
		if ( wishedPoint >= 0 )
		{
			Assert.IsTrue( wishedPoint <= road.nodes.Count );
			Assert.AreEqual( Math.Abs( wishedPoint - currentPoint ), 1 );
		}
	}

	static float[] angles = new float[6] { 210, 150, 90, 30, 330, 270 };
	public void UpdateBody()
	{
		if ( walkTo == null )
		{
			if ( animator != null && animator.runtimeAnimatorController == walkingController )
				animator.runtimeAnimatorController = idleController;

			if ( road != null )
				transform.localPosition = road.nodes[currentPoint].Position();
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
}

public class WorkerMan : Worker
{
	new static public Worker Create()
	{
		GameObject workerBody = (GameObject)GameObject.Instantiate( templateMan );
		workerBody.name = "Worker";
		Worker worker = workerBody.AddComponent<Worker>();
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
		Worker worker = workerBody.AddComponent<Worker>();
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
		Worker worker = workerBody.AddComponent<Worker>();
		worker.transform.localScale *= 0.35f;
		return worker;
	}
}

