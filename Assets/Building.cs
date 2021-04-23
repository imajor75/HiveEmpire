﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

[SelectionBase]
abstract public class Building : HiveObject
{
	public string title;
	public Player owner;
	public Worker worker, workerMate;
	public Flag flag;
	public int flagDirection;
	public Ground ground;
	public GroundNode node;
	[JsonIgnore]
	public Road exit;
	[JsonIgnore]
	public List<MeshRenderer> renderers;
	public Construction construction = new Construction();
	public const int flatteningTime = 220;
	public float height = 1.5f;
	public float levelBrake = 1;
	public List<Item> itemsOnTheWay = new List<Item>();
	public GroundNode.Type groundTypeNeeded = GroundNode.Type.land;
	public bool huge;
	static List<Ground.Offset> foundationHelper;
	[JsonIgnore]
	public List<Ground.Offset> Foundation
	{
		get
		{
			return GetFoundation( huge, flagDirection );
		}
	}
	static public List<Ground.Offset> GetFoundation( bool huge, int flagDirection )
	{
		var list = new List<Ground.Offset> { new Ground.Offset( 0, 0, 0 ) };
		if ( huge )
		{
			list.Add( foundationHelper[( flagDirection + 2 ) % GroundNode.neighbourCount] );
			list.Add( foundationHelper[( flagDirection + 3 ) % GroundNode.neighbourCount] );
			list.Add( foundationHelper[( flagDirection + 4 ) % GroundNode.neighbourCount] );
		}
		return list;
	}
	GameObject highlightArrow;
	bool currentHighlight;
	float currentLevel;
	static int highlightID;

	[System.Serializable]
	public class Configuration
	{
		public GroundNode.Type groundTypeNeeded = GroundNode.Type.land;

		public int plankNeeded = 2;
		public int stoneNeeded = 0;
		public bool flatteningNeeded = true;
		public bool huge = false;
		public int constructionTime = 0;
	}

	[System.Serializable]
	public class Flattening : Worker.Callback.IHandler
	{
		public bool flatteningNeeded;
		public int corner;
		public bool permanent;
		public HiveObject ignoreDuringWalking;
		public World.Timer suspend;
		public float level;
		public Worker worker;
		public List<GroundNode> area;

		[JsonIgnore, Obsolete( "Compatibility with old files", true )]
		public bool flatteningCorner;
		[JsonIgnore, Obsolete( "Compatibility with old files", true )]
		public List<GroundNode> flatteningArea;

		public void Setup( List<GroundNode> area, bool permanent = true, HiveObject ignoreDuringWalking = null )
		{
			this.area = area;
			this.ignoreDuringWalking = ignoreDuringWalking;
			this.permanent = permanent;
			this.corner = 0;
			flatteningNeeded = true;
			level = 0;
			foreach ( var o in area )
			{
				if ( o.staticHeight > 0 )
				{
					level = o.staticHeight;
					break;
				}
				level += o.height / area.Count;
			}
			if ( permanent )
				foreach ( var node in area )
					node.staticHeight = level;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns>True if the function call was useful</returns>
		public bool FixedUpdate()
		{
			if ( flatteningNeeded == false )
			{
				worker?.Remove( true );
				worker = null;
				return false;
			}

			if ( worker == null )
			{	// TODO Check the path before creating the worker
				worker = Worker.Create();
				worker.SetupForFlattening( area[0].flag );
			}
			if ( worker == null || !worker.IsIdle( false ) )
				return true;
			if ( corner < area.Count )
			{
				if ( worker && worker.IsIdle() )
				{
					GroundNode node = area[corner++];
					if ( node.fixedHeight && node.staticHeight != level )
						return true;
					float dif = node.height - level;
					if ( Math.Abs( dif ) > 0.001f )
					{
						worker.ScheduleWalkToNode( node, true, false, null, ignoreDuringWalking );
						worker.ScheduleDoAct( Worker.shovelingAct );
						worker.ScheduleCall( this );
					}
				}
				return true;
			}
			flatteningNeeded = false;
			return false;
		}

		public void Callback( Worker worker )
		{
			if ( permanent || worker.node.fixedHeight == false )
				worker.node.SetHeight( level );
		}
	}

	[System.Serializable]
	public class Construction : Flattening
	{
		public Building boss;
		public bool done;
		public int duration = 1000;
		public float progress;
		public int plankNeeded;
		public int plankOnTheWay;
		public int plankArrived;
		public int stoneNeeded;
		public int stoneOnTheWay;
		public int stoneArrived;
		public static Shader shader;
		public static int sliceLevelID;
		[JsonIgnore, Obsolete( "Compatibility with old files", true )]
		public int timeSinceCreated;
		public Worker.DoAct hammering;

		static public void Initialize()
		{
			shader = (Shader)Resources.Load( "Construction" );
			Assert.global.IsNotNull( shader );
			sliceLevelID = Shader.PropertyToID( "_SliceLevel" );
		}

		public void Setup( Building boss )
		{
			this.boss = boss;
			List<GroundNode> flatteningArea = new List<GroundNode>();
			var area = boss.Foundation;
			flatteningArea.Add( boss.node );
			foreach ( var o in area )
			{
				GroundNode basis = boss.node.Add( o );
				foreach ( var b in Ground.areas[1] )
				{
					GroundNode node = basis.Add( b );
					if ( !flatteningArea.Contains( node ) )
						flatteningArea.Add( node );
				}
			}
			boss.assert.IsTrue( flatteningArea.Count == 7 || flatteningArea.Count == 14, "Area has " + flatteningArea.Count + " nodes" );
			base.Setup( flatteningArea, true, boss );
		}

		public bool Remove( bool takeYourTime )
		{
			hammering?.Stop();
			worker?.Remove( takeYourTime );
			return true;
		}

		public void Update( Building building )
		{
			if ( done || boss.blueprintOnly )
				return;

			int plankMissing = plankNeeded - plankOnTheWay - plankArrived;
			boss.owner.itemDispatcher.RegisterRequest( building, Item.Type.plank, plankMissing, ItemDispatcher.Priority.high, Ground.Area.global, boss.owner.plankForConstructionWeight.weight );
			int stoneMissing = stoneNeeded - stoneOnTheWay - stoneArrived;
			boss.owner.itemDispatcher.RegisterRequest( building, Item.Type.stone, stoneMissing, ItemDispatcher.Priority.high, Ground.Area.global, boss.owner.stoneForConstructionWeight.weight );
		}

		new public void FixedUpdate()
		{
			if ( done || suspend.InProgress || boss.blueprintOnly )
				return;

			if ( worker == null && Path.Between( boss.owner.mainBuilding.flag.node, boss.flag.node, PathFinder.Mode.onRoad, boss ) != null )
			{
				worker = Worker.Create();
				worker.SetupForConstruction( boss );
				return;
			}

			if ( worker == null )
			{
				suspend.Start( 200 );
				return;
			};

			if ( flatteningNeeded && base.FixedUpdate() )
				return;

			if ( !worker.IsIdle() )
				return;

			if ( progress == 0 )
			{
				var o = new Ground.Offset( 0, -1, 1 );
				GroundNode node = boss.node.Add( o );
				if ( worker.node != node )
				{
					worker.ScheduleWalkToNode( node, true, false, null, boss );
					return;
				}

				worker.TurnTo( boss.node );
				hammering = ScriptableObject.CreateInstance<Worker.DoAct>();
				hammering.Setup( worker, Worker.constructingAct );
				hammering.Start();
			}
			progress += boss.ground.world.timeFactor / duration;
			float maxProgress = ((float)plankArrived+stoneArrived)/(plankNeeded+stoneNeeded);
			if ( progress > maxProgress )
			{
				progress = maxProgress;
				hammering.Stop();
			}
			else
				hammering.Start();

			if ( progress < 1 )
				return;

			done = true;
			worker.ScheduleWalkToNeighbour( boss.flag.node );
			worker.type = Worker.Type.unemployed;
			worker = null;
			hammering = null;
		}
		public bool ItemOnTheWay( Item item, bool cancel = false )
		{
			if ( done )
				return false;

			if ( item.type == Item.Type.plank )
			{
				if ( cancel )
				{
					plankOnTheWay--;
					boss.assert.IsTrue( plankOnTheWay >= 0 );
				}
				else
				{
					plankOnTheWay++;
					boss.assert.IsTrue( plankArrived + plankOnTheWay <= plankNeeded );
				}
				return true;
			}
			if ( item.type == Item.Type.stone )
			{
				if ( cancel )
				{
					stoneOnTheWay--;
					boss.assert.IsTrue( stoneOnTheWay >= 0 );
				}
				else
				{
					stoneOnTheWay++;
					boss.assert.IsTrue( stoneArrived + stoneOnTheWay <= stoneNeeded );
				}
				return true;			}

			boss.assert.Fail( "Item is not expected (" + item.type + ")" );
			return false;
		}

		public virtual bool ItemArrived( Item item )
		{
			if ( done )
				return false;

			if ( item.type == Item.Type.plank )
			{
				boss.assert.IsTrue( plankOnTheWay > 0 );
				plankOnTheWay--;
				plankArrived++;
				boss.assert.IsTrue( plankArrived + plankOnTheWay <= plankNeeded );
				return true;
			}
			if ( item.type == Item.Type.stone )
			{
				boss.assert.IsTrue( stoneOnTheWay > 0 );
				stoneOnTheWay--;
				stoneArrived++;
				boss.assert.IsTrue( stoneArrived + stoneOnTheWay <= stoneNeeded );
				return true;
			}

			boss.assert.Fail();
			return false;
		}

		public void Validate()
		{
			worker?.Validate();
			if ( !done )
				boss.assert.AreEqual( plankOnTheWay + stoneOnTheWay, boss.itemsOnTheWay.Count );
		}
	}

	public static void Initialize()
	{
		foundationHelper = new List<Ground.Offset>
		{
			new Ground.Offset( 0, -1, 1 ),
			new Ground.Offset( 1, -1, 1 ),
			new Ground.Offset( 1, 0, 1 ),
			new Ground.Offset( 0, 1, 1 ),
			new Ground.Offset( -1, 1, 1 ),
			new Ground.Offset( -1, 0, 1 )
		};

		highlightID = Shader.PropertyToID( "_StencilRef" );
		Construction.Initialize();
	}

	static public bool IsNodeSuitable( GroundNode placeToBuild, Player owner, Configuration configuration, int flagDirection )
	{
		var area = GetFoundation( configuration.huge, flagDirection );

		foreach ( var o in area )
		{
			var basis = placeToBuild.Add( o );
			if ( basis.IsBlocking() )
				return false;
			if ( basis.owner != owner )
				return false;
			if ( configuration.flatteningNeeded )
			{
				foreach ( var b in Ground.areas[1] )
				{
					var perim = basis.Add( b );
					if ( perim.owner != owner )
						return false;
					if ( perim.fixedHeight )
						return false;
				}
			}
			if ( !basis.CheckType( configuration.groundTypeNeeded ) )
				return false;
		}
		GroundNode flagLocation = placeToBuild.Neighbour( flagDirection );
		return flagLocation.validFlag || Flag.IsNodeSuitable( flagLocation, owner );
	}

	public Building Setup( GroundNode node, Player owner, Configuration configuration, int flagDirection, bool blueprintOnly = false )
	{
		if ( !IsNodeSuitable( node, owner, configuration, flagDirection ) )
		{
			Destroy( gameObject );
			return null;
		}
		var flagNode = node.Neighbour( flagDirection );
		Flag flag = flagNode.validFlag;
		if ( flag == null )
			flag = Flag.Create().Setup( flagNode, owner, blueprintOnly );
		if ( flag == null )
		{
			Debug.Log( "Flag couldn't be created" );
			Destroy( gameObject );
			return null;
		}

		ground = node.ground;
		this.flag = flag;
		this.flagDirection = flagDirection;
		this.owner = owner;
		this.blueprintOnly = blueprintOnly;

		this.node = node;
		construction.Setup( this );
		var area = Foundation;
		foreach ( var o in area )
		{
			var basis = node.Add( o );
			basis.building = this;
		}

		return this;
	}

	public override void Materialize()
	{
		if ( flag.blueprintOnly )
			flag.Materialize();
		base.Materialize();
	}

	public void Start()
	{
		name = "Building " + node.x + ", " + node.y;
		transform.SetParent( ground.transform );
		UpdateBody();
		renderers = new List<MeshRenderer>();

		World.CollectRenderersRecursive( gameObject, renderers );
		float lowerLimit = transform.position.y;
		float upperLimit = lowerLimit + height;
		float level = upperLimit;
		if ( !construction.done )
			level = lowerLimit + ( upperLimit - lowerLimit ) * (float)Math.Pow( construction.progress, levelBrake );
		foreach ( var renderer in renderers )
		{
			foreach ( var m in renderer.materials )
			{
				m.shader = Construction.shader;
				m.SetFloat( Construction.sliceLevelID, level );
			}
		}

		assert.IsNull( exit, "Building already has an exit road" );
		exit = Road.Create();
		exit.SetupAsBuildingExit( this, blueprintOnly );
		highlightArrow = Instantiate( Resources.Load<GameObject>( "prefabs/others/gem" ) );
		highlightArrow.transform.SetParent( transform );
		highlightArrow.transform.localScale = Vector3.one * 3f;
	}

	public void FixedUpdate()
	{
		Profiler.BeginSample( "Construction" );
		construction.FixedUpdate();
		Profiler.EndSample();
	}

	public void Update()
	{
		construction.Update( this );
		UpdateLook();
	}

	public virtual Item SendItem( Item.Type itemType, Building destination, ItemDispatcher.Priority priority )
	{
		Worker worker = workerMate ?? this.worker;
		if ( worker == null || !worker.IsIdle( true ) || flag.FreeSpace() == 0 )
			return null;

		worker.gameObject.SetActive( true );
		// TODO Don't create the item, if there is no path between this and destination
		Item item = Item.Create().Setup( itemType, this, destination, priority );
		if ( item != null )
		{
			flag.ReserveItem( item );
			worker.SchedulePickupItem( item );
			worker.ScheduleWalkToNeighbour( flag.node );
			worker.ScheduleDeliverItem( item );
			worker.ScheduleWalkToNeighbour( node );
			item.worker = worker;
		}
		return item;
	}

	public virtual void ItemOnTheWay( Item item, bool cancel = false )
	{
		if ( cancel )
		{
			item.assert.IsTrue( itemsOnTheWay.Contains( item ) );
			itemsOnTheWay.Remove( item );
		}
		else
			itemsOnTheWay.Add( item );
		construction.ItemOnTheWay( item, cancel );
	}

	public virtual void ItemArrived( Item item )
	{
		item.assert.IsTrue( itemsOnTheWay.Contains( item ) );
		itemsOnTheWay.Remove( item );
		construction.ItemArrived( item );
	}

	public override void OnClicked()
	{
		if ( !construction.done )
			Interface.ConstructionPanel.Create().Open( construction );
	}

	public void UpdateLook()
	{
		float lowerLimit = transform.position.y;
		float upperLimit = lowerLimit + height;
		float level = upperLimit;
		if ( !construction.done && !blueprintOnly )
			level = lowerLimit + ( upperLimit - lowerLimit ) * (float)Math.Pow( construction.progress, levelBrake );

		bool highlight = Interface.root.highlightType == Interface.HighlightType.stocks && this as Stock;

		if ( currentHighlight != highlight || currentLevel != level )
		{
			currentLevel = level;
			currentHighlight = highlight;
			foreach ( var r in renderers )
			{
				foreach ( var m in r.materials )
				{
					m.SetFloat( Construction.sliceLevelID, level );
					m.SetInt( highlightID, highlight ? 1 : 0 );
				}
			}
		}

		if ( Interface.root.highlightType == Interface.HighlightType.area && Interface.root.highlightArea != null && Interface.root.highlightArea.IsInside( node ) )
		{
			highlightArrow.transform.localPosition = Vector3.up * ( ( float )( 1.5f + 0.3f * Math.Sin( 2 * Time.time ) ) );
			highlightArrow.transform.rotation = Quaternion.Euler( 0, Time.time * 200, 0 );
			highlightArrow.SetActive( true );
		}
		else
			highlightArrow.SetActive( false );
	}

	public override bool Remove( bool takeYourTime )
	{
		construction.Remove( takeYourTime );

		var list = itemsOnTheWay.GetRange( 0, itemsOnTheWay.Count );
		foreach ( var item in list )
			item.CancelTrip();
		if ( !exit.Remove( takeYourTime ) )
			return false;
		if ( worker != null && !worker.Remove() )
			return false;
		if ( workerMate != null && !workerMate.Remove() )
			return false;

		if ( construction.area != null )	// Should never be null, but old saves are having this.
			foreach ( var o in construction.area )
				o.fixedHeight = false;
		var area = Foundation;
		foreach ( var o in area )
		{
			var basis = node.Add( o );
			assert.AreEqual( basis.building, this );
			basis.building = null;
		}
		int roads = 0;
		foreach ( var road in flag.roadsStartingHere )
			if ( road )
				roads++;
		if ( roads == 0 )
			flag.Remove();
		owner?.versionedBuildingDelete.Trigger();
		Destroy( gameObject );
		return true;
	}

	public virtual int Influence( GroundNode node )
	{
		return 0;
	}

	public void UpdateBody()
	{
		transform.localPosition = node.Position;
		exit?.RebuildMesh( true );
	}

	public override void Reset()
	{
		worker?.Reset();
		workerMate?.Reset();
	}

	public override GroundNode Node { get { return node; } }

	public override void Validate()
	{
		assert.IsTrue( flag.Buildings().Contains( this ) );
		assert.AreEqual( this, node.building );
		assert.AreEqual( flag, node.Neighbour( flagDirection ).flag );
		worker?.Validate();
		workerMate?.Validate();
		exit?.Validate();
		construction?.Validate();
		foreach ( var item in itemsOnTheWay )
		{
			assert.IsNotNull( item );	// TODO Triggered for a sawmill (6 items are in the itemsOnTheWay array, one of them is missing. The missing item has a flag and a nextFlag, and a valid path, it also has a worker.
			// Originated at a stock. The worker has another log in hands. The path of the item has 7 roads, progress is 4
			assert.AreEqual( item.destination, this );
		}
	}
}
