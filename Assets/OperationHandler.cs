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
    public int currentGroup = 0;
    public string lastSave;
    public bool recordCRC;
    public bool recalculateCRC;
    public bool insideFrame
    {
        get
        {
            return time < finishedFrameIndex;
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
            while ( executeIndex+skip+1 < repeatBuffer.Count && repeatBuffer[executeIndex+skip+1].group == repeatBuffer[executeIndex+skip].group )
                skip++;
            return repeatBuffer[executeIndex+skip]; 
        } 
    }

    public static OperationHandler Create()
    {
        return new GameObject( "Operation handler").AddComponent<OperationHandler>();
    }

    public new OperationHandler Setup()
    {
        base.Setup();
        return this;
    }

    new void Start()
    {
        transform.SetParent( root.transform );
        base.Start();
    }

    public void StartReplay( int from = 0, bool recalculateCRC = false )
    {
        world.roadTutorialShowed = world.createRoadTutorialShowed = true;
        mode = Mode.repeating;
        finishedFrameIndex = time;
        executeIndex = from;
        this.recalculateCRC = recalculateCRC;
    }

    public void CancelReplay()
    {
        Assert.global.AreEqual( mode, Mode.repeating );
        mode = Mode.recording;
        repeatBuffer.RemoveRange( executeIndex, repeatBuffer.Count - executeIndex );
        CRCCodes.RemoveRange( time + 1, CRCCodes.Count - time - 1 );
        replayLength = 0;
    }

    public void StartGroup()
    {
        currentGroup++;
    }

    public void ScheduleOperation( Operation operation, bool standalone = true )
	{
        if ( standalone )
            currentGroup++;
        operation.scheduleAt = time;
        if ( operation.group < 0 )
            operation.group = currentGroup;
        if ( !insideFrame && world.speed != World.Speed.pause )
            operation.scheduleAt++;
        repeatBuffer.Add( operation );
	}

	public void UndoRedo( List<Operation> queue )
	{
        if ( queue.Count == 0 )
            return;
        var operation = queue.Last();
        ExecuteOperation( operation );
        queue.RemoveAt( queue.Count - 1 );
        if ( queue.Count > 0 && operation.group == queue.Last().group )
            UndoRedo( queue );
	}

    public void SaveReplay( string name )
    {
		undoQueue.Clear();		// TODO Is this necessary?
		redoQueue.Clear();
		if ( finishedFrameIndex > replayLength )
			replayLength = finishedFrameIndex;
		Serializer.Write( name, this, true );
    }

	public void ScheduleChangeRoadWorkerCount( Road road, int count )
	{
		ScheduleOperation( Operation.Create().SetupAsChangeWorkerCount( road, count ) );
	}

	public void ScheduleRemoveBuilding( Building building, bool standalone = true )
	{
        if ( building )
		    ScheduleOperation( Operation.Create().SetupAsRemoveBuilding( building ), standalone );
	}

	public void ScheduleCreateBuilding( Node location, int direction, Building.Type buildingType, bool standalone = true )
	{
		ScheduleOperation( Operation.Create().SetupAsCreateBuilding( location, direction, buildingType ), standalone );
	}

	public void ScheduleRemoveRoad( Road road, bool standalone = true )
	{
        if ( road )
		    ScheduleOperation( Operation.Create().SetupAsRemoveRoad( road ), standalone );
	}

	public void ScheduleCreateRoad( Road road, bool standalone = true )
	{
		ScheduleOperation( Operation.Create().SetupAsCreateRoad( road.nodes ), standalone );
	}

	public void ScheduleRemoveFlag( Flag flag )
	{
        if ( flag == null )
            return;

        StartGroup();
        foreach ( var building in flag.Buildings() )
            ScheduleRemoveBuilding( building, false );
        List<Road> realRoads = new List<Road>();
        foreach ( var road in flag.roadsStartingHere )
            if ( road )
                realRoads.Add( road );
        if ( realRoads.Count != 2 )
        {
            foreach ( var road in realRoads )
                ScheduleRemoveRoad( road, false );
        }
        
		ScheduleOperation( Operation.Create().SetupAsRemoveFlag( flag ), false );
	}

	public void ScheduleCreateFlag( Node location, bool crossing = false, bool standalone = true )
	{
		ScheduleOperation( Operation.Create().SetupAsCreateFlag( location, crossing ), standalone );
	}

	public void ScheduleRemoveFlag( Flag flag, bool standalone = true )
	{
		ScheduleOperation( Operation.Create().SetupAsRemoveFlag( flag ), standalone );
	}

	public void ScheduleCaptureRoad( Flag flag, bool standalone = true )
	{
		ScheduleOperation( Operation.Create().SetupAsCaptureRoad( flag ), standalone );
	}

	public void ScheduleChangeArea( Building building, Ground.Area area, Node center, int radius )
	{
        if ( area != null )
		    ScheduleOperation( Operation.Create().SetupAsChangeArea( building, area, center, radius ) );
	}

	public void ScheduleChangeBufferUsage( Workshop workshop, Workshop.Buffer buffer, bool enabled )
	{
	    ScheduleOperation( Operation.Create().SetupAsChangeBufferUsage( workshop, buffer, enabled ) );
	}

    public void ScheduleMoveFlag( Flag flag, int direction )
    {
        ScheduleOperation( Operation.Create().SetupAsMoveFlag( flag, direction ) );
    }

    public void ScheduleChangePriority( Stock.Route route, int direction )
    {
        ScheduleOperation( Operation.Create().SetupAsChangePriority( route, direction ) );
    }

    public void ScheduleMoveRoad( Road road, int index, int direction )
    {
        ScheduleOperation( Operation.Create().SetupAsMoveRoad( road, index, direction ) );
    }

    public void ScheduleStockAdjustment( Stock stock, Item.Type itemType, Stock.Channel channel, int value, bool standalone = true )
    {
        ScheduleOperation( Operation.Create().SetupAsStockAdjustment( stock, itemType, channel, value ), standalone );
    }

    void FixedUpdate()
    {
        if ( this != oh )
            return;

#if DEBUG
        if ( recordCRC && mode == Mode.recording )
        {
            assert.AreEqual( time, CRCCodes.Count );
            CRCCodes.Add( currentCRCCode );
            Log( $"End of frame, CRC {currentCRCCode} was stored" );
        }
        if ( mode == Mode.repeating )
        {
            assert.IsTrue( CRCCodes.Count > time );
            if ( !recalculateCRC )
            {
                assert.AreEqual( CRCCodes[time], currentCRCCode, "CRC mismatch" );
                Log( $"End of frame, CRC {currentCRCCode} was checked" );
            }
            else
            {
                CRCCodes[time] = currentCRCCode;
                Log( $"End of frame, CRC recalculated as {currentCRCCode}" );
            }
        }
        currentCRCCode = 0;
#endif
        world.fixedOrderCalls = true;
        world.OnEndOfLogicalFrame();

        while ( executeIndex < repeatBuffer.Count && repeatBuffer[executeIndex].scheduleAt == time )
            ExecuteOperation( repeatBuffer[executeIndex++] );

        world.fixedOrderCalls = false;

        finishedFrameIndex++;
        if ( finishedFrameIndex == replayLength )
        {
            Assert.global.AreEqual( mode, Mode.repeating );
            mode = Mode.recording;
        }

        assert.AreEqual( finishedFrameIndex, time );
    }

    void Update()
    {
        if ( this != oh )
        {
            DestroyThis();
            return;
        }

        while ( executeIndex < repeatBuffer.Count && repeatBuffer[executeIndex].scheduleAt == time )
        {
            assert.AreEqual( world.speed, World.Speed.pause );
            ExecuteOperation( repeatBuffer[executeIndex++] );
        }
        
		if ( undoHotkey.IsDown() )
			UndoRedo( undoQueue );
		if ( redoHotkey.IsDown() )
			UndoRedo( redoQueue );
    }

    void ExecuteOperation( Operation operation )
    {
        HiveObject.Log( $"Executing {operation.name}" );
        var inverse = operation.ExecuteAndInvert();
        if ( inverse )
        {
            inverse.group = int.MaxValue - operation.group;
            switch ( operation.source )
            {
                case Operation.Source.manual:
                    inverse.source = Operation.Source.undo;
                    undoQueue.Add( inverse );
                    redoQueue.Clear();
                    break;
                case Operation.Source.undo:
                    inverse.source = Operation.Source.redo;
                    redoQueue.Add( inverse );
                    break;
                case Operation.Source.redo:
                    inverse.source = Operation.Source.undo;
                    undoQueue.Add( inverse );
                    break;
            }
        }
    }
}

public class Operation : ScriptableObject
{
    public Type type;
    public int workerCount;
    public int locationX, locationY;
    public int areaX, areaY;
    public Building.Type buildingType;
    public int direction;
    public List<int> roadPathX, roadPathY;
    public bool crossing;
    public int group = -1;
    public int areaIndex;
    public int radius;
    public int endLocationX, endLocationY;
    public Item.Type itemType;
    public Stock.Channel stockChannel;
    public int itemCount;
    public int scheduleAt;
    public Source source;
    public int bufferIndex;
    public bool useBuffer;

    public enum Source
    {
        manual,
        undo,
        redo
    }

    public Node location
    {
        get
        {
            return HiveCommon.ground.GetNode( locationX, locationY );
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
            return HiveCommon.ground.GetNode( endLocationX, endLocationY ).building as Stock;
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
            roadPath.Add( HiveCommon.ground.GetNode( roadPathX[i], roadPathY[i] ) );
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
                Type.moveRoad => "Moving a road block",
                Type.removeBuilding => "Remove a building",
                Type.removeFlag => "Remove a flag",
                Type.removeRoad => "Remove a road",
                Type.stockAdjustment => "Adjust stock item counts",
                Type.captureRoad => "Capture nearby roads",
                Type.changeBufferUsage => "Change Buffer Usage",
                _ => ""
            };
            if ( type == Type.createBuilding )
            {
                if ( buildingType < Building.Type.stock )
                    text += ((Workshop.Type)buildingType).ToString().GetPrettyName( false );
                else
                    text += buildingType.ToString().GetPrettyName( false );
            }
            return text;
        }
    }

    public Node place
    {
        get
        {
            if ( type == Type.createRoad )
                return HiveCommon.ground.GetNode( roadPathX[1], roadPathY[1] );
            return location;
        }
    }
    [JsonProperty]
    public string title
    {
        get { return name; }
        set { name = value; }
    }

	[Obsolete( "Compatibility for old files", true )]
    bool merge { set {} }
	[Obsolete( "Compatibility for old files", true )]
    Workshop.Type workshopType { set {} }
	[Obsolete( "Compatibility for old files", true )]
    Ground.Area area { set {} }

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
        moveRoad,
        stockAdjustment,
        captureRoad,
        changeBufferUsage
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

    public Operation SetupAsRemoveBuilding( Building building )
    {
        type = Type.removeBuilding;
        this.building = building;
        name = "Delete Building";
        return this;
    }

    public Operation SetupAsCreateBuilding( Node location, int direction, Building.Type buildingType )
    {
        type = Type.createBuilding;
        this.location = location;
        this.direction = direction;
        this.buildingType = buildingType;
        name = "Create Building";
        return this;
    }

    public Operation SetupAsRemoveRoad( Road road )
    {
        type = Type.removeRoad;
        this.road = road;
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

    public Operation SetupAsRemoveFlag( Flag flag )
    {
        type = Type.removeFlag;
        this.flag = flag;
        name = "Remove Flag";
        return this;
    }

    public Operation SetupAsCaptureRoad( Flag flag )
    {
        type = Type.captureRoad;
        this.flag = flag;
        name = "Capture road";
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

    public Operation SetupAsMoveFlag( Flag flag, int direction )
    {
        type = Type.moveFlag;
        this.flag = flag;
        this.direction = direction;
        name = "Move Flag";
        return this;
    }

    public Operation SetupAsChangeArea( Building building, Ground.Area area, Node center, int radius )
    {
        type = Type.changeArea;
        this.building = building;
        areaIndex = building.areas.IndexOf( area );
        if ( center )
        {
            areaX = center.x;
            areaY = center.y;
        }
        else
            areaX = areaY = -1;
        this.radius = radius;
        name = "Change Area";
        return this;
    }

    public Operation SetupAsChangeBufferUsage( Workshop workshop, Workshop.Buffer buffer, bool use )
    {
        int index = workshop.buffers.IndexOf( buffer );
        if ( index < 0 )
            return null;
        type = Type.changeBufferUsage;
        this.building = workshop;
        this.bufferIndex = index;
        this.useBuffer = use;
        name = "Change Buffer Usage";
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

    public Operation SetupAsMoveRoad( Road road, int index, int direction )
    {
        type = Type.moveRoad;
        location = road.nodes[index];
        this.direction = direction;
        name = "Move Road";
        return this;
    }

    public Operation SetupAsStockAdjustment( Stock stock, Item.Type itemType, Stock.Channel channel, int value )
    {
        type = Type.stockAdjustment;
        building = stock;
        this.stockChannel = channel;
        this.itemType = itemType;
        itemCount = value;
        name = "Stock Adjustment";
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
                        newBuilding = Workshop.Create().Setup( location, HiveCommon.root.mainPlayer, (Workshop.Type)buildingType, direction, block:Resource.BlockHandling.remove );
                    if ( buildingType == Building.Type.stock )
                        newBuilding = Stock.Create().Setup( location, HiveCommon.root.mainPlayer, direction, block:Resource.BlockHandling.remove );
                    if ( buildingType == Building.Type.guardHouse )
                        newBuilding = GuardHouse.Create().Setup( location, HiveCommon.root.mainPlayer, direction, block:Resource.BlockHandling.remove );
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
                var road = this.road;
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
                var flag = this.flag;
                if ( flag == null || !flag.Remove( true ) )
                    return null;

                return Create().SetupAsCreateFlag( flag.node, flag.crossing );
            }
            case Type.createFlag:
            {
                Flag newFlag = location.flag;
                if ( !newFlag )
                    newFlag = Flag.Create().Setup( location, HiveCommon.root.mainPlayer, false, crossing );
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
                var area = building.areas[areaIndex];
                var oldCenter = area.center;
                var oldRadius = area.radius;
                if ( areaX < 0 )
                    area.center = null;
                else
                    area.center = HiveCommon.ground.GetNode( areaX, areaY );
                area.radius = radius;
                return Create().SetupAsChangeArea( building, area, oldCenter, oldRadius );
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
                var road = this.road;
                var index = location.road.nodes.IndexOf( location );
                if ( !location.road.Move( index, direction ) )
                    return null;
                return Create().SetupAsMoveRoad( road, index, ( direction + Constants.Node.neighbourCount / 2 ) % Constants.Node.neighbourCount );
            }
            case Type.stockAdjustment:
            {
                var i = (building as Stock).itemData[(int)itemType];
                var oldValue = i.ChannelValue( stockChannel );
                i.ChannelValue( stockChannel ) = itemCount;
				building.owner.UpdateStockRoutes( itemType );
                return Create().SetupAsStockAdjustment( building as Stock, itemType, stockChannel, oldValue );
            }
            case Type.captureRoad:
            {
                flag.CaptureRoads();
                return null;
            }
            case Type.changeBufferUsage:
            {
                var workshop = building as Workshop;
                workshop.buffers[bufferIndex].disabled = useBuffer;
                return Create().SetupAsChangeBufferUsage( workshop, workshop.buffers[bufferIndex], !useBuffer );
            }
        }
        return null;
    }
}
