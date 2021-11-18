using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
#pragma warning disable 0618

public class OperationHandler : HiveObject
{
	public List<Operation> undoQueue = new List<Operation>(), redoQueue = new List<Operation>(), executeBuffer = new List<Operation>();
    public List<int> CRCCodes = new List<int>();
    public int CRCCodesSkipped;
    public bool purgeCRCTable;
    public int currentCRCCode;
    public Mode mode;
    public World.Challenge challenge;
    public int executeIndex = 0;
    public int finishedFrameIndex = -1;
    public int replayLength = -1;
    public int currentGroup = 0;
    public List<string> saveFileNames = new List<string>();
    public bool recordCRC;
    public bool recordEvents;
    public bool recalculateCRC;
    public bool frameFinishPending;
    public bool insideFrame
    {
        get
        {
            return time < finishedFrameIndex;
        }
    }
    [JsonIgnore]
    public int dumpEventsInFrame;

	static public Interface.Hotkey undoHotkey = new Interface.Hotkey( "Undo", KeyCode.Z, true );
	static public Interface.Hotkey redoHotkey = new Interface.Hotkey( "Redo", KeyCode.Y, true );

    public enum Mode
    {
        recording,
        repeating
    }

    public struct Event
    {
        public Type type;
        public int code, current;
        public CodeLocation origin;

        public string description
        {
            get
            {
                return $"{current}: {type} (code: {code}) from {origin}";
            }
        }

        public enum Type
        {
            frameStart,
            frameEnd,
            CRC,
            rndRequest,
            rndRequestFloat,
            execution
        }

        public enum CodeLocation
        {
            worldFrameStart,
            nodeSetup,
            nodeAddResourcePatch,
            stockCriticalUpdate,
            workerWalk,
            workerCarryItem,
            workerSetupAsAnimal,
            workerSetupAsCart,
            workerSetupAsHauler,
            workerSetupAsTinkerer,
            workerSetupAsBuilder,
            workerSetupAsSoldier,
            workerSetupAsAttacker,
            workerTaskPlant,
            flagFreeSlots,
            itemDispatcherAttach,
            workshopDisabledBuffers,
            workshopForester,
            workshopWorkProgress,
            workshopBufferSelection,
            workshopCollectResource,
            operationHandlerFixedUpdate,
            worldOnEndOfLogicalFrame,
            worldNewGame,
            worldNewFrame,
            resourceSetup,
            resourceSound,
            resourceSilence,
            challengePanelRestart,
            animalDirection,
            animalPasturing
        }
    }

    List<Event> events = new List<Event>(), frameEvents = new List<Event>(), previousFrameEvents;
    bool eventsDumped;

	[Conditional( "DEBUG" )]
    public void RegisterEvent( Event.Type type, Event.CodeLocation caller, int code = 0 )
    {
        if ( !recordEvents )
            return;
        var e = new Event{ type = type, code = code, current = currentCRCCode, origin = caller };
        if ( mode == Mode.recording )
            events.Add( e );
        if ( mode == Mode.repeating )
            frameEvents.Add( e );
    }

	[Conditional( "DEBUG" )]
    public void SaveEvents( string file )
    {
        if ( !recordEvents )
            return;
        using ( BinaryWriter writer = new BinaryWriter( File.Open( file, FileMode.Create ) ) )
        {
            writer.Write( events.Count );
            foreach ( var e in events )
            {
                writer.Write( (int)e.type );
                writer.Write( (int)e.code );
                writer.Write( (int)e.current );
                writer.Write( (int)e.origin );
            }
        }
    }

	[Conditional( "DEBUG" )]
    public void LoadEvents( string file )
    {
        if ( !System.IO.File.Exists( file ) )
            return;

        events.Clear();
        using ( BinaryReader reader = new BinaryReader( File.Open( file, FileMode.Open ) ) )
        {
            int count = reader.ReadInt32();
            while ( count-- > 0 )
            {
                Event e;
                e.type = (Event.Type)reader.ReadInt32();
                e.code = reader.ReadInt32();
                e.current = reader.ReadInt32();
                e.origin = (Event.CodeLocation)reader.ReadInt32();
                events.Add( e );
            }
        }
        for ( int i = events.Count - 1; i > 0; i-- )
        {
            if ( events[i].type == Event.Type.frameStart )
            {
                Log( $"Events loaded from {file}, first frame: {events.First().code}, last frame: {events[i].code}" );
                break;
            }
        }
    }

    public int fileIndex;
	public string nextFileName { get { return $"{world.name} ({fileIndex})"; } }

    public override Node location => throw new System.NotImplementedException();

	[Obsolete( "Compatibility with old files", true )]
    string lastSave { set {} }
	[Obsolete( "Compatibility with old files", true )]
    List<Operation> repeatBuffer { set { executeBuffer = value; } }

    public Operation next 
    { 
        get 
        { 
            if ( executeIndex >= executeBuffer.Count )
                return null;
            int skip = 0;
            while ( executeIndex+skip+1 < executeBuffer.Count && executeBuffer[executeIndex+skip+1].group == executeBuffer[executeIndex+skip].group )
                skip++;
            return executeBuffer[executeIndex+skip]; 
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

    public void StartReplay( int saveFileIndex = -1, bool recalculateCRC = false )
    {
        executeIndex = 0;
        if ( saveFileIndex != -1 )
        {
            root.Load( Application.persistentDataPath + "/Saves/" + saveFileNames[saveFileIndex] );  // TODO This should call World.Load and World.NewGame
            executeIndex = oh.executeIndex;
            challenge = world.challenge;
        }
        else
            root.NewGame( challenge );
        destroyed = false;	// TODO This is a hack. World.Clear sets this bool field to true for every hive object in the memory, not only for those which were really destroyed

        frameEvents = world.operationHandler.events;
        currentCRCCode = world.operationHandler.currentCRCCode;
        if ( world.operationHandler )
            world.operationHandler.DestroyThis();
        world.operationHandler = this;

        world.roadTutorialShowed = world.createRoadTutorialShowed = true;
        mode = Mode.repeating;
        finishedFrameIndex = time;
        this.recalculateCRC = recalculateCRC;
    }

    public void CancelReplay()
    {
        Assert.global.AreEqual( mode, Mode.repeating );
        mode = Mode.recording;
        executeBuffer.RemoveRange( executeIndex, executeBuffer.Count - executeIndex );
        int CRCIndex = time - CRCCodesSkipped;
        CRCCodes.RemoveRange( CRCIndex + 1, CRCCodes.Count - CRCIndex - 1 );
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
        if ( operation.group < 0 )
            operation.group = currentGroup;
        Assert.global.AreNotEqual( network.state, Network.State.receivingGameState );
        if ( network.state == Network.State.client )
        {
            BinaryFormatter bf = new BinaryFormatter();
            var ms = new MemoryStream();
            bf.Serialize( ms, operation );
            byte error;
            NetworkTransport.Send( network.host, network.clientConnection, Network.reliableChannel, ms.ToArray(), (int)ms.Length, out error );
            return;
        }
    
        operation.scheduleAt = time;
        if ( !insideFrame && world.speed != World.Speed.pause )
            operation.scheduleAt++;
        executeBuffer.Add( operation );
	}

	public void UndoRedo( List<Operation> queue )
	{
        if ( queue.Count == 0 )
            return;
        var operation = queue.Last();
        ScheduleOperation( operation );
        queue.RemoveAt( queue.Count - 1 );
        if ( queue.Count > 0 && operation.group == queue.Last().group )
            UndoRedo( queue );
	}

    public string SaveReplay( string name = null )
    {
        if ( name == null )
            name = Application.persistentDataPath + $"/Replays/{nextFileName}.json";
        if ( name.Contains( nextFileName ) )
            fileIndex++;
		undoQueue.Clear();		// TODO Is this necessary?
		redoQueue.Clear();
		if ( finishedFrameIndex > replayLength )
			replayLength = finishedFrameIndex;
		Serializer.Write( name, this, true );
        SaveEvents( System.IO.Path.ChangeExtension( name, "bin" ) );
        return name;
    }

    public static OperationHandler LoadReplay( string name )
    {
        var t = Serializer.Read<OperationHandler>( name );
        t.LoadEvents( System.IO.Path.ChangeExtension( name, "bin" ) );
        return t;
    }

	public void ScheduleChangeRoadWorkerCount( Road road, int count )
	{
		ScheduleOperation( Operation.Create().SetupAsChangeWorkerCount( road, count ) );
	}

	public void ScheduleChangeDefenderCount( GuardHouse building, int count )
	{
		ScheduleOperation( Operation.Create().SetupAsChangeDefenderCount( building, count ) );
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

	public void ScheduleFlattenFlag( Flag flag, bool standalone = true )
	{
		ScheduleOperation( Operation.Create().SetupAsFlattenFlag( flag ), standalone );
	}

	public void ScheduleChangeFlagType( Flag flag, bool standalone = true )
	{
		ScheduleOperation( Operation.Create().SetupAsChangeFlagType( flag ), standalone );
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

    public void ScheduleAttack( Team team, GuardHouse target, int attackedCount, bool standalone = true )
    {
        ScheduleOperation( Operation.Create().SetupAsAttack( team, target, attackedCount ), standalone );
    }

    public void ScheduleCreatePlayer( string name, string team, bool standalone = true )
    {
        ScheduleOperation( Operation.Create().SetupAsCreatePlayer( name, team ), standalone );
    }

    void FixedUpdate()
    {
        if ( frameFinishPending )
            return;
        if ( this != oh )
            return;

#if DEBUG
        if ( recordCRC && mode == Mode.recording )
        {
            assert.AreEqual( time, CRCCodesSkipped + CRCCodes.Count );
            CRCCodes.Add( currentCRCCode );
            RegisterEvent( Event.Type.frameEnd, Event.CodeLocation.operationHandlerFixedUpdate );
        }
        if ( mode == Mode.repeating )
        {
            assert.IsTrue( CRCCodesSkipped + CRCCodes.Count > time );
            if ( !recalculateCRC )
            {
                if ( CRCCodes[time - CRCCodesSkipped] != currentCRCCode )
                {
                    if ( !eventsDumped )
                        DumpEventDif();
                    assert.Fail( $"CRC mismatch in frame {time}" );
                }
                RegisterEvent( Event.Type.frameEnd, Event.CodeLocation.operationHandlerFixedUpdate, CRCCodes[time - CRCCodesSkipped] );
            }
            else
            {
                CRCCodes[time - CRCCodesSkipped] = currentCRCCode;
                RegisterEvent( Event.Type.frameEnd, Event.CodeLocation.operationHandlerFixedUpdate );
            }
        }
#else
        if ( recordCRC && mode == Mode.recording )
            CRCCodesSkipped += 1;
#endif
        world.fixedOrderCalls = true;
        world.OnEndOfLogicalFrame();
        FinishFrame();
#if DEBUG
        previousFrameEvents = frameEvents;
        frameEvents = new List<Event>();
#endif
    }

    /// Returns true, if succeed
    public bool FinishFrame()
    {
        if ( network.state == Network.State.client )
        {
            if ( orders.Count == 0 || orders.First().operationsLeftFromServer > 0 )
            {
                Log( $"Client is stuck at time {time}, no order from server yet" );
                world.SetSpeed( World.Speed.pause );
                frameFinishPending = true;
                return false;
            }
            else
            {
                Assert.global.AreEqual( orders.First().time, time, $"Network time mismatch (server: {orders.First().time}, client: {time})" );
                if ( orders.Count > Constants.Network.lagTolerance * Constants.World.normalSpeedPerSecond )
                {
					Interface.status.SetText( this, "Catching up server", pinX:0.5f, pinY:0.5f, time:100 );
                    world.SetSpeed( World.Speed.fast );
                }
                var order = orders.First();
                orders.RemoveFirst();
                if ( order.CRC != currentCRCCode )
                {
                    if ( !eventsDumped )
                    {
                        DumpEvents( events, "events-client.txt", time );
                        eventsDumped = true;
                    }
                    Assert.global.Fail( $"Network CRC mismatch, server: {order.CRC}, client: {currentCRCCode} at {time}" );
                }
            }
        }

        while ( executeIndex < executeBuffer.Count && executeBuffer[executeIndex].scheduleAt == time )
            ExecuteOperation( executeBuffer[executeIndex++] );

        world.fixedOrderCalls = false;

        finishedFrameIndex++;
        if ( finishedFrameIndex == replayLength )
        {
            Assert.global.AreEqual( mode, Mode.repeating );
            mode = Mode.recording;
        }

        assert.AreEqual( finishedFrameIndex, time );
        frameFinishPending = false;
        return true;
    }

    static void DumpEvents( List<Event> events, string file, int frame = -1 )
    {
        for ( int i = 0; i < events.Count; i++ )
        {
            var ie = events[i];
            if ( ( ie.type == Event.Type.frameStart && ie.code == frame ) || frame == -1 )
            {
                using ( StreamWriter writer = File.CreateText( Application.persistentDataPath + "/" + file ) )
                {
                    int j = i;
                    while ( j < events.Count() && ( events[j].type != Event.Type.frameStart || events[j].code != frame + 1 ) )
                        writer.Write( events[j++].description + "\n" );
                }
                break;
            }
        }
    }

    void DumpEventDif()
    {
        if ( eventsDumped )
            return;

        DumpEvents( previousFrameEvents, "events-prev-replay.txt" );
        DumpEvents( frameEvents, "events-replay.txt" );
        DumpEvents( events, "events-orig.txt", time );
        DumpEvents( events, "events-prev-orig.txt", time - 1 );
        eventsDumped = true;
    }

    public class FrameOrder
    {
        public int time;
        public int CRC;
        public int operationsLeftFromServer;
    }

    public LinkedList<FrameOrder> orders = new LinkedList<FrameOrder>();

    void Update()
    {
        if ( this != oh )
            return;

        while ( executeIndex < executeBuffer.Count && executeBuffer[executeIndex].scheduleAt == time )
        {
            assert.AreEqual( world.speed, World.Speed.pause );
            ExecuteOperation( executeBuffer[executeIndex++] );
        }
        
		if ( undoHotkey.IsDown() )
			UndoRedo( undoQueue );
		if ( redoHotkey.IsDown() )
			UndoRedo( redoQueue );
        if ( purgeCRCTable )
        {
            CRCCodesSkipped += CRCCodes.Count;
            CRCCodes.Clear();
            purgeCRCTable = false;
        }

        if ( dumpEventsInFrame != 0 )
        {
            DumpEvents( events, "events-debug.txt", dumpEventsInFrame );
            dumpEventsInFrame = 0;
        }
    }

    public void PurgeCRCTable()
    {
        CRCCodesSkipped += CRCCodes.Count;
        CRCCodes.Clear();
    }

    void ExecuteOperation( Operation operation )
    {
        HiveObject.Log( $"Executing {operation.name}" );
        var inverse = operation.ExecuteAndInvert();
        if ( inverse != null )
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

    public override void Validate( bool chain )
    {
        assert.IsFalse( destroyed );
    }
}
[Serializable]
public class Operation
{
    public Type type;
    public string name;
    public int workerCount;
    public int locationX, locationY;
    public int areaX, areaY;
    public Building.Type buildingType;
    public int direction;
    public List<int> roadPathX = new List<int>(), roadPathY = new List<int>();
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
    public int networkId;
    public bool useBuffer;
    public string playerName, teamName;

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
            foreach ( var node in value )
            {
                roadPathX.Add( node.x );
                roadPathY.Add( node.y );
            }
        }
    }
    public Team team
    {
        get
        {
            return HiveCommon.world.teams[bufferIndex];
        }
        set
        {
            bufferIndex = HiveCommon.world.teams.IndexOf( value );
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
                Type.changeDefenderCount => "Change defender count",
                Type.createBuilding => "Constructing a new ",
                Type.createFlag => "Creating a new flag",
                Type.createRoad => "Create new road",
                Type.moveFlag => "Moving a flag",
                Type.moveRoad => "Moving a road block",
                Type.removeBuilding => "Remove a building",
                Type.removeFlag => "Remove a flag",
                Type.removeRoad => "Remove a road",
                Type.stockAdjustment => "Adjust stock item counts",
                Type.attack => "Start an attack on the enemy",
                Type.createPlayer => "Creating a new player",
                Type.captureRoad => "Capture nearby roads",
                Type.changeBufferUsage => "Change Buffer Usage",
                Type.changeFlagType => "Convert a junction to crossing or vice versa",
                Type.flattenFlag => "Flatten the area around a junction",
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

	[Obsolete( "Compatibility for old files", true )]
    bool merge { set {} }
	[Obsolete( "Compatibility for old files", true )]
    Workshop.Type workshopType { set {} }
	[Obsolete( "Compatibility for old files", true )]
    Ground.Area area { set {} }
	[Obsolete( "Compatibility for old files", true )]
    string title { set { name = value; } }

    public enum Type
    {
        changeWorkerCount,
        changeDefenderCount,
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
        attack,
        createPlayer,
        captureRoad,
        changeBufferUsage,
        flattenFlag,
        changeFlagType
    }

    public static Operation Create()
    {
        return new Operation();
    }

    public Operation SetupAsChangeWorkerCount( Road road, int count )
    {
        type = Type.changeWorkerCount;
        this.road = road;
        workerCount = count;
        name = "Change Worker Count";
        return this;
    }

    public Operation SetupAsChangeDefenderCount( GuardHouse building, int count )
    {
        type = Type.changeDefenderCount;
        this.building = building;
        workerCount = count;
        name = "Change Defender Count";
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

    public Operation SetupAsFlattenFlag( Flag flag )
    {
        type = Type.flattenFlag;
        this.flag = flag;
        name = "Flatten Flag";
        return this;
    }

    public Operation SetupAsChangeFlagType( Flag flag )
    {
        type = Type.changeFlagType;
        this.flag = flag;
        name = "Change Flag Type";
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

    public Operation SetupAsAttack( Team team, GuardHouse target, int attackerCount )
    {
        type = Type.attack;
        building = target;
        this.team = team;
        workerCount = attackerCount;
        name = "Attack";
        return this;
    }

    public Operation SetupAsCreatePlayer( string name, string team )
    {
        type = Type.createPlayer;
        playerName = name;
        teamName = team;
        networkId = HiveCommon.network.id;
        name = "Create new player";
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
            case Type.changeDefenderCount:
            {
                if ( building is GuardHouse guardHouse )
                {
                    int oldCount = guardHouse.optimalSoldierCount;
                    guardHouse.optimalSoldierCount = workerCount;
                    return Create().SetupAsChangeDefenderCount( guardHouse, oldCount );
                }
                break;
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
                        newBuilding = Workshop.Create().Setup( location, HiveCommon.root.mainTeam, (Workshop.Type)buildingType, direction, block:Resource.BlockHandling.remove );
                    if ( buildingType == Building.Type.stock )
                        newBuilding = Stock.Create().Setup( location, HiveCommon.root.mainTeam, direction, block:Resource.BlockHandling.remove );
                    if ( buildingType == Building.Type.guardHouse )
                        newBuilding = GuardHouse.Create().Setup( location, HiveCommon.root.mainTeam, direction, block:Resource.BlockHandling.remove );
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
                    newFlag = Flag.Create().Setup( location, HiveCommon.root.mainTeam, false, crossing );
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
                var flag = this.flag;
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
                route.start.team.UpdateStockRoutes( route.itemType );
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
				building.team.UpdateStockRoutes( itemType );
                return Create().SetupAsStockAdjustment( building as Stock, itemType, stockChannel, oldValue );
            }
            case Type.attack:
            {
                team.Attack( building as GuardHouse, workerCount );
                return null;

            }
            case Type.createPlayer:
            {
                Team team = null;
                foreach ( var existingTeam in HiveCommon.world.teams )
                {
                    if ( existingTeam.name == teamName )
                        team = existingTeam;
                }
                if ( team == null )
                {
                    team = Team.Create().Setup( teamName, Constants.Player.teamColors[HiveCommon.world.teams.Count%Constants.Player.teamColors.Length] );
                    if ( team == null )
                    {
                        Interface.status.SetText( HiveCommon.root, "No room for a new headquarters", pinX:0.5f, pinY:0.5f, time:100 );
                        return null;
                    }
                    HiveCommon.world.teams.Add( team );
                }
                var newPlayer = Player.Create().Setup( playerName, team );
                HiveCommon.world.players.Add( newPlayer );
                if ( networkId == HiveCommon.network.id )
                    HiveCommon.root.mainPlayer = newPlayer;
                return null;
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
            case Type.flattenFlag:
            {
                flag.requestFlattening = true;
                return null;
            }
            case Type.changeFlagType:
            {
                if ( !flag.crossing )
                    flag.ConvertToCrossing();
                else
                    flag.ConvertToNormal();
                return Create().SetupAsChangeFlagType( flag );
            }
        }
        return null;
    }
}
