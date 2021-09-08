using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

[SelectionBase]
public class Worker : HiveObject
{
	public Type type;
	public Road road;
	public bool exclusiveMode;
	public Building building;
	public Player owner;
	public Node walkFrom;
	public Node walkTo;
	public Road walkBase;
	public int walkBlock;
	public bool walkBackward;
	public float walkProgress;
	public float standingHeight = 0;
	public float speed = 1;
	public Node node;
	public Item[] itemsInHands = new Item[2];
	public Type look;
	public Resource origin;
	public float currentSpeed;
	public Flag exclusiveFlag;
	public bool recalled;
	public int itemsDelivered;
	public World.Timer bored = new World.Timer();
	public BodyState bodyState = BodyState.unknown;
	public SerializableColor currentColor;
	public List<Task> taskQueue = new List<Task>();

	[JsonIgnore]
	public bool debugReset;

	public static Act[] resourceCollectAct = new Act[(int)Resource.Type.total];
	public static Act shovelingAct;
	public static Act constructingAct;
	static MediaTable<GameObject, Type> looks;
	static public MediaTable<AudioClip, Type> walkSounds;
	static public MediaTable<AudioClip, AnimationSound> animationSounds;
	static public List<GameObject> templates = new List<GameObject>();
	static public int walkingID, pickupHeavyID, pickupLightID, putdownID;
	static public int buildingID, shovelingID, fishingID, harvestingID, sowingID, choppingID, miningID, skinningID;

	public Animator animator;
	public AudioSource soundSource;
	public GameObject mapObject;
	Material mapMaterial;
	GameObject arrowObject;
	SpriteRenderer itemOnMap;
	static public Sprite arrowSprite;
	Material shirtMaterial;
	protected GameObject body;
	[JsonIgnore]
	public GameObject[] links = new GameObject[(int)LinkType.total];
	readonly GameObject[] wheels = new GameObject[4];

	[Obsolete( "Compatibility with old files", true )]
	bool underControl { set {} }
	[Obsolete( "Compatibility with old files", true )]
	Item itemInHands { set { itemsInHands[0] = value; } }
	[Obsolete( "Compatibility with old files", true )]
	Color cachedColor;
	[Obsolete( "Compatibility with old files", true )]
	public bool onRoad { set { exclusiveMode = value; } }
	[Obsolete( "Compatibility with old files", true )]
	GameObject haulingBox;

	public Ground ground
	{
		get { return World.instance.ground; }
		[Obsolete( "Compatibility for old files", true )]
		set {}
	}

	public enum LinkType
	{
		haulingBoxLight,
		haulingBoxHeavy,
		haulingBoxSecondary,
		rightHand,
		leftHand,
		total
	}

	public enum BodyState
	{
		unknown,
		standing,
		walking,
		custom,
	}

	public Color Color
	{
		set
		{
			currentColor = value;
			if ( shirtMaterial )
				shirtMaterial.color = value;
			if ( mapMaterial )
				mapMaterial.color = value;
		}
	}

	public class Act
	{
		public float timeToInterrupt = -1;
		public int duration;
		public int animation;
		public GameObject toolTemplate;
		public LinkType toolSlot;
		public Node.Type turnTo = Node.Type.anything;
	}

	public enum AnimationSound
	{
		axe,
		harvest,
		pickaxeOnRock,
		construction,
		stab
	}

	public class Task : ScriptableObject // TODO Inheriting from ScriptableObject really slows down the code.
	{
		protected const bool finished = true;
		protected const bool needModeCalls = false;
		public Worker boss;

		public Task Setup( Worker boss )
		{
			this.boss = boss;
			return this;
		}
		public virtual void Prepare() { }
		public virtual bool InterruptWalk() { return false; }
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

		public bool ResetBossTasks()
		{
			boss.ResetTasks();
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

	public class DoAct : Task
	{
		public Act act;
		public World.Timer timer = new World.Timer();
		public bool started = false;
		public BodyState preState;
		GameObject tool;
		public bool wasWalking;

		public void Setup( Worker boss, Act act )
		{
			base.Setup( boss );
			this.act = act;
		}

		public void Start()
		{
			if ( started )
				return;

			if ( act.turnTo != Node.Type.anything )
			{
				foreach ( var c in Ground.areas[1] )
				{
					var n = boss.node.Add( c );
					if ( n.type == Node.Type.underWater )
						boss.TurnTo( n );
				}
			}

			if ( boss.animator )
				wasWalking = boss.animator.GetBool( walkingID );
			boss.animator?.SetBool( walkingID, false );
			boss.animator?.SetBool( act.animation, true );
			preState = boss.bodyState;
			boss.bodyState = BodyState.custom;
			if ( act.toolTemplate )
				tool = Instantiate( act.toolTemplate, boss.links[(int)act.toolSlot]?.transform );
			timer.Start( act.duration );
			started = true;
		}

		public void Stop()
		{
			if ( !started )
				return;
			timer.Reset();
			boss.animator?.SetBool( act.animation, false );
			boss.animator?.SetBool( walkingID, wasWalking );
			boss.bodyState = preState;
			if ( tool )
				Destroy( tool );
			started = false;
		}

		public override bool InterruptWalk()
		{
			if ( act == null )
				return false;
			if ( timer.inProgress )
				return true;

			if ( timer.done )
			{
				Stop();
				act = null;
				return false;
			}

			if ( !started && boss.walkProgress >= act.timeToInterrupt && act.timeToInterrupt >= 0 )
			{
				Start();
				return true;
			}

			return false;
		}

		public override bool ExecuteFrame()
		{
			if ( act == null )
				return true;
			if ( timer.inProgress || act.duration < 0 )
				return false;

			if ( timer.done )
			{
				Stop();
				return true;
			}

			Start();
			return false;
		}

		public override void Cancel()
		{
			if ( started )
				Stop();
			base.Cancel();
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
		public override void Cancel()
		{
			boss.LeaveExclusivity();
			base.Cancel();
		}

		public override bool ExecuteFrame()
		{
			bool wasExclusive = boss.exclusiveMode;
			if ( exclusive )
				boss.LeaveExclusivity();

			if ( boss.node.flag == target || target == null )	// Target could be n
				return true;

			if ( path == null || path.road == null )
			{
				if ( !boss.node.validFlag )
				{
					ResetBossTasks();
					return finished;
				}
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
			Road nextRoad = path.NextRoad();
			if ( boss.node.flag == nextRoad.ends[0] )
				point = nextRoad.nodes.Count - 1;
			if ( exclusive )
			{
				if ( !boss.EnterExclusivity( nextRoad, boss.node ) )
				{
					// Boss is trying to move to the next road. This is always possible when the flag is not a crossing, since no workers 
					// can use the same entry as the cart. But when the flag is a crossing, it is possible that the cart cannot jump to the 
					// road in an exclusive way, because a worker is in the way. In that case the cart simply waits.
					if ( wasExclusive )
					{
						var s = boss.EnterExclusivity( boss.road, boss.node );
						boss.assert.IsTrue( s );
					}
					path.progress--;    // cancel advancement
					boss.assert.IsTrue( boss.node.flag.crossing || path.progress == 0 );	// TODO Triggered when a cart full of fish just started to deliver stuff, there was a normal hauler in the way
					return false;
				}
			}
			boss.ScheduleWalkToRoadPoint( nextRoad, point, exclusive, true );
			return false;
		}

		public override void Validate()
		{
			base.Validate();
		}
	}

	public class WalkToNode : Task
	{
		public Node target;
		public Path path;
		public bool ignoreFinalObstacle;
		public HiveObject ignoreObject;

		public World.Timer interruptionTimer = new World.Timer();
		public Act lastStepInterruption;

		public void Setup( Worker boss, Node target, bool ignoreFinalObstacle = false, Act lastStepInterruption = null, HiveObject ignoreObject = null )
		{
			base.Setup( boss );
			this.target = target;
			this.ignoreFinalObstacle = ignoreFinalObstacle;
			this.lastStepInterruption = lastStepInterruption;
			this.ignoreObject = ignoreObject;
		}
		public override bool ExecuteFrame()
		{
			if ( boss.node == target )
				return true;

			if ( path == null )
			{
				path = Path.Between( boss.node, target, PathFinder.Mode.forWorkers, boss, ignoreFinalObstacle, ignoreObject );
				if ( path == null )
				{
					Debug.Log( "Worker failed to go to " + target.x + ", " + target.y );
					return true;
				}
			}
			boss.Walk( path.NextNode() );   // TODO Check if the node is blocked? The shoveling for construction code relies on we don't check.
			if ( path.isFinished && lastStepInterruption != null )
				boss.ScheduleDoAct( lastStepInterruption, true );
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
		public World.Timer stuck = new World.Timer();

		public void Setup( Worker boss, Road road, int point, bool exclusive )
		{
			boss.assert.IsTrue( point >= 0 && point < road.nodes.Count, "Invalid road point (" + point + ", " + road.nodes.Count + ")" ); // TODO Triggered (point=-1)
			base.Setup( boss );
			this.road = road;
			this.exclusive = exclusive;
			targetPoint = point;
		}

		public override bool ExecuteFrame()
		{
			if ( road == null )
				return ResetBossTasks();

			if ( stuck.done && boss.type == Type.hauler && road.ActiveWorkerCount > 1 )
			{
				boss.Remove( true );
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
				Flag flag = road.nodes[nextPoint].validFlag;
				if ( flag && !flag.crossing )
				{
					boss.assert.IsTrue( other == null || other == flag.user );
					other = flag.user;
				}
				if ( other && !other.Call( road, currentPoint ) )
				{
					// As a last resort to make space is to simply remove the other hauler
					if ( other.exclusiveMode && other.type == Type.hauler && other.road != road && other.road.ActiveWorkerCount > 1 && other.IsIdle() )
						other.Remove( true );
					else
					{
						if ( stuck.empty )
							stuck.Start( Constants.Worker.stuckTimeout );
						return false;
					}
				}
			}

			wishedPoint = -1;
			boss.assert.IsTrue( currentPoint >= 0 && currentPoint < road.workerAtNodes.Count ); // TODO Triggered, happens when a road starts and ends at the same flag
			// Triggered again, when a road is under construction, and a construction worker walks on the area while the end point is aligned, the road is not yet finalized																								
			if ( road.workerAtNodes[currentPoint] == boss ) // it is possible that the other worker already took the place, so it must be checked
				road.workerAtNodes[currentPoint] = null;

			boss.assert.AreEqual( boss.node, road.nodes[currentPoint] );
			boss.walkBase = road;
			boss.Walk( road.nodes[nextPoint] );
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
				if ( boss.walkTo.validFlag && !boss.walkTo.validFlag.crossing )
				{
					if ( !ignoreOtherWorkers )
						boss.assert.IsNull( boss.walkTo.flag.user, "Worker still in way at flag." );
					boss.walkTo.flag.user = boss;
					boss.walkTo.flag.recentlyLeftCrossing = false;
					boss.exclusiveFlag = boss.walkTo.flag;
				}
				if ( boss.walkFrom.validFlag && !boss.walkFrom.validFlag.crossing )
				{
					boss.assert.AreEqual( boss.walkFrom.flag, boss.exclusiveFlag ); // TODO Triggered (onroad hauler, excflag 0, didn't build anything recently)
					if ( boss.walkFrom.flag.user == boss )
						boss.walkFrom.flag.user = null;
					boss.exclusiveFlag = null;
				}
			}
			stuck.Reset();
			return true;
		}

		public override void Validate()
		{
			base.Validate();
			if ( this != boss.taskQueue[0] || road == null )	// road might be unity null here
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
						boss.assert.AreEqual( i, cp );	// Triggered for a cart
														// TODO Triggered again during a stress test, a hauler was delivering two items into a brewery I think. The building was destroyed right when the 
														// hauler was trying to drop items on the floor. It was 0, -1 I think. Task queue only had this task in it.
														// Triggered again for a hauler during a stress test (5, -1) boss.node is 11,26, road nodes are:
														// 14 21
														// 13 22
														// 13 23
														// 13 24
														// 12 25
														// 12 26	<- worker was here somewhere?
														// 13 26
														// 13 27
														// Task queue is walktoroadpoint and deliveritem
														// a single fish is in hand, this fish has nextFlag pointing to the beginning of the road (14, 21)
														// targetpoint is 0, wishedpoint is -1, currentpoint -1
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
		public Node target;
		public void Setup( Worker boss, Node target )
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
		static public int pickupTimeStart = 120;
		static public int pickupReparentTime = 60;
		public Item[] items = new Item[2];
		public Path path;  // Save the path just to be able to test if it has been changed
		public bool[] reparented = new bool[2];	// See PickupItem.Cancel. Originally I wanted to save the Transformation reference, but that cannot be serialized
		public World.Timer timer = new World.Timer();
		public bool expectingSecondary;

		[Obsolete( "Compatibility with old files", true )]
		Item item { set { items[0] = value; } }
		[Obsolete( "Compatibility with old files", true )]
		Path[] paths { set { path = value[0]; } }

		public void Setup( Worker boss, Item item )
		{
			base.Setup( boss );
			items[0] = item;
			path = item.path;
			items[1] = null;
		}
		public override void Cancel()
		{
			for ( int i = 0; i < items.Length; i++ )
			{
				if ( items[i] == null )
					continue;

				boss.assert.AreEqual( boss, items[i].worker );
				items[i].worker = null;
				// It is possible in rare cases, that the worker was reset AFTER relinking the item to its hand, but before this task would actually take the item in hands. This leads to a crash when the worker arrives back at the HQ, as the 
				// worker object gets destroyed, the item gets destroyed too, because it is linked to the hauling box of the worker, but still sitting at the flag. In this case the item should be linked back to the previous parent (the frame at the flag)
				if ( reparented[i] && items[i].flag )
				{
					for ( int j = 0; j < items[i].flag.items.Length; j++ )
					{
						if ( items[i].flag.items[j] == items[i] )
							items[i].transform.SetParent( items[i].flag.frames[j].transform, false );
					}
					boss.animator?.Play( "idle" );
				}
				// items[i].flag should never be zero here, but it was after a global reset

				// TODO Exception was thrown here, items[0].flag was null. This happened after I removed a road.
				// The whole reset thing was called from PickupItem.executeFrame where it realized that there is no path for the item
				if ( items[i].justCreated )
				{
					// Item is hanging in the air, it never actually entered the logistic network
					items[i].assert.IsNull( items[i].flag );
					items[i].Remove( false );
				}
			}
			base.Cancel();
		}
		bool ConsiderSecondary( bool checkOnly = false )
		{
			if ( items[1] || boss.type != Type.hauler )
				return false;
			if ( items[0].path.isFinished || items[0].buddy )
				return false;
			var deliverTask = boss.FindTaskInQueue<DeliverItem>();
			if ( deliverTask == null )
				return false;
			boss.assert.IsNull( deliverTask.items[1] );
			Flag target = boss.road.OtherEnd( boss.node.flag );
			if ( target.freeSlots == 0 && items[0].path.stepsLeft != 1 )
				return false;
			foreach ( var secondary in boss.node.flag.items )
			{
				if ( secondary == null || secondary == items[0] || secondary.flag != boss.node.flag )
					continue;
				if ( secondary.worker && secondary.worker.road != boss.road )
					continue;
				if ( secondary.buddy )	// Is it possible, that there is a buddy but no worker?
					continue;
				if ( secondary.type != items[0].type )
					continue;
				if ( secondary.road != boss.road || secondary.path == null )
					continue;
				if ( items[0].path.stepsLeft == 1 )
				{
					if ( secondary.path.stepsLeft > 1 )
						continue;
					else
						if ( items[0].destination != secondary.destination )
							continue;
				}
				else
					if ( secondary.path.stepsLeft == 1 )
						continue;

				if ( !checkOnly )
				{
					// At this point the item secondary seems like a good one to carry
					secondary.worker?.ResetTasks();
					if ( secondary.path.stepsLeft != 1 )
						target.ReserveItem( secondary );
					secondary.worker = boss;
					items[1] = secondary;
					deliverTask.items[1] = secondary;
					var box = boss.links[(int)LinkType.haulingBoxSecondary]?.transform;
					if ( box )
					{
						secondary.transform.SetParent( box, false );
						box.localPosition = Constants.Item.secondItemOffset[(int)secondary.type];
					}
				}
				return true;
			}
			return false;
		}
		public override bool ExecuteFrame()
		{
			if ( items[0].path != path )
				return ResetBossTasks();

			boss.assert.AreEqual( items[0].worker, boss );
			if ( items[0].buddy )
				boss.assert.AreEqual( items[0].buddy.worker, boss );
			if ( boss.type == Type.hauler )
				boss.assert.IsNull( boss.itemsInHands[0] );
			if ( timer.empty )
			{
				timer.Start( pickupTimeStart );
				boss.animator?.ResetTrigger( putdownID );
				// The ConsiderSecondary call in the next line is only done to foresee if a second item will be picked, it might be different 
				expectingSecondary = ConsiderSecondary( true );
				boss.animator?.SetTrigger( expectingSecondary ? pickupHeavyID : pickupLightID );   // TODO Animation phase is not saved in file. This will always be light
			}

			var attachAt = boss.links[(int)( expectingSecondary ? LinkType.haulingBoxHeavy : LinkType.haulingBoxLight )];
			if ( items[0].transform.parent != attachAt && timer.age > -pickupReparentTime )
			{
				reparented[0] = true;
				items[0].transform.SetParent( attachAt?.transform, false );
				attachAt?.SetActive( true );
			}

			if ( !timer.done )
				return false;

			// This is the very last moment to pick another item
			ConsiderSecondary();

			for ( int i = 0; i < items.Length; i++ )
			{
				if ( items[i] == null )
					continue;

				items[i].flag?.ReleaseItem( items[i] );
				items[i].justCreated = false;
				boss.itemsInHands[i] = items[i];
				boss.assert.IsTrue( items[i].worker == boss || items[i].worker == null );
				if ( items[i].worker.type == Type.hauler )
				{
					if ( items[i].path.isFinished )
						boss.assert.AreEqual( items[i].destination.flag.node, boss.node );
					else
						items[i].path.NextRoad();
				}
			}
			return true;
		}
	}

	public class DeliverItem : Task
	{
		static public int putdownTimeStart = 120;
		static public int putdownRelinkTime = 80;
		public Item[] items = new Item[2];
		public World.Timer timer = new World.Timer();

		[Obsolete( "Compatibility with old files", true )]
		Item item { set { items[0] = value; } }

		public void Setup( Worker boss, Item item )
		{
			base.Setup( boss );
			this.items[0] = item;
		}
		public override void Cancel()
		{
			foreach ( var item in items )
			{
				if ( item == null || item.nextFlag == null )
					continue;

				if ( item.buddy?.worker )
				{
					// If two items are swapping, the second one has no assigned PickupItem task, the DeliverItem task will handle
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
			if ( timer.empty )
			{
				timer.Start( putdownRelinkTime );
				foreach ( var item in items )
				{
					if ( item == null )
						continue;

					if ( item.buddy )
					{
						item.buddy.transform.SetParent( boss.links[(int)LinkType.haulingBoxLight]?.transform, false );
						timer.reference -= 30;
					}
					else
					{
						boss.animator?.ResetTrigger( pickupHeavyID );
						boss.animator?.ResetTrigger( pickupLightID );
						boss.animator?.SetTrigger( putdownID );
					}
				}
			}

			if ( !timer.done )
				return needModeCalls;

			boss.itemsDelivered++;
			if ( items[1] )
				boss.itemsDelivered++;
			boss.bored.Start( Constants.Worker.boredTimeBeforeRemove );
			boss.links[(int)LinkType.haulingBoxLight]?.SetActive( items[0].buddy != null );
			boss.links[(int)LinkType.haulingBoxHeavy]?.SetActive( false );
			for ( int i = 0; i < items.Length; i++ )
			{
				if ( items[i] == null )
					continue;

				boss.assert.AreEqual( boss.itemsInHands[i], items[i] );
				if ( items[i].destination?.node == boss.node )
				{
					items[i].Arrived();
					boss.itemsInHands[i] = null;
				}
				else
				{
					if ( items[i].nextFlag && boss.node.flag == items[i].nextFlag )
					{
						Item change = boss.itemsInHands[i] = items[i].ArrivedAt( items[i].nextFlag );
						if ( change )
						{
							if ( boss.road && change.road == boss.road )
								change.path.NextRoad();
							else
								change.CancelTrip();
						}
					}
					else
						return ResetBossTasks(); // This happens when the previous walk tasks failed, and the worker couldn't reach the target or if the item lost its destination during the last segment of the path
				}
			}

			boss.ScheduleWait( putdownTimeStart - putdownRelinkTime, true );
			return finished;
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
				boss.Remove( true );
				return finished;    // Task failed
			}
			if ( road.NodeIndex( boss.node ) == -1 )
				return finished;
			return boss.EnterExclusivity( road, boss.node );
		}
	}

	public class Wait : Task
	{
		public int time;
		public World.Timer timer = new World.Timer();

		public void Setup( Worker boss, int time )
		{
			base.Setup( boss );
			this.time = time;
		}

		public override bool ExecuteFrame()
		{
			if ( timer.empty )
				timer.Start( time );

			return timer.done && time >= 0;
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
		cart,
		tinkererMate
	}

	public static void Initialize()
	{
		object[] lookData = {
		"prefabs/characters/peasant", Type.tinkerer,
		"prefabs/characters/peasantFemale", Type.tinkererMate,
		"prefabs/characters/blacksmith", Type.constructor,
		"prefabs/characters/hauler", Type.hauler,
		"prefabs/characters/soldier", Type.soldier,
		"Rabbits/Prefabs/Rabbit 1", Type.wildAnimal,
		"prefabs/characters/cart", Type.cart };

		looks.Fill( lookData );

		walkingID = Animator.StringToHash( "walk" );
		pickupHeavyID = Animator.StringToHash( "pick up heavy" );
		pickupLightID = Animator.StringToHash( "pick up light" );
		putdownID = Animator.StringToHash( "put down" );
		choppingID = Animator.StringToHash( "chopping" );
		miningID = Animator.StringToHash( "mining" );
		skinningID = Animator.StringToHash( "skinning" );
		buildingID = Animator.StringToHash( "building" );
		fishingID = Animator.StringToHash( "fishing" );
		shovelingID = Animator.StringToHash( "shoveling" );
		harvestingID = Animator.StringToHash( "harvesting" );
		sowingID = Animator.StringToHash( "sowing" );

		object[] walk = {
			"effects/cart", Type.cart };
		walkSounds.Fill( walk );

		arrowSprite = Resources.Load<Sprite>( "icons/arrow" );

		resourceCollectAct[(int)Resource.Type.tree] = new Act
		{
			animation = choppingID,
			toolTemplate = Resources.Load<GameObject>( "prefabs/tools/axe" ),
			toolSlot = LinkType.leftHand,
			timeToInterrupt = 0.7f,
			duration = 500
		};
		resourceCollectAct[(int)Resource.Type.rock] = new Act
		{
			animation = miningID,
			toolTemplate = Resources.Load<GameObject>( "prefabs/tools/pickaxe" ),
			toolSlot = LinkType.leftHand,
			timeToInterrupt = 0.7f,
			duration = 500
		};
		resourceCollectAct[(int)Resource.Type.fish] = new Act
		{
			animation = fishingID,
			toolTemplate = Resources.Load<GameObject>( "prefabs/tools/stick" ),
			toolSlot = LinkType.rightHand,
			timeToInterrupt = 1.0f,
			duration = 500,
			turnTo = Node.Type.underWater
		};
		resourceCollectAct[(int)Resource.Type.cornfield] = new Act
		{
			animation = harvestingID,
			timeToInterrupt = 1.0f,
			duration = 500
		};
		resourceCollectAct[(int)Resource.Type.pasturingAnimal] = new Act
		{
			animation = skinningID,
			toolTemplate = Resources.Load<GameObject>( "prefabs/tools/dagger" ),
			toolSlot = LinkType.rightHand,
			timeToInterrupt = 0.8f,
			duration = 300
		};
		shovelingAct = new Act
		{
			animation = shovelingID,
			toolTemplate = Resources.Load<GameObject>( "prefabs/tools/shovel" ),
			toolSlot = LinkType.leftHand,
			duration = Constants.Building.flatteningTime
		};
		constructingAct = new Act
		{
			animation = buildingID,
			toolTemplate = Resources.Load<GameObject>( "prefabs/tools/hammer" ),
			toolSlot = LinkType.rightHand,
			duration = -1
		};

		animationSounds.fileNamePrefix = "effects/";
		object[] animationSoundData = 
		{
			"construction", 0.2f, AnimationSound.construction
		};
		animationSounds.Fill( animationSoundData );
	}

	static public Worker Create()
	{
		return new GameObject().AddComponent<Worker>();
	}

	public Worker SetupForRoad( Road road )
	{
		look = type = Type.hauler;
		name = "Hauler";
		owner = road.owner;
		currentColor = Color.grey;
		Building main = road.owner.mainBuilding;
		SetNode( main.node );
		this.road = road;
		exclusiveMode = false;
		ScheduleWalkToNeighbour( main.flag.node );
		ScheduleWalkToFlag( road.ends[0] ); // TODO Pick the end closest to the main building
		ScheduleWalkToRoadNode( road, road.centerNode, false );
		ScheduleStartWorkingOnRoad( road );
		base.Setup();
		return this;
	}

	public Worker SetupForBuilding( Building building, bool mate = false )
	{
		look = type = Type.tinkerer;
		if ( mate )
			look = Type.tinkererMate;
		currentColor = Color.cyan;
		name = "Tinkerer";
		return SetupForBuildingSite( building );
	}

	public Worker SetupForConstruction( Building building )
	{
		look = type = Type.constructor;
		name = "Builder";
		currentColor = Color.cyan;
		return SetupForBuildingSite( building );
	}

	public Worker SetupForFlattening( Flag flag )
	{
		assert.IsNotNull( flag );

		look = type = Type.constructor;
		name = "Builder";
		currentColor = Color.cyan;
		owner = flag.owner;
		Building main = owner.mainBuilding;
		SetNode( main.node );
		ScheduleWalkToNeighbour( main.flag.node );
		ScheduleWalkToFlag( flag );
		base.Setup();
		return this;
	}

	public Worker SetupAsSoldier( Building building )
	{
		look = type = Type.soldier;
		name = "Soldier";
		currentColor = Color.red;
		if ( building is GuardHouse guardHouse )
		{
			standingHeight = 0.367f;
			return SetupForBuildingSite( building );
		}
		
		owner = building.owner;
		this.building = building;
		SetNode( building.node );
		base.Setup();
		return this;
	}

	Worker SetupForBuildingSite( Building building )
	{
		owner = building.owner;
		this.building = building;
		Building main = owner.mainBuilding;
		if ( main && main != building )
		{
			SetNode( main.node );
			ScheduleWalkToNeighbour( main.flag.node );
			ScheduleWalkToFlag( building.flag );
			ScheduleWalkToNeighbour( building.node );
		}
		else
			SetNode( building.node );
		base.Setup();
		return this;
	}

	public Worker SetupAsAnimal( Resource origin, Node node )
	{
		look = type = Type.wildAnimal;
		SetNode( node );
		this.origin = origin;
		base.Setup();
		return this;
	}

	public Worker SetupAsCart( Stock stock )
	{
		look = type = Type.cart;
		building = stock;
		SetNode( stock.node );
		speed = Constants.Stock.cartSpeed;
		owner = stock.owner;
		currentColor = Color.white;
		base.Setup();
		return this;
	}

	public void SetNode( Node node )
	{
		this.node = node;
		node.ground.Link( this, walkBase?.location );
	}

	new public void Start()
	{
		node.ground.Link( this, walkBase?.location );
		transform.position = node.position + Vector3.up * standingHeight;

		body = Instantiate( looks.GetMediaData( look ), transform );
		links[(int)LinkType.haulingBoxLight] = World.FindChildRecursive( body.transform, "haulingBoxLight" )?.gameObject;
		links[(int)LinkType.haulingBoxHeavy] = World.FindChildRecursive( body.transform, "haulingBoxHeavy" )?.gameObject;
		links[(int)LinkType.haulingBoxSecondary] = World.FindChildRecursive( body.transform, "haulingBoxSecondary" )?.gameObject;
		links[(int)LinkType.rightHand] = World.FindChildRecursive( body.transform, "Hand_R" )?.gameObject;
		links[(int)LinkType.leftHand] = World.FindChildRecursive( body.transform, "Hand_L" )?.gameObject;
		Transform shirt = World.FindChildRecursive( body.transform, "PT_Medieval_Boy_Peasant_01_upper" );
		if ( shirt )
		{
			var skinnedMeshRenderer = shirt.GetComponent<SkinnedMeshRenderer>();
			if ( skinnedMeshRenderer )
			{
				Material[] materials = skinnedMeshRenderer.materials;
				materials[0] = shirtMaterial = new Material( World.defaultShader );
				skinnedMeshRenderer.materials = materials;
			}
		}
		animator = body.GetComponent<Animator>();
		if ( animator )
		{
			animator.applyRootMotion = false;
			if ( itemsInHands[0] )
				animator.Play( "idle light" );
		}
		else
			animator = null;
		if ( type == Type.cart )
		{
			wheels[0] = World.FindChildRecursive( body.transform, "SM_Veh_Cart_02_Wheel_fl" )?.gameObject;
			wheels[1] = World.FindChildRecursive( body.transform, "SM_Veh_Cart_02_Wheel_fr" )?.gameObject;
			wheels[2] = World.FindChildRecursive( body.transform, "SM_Veh_Cart_02_Wheel_rl" )?.gameObject;
			wheels[3] = World.FindChildRecursive( body.transform, "SM_Veh_Cart_02_Wheel_rr" )?.gameObject;
		}

		UpdateBody();
		name = type switch
		{
			Type.soldier => "Soldier",
			Type.wildAnimal => "Bunny",
			Type.hauler => "Hauler",
			Type.constructor => "Builder",
			Type.tinkerer => "Tinkerer",
			_ => "Worker",
		};
		soundSource = World.CreateSoundSource( this );
		
		World.SetLayerRecursive( gameObject, World.layerIndexNotOnMap );
		var collider = World.FindChildRecursive( body.transform, "collider" );
		if ( collider )
			collider.gameObject.layer = World.layerIndexPickable;

		mapObject = GameObject.CreatePrimitive( PrimitiveType.Sphere );
		World.SetLayerRecursive( mapObject, World.layerIndexMapOnly );
		mapObject.transform.SetParent( transform );
		mapObject.transform.localPosition = Vector3.up * 2;
		mapObject.transform.localScale = Vector3.one * ( type == Type.cart ? 0.5f : 0.3f );
		var r = mapObject.GetComponent<MeshRenderer>();
		r.material = mapMaterial = new Material( World.defaultShader );
		r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
		mapMaterial.renderQueue = 4002;

		arrowObject = new GameObject( "Marker" );
		World.SetLayerRecursive( arrowObject, World.layerIndexMapOnly );
		arrowObject.transform.SetParent( transform, false );
		var sr = arrowObject.AddComponent<SpriteRenderer>();
		sr.sprite = arrowSprite;
		sr.color = new Color( 1, 0.75f, 0.15f );
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

		Color = currentColor;

		if ( itemsInHands[1] )
		{
			if ( itemsInHands[0] )
				itemsInHands[0].transform.SetParent( links[(int)LinkType.haulingBoxHeavy].transform, false );
			itemsInHands[1].transform.SetParent( links[(int)LinkType.haulingBoxSecondary].transform, false );
		}
		else if ( itemsInHands[0] )
			itemsInHands[0].transform.SetParent( links[(int)LinkType.haulingBoxLight].transform, false );

		base.Start();
	}

	// Distance the worker is taking in a single frame (0.02 sec)
	public static float SpeedBetween( Node a, Node b )
	{
		float heightDifference = Math.Abs( a.height - b.height );
		float time = 2f + heightDifference * 4f;    // Number of seconds it takes for the worker to reach the other node
		return 1 / time / Constants.World.normalSpeedPerSecond;
	}

	public void Walk( Node target )
	{
		assert.IsTrue( node.DirectionTo( target ) >= 0, "Trying to walk to a distant node" );
		currentSpeed = speed * SpeedBetween( target, node );
		walkFrom = node;
		SetNode( walkTo = target );
	}

	void FixedUpdate()
	{
		if ( ( type == Type.tinkerer || type == Type.cart ) && IsIdle( true ) )
		{
			SetActive( false );
			return;
		}
		if ( debugReset )
		{
			ResetTasks();
			debugReset = false;
			return;
		}
	}

	// Update is called once per frame
	public override void CriticalUpdate()
	{
		// If worker is between two nodes, simply advancing it
		if ( walkTo != null )
		{
			if ( taskQueue.Count > 0 && taskQueue[0].InterruptWalk() )
				return;
			walkProgress += currentSpeed;
			if ( World.instance.operationHandler.recordCRC )
				Log( $"worker {id}: {node.x}, {node.y}, {(int)( walkProgress * 10000)}" );

			World.CRC = node.x + node.y + (int)( walkProgress * 10000 );
			if ( walkProgress >= 1 )
			{
				walkTo = walkFrom = null;
				walkBase = null;
				walkProgress -= 1;
			}
		}

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
	}

	void Update()
	{
		UpdateBody();
		UpdateOnMap();
	}

	void UpdateOnMap()
	{
		arrowObject.SetActive( false );
		if ( type == Type.hauler || type == Type.cart )
		{
			if ( taskQueue.Count > 0 )
			{
				var t = taskQueue[0] as WalkToRoadPoint;
				if ( t && t.wishedPoint >= 0 )
				{
					var wp = t.road.nodes[t.wishedPoint].position;
					arrowObject.SetActive( true );
					var dir = wp - node.position;
					arrowObject.transform.rotation = Quaternion.LookRotation( dir ) * Quaternion.Euler( 0, -90, 0 ) * Quaternion.Euler( 90, 0, 0 );
					arrowObject.transform.position = node.position + Vector3.up * 4 + 0.5f * dir;
				}
			}
		}

		{
			var t = Item.Type.unknown;
			if ( itemsInHands[0] )
				t = itemsInHands[0].type;
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

	public override bool Remove( bool returnToMainBuilding = true )
	{
		assert.IsTrue( type != Type.cart || building == null );
		ResetTasks();
		if ( origin != null )
		{
			assert.AreEqual( type, Type.wildAnimal );
			assert.AreEqual( origin.type, Resource.Type.animalSpawner );
			origin.animals.Remove( this );
		}
		LeaveExclusivity();
		if ( road != null )
		{
			road.workers.Remove( this );
			road = null;
		}
		if ( !returnToMainBuilding )
		{
			foreach ( var item in itemsInHands )
				item?.Remove( false );
			itemsInHands[0] = itemsInHands[1] = null;

			DestroyThis();
			return true;
		}

		SetActive( true );       // Tinkerers are not active when they are idle

		// Try to get to a flag, so that we could walk on the road network to the main building
		// In case of haulers, node.road should be nonzero, except for the ends, but those already 
		// has a flag
		// TODO Pick the closer end
		if ( node.road != null )
			ScheduleWalkToRoadPoint( node.road, 0, false );
		foreach ( var o in Ground.areas[1] )        // Tinkerers are often waiting in building, so there is a flag likely nearby
		{
			if ( node.Add( o ).validFlag == null )
				continue;
			ScheduleWalkToNeighbour( node.Add( o ) );
			break;
		}

		road = null;
		building = null;
		Color = Color.black;
		type = Type.unemployed;
		return true;
	}

	virtual public void FindTask()
	{
		assert.IsTrue( IsIdle() );
		if ( type == Type.constructor )
			return;     // Let the building control it

		if ( type == Type.hauler )
		{
			FindHaulerTask();
			return;
		}

		if ( type == Type.wildAnimal )
		{
			int r = World.NextRnd( 6 );
			var d = Ground.areas[1];
			for ( int i = 0; i < d.Count; i++ )
			{
				Node t = node.Add( d[(i+r)%d.Count] );
				if ( t.block.IsBlocking( Node.Block.Type.workers ) )
					continue;
				if ( t.DistanceFrom( origin.node ) > 8 )
					continue;
				ScheduleWalkToNeighbour( t );
				if ( World.NextFloatRnd() < Constants.Workshop.pasturingPrayChance )
					ScheduleTask( ScriptableObject.CreateInstance<Workshop.Pasturing>().Setup( this ) );
				return;
			}
		}

		if ( type == Type.cart )
		{
			assert.IsNotNull( owner );
			var stock = building as Stock;
			if ( exclusiveFlag )
			{
				exclusiveFlag.user = null;
				exclusiveFlag = null;
			}
			if ( road )
			{
				int index = IndexOnRoad();
				assert.AreEqual( road.workerAtNodes[index], this );
				road.workerAtNodes[index] = null;
				road = null;
			}

			if ( node == stock.node )
				return;

			assert.IsNotNull( stock );
			if ( node.validFlag )
				ScheduleWalkToFlag( building.flag );
			else
				ScheduleWalkToNode( building.flag.node );
			ScheduleWalkToNeighbour( building.node );
			var task = ScriptableObject.CreateInstance<Stock.DeliverStackTask>();
			task.Setup( this, stock );
			ScheduleTask( task );
			return;
		}

		if ( type == Type.soldier )
		{
			if ( building && building is GuardHouse )
				return;

			ReturnToHeadquarters();
			return;
		}

		if ( type != Type.unemployed && building != null && node != building.node )
		{
			assert.IsTrue( type == Type.tinkerer || type == Type.constructor ); // This happens if the path to the building gets disabled for any reason
			ScheduleWait( 300 );
			if ( node.validFlag )   // TODO Do something if the worker can't get home
				ScheduleWalkToFlag( building.flag );
			else
				ScheduleWalkToNode( building.flag.node );
			ScheduleWalkToNeighbour( building.node );
			if ( itemsInHands[0] )
				ScheduleDeliverItem( itemsInHands[0], itemsInHands[1] );
			if ( type == Type.tinkerer && building.worker == this )
				ScheduleCall( building as Workshop );
			return;
		}

		if ( type == Type.unemployed )
			ReturnToHeadquarters();
	}

	void ReturnToHeadquarters()
	{
		if ( this as Stock.Cart )
			assert.IsNull( building as Stock );     // ?
		if ( !owner.mainBuilding.returningUnits.Contains( this ) )
			owner.mainBuilding.returningUnits.Add( this );
		if ( !recalled )
		{
			if ( node.validFlag )
				ScheduleWalkToFlag( owner.mainBuilding.flag );
			else
				ScheduleWalkToNode( owner.mainBuilding.flag.node );
			ScheduleWalkToNeighbour( owner.mainBuilding.node );
		};
		ScheduleCall( owner.mainBuilding );
		ScheduleWait( Constants.World.normalSpeedPerSecond );	// Wait to prevent further calls to this function once the unit reached the headquarters
		recalled = true;
	}

	void FindHaulerTask()
	{
		if ( ( bored.done && road.ActiveWorkerCount > 1 ) || ( road.ActiveWorkerCount > road.targetWorkerCount && road.targetWorkerCount != 0 ) )
		{
			Remove( true );
			return;
		}

		if ( !exclusiveMode )
		{
			ScheduleWalkToNode( road.centerNode, false );
			ScheduleStartWorkingOnRoad( road );
			return;
		}
		if ( itemsInHands[0] == null )
		{
			itemsInHands[0] = itemsInHands[1];
			itemsInHands[1] = null;
		}
		if ( itemsInHands[0] )
		{
			for ( int i = 0; i < 2; i++ )
			{
				Flag flag = road.ends[i];
				if ( flag.freeSlots < 1 )
					continue;

				flag.ReserveItem( itemsInHands[0] );
				bool onRoad = true;
				if ( road.NodeIndex( node ) == -1 ) // Not on the road, it was stepping into a building, or the road is rearranged by a flag magnet (Road.Split)
				{
					onRoad = false;
					foreach ( var o in Ground.areas[1] )
					{
						var nn = node.Add( o );
						if ( nn.flag == road.ends[0] || nn.flag == road.ends[1] )
						{
							ScheduleWalkToNeighbour( nn ); // It is possible, that the building is not there anymore
							onRoad = true;
							break;
						}
					}
				}
				if ( onRoad )
					ScheduleWalkToRoadPoint( road, i * ( road.nodes.Count - 1 ) );
				else
					ScheduleWalkToNode( road.ends[i].node );
				ScheduleDeliverItem( itemsInHands[0] );

				// The item is expecting the hauler to deliver it to nextFlag, but the hauled is delivering it to whichever flag has space
				// By calling CancelTrip, this expectation is eliminated, and won't cause an assert fail.
				itemsInHands[0].CancelTrip();
				return;
			}
			return;
		}

		if ( FindItemToCarry() )
		{
			Color = Color.white;
			return;
		}
		Color = Color.green;

		if ( node == road.nodes[0] || node == road.lastNode )
		{
			int restIndex = ( road.nodes.Count - 1 ) / 2;
			if ( node == road.nodes[0] )
				restIndex = road.nodes.Count / 2;
			if ( road.workerAtNodes[restIndex] )
			{
				for ( restIndex = 1; restIndex < road.nodes.Count - 2; restIndex++ )
					if ( road.workerAtNodes[restIndex] == null )
						break;
			}
			ScheduleWalkToRoadPoint( road, restIndex );
			return;
		}
	}

	/// <summary>
	/// 
	/// </summary>
	/// <returns>True if an item has been found and assigned to the worker</returns>
	bool FindItemToCarry()
	{
		Item bestItem = null;
		Item[] bestItemOnSide = { null, null };
		float bestScore = 0;
		float[] bestScoreOnSide = { 0, 0 };
		for ( int c = 0; c < 2; c++ )
		{
			Flag flag = road.ends[c];
			foreach ( var item in flag.items )
			{
				if ( item == null || item.flag == null )    // It can be nextFlag as well
					continue;
				World.CRC = item.id;
				var ( score, swapOnly ) = CheckItem( item );
				if ( swapOnly )
				{
					if ( score > bestScoreOnSide[c] )
					{
						bestScoreOnSide[c] = score;
						bestItemOnSide[c] = item;
					}
				}
				else
				{
					if ( score > bestScore )
					{
						bestScore = score;
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

	public (float score, bool swapOnly) CheckItem( Item item )
	{
		float value = road.owner.itemHaulPriorities[(int)item.type];

		// TODO Better prioritization of items
		if ( item.flag.node == node )
			value *= 2;

		if ( item.worker || item.destination == null )
			return (0f, false);

		if ( item.buddy )
			return (0f, false);

		if ( item.path == null )
			return (0f, false);

		if ( !item.path.isFinished && item.path.road != road )
			return (0f, false);

		if ( road.OtherEnd( item.flag ).freeSlots == 0 && item.path.stepsLeft != 1 )
		{
			if ( item.path.stepsLeft <= 1 )
				return (0f, false);
			return (value, true);
		}

		value *= 500 + item.atFlag.age;

		return (value, false);
	}

	public void ScheduleCall( Callback.IHandler handler )
	{
		assert.IsNotNull( handler );
		var instance = ScriptableObject.CreateInstance<Callback>();
		instance.Setup( this, handler );
		ScheduleTask( instance );
	}

	public void ScheduleWalkToNeighbour( Node target, bool first = false )
	{
		var instance = ScriptableObject.CreateInstance<WalkToNeighbour>();
		instance.Setup( this, target );
		ScheduleTask( instance, first );
	}

	public bool ScheduleGetToFlag()
	{
		if ( node.validFlag )
			return true;

		Flag destination = null;
		int closest = int.MaxValue;
		foreach ( var o in Ground.areas[Constants.Worker.flagSearchDistance] )
		{
			var n = node + o;
			if ( n.validFlag && n.validFlag.owner == owner && n.DistanceFrom( node ) < closest )
			{
				destination = n.flag;
				closest = n.DistanceFrom( node );
			}
		}
		if ( !destination )
			return false;
		ScheduleWalkToNode( destination.node );
		return true;
	}

	/// <summary>
	/// Adds a task to the worker to reach a specific node.
	/// </summary>
	/// <param name="target"></param>
	/// <param name="ignoreFinalObstacle"></param>
	/// <param name="first"></param>
	/// <param name="interruption"></param>
	/// <param name="findPathNow"></param>
	/// <returns>True, if a valid path is found.</returns>
	public void ScheduleWalkToNode( Node target, bool ignoreFinalObstacle = false, bool first = false, Act interruption = null, HiveObject ignoreObject = null )
	{
		var instance = ScriptableObject.CreateInstance<WalkToNode>();
		instance.Setup( this, target, ignoreFinalObstacle, interruption, ignoreObject );
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

	public void ScheduleWalkToRoadNode( Road road, Node target, bool exclusive = true, bool first = false )
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

	public void ScheduleDoAct( Act act, bool first = false )
	{
		var instance = ScriptableObject.CreateInstance<DoAct>();
		instance.Setup( this, act );
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
		if ( !item.path.isFinished )
		{
			assert.AreEqual( road, item.road );
		}
		int itemPoint = road.NodeIndex( item.flag.node ), otherPoint = 0;
		if ( itemPoint == 0 )
			otherPoint = road.nodes.Count - 1;
		Flag other = road.ends[otherPoint > 0 ? 1 : 0];

		if ( replace )
			assert.AreEqual( replace.flag, other );

		if ( item.buddy == null )
		{
			ScheduleWalkToRoadPoint( road, itemPoint );
			SchedulePickupItem( item );
		}

		if ( !item.path.isFinished )	// When the path is finished, the item is already at the target flag, the worker only needs to carry it inside a building
			ScheduleWalkToRoadPoint( road, otherPoint );

		if ( item.path.stepsLeft <= 1 )	// When the number of steps left is one or zero, the worker also has to carry the item into a building
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
				assert.IsTrue( other.freeSlots > 0 );
			other.ReserveItem( item, replace );
			ScheduleDeliverItem( item );
			if ( replace && item.buddy == null )
				CarryItem( replace, item );
		}
		item.worker = this;
	}

	public void ResetTasks()
	{
		foreach ( var task in taskQueue )
			task.Cancel();
		taskQueue.Clear();
	}

	static readonly float[] angles = new float[6] { 210, 150, 90, 30, 330, 270 };
	public void TurnTo( Node node, Node from = null )
	{
		from ??= this.node;
		int direction = from.DirectionTo( node );
		assert.IsTrue( direction >= 0, "Asked to turn towards a distant node" );
		transform.rotation = Quaternion.Euler( Vector3.up * angles[direction] );
	}

	public void UpdateBody()
	{
		if ( bodyState == BodyState.custom )
			return;

		if ( walkTo == null )
		{
			if ( bodyState != BodyState.standing )
			{
				animator?.SetBool( walkingID, false );
				soundSource?.Stop();
				node.ground.Link( this );
				transform.localPosition = node.position + Vector3.up * standingHeight;
				if ( taskQueue.Count > 0 )
				{
					WalkToRoadPoint task = taskQueue[0] as WalkToRoadPoint;
					if ( task == null || task.wishedPoint < 0 )
						return;

					TurnTo( task.road.nodes[task.wishedPoint] );
				}
				bodyState = BodyState.standing;
			}
			return;
		}

		//if ( bodyState != BodyState.walking )
		{
			animator?.SetBool( walkingID, true );
			bodyState = BodyState.walking;
		}

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
			transform.localPosition = Vector3.Lerp( walkFrom.GetPositionRelativeTo( walkTo ), walkTo.position, walkProgress ) + Vector3.up * Constants.Node.size * Constants.Road.bodyHeight;
			assert.IsTrue( walkTo.valid && walkFrom.valid, "Not valid" );
			TurnTo( walkTo, walkFrom );		// TODO We should not do this in every frame
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
					g.transform.Rotate( World.instance.timeFactor * currentSpeed * 300, 0, 0 );
				}
				body.transform.localRotation = Quaternion.Euler( ( walkTo.height - walkFrom.height ) / Constants.Node.size * -50, 0, 0 );
			}
		}
	}

	public bool IsIdle( bool inBuilding = false )
	{
		if ( taskQueue.Count != 0 || walkTo != null )
			return false;
		if ( !inBuilding || !( building is Workshop ) )
			return true;
		Workshop workshop = building as Workshop;
		if ( workshop && workshop.working && !workshop.gatherer && workshop.worker == this )
			return false;
		return node == building.node;
	}

	public bool Call( Road road, int point )
	{
		if ( this.road != road || !exclusiveMode )
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

	public override void OnClicked( bool show = false )
	{
		Interface.WorkerPanel.Create().Open( this, show );
	}

	public T FindTaskInQueue<T>() where T : class
	{
		foreach ( var task in taskQueue )
		{
			if ( task is T result )
				return result;
		}
		return null;
	}

	public int IndexOnRoad()
	{
		if ( !exclusiveMode )
			return -1;
		assert.IsNotNull( road );
		int i = road.NodeIndex( node );
		if ( i >= 0 )
			return i;
		foreach ( var o in Ground.areas[1] )
		{
			var nn = node.Add( o );
			if ( nn == road.nodes[0] )
				return 0;
			if ( nn == road.lastNode )
				return road.nodes.Count - 1;
		}
		return -1;
	}

	public override void Reset()
	{
		if ( type == Type.tinkerer )
			building.assert.IsNotSelected();
		ResetTasks();
		foreach ( var item in itemsInHands )
			item?.Remove( false );
		assert.IsNull( itemsInHands[0] );
		assert.IsNull( itemsInHands[1] );
		walkTo = walkFrom = null;
		walkProgress = 0;
		LeaveExclusivity();
		if ( type == Type.hauler )
		{
			assert.IsNotNull( road );
			int newIndex = road.nodes.Count / 2;
			SetNode( road.nodes[newIndex] );
			EnterExclusivity( road, road.nodes[newIndex] );
		}
		if ( type == Type.tinkerer || type == Type.tinkererMate || type == Type.cart )
			SetNode( building.node );
		if ( type == Type.constructor || type == Type.unemployed )
			Remove( false );
	}

	public override Node location { get { return node; } }

	public override void DestroyThis( bool noAssert = false )
	{
		if ( noAssert == false )
		{
			var box = links[(int)LinkType.haulingBoxLight];
			if ( box )
				assert.AreEqual( box.transform.childCount, 0 );		 // Triggered after undoing building remove
			box = links[(int)LinkType.haulingBoxHeavy];
			if ( box )
				assert.AreEqual( box.transform.childCount, 1 );
		}
		// TODO Triggered, called from Worker:FindTask() line 1342
		// Triggered again called from worker.FindTask, worker still has the plank in hand, after entering the headquarters. 
		// Item is still registered in Player.items, and still has a valid destination (sawmill) where it is still registered in 
		// Building.itemsOnTheWay. Item still has a flag, which is the one in front of the headquarters. 
		// The item is still registered there in Flag.items. nextFlag is null, worker is null. Origin is headquarters.
		// Hopefully fixed.
		// Triggered again, called from Worker.FindTask. Both items in itemsInHands is null. haulingBox is inactive, and has a child, a beer.
		// This beer has no worker and nextFlag, but the flag reference is valid (19:10). Has a destination (barrack 19:7) has a path
		// with two roads (between 20:9 and 19:11 then between 20:9 19:8)
		// Triggered again

		base.DestroyThis( noAssert );
	}

	public bool hasItems { get { return itemsInHands[0] != null; } }

	public void MakeSound( int soundID )
	{
		if ( soundSource )
		{
			var m = animationSounds.GetMedia( (AnimationSound)soundID );
			soundSource.clip = m.data;
			if ( m.floatData != 0 )
				soundSource.volume = m.floatData;
			else
				soundSource.volume = 1;
			soundSource.loop = false;
			
			assert.IsNotNull( soundSource.clip, $"No sound found for AnimationSouns.{((AnimationSound)soundID).ToString()}" );
			soundSource.Play();
		}
	}

	public void SetStandingHeight( float standingHeight )
	{
		this.standingHeight = standingHeight;
		transform.localPosition = node.position + Vector3.up * standingHeight;
	}

	public Node LeaveExclusivity()
	{
		if ( !exclusiveMode || road == null )
			return null;

		int index = road.workerAtNodes.IndexOf( this );
		if ( index < 0 )
		{
			assert.IsTrue( false );
			return null;
		}
		road.workerAtNodes[index] = null;

		if ( exclusiveFlag )
		{
			assert.AreEqual( exclusiveFlag.user, this );
			exclusiveFlag.user = null;
			exclusiveFlag = null;
		}

		exclusiveMode = false;
		return road.nodes[index];
	}

	public bool EnterExclusivity( Road road, Node node )
	{
		if ( node == null )
			return false;
		if ( node.flag && !node.flag.crossing && node.flag.user )
			return false;
		int index = road.nodes.IndexOf( node );	// This could be Road.NodeIndex, which is faster, but that depends on RegisterOnRoad, which is not always called at this moment
		if ( index < 0 )
			return false;
		if ( road.workerAtNodes[index] )
			return false;

		road.workerAtNodes[index] = this;

		if ( node.flag && !node.flag.crossing )
		{
			node.flag.user = this;
			exclusiveFlag = node.flag;
		}

		exclusiveMode = true;
		this.road = road;
		return true;
	}

	public override void Validate( bool chain )
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
		if ( exclusiveMode )
		{
			assert.IsValid( road );			// TODO Triggered when stress deleting all the roads flags and buildings on a map for a cart (?) going back home
											// TODO Triggered again when pressing the magnet icon on a flag. Worker is a cart which was rolling on the road which was merged to the flag by the magnet
											// The cart is just walking to the end of the road (an unaffected flag) the segment between walkTo and walkFrom is not affected by the magnet
											// It is in an exclusive mode, exclusiveFlag is correct, but the road field is referring to the old deleted road. The new road correctly has the card in the
											// workerAtNodes array
			if ( type == Type.hauler )
				assert.IsTrue( road.workers.Contains( this ) );
			int point = road.NodeIndex( node );
			if ( point < 0 && type == Type.hauler )
			{
				if ( itemsInHands[0] )
					assert.IsTrue( node.building || itemsInHands[0].tripCancelled );	// It is possible, that the item destination was destroyed during the last step
				else
					assert.IsTrue( node.building || walkTo == null );		// It is possible, that the building was just destroyed, but the worker did not yet start moving back to the road (?)
				if ( node.building )
				{
					point = road.NodeIndex( node.building.flag.node );
					assert.IsTrue( point >= 0 );
				}
			}
			if ( point >= 0 )
				assert.AreEqual( road.workerAtNodes[point], this );
		}
		foreach ( var item in itemsInHands )
		{
			if ( item == null )
				continue;

			assert.AreEqual( item.worker, this, "Unknown worker " + item.worker );
			assert.IsTrue( item.destination != null || item.tripCancelled || type == Type.tinkerer );	// destination is also null if a workshop is set to work "always"
			if ( chain )
				item.Validate( true );
			assert.IsNull( item.flag );	// ?
		}
		if ( itemsInHands[0] && itemsInHands[1] )
			assert.AreEqual( itemsInHands[0].type, itemsInHands[1].type );
		foreach ( Task task in taskQueue )
			task.Validate();	// Since tasks are not HiveObjects, chain validate them always
		if ( exclusiveFlag )
		{
			assert.IsFalse( exclusiveFlag.crossing );
			assert.IsTrue( type == Type.hauler || type == Type.cart );
			if ( type != Type.cart )
			{
				assert.IsTrue( exclusiveMode );
				assert.IsNotNull( road );
				assert.IsTrue( road.ends[0] == exclusiveFlag || road.ends[1] == exclusiveFlag );
			}
			assert.AreEqual( exclusiveFlag.user, this, "Flag exclusivity mismatch" );
		}
		if ( type == Type.tinkerer && building is Workshop workshop && workshop.gatherer )
		{
			if ( IsIdle( true ) )
				assert.IsNull( itemsInHands[0] );
			foreach ( var item in itemsInHands )
				if ( item && building.worker == this )
					assert.AreEqual( item.destination, building );
		}
		assert.IsTrue( owner == null || World.instance.players.Contains( owner ) );
		assert.IsTrue( registered );
	}
}