using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

public class Worker : MonoBehaviour
{
	public Road road;
	public int roadPointGoal;
	public int currentPoint;
	public GroundNode walkFrom;
	public GroundNode walkTo;
	public float walkProgress;
	public Item item;
	public bool handsFull = false;

	static public Worker Create( Ground ground, Road road )
	{
		GameObject workerBody = GameObject.CreatePrimitive( PrimitiveType.Cylinder );
		workerBody.name = "Worker";
		Worker worker = workerBody.AddComponent<Worker>();
		worker.road = road;
		worker.currentPoint = road.Length() / 2;
		worker.transform.SetParent( ground.transform );
		worker.transform.localScale *= 0.3f;
		worker.UpdateBody();
		return worker;
	}

    // Start is called before the first frame update
    void Start()
    {
        
    }

	// Update is called once per frame
	void FixedUpdate()
	{
		// If worker is between two nodes, simply advancing it
		if ( walkTo != null )
		{
			walkProgress += 0.015f;
			if ( walkProgress >= 1 )
			{
				walkProgress -= 1;
				currentPoint = road.NodeIndex( walkTo );
				walkTo = walkFrom = null;
				if ( !NextStep() )
					FindGoal();
			}
			UpdateBody();
			return;
		}
		FindGoal();
	}

	public bool NextStep()
	{
		if ( currentPoint == roadPointGoal )
			return false;

		int nextPoint;
		if ( currentPoint < roadPointGoal )
			nextPoint = currentPoint + 1;
		else
			nextPoint = currentPoint - 1;

		walkFrom = road.nodes[currentPoint];
		walkTo = road.nodes[nextPoint];
		return true;
	}

	public void FindGoal()
	{
		if ( item != null )
		{
			if ( handsFull )
			{
				Flag flag = road.GetEnd( currentPoint );
				Assert.IsNotNull( flag );
				flag.StoreItem( item );
				item.UpdateLook();
				item.worker = null;
				item = null;
				handsFull = false;
				FindGoal();
			}
			else
			{
				// Picking up item
				Assert.AreEqual( road.GetEnd( currentPoint ), item.flag );
				item.flag.ReleaseItem( item );
				handsFull = true;
				if ( currentPoint == 0 )
					WalkToRoadPoint( road.Length() - 1 );
				else
					WalkToRoadPoint( 0 );
			}
			return;
		}

		// TODO Pick the most important item rather than the first available

		foreach ( var item in road.GetEnd( 0 ).items )
			CheckItem( item );
		foreach ( var item in road.GetEnd( 1 ).items )
			CheckItem( item );
	}

	public void CheckItem( Item item )
	{
		if ( this.item )
			return;
		if ( item == null || item.worker )
			return;

		// TODO Check the result of the path finding, to see if the item should be carried this way
		CarryItem( item );
	}

	public void CarryItem( Item item )
	{
		Assert.IsFalse( handsFull );
		Assert.IsNotNull( item.flag );
		item.worker = this;
		this.item = item;
		WalkToRoadPoint( road.NodeIndex( item.flag.node ) );
	}

	public void WalkToRoadPoint( int index )
	{
		Assert.IsTrue( index >= 0 && index <= road.Length() );
		roadPointGoal = index;
		walkProgress = 0;
		NextStep();
	}

	public void Validate()
	{
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
		Assert.AreEqual( road.worker, this );
		Assert.IsTrue( currentPoint >= 0 && currentPoint < road.Length() );
		Assert.IsTrue( roadPointGoal >= 0 && roadPointGoal < road.Length() );
	}

	public void UpdateBody()
	{
		if ( walkTo == null )
		{
			transform.localPosition = road.nodes[currentPoint].Position();
			return;
		}
		if ( item && handsFull )
			item.UpdateLook();

		transform.localPosition = Vector3.Lerp( walkFrom.Position(), walkTo.Position(), walkProgress );
	}
}

