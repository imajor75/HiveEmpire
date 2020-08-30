﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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
	public int wishedPoint = -1;

	static public Worker Create( Ground ground, Road road )
	{
		int i = road.nodes.Count / 2;
		while ( i < road.nodes.Count - 1 && road.workersAtNodes[i] != null )
			i++;
		if ( road.workersAtNodes[i] != null )
			return null;

		GameObject workerBody = GameObject.CreatePrimitive( PrimitiveType.Cylinder );
		workerBody.name = "Worker";
		Worker worker = workerBody.AddComponent<Worker>();
		worker.road = road;
		worker.currentPoint = worker.roadPointGoal = i;
		road.workersAtNodes[i] = worker;
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
			walkProgress += 0.015f; // TODO Speed should depend on the steepness of the road
			if ( walkProgress >= 1 )
			{
				currentPoint = road.NodeIndex( walkTo );
				walkTo = walkFrom = null;
				walkProgress -= 1;
			}
			UpdateBody();
			return;
		}
		if ( currentPoint == roadPointGoal )
		{
			if ( !FindGoal() && road.workers.Count > 1 )
				Remove();
		}
		else
			NextStep();	// TODO This should cause unevent movement at nodes
	}

	void Remove()
	{
		GroundNode point = road.nodes[currentPoint];
		Assert.AreEqual( road.workersAtNodes[currentPoint], this );
		road.workersAtNodes[currentPoint] = null;
		Flag flag = road.nodes[currentPoint].flag;
		if ( flag )
		{
			Assert.AreEqual( flag.user, this );
			flag.user = null;
		}
		road.workers.Remove( this );
		GetComponent<MeshRenderer>().enabled = false;
		Destroy( this );
	}

	public bool NextStep()
	{
		Assert.AreEqual( road.workersAtNodes[currentPoint], this );
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
		road.workersAtNodes[currentPoint] = null;
		if ( road.workersAtNodes[nextPoint] != null )
		{
			var otherWorker = road.workersAtNodes[nextPoint];
			if ( otherWorker.wishedPoint == currentPoint )
			{
				// TODO Workers should avoid each other
				road.workersAtNodes[currentPoint] = null;
				bool coming = otherWorker.NextStep();
				Assert.IsTrue( coming );
			}
			else
			{
				road.workersAtNodes[currentPoint] = this;
				wishedPoint = nextPoint;
				return false;
			}
		}

		wishedPoint = -1;
		walkFrom = road.nodes[currentPoint];
		walkTo = road.nodes[nextPoint];
		if ( walkFrom.flag && walkFrom.flag.user == this )
			walkFrom.flag.user = null;
		road.workersAtNodes[nextPoint] = this;
		return true;
	}

	public bool FindGoal()
	{
		if ( item != null )
		{
			if ( handsFull )
			{
				Flag flag = road.GetEnd( currentPoint );
				Assert.IsNotNull( flag );
				item.worker = null;
				item.pathProgress++;
				if ( item.pathProgress == item.path.roadPath.Count )
				{
					Assert.AreEqual( item.destination.flag, flag );
					item.destination.ItemArrived( item );
					item.GetComponent<SpriteRenderer>().enabled = false;
					Destroy( item.gameObject );
				}
				else
					flag.StoreItem( item );
				item.UpdateLook();
				item = null;
				handsFull = false;
				if ( !FindGoal() )
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

		foreach ( var item in road.GetEnd( 0 ).items )
			CheckItem( item );
		foreach ( var item in road.GetEnd( 1 ).items )
			CheckItem( item );
		return item != null;
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
		Assert.IsTrue( index >= 0 && index <= road.nodes.Count );
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
		Assert.IsTrue( road.workers.Contains( this ) );
		Assert.IsTrue( currentPoint >= 0 && currentPoint < road.nodes.Count );
		Assert.IsTrue( roadPointGoal >= 0 && roadPointGoal < road.nodes.Count );
		if ( wishedPoint >= 0 )
		{
			Assert.IsTrue( wishedPoint <= road.nodes.Count );
			Assert.AreEqual( Math.Abs( wishedPoint - currentPoint ), 1 );
		}
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

