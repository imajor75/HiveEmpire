using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

public class OperationHandler : HiveObject
{
	public List<Operation> undoQueue = new List<Operation>(), redoQueue = new List<Operation>(), repeatBuffer = new List<Operation>();
    public List<int> CRCCodes = new List<int>();
    public int currentCRCCode;
    public Mode mode;
    public World.Challenge challenge;
    public int executeIndex = 0;
    public int finishedFrameIndex = -1;
    public int replayLength = -1;
    public string lastSave;
    public bool recordCRC;
    public bool insideFrame
    {
        get
        {
            return World.instance.time < finishedFrameIndex;
        }
    }

	static public Interface.Hotkey undoHotkey = new Interface.Hotkey( "Undo", KeyCode.Z, true );
	static public Interface.Hotkey redoHotkey = new Interface.Hotkey( "Redo", KeyCode.Y, true );

    public enum Mode
    {
        recording,
        repeating
    }

    public override Node location => throw new System.NotImplementedException();

    public Operation next 
    { 
        get 
        { 
            if ( executeIndex >= repeatBuffer.Count )
                return null;
            int skip = 0;
            while ( executeIndex+skip+1 < repeatBuffer.Count && repeatBuffer[executeIndex+skip+1].merge )
                skip++;
            return repeatBuffer[executeIndex+skip]; 
        } 
    }

    public static OperationHandler Create()
    {
        return new GameObject( "Operation handler").AddComponent<OperationHandler>();
    }

    new void Start()
    {
        transform.SetParent( Interface.root.transform );
        base.Start();
    }

    public void StartReplay( int from = 0 )
    {
        mode = Mode.repeating;
        finishedFrameIndex = World.instance.time;
        executeIndex = from;
    }

    void Update()
    {
		if ( undoHotkey.IsDown() )
			Undo();
		if ( redoHotkey.IsDown() )
			Redo();
    }

    public void ExecuteOperation( Operation operation )
	{
        operation.scheduleAt = World.instance.time;
        if ( !insideFrame )
            operation.scheduleAt++;
        repeatBuffer.Add( operation );
	}

	void RepeatOperation( List<Operation> from, List<Operation> to, bool merge = false )
	{
		if ( from.Count == 0 )
			return;
		var operation = from.Last();
        var hadMerge = operation.merge;
		from.Remove( operation );
		var inverted = operation.ExecuteAndInvert();
		if ( inverted )
        {
            inverted.merge = merge;
			to.Add( inverted );
        }
        if ( hadMerge )
            RepeatOperation( from, to, true );
	}

	public void Undo()
	{
		RepeatOperation( undoQueue, redoQueue );
	}

	public void Redo()
	{
		RepeatOperation( redoQueue, undoQueue );
	}

	public void ExecuteChangeRoadWorkerCount( Road road, int count )
	{
		ExecuteOperation( Operation.Create().SetupAsChangeWorkerCount( road, count ) );
	}

	public void ExecuteRemoveBuilding( Building building, bool merge = false )
	{
        if ( building )
		    ExecuteOperation( Operation.Create().SetupAsRemoveBuilding( building, merge ) );
	}

	public void ExecuteCreateBuilding( Node location, int direction, Building.Type buildingType, bool merge = false )
	{
		ExecuteOperation( Operation.Create().SetupAsCreateBuilding( location, direction, buildingType, merge ) );
	}

	public void ExecuteRemoveRoad( Road road, bool merge = false )
	{
        if ( road )
		    ExecuteOperation( Operation.Create().SetupAsRemoveRoad( road, merge ) );
	}

	public void ExecuteCreateRoad( Road road )
	{
		ExecuteOperation( Operation.Create().SetupAsCreateRoad( road.nodes ) );
	}

	public void ExecuteRemoveFlag( Flag flag )
	{
        if ( flag == null )
            return;

        bool merge = false;
        foreach ( var building in flag.Buildings() )
        {
            ExecuteRemoveBuilding( building, merge );
            merge = true;
        }
        List<Road> realRoads = new List<Road>();
        foreach ( var road in flag.roadsStartingHere )
            if ( road )
                realRoads.Add( road );
        if ( realRoads.Count != 2 )
        {
            foreach ( var road in realRoads )
                ExecuteRemoveRoad( road, merge );
            merge = true;
        }
        
		ExecuteOperation( Operation.Create().SetupAsRemoveFlag( flag, merge ) );
	}

	public void ExecuteCreateFlag( Node location, bool crossing = false )
	{
		ExecuteOperation( Operation.Create().SetupAsCreateFlag( location, crossing ) );
	}

	public void ExecuteRemoveFlag( Flag flag, bool merge = false )
	{
		ExecuteOperation( Operation.Create().SetupAsRemoveFlag( flag, merge ) );
	}

	public void ExecuteChangeArea( Ground.Area area, Node center, int radius )
	{
        if ( area != null )
		    ExecuteOperation( Operation.Create().SetupAsChangeArea( area, center, radius ) );
	}

    public void ExecuteMoveFlag( Flag flag, int direction )
    {
        ExecuteOperation( Operation.Create().SetupAsMoveFlag( flag, direction ) );
    }

    public void ExecuteChangePriority( Stock.Route route, int direction )
    {
        ExecuteOperation( Operation.Create().SetupAsChangePriority( route, direction ) );
    }

    public void ExecuteMoveRoad( Road road, int index, int direction )
    {
        ExecuteOperation( Operation.Create().SetupAsMoveRoad( road, index, direction ) );
    }

    void FixedUpdate()
    {
        if ( this != World.instance.operationHandler )
            return;

#if DEBUG
        if ( recordCRC && mode == Mode.recording && World.instance.speed != World.Speed.pause )
        {
            assert.AreEqual( World.instance.time, CRCCodes.Count );
            CRCCodes.Add( currentCRCCode );
        }
        if ( mode == Mode.repeating && CRCCodes.Count > World.instance.time )
            assert.AreEqual( CRCCodes[World.instance.time], currentCRCCode, "CRC mismatch" );
        currentCRCCode = 0;
#endif

        World.instance.fixedOrderCalls = true;
        while ( executeIndex < repeatBuffer.Count && repeatBuffer[executeIndex].scheduleAt == World.instance.time )
        {
            HiveObject.Log( $"Executing {repeatBuffer[executeIndex].name}" );
            var inverse = repeatBuffer[executeIndex].ExecuteAndInvert();
            if ( inverse )
            {
                undoQueue.Add( inverse );
                redoQueue.Clear();
            }
            else
                assert.Fail( "Not invertible operation" );
            executeIndex++;
        }

        finishedFrameIndex++;
        if ( finishedFrameIndex == replayLength )
        {
            Assert.global.AreEqual( mode, Mode.repeating );
            mode = Mode.recording;
        }
        World.instance.fixedOrderCalls = false;

        assert.AreEqual( finishedFrameIndex, World.instance.time );
    }
}

public class Operation : ScriptableObject
{
    public Type type;
    public int workerCount;
    public int locationX, locationY;
    public Building.Type buildingType;
    public int direction;
    public List<int> roadPathX, roadPathY;
    public bool crossing;
    public bool merge;
    public Ground.Area area;            // TODO We cannot store references here
    public int radius;
    public int endLocationX, endLocationY;
    public Item.Type itemType;
    public int scheduleAt;

    public Node location
    {
        get
        {
            return World.instance.ground.GetNode( locationX, locationY );
        }
        set
        {
            locationX = value.x;
            locationY = value.y;
        }
    }
    public Building building
    {
        get
        {
            return location.building;
        }
        set
        {
            location = value.node;
        }
    }
    public Road road
    {
        get
        {
            return location.road;
        }
        set
        {
            location = value.nodes[1];
        }
    }
    public Flag flag
    {
        get
        {
            return location.flag;
        }
        set
        {
            location = value.location;
        }
    }
    public Stock start
    {
        get
        {
            return location.building as Stock;
        }
        set
        {
            location = value.node;
        }
    }
    public Stock end
    {
        get
        {
            return World.instance.ground.GetNode( endLocationX, endLocationY ).building as Stock;
        }
        set
        {
            endLocationX = value.node.x;
            endLocationY = value.node.y;
        }
    }
    public Stock.Route route
    {
        get
        {
            return start.itemData[(int)itemType].GetRouteForDestination( end );
        }
        set
        {
            start = value.start;
            end = value.end;
            itemType = value.itemType;
        }
    }
    public List<Node> roadPath
    {
        get
        {
            if ( roadPathX == null )
                return null;
            List<Node> roadPath = new List<Node>();
            Assert.global.AreEqual( roadPathX.Count, roadPathY.Count );
            for ( int i = 0; i < roadPathX.Count; i++ )
            roadPath.Add( World.instance.ground.GetNode( roadPathX[i], roadPathY[i] ) );
            return roadPath;
        }
        set
        {
            roadPathX = new List<int>();
            roadPathY = new List<int>();
            foreach ( var node in value )
            {
                roadPathX.Add( node.x );
                roadPathY.Add( node.y );
            }
        }
    }
    public string description
    {
        get
        {
            string text = type switch
            {
                Type.changeArea => "Change area",
                Type.changeRoutePriority => "Change route priority",
                Type.changeWorkerCount => "Change worker count",
                Type.createBuilding => "Constructing a new ",
                Type.createFlag => "Creating a new flag",
                Type.createRoad => "Create new road",
                Type.moveFlag => "Moving a flag",
                Type.moveRoad => "Move flag",
                Type.removeBuilding => "Remove a building",
                Type.removeFlag => "Remove a flag",
                Type.removeRoad => "Remove a road",
                _ => ""
            };
            if ( type == Type.createBuilding )
                text += buildingType.ToString();
            return text;
        }
    }
    [JsonProperty]
    public string title
    {
        get { return name; }
        set { name = value; }
    }

	[Obsolete( "Compatibility for old files", true )]
    Workshop.Type workshopType { set {} }

    public enum Type
    {
        changeWorkerCount,
        removeBuilding,
        createBuilding,
        removeRoad,
        createRoad,
        removeFlag,
        createFlag,
        changeArea,
        moveFlag,
        changeRoutePriority,
        moveRoad
    }

    public static Operation Create()
    {
        return ScriptableObject.CreateInstance<Operation>();
    }

    public Operation SetupAsChangeWorkerCount( Road road, int count )
    {
        type = Type.changeWorkerCount;
        this.road = road;
        workerCount = count;
        name = "Change Worker Count";
        return this;
    }

    public Operation SetupAsRemoveBuilding( Building building, bool merge = false )
    {
        type = Type.removeBuilding;
        this.building = building;
        this.merge = merge;
        name = "Delete Building";
        return this;
    }

    public Operation SetupAsCreateBuilding( Node location, int direction, Building.Type buildingType, bool merge = false )
    {
        type = Type.createBuilding;
        this.location = location;
        this.direction = direction;
        this.buildingType = buildingType;
        this.merge = merge;
        name = "Create Building";
        return this;
    }

    public Operation SetupAsRemoveRoad( Road road, bool merge = false )
    {
        type = Type.removeRoad;
        this.road = road;
        this.merge = merge;
        name = "Remove Road";
        return this;
    }

    public Operation SetupAsCreateRoad( List<Node> path )
    {
        type = Type.createRoad;
        this.roadPath = path;
        name = "Create Road";
        return this;
    }

    public Operation SetupAsRemoveFlag( Flag flag, bool merge = false )
    {
        type = Type.removeFlag;
        this.flag = flag;
        this.merge = merge;
        name = "Remove Flag";
        return this;
    }

    public Operation SetupAsCreateFlag( Node location, bool crossing )
    {
        type = Type.createFlag;
        this.location = location;
        this.crossing = crossing;
        name = "Create Flag";
        return this;
    }

    public Operation SetupAsMoveFlag( Flag flag, int direction, bool merge = false )
    {
        type = Type.moveFlag;
        this.flag = flag;
        this.direction = direction;
        this.merge = merge;
        name = "Move Flag";
        return this;
    }

    public Operation SetupAsChangeArea( Ground.Area area, Node center, int radius )
    {
        type = Type.changeArea;
        this.area = area;
        this.location = center;
        this.radius = radius;
        name = "Change Area";
        return this;
    }

    public Operation SetupAsChangePriority( Stock.Route route, int direction )
    {
        type = Type.changeRoutePriority;
        this.route = route;
        this.direction = direction;
        name = "Change Route Priority";
        return this;
    }

    public Operation SetupAsMoveRoad( Road road, int index, int direction, bool merge = false )
    {
        type = Type.moveRoad;
        location = road.nodes[index];
        this.direction = direction;
        this.merge = merge;
        name = "Move Road";
        return this;
    }

    public Operation ExecuteAndInvert()
    {
        switch ( type )
        {
            case Type.changeWorkerCount:
            {
                int oldCount = road.targetWorkerCount;
                road.targetWorkerCount = workerCount;
                return Create().SetupAsChangeWorkerCount( road, oldCount );
            }
            case Type.removeBuilding:
            {
                Building building = this.building;
                var inverse = Operation.Create();
                if ( building is Workshop workshop )
                    inverse.SetupAsCreateBuilding( building.node, building.flagDirection, (Building.Type)workshop.type );
                if ( building is Stock )
                    inverse.SetupAsCreateBuilding( building.node, building.flagDirection, Building.Type.stock );
                if ( building is GuardHouse )
                    inverse.SetupAsCreateBuilding( building.node, building.flagDirection, Building.Type.guardHouse );

                building.Remove( true );
                return inverse;
            }
            case Type.createBuilding:
            {
                Building newBuilding = location.building;
                if ( !newBuilding )
                {
                    if ( buildingType < (Building.Type)Workshop.Type.total )
                        newBuilding = Workshop.Create().Setup( location, Interface.root.mainPlayer, (Workshop.Type)buildingType, direction );
                    if ( buildingType == Building.Type.stock )
                        newBuilding = Stock.Create().Setup( location, Interface.root.mainPlayer, direction );
                    if ( buildingType == Building.Type.guardHouse )
                        newBuilding = GuardHouse.Create().Setup( location, Interface.root.mainPlayer, direction );
                }
                else
                {
                    newBuilding.assert.IsTrue( newBuilding.blueprintOnly );
                    newBuilding.Materialize();
                }

                if ( newBuilding )
                    return Create().SetupAsRemoveBuilding( newBuilding );
                else
                    return null;
            }
            case Type.removeRoad:
            {
                if ( road == null || !road.Remove( true ) )
                    return null;
                return Create().SetupAsCreateRoad( road.nodes );    // TODO Seems to be dangerous to use the road after it was removed
            }
            case Type.createRoad:
            {
                Road newRoad = roadPath[1].road;
                if ( !newRoad )
                {
                    newRoad = Road.Create().Setup( roadPath[0].flag );
                    if ( newRoad )
                    {
                        bool allGood = true;
                        for ( int i = 1; i < roadPath.Count && allGood; i++ )
                            allGood &= newRoad.AddNode( roadPath[i] );
                        if ( allGood )
                        {
                                if ( !newRoad.Finish() )
                                {
                                    newRoad.Remove( false );
                                    newRoad = null;
                                }

                        }
                    }
                }
                else
                {
                    newRoad.assert.IsTrue( newRoad.blueprintOnly );
                    newRoad.Finish();
                }

                if ( newRoad )
                    return Create().SetupAsRemoveRoad( newRoad );
                else
                    return null;
            }
            case Type.removeFlag:
            {
                if ( flag == null || !flag.Remove( true ) )
                    return null;

                return Create().SetupAsCreateFlag( flag.node, flag.crossing );
            }
            case Type.createFlag:
            {
                Flag newFlag = location.flag;
                if ( !newFlag )
                    newFlag = Flag.Create().Setup( location, Interface.root.mainPlayer, false, crossing );
                else
                {
                    newFlag.assert.IsTrue( newFlag.blueprintOnly );
                    newFlag.Materialize();
                }
                    
                if ( newFlag == null )
                    return null;
                return Create().SetupAsRemoveFlag( newFlag );
            }
            case Type.moveFlag:
            {
                if ( !flag.Move( direction ) )
                    return null;
                return Create().SetupAsMoveFlag( flag, ( direction + Constants.Node.neighbourCount / 2 ) % Constants.Node.neighbourCount );
            }
            case Type.changeArea:
            {
                if ( area == null )
                    return null;
                var oldCenter = area.center;
                var oldRadius = area.radius;
                area.center = location;
                area.radius = radius;
                return Create().SetupAsChangeArea( area, oldCenter, oldRadius );
            }
            case Type.changeRoutePriority:
            {
                if ( route == null )
                    return null;
                route.priority += direction;
                route.start.owner.UpdateStockRoutes( route.itemType );
                return Create().SetupAsChangePriority( route, direction * -1 );
            }
            case Type.moveRoad:
            {
                var index = location.road.nodes.IndexOf( location );
                if ( !location.road.Move( index, direction ) )
                    return null;
                return Create().SetupAsMoveRoad( road, index, ( direction + Constants.Node.neighbourCount / 2 ) % Constants.Node.neighbourCount );
            }
        }
        return null;
    }
}
