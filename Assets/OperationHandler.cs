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
	public List<Operation> undoQueue = new (), redoQueue = new (), executeBuffer = new ();
    public List<int> CRCCodes = new ();
    public int CRCCodesSkipped;
    public int currentCRCCode;
    public Mode mode;
    public Game.Challenge challenge;
    public int executeIndex = 0;
    public int replayLength = -1;
    public int currentGroup = 0;
    public string currentGroupName;
    public List<string> saveFileNames = new ();
    public bool recordCRC;
    public bool recordEvents;
    public bool recalculateCRC;
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
            unitWalk,
            unitCarryItem,
            unitSetupAsAnimal,
            unitSetupAsCart,
            unitSetupAsHauler,
            unitSetupAsTinkerer,
            unitSetupAsBuilder,
            unitSetupAsSoldier,
            unitSetupAsAttacker,
            unitTaskPlant,
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

    public List<Event> events = new (), frameEvents = new (), previousFrameEvents;
    public bool eventsDumped;

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
	public string nextFileName { get { return $"{game.name} ({fileIndex})"; } }

	[Obsolete( "Compatibility with old files", true )]
    string lastSave { set {} }
	[Obsolete( "Compatibility with old files", true )]
    bool purgeCRCTable { set {} }
	[Obsolete( "Compatibility with old files", true )]
    List<Operation> repeatBuffer { set { executeBuffer = value; } }
	[Obsolete( "Compatibility with old files", true )]
    int finishedFrameIndex { set {} }
	[Obsolete( "Compatibility with old files", true )]
    bool frameFinishPending { set {} }

    public Operation NextToExecute( Team team ) 
    {
        if ( executeBuffer.Count == executeIndex )
            return null;

        int index = executeIndex;
        while ( executeBuffer[index].initiator != team )
        {
            index++;
            if ( index == executeBuffer.Count )
                return null;
        }

        int skip = 0;
        while ( index+skip+1 < executeBuffer.Count && executeBuffer[index+skip+1].group == executeBuffer[index+skip].group )
            skip++;
        return executeBuffer[index+skip]; 
    }

    public static OperationHandler Create()
    {
        return new GameObject( "Operation handler").AddComponent<OperationHandler>();
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
        }
        else
            root.NewGame( challenge );

        challenge = game.challenge;

        frameEvents = game.operationHandler.events;
        currentCRCCode = game.operationHandler.currentCRCCode;
        if ( game.operationHandler )
            game.operationHandler.Remove();
        game.operationHandler = this;

        game.roadTutorialShowed = game.createRoadTutorialShowed = true;
        mode = Mode.repeating;
        this.recalculateCRC = recalculateCRC;
    }

    public void CancelReplay()
    {
        Assert.global.AreEqual( mode, Mode.repeating );
        mode = Mode.recording;
        executeBuffer.RemoveRange( executeIndex, executeBuffer.Count - executeIndex );
        int CRCIndex = time - CRCCodesSkipped;
        CRCCodes.RemoveRange( CRCIndex, CRCCodes.Count - CRCIndex );
        replayLength = 0;
    }

    public void StartGroup( string name = null )
    {
        currentGroup++;
        currentGroupName = name;
    }

    public void ScheduleOperation( Operation operation, bool standalone = true, Operation.Source source = Operation.Source.manual )
	{
        if ( mode == Mode.repeating )
            return;

        operation.source = source;
        if ( standalone )
        {
            currentGroup++;
            currentGroupName = null;
        }
        if ( operation.group < 0 )
        {
            operation.group = currentGroup;
            operation.groupName = currentGroupName;
        }

        if ( source == Operation.Source.preparation )
            ExecuteOperation( operation );
        else
            ScheduleOperationRaw( operation );
    }

    public void ScheduleOperationRaw( Operation operation )
	{
        if ( !network.OnScheduleOperation( operation ) )
            return;
    
        operation.scheduleAt = time;
        if ( game.updateStage != UpdateStage.none )
            operation.scheduleAt++;

        executeBuffer.Add( operation );
	}

	public void UndoRedo( List<Operation> queue )
	{
        if ( queue.Count == 0 )
            return;
        var operation = queue.Last();
        ScheduleOperationRaw( operation );
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
		if ( time > replayLength )
			replayLength = time;
        if ( recordCRC )
            assert.AreEqual( replayLength, CRCCodesSkipped + CRCCodes.Count );
		Serializer.Write( name, this, true );
        SaveEvents( System.IO.Path.ChangeExtension( name, "bin" ) );
        return name;
    }

    public static OperationHandler LoadReplay( string name )
    {
        var t = Serializer.Read<OperationHandler>( name );
        // TODO is this line needed?
        // t.challenge.updateIndices = -1;
        t.LoadEvents( System.IO.Path.ChangeExtension( name, "bin" ) );
        return t;
    }

	public void ScheduleChangeRoadHaulerCount( Road road, int count, bool standalone = true, Operation.Source source = Operation.Source.manual )
	{
		ScheduleOperation( Operation.Create().SetupAsChangeHaulerCount( road, count ), standalone, source );
	}

	public void ScheduleChangeDefenderCount( GuardHouse building, int count, bool standalone = true, Operation.Source source = Operation.Source.manual )
	{
		ScheduleOperation( Operation.Create().SetupAsChangeDefenderCount( building, count ), standalone, source );
	}

	public void ScheduleToggleEmergencyConstruction( Team team, bool standalone = true, Operation.Source source = Operation.Source.manual )
	{
		ScheduleOperation( Operation.Create().SetupAsToggleEmergencyConstruction( team ), standalone, source );
	}

	public void ScheduleRemoveBuilding( Building building, bool standalone = true, Operation.Source source = Operation.Source.manual )
	{
        if ( building )
		    ScheduleOperation( Operation.Create().SetupAsRemoveBuilding( building ), standalone, source );
	}

	public void ScheduleCreateBuilding( Node location, int direction, Building.Type buildingType, Team team, bool standalone = true, Operation.Source source = Operation.Source.manual )
	{
		ScheduleOperation( Operation.Create().SetupAsCreateBuilding( location, direction, buildingType, team ), standalone, source );
	}

	public void ScheduleRemoveRoad( Road road, bool standalone = true, Operation.Source source = Operation.Source.manual )
	{
        if ( road )
		    ScheduleOperation( Operation.Create().SetupAsRemoveRoad( road ), standalone, source );
	}

	public void ScheduleCreateRoad( List<Node> path, Team team, bool standalone = true, Operation.Source source = Operation.Source.manual )
	{
		ScheduleOperation( Operation.Create().SetupAsCreateRoad( path, team ), standalone, source );
	}

	public void ScheduleRemoveFlag( Flag flag, bool standalone = true, Operation.Source source = Operation.Source.manual )
	{
        if ( flag == null )
            return;

        if ( standalone )
            StartGroup( "Removing a junction" );
        foreach ( var building in flag.Buildings() )
            ScheduleRemoveBuilding( building, false, source );
        List<Road> realRoads = new ();
        foreach ( var road in flag.roadsStartingHere )
            if ( road )
                realRoads.Add( road );
        if ( realRoads.Count != 2 )
        {
            foreach ( var road in realRoads )
                ScheduleRemoveRoad( road, false, source );
        }
        
		ScheduleOperation( Operation.Create().SetupAsRemoveFlag( flag ), false, source );
	}

	public void ScheduleCreateFlag( Node location, Team team, bool crossing = false, bool standalone = true, Operation.Source source = Operation.Source.manual )
	{
		ScheduleOperation( Operation.Create().SetupAsCreateFlag( location, team, crossing ), standalone, source );
	}

	public void ScheduleCreateResource( Node location, Resource.Type type, bool standalone = true, Operation.Source source = Operation.Source.manual )
	{
		ScheduleOperation( Operation.Create().SetupAsCreateResource( location, type ), standalone, source );
	}

	public void ScheduleRemoveResource( Resource resource, bool standalone = true, Operation.Source source = Operation.Source.manual )
	{
		ScheduleOperation( Operation.Create().SetupAsRemoveResource( resource ), standalone, source );
	}

	public void ScheduleFlattenFlag( Flag flag, bool standalone = true, Operation.Source source = Operation.Source.manual )
	{
		ScheduleOperation( Operation.Create().SetupAsFlattenFlag( flag ), standalone, source );
	}

	public void ScheduleChangeFlagType( Flag flag, bool standalone = true, Operation.Source source = Operation.Source.manual )
	{
		ScheduleOperation( Operation.Create().SetupAsChangeFlagType( flag ), standalone, source );
	}

	public void ScheduleCaptureRoad( Flag flag, bool standalone = true, Operation.Source source = Operation.Source.manual )
	{
		ScheduleOperation( Operation.Create().SetupAsCaptureRoad( flag ), standalone, source );
	}

	public void ScheduleChangeArea( Building building, Ground.Area area, Node center, int radius, bool standalone = true, Operation.Source source = Operation.Source.manual )
	{
        if ( area != null )
		    ScheduleOperation( Operation.Create().SetupAsChangeArea( building, area, center, radius ), standalone, source );
	}

	public void ScheduleChangeBufferUsage( Workshop workshop, Workshop.Buffer buffer, Workshop.Buffer.Priority usage, bool standalone = true, Operation.Source source = Operation.Source.manual )
	{
	    ScheduleOperation( Operation.Create().SetupAsChangeBufferUsage( workshop, buffer, usage ), standalone, source );
	}

    public void ScheduleMoveFlag( Flag flag, int direction, bool standalone = true, Operation.Source source = Operation.Source.manual )
    {
        ScheduleOperation( Operation.Create().SetupAsMoveFlag( flag, direction ), standalone, source );
    }

    public void ScheduleChangePriority( Stock.Route route, int direction, bool standalone = true, Operation.Source source = Operation.Source.manual )
    {
        ScheduleOperation( Operation.Create().SetupAsChangePriority( route, direction ), standalone, source );
    }

    public void ScheduleChangeWorkshopRunningMode( Workshop workshop, Workshop.Mode mode, bool standalone = true, Operation.Source source = Operation.Source.manual )
    {
        ScheduleOperation( Operation.Create().SetupAsChangeWorkshopRunningMode( workshop, mode ), standalone, source );
    }

    public void ScheduleMoveRoad( Road road, int index, int direction, bool standalone = true, Operation.Source source = Operation.Source.manual )
    {
        ScheduleOperation( Operation.Create().SetupAsMoveRoad( road, index, direction ), standalone, source );
    }

    public void ScheduleStockAdjustment( Stock stock, Item.Type itemType, Stock.Channel channel, float value, bool standalone = true, Operation.Source source = Operation.Source.manual )
    {
        ScheduleOperation( Operation.Create().SetupAsStockAdjustment( stock, itemType, channel, value ), standalone, source );
    }

    public void ScheduleInputWeightChange( Team team, Workshop.Type workshopType, Item.Type itemType, float weight, bool standalone = true, Operation.Source source = Operation.Source.manual )
    {
        ScheduleOperation( Operation.Create().SetupAsInputWeightChange( team, workshopType, itemType, weight ), standalone, source );
    }

    public void ScheduleAttack( Team team, Attackable target, int attackedCount, bool standalone = true, Operation.Source source = Operation.Source.manual )
    {
        ScheduleOperation( Operation.Create().SetupAsAttack( team, target, attackedCount ), standalone, source );
    }

    public void ScheduleCreatePlayer( string name, string team, bool standalone = true, Operation.Source source = Operation.Source.manual )
    {
        ScheduleOperation( Operation.Create().SetupAsCreatePlayer( name, team ), standalone, source );
    }

    public void OnBeginGameStep()
    {
        while ( executeIndex < executeBuffer.Count && executeBuffer[executeIndex].scheduleAt == time )
            ExecuteOperation( executeBuffer[executeIndex++] );
    }

    public void OnEndGameStep()
    {
        assert.AreEqual( this, oh );
        challenge?.CheckStatus();

#if DEBUG
        if ( recordCRC && mode == Mode.recording )
        {
            assert.AreEqual( time, CRCCodesSkipped + CRCCodes.Count );
            CRCCodes.Add( currentCRCCode );
            RegisterEvent( Event.Type.frameEnd, Event.CodeLocation.operationHandlerFixedUpdate );
        }
        if ( mode == Mode.repeating && recordCRC && time >= CRCCodesSkipped )      // TODO Probably wrong condition here
        {
            assert.IsTrue( CRCCodesSkipped + CRCCodes.Count > time );
            if ( !recalculateCRC )
            {
                if ( CRCCodes[time - CRCCodesSkipped] != currentCRCCode )
                {
                    if ( !eventsDumped )
                        DumpEventDif();
                    assert.Fail( $"CRC mismatch in frame {time} ({currentCRCCode} vs {CRCCodes[time - CRCCodesSkipped]})" );
                }
                RegisterEvent( Event.Type.frameEnd, Event.CodeLocation.operationHandlerFixedUpdate, CRCCodes[time - CRCCodesSkipped] );
            }
            else
            {
                CRCCodes[time - CRCCodesSkipped] = currentCRCCode;
                RegisterEvent( Event.Type.frameEnd, Event.CodeLocation.operationHandlerFixedUpdate );
            }
        }
        previousFrameEvents = frameEvents;
        frameEvents = new ();
#else
        if ( recordCRC && mode == Mode.recording )
            CRCCodesSkipped += 1;
#endif

        if ( time == replayLength - 1 )
        {
            Assert.global.AreEqual( mode, Mode.repeating );
            mode = Mode.recording;
        }
   }

    public static void DumpEvents( List<Event> events, string file, int frame = -1 )
    {
        if ( events == null )
            return;
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

    public class GameStepOrder
    {
        public int time;
        public int CRC;
    }

	[Obsolete( "Compatibility with old files", true )]
    LinkedList<GameStepOrder> orders { set {} }

    new void Update()
    {
        base.Update();

        if ( this != oh )
            return;

        while ( executeIndex < executeBuffer.Count && executeBuffer[executeIndex].scheduleAt == time )
        {
            game.lastChecksum = 0;
            ExecuteOperation( executeBuffer[executeIndex++] );
        }
        
		if ( undoHotkey.IsPressed() )
			UndoRedo( undoQueue );
		if ( redoHotkey.IsPressed() )
			UndoRedo( redoQueue );

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
        if ( operation.source != Operation.Source.preparation )
            HiveObject.Log( $"{time}: Executing {operation.name}" );
        network.OnExecuteOperation( operation );

        if ( root.mainPlayer is Simpleton simpleton && simpleton.showActions && operation.source == Operation.Source.computer )
            root.ShowOperation( operation );    // TODO Is this the correct place?

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
        base.Validate( chain );
    }
}
[Serializable]
public class Operation
{
    public Type type;
    public string name;
    public int unitCount;
    public int locationX, locationY;
    public int areaX, areaY;
    public Building.Type buildingType;
    public int direction;
    public List<int> roadPathX = new (), roadPathY = new ();
    public bool crossing;
    public int group = -1;
    public string groupName;
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
    public string playerName, teamName;
    public float weight;

    public enum Source
    {
        manual,
        archive,    // not used, listed only for compatibility reasons when loading json files
        networkClient,
        networkServer,
        computer,
        undo,
        redo,
        preparation
    }

    public Node location
    {
        get => HiveCommon.ground.GetNode( locationX, locationY );
        set { locationX = value.x; locationY = value.y; }
    }
    public Team initiator
    {
        get
        {
            if ( type == Type.attack || type == Type.toggleEmergencyConstruction || type == Type.createRoad || type == Type.createPlayer )
                return team;

            return location.team;
        }
    }
    public Building building { get => location.building; set => location = value.node; }        
    public Workshop workshop { get => building as Workshop; set => building = value; }
    public Road road { get => location.road; set => location = value.nodes[1]; }
    public Flag flag { get => location.flag; set => location = value.location; }
    public Stock start { get => location.building as Stock; set => location = value.node; }
    public Stock end { get => HiveCommon.ground.GetNode( endLocationX, endLocationY ).building as Stock; set { endLocationX = value.node.x; endLocationY = value.node.y; } }
    public Resource resource { get => location.resources[direction]; set { location = value.node; direction = resource.node.resources.IndexOf( value ); } }
    public Resource.Type resourceType { get => (Resource.Type)direction; set => direction = (int)value; }
    public Stock.Route route
    {
        get => start.itemData[(int)itemType].GetRouteForDestination( end );
        set { start = value.start; end = value.end; itemType = value.itemType; }
    }
    public List<Node> roadPath
    {
        get
        {
            if ( roadPathX == null )
                return null;
            List<Node> roadPath = new ();
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
    public Team team { get => HiveCommon.game.teams[bufferIndex]; set => bufferIndex = HiveCommon.game.teams.IndexOf( value ); }
    public Node place
    {
        get
        {
            if ( type == Type.createRoad )
                return HiveCommon.ground.GetNode( roadPathX[1], roadPathY[1] );
            return location;
        }
    }
    public Workshop.Mode workshopMode { get => (Workshop.Mode)direction; set => direction = (int)value; }
    public Workshop.Buffer.Priority useBuffer { get => (Workshop.Buffer.Priority)direction; set => direction = (int)value; }

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
        changeHaulerCount,
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
        changeWorkshopRunningMode,
        moveRoad,
        stockAdjustment,
        attack,
        createPlayer,
        captureRoad,
        changeBufferUsage,
        flattenFlag,
        changeFlagType,
        toggleEmergencyConstruction,
        inputWeightChange,
        createResource,
        removeResource
    }

    public static Operation Create()
    {
        return new Operation();
    }

    public Operation SetupAsChangeHaulerCount( Road road, int count )
    {
        type = Type.changeHaulerCount;
        this.road = road;
        unitCount = count;
        name = "Change Hauler Count";
        return this;
    }

    public Operation SetupAsChangeDefenderCount( GuardHouse building, int count )
    {
        type = Type.changeDefenderCount;
        this.building = building;
        unitCount = count;
        name = "Change Defender Count";
        return this;
    }

    public Operation SetupAsToggleEmergencyConstruction( Team team )
    {
        type = Type.toggleEmergencyConstruction;
        this.team = team;
        location = team.mainBuilding.node;  // only for demo mode
        name = "Toggle Emergency Construction";
        return this;
    }

    public Operation SetupAsRemoveBuilding( Building building )
    {
        type = Type.removeBuilding;
        this.building = building;
        name = "Delete Building";
        return this;
    }

    public Operation SetupAsCreateBuilding( Node location, int direction, Building.Type buildingType, Team team )
    {
        type = Type.createBuilding;
        this.location = location;
        this.direction = direction;
        this.buildingType = buildingType;
        this.team = team;
        if ( buildingType < Building.Type.stock )
            name = $"Building a {(Workshop.Type)buildingType}";
        else
            name = $"Building a {buildingType}";
        return this;
    }

    public Operation SetupAsRemoveRoad( Road road )
    {
        type = Type.removeRoad;
        this.road = road;
        name = "Remove Road";
        return this;
    }

    public Operation SetupAsCreateRoad( List<Node> path, Team team )
    {
        type = Type.createRoad;
        this.roadPath = path;
        this.team = team;
        name = $"Create Road at {path[1]}";
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

    public Operation SetupAsCreateFlag( Node location, Team team, bool crossing )
    {
        type = Type.createFlag;
        this.location = location;
        this.team = team;
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

    public Operation SetupAsCreateResource( Node location, Resource.Type type )
    {
        this.type = Type.createResource;
        this.location = location;
        this.resourceType = type;
        name = "Create Resource";
        return this;
    }

    public Operation SetupAsRemoveResource( Resource resource )
    {
        type = Type.removeResource;
        this.resource = resource;
        name = "Remove Resource";
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

    public Operation SetupAsChangeBufferUsage( Workshop workshop, Workshop.Buffer buffer, Workshop.Buffer.Priority usage )
    {
        int index = workshop.buffers.IndexOf( buffer );
        if ( index < 0 )
            return null;
        type = Type.changeBufferUsage;
        this.building = workshop;
        this.bufferIndex = index;
        this.useBuffer = usage;
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

    public Operation SetupAsChangeWorkshopRunningMode( Workshop workshop, Workshop.Mode mode )
    {
        type = Type.changeWorkshopRunningMode;
        this.workshop = workshop;
        this.workshopMode = mode;
        name = "Change Workshop Running Mode";
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

    public Operation SetupAsStockAdjustment( Stock stock, Item.Type itemType, Stock.Channel channel, float value )
    {
        type = Type.stockAdjustment;
        building = stock;
        this.stockChannel = channel;
        this.itemType = itemType;
        weight = value;
        name = $"Stock Adjustment ({value} => {itemType} {channel})";
        return this;
    }

    public Operation SetupAsInputWeightChange( Team team, Workshop.Type workshopType, Item.Type itemType, float weight )
    {
        type = Type.inputWeightChange;
        this.team = team;
        buildingType = (Building.Type)workshopType;
        this.itemType = itemType;
        this.weight = weight;
        name = "Input Weight Change";
        return this;
    }

    public Operation SetupAsAttack( Team team, Attackable target, int attackerCount )
    {
        type = Type.attack;
        building = target;
        this.team = team;
        unitCount = attackerCount;
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
            case Type.changeHaulerCount:
            {
                int oldCount = road.targetHaulerCount;
                road.targetHaulerCount = unitCount;
                return Create().SetupAsChangeHaulerCount( road, oldCount );
            }
            case Type.changeDefenderCount:
            {
                if ( building is GuardHouse guardHouse )
                {
                    int oldCount = guardHouse.optimalSoldierCount;
                    guardHouse.optimalSoldierCount = unitCount;
                    return Create().SetupAsChangeDefenderCount( guardHouse, oldCount );
                }
                break;
            }
            case Type.removeBuilding:
            {
                Building building = this.building;
                if ( building == null )
                    return null;
                var inverse = Operation.Create();
                if ( building is Workshop workshop )
                    inverse.SetupAsCreateBuilding( building.node, building.flagDirection, (Building.Type)workshop.kind, building.team );
                if ( building is Stock )
                    inverse.SetupAsCreateBuilding( building.node, building.flagDirection, Building.Type.stock, building.team );
                if ( building is GuardHouse )
                    inverse.SetupAsCreateBuilding( building.node, building.flagDirection, Building.Type.guardHouse, building.team );

                building.Remove();
                return inverse;
            }
            case Type.createBuilding:
            {
                Building newBuilding = location.building;
                if ( !newBuilding )
                {
                    if ( buildingType < (Building.Type)Workshop.Type.total )
                        newBuilding = Workshop.Create().Setup( location, team, (Workshop.Type)buildingType, direction, block:Resource.BlockHandling.remove );
                    if ( buildingType == Building.Type.stock )
                        newBuilding = Stock.Create().Setup( location, team, direction, block:Resource.BlockHandling.remove );
                    if ( buildingType == Building.Type.guardHouse )
                        newBuilding = GuardHouse.Create().Setup( location, team, direction, block:Resource.BlockHandling.remove );
                }
                else
                {
                    if ( !newBuilding.blueprintOnly || newBuilding.type != buildingType )
                        return null;
                        
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
                if ( road == null )
                    return null;

                road.Remove();
                return Create().SetupAsCreateRoad( road.nodes, road.team );    // TODO Seems to be dangerous to use the road after it was removed
            }
            case Type.createRoad:
            {
                Road newRoad = roadPath[1].road;
                if ( !newRoad )
                {
                    newRoad = Road.Create().Setup( roadPath[0].flag );
                    Assert.global.AreEqual( team, roadPath[0].flag.team );
                    if ( newRoad )
                    {
                        bool allGood = true;
                        for ( int i = 1; i < roadPath.Count && allGood; i++ )
                            allGood &= newRoad.AddNode( roadPath[i] );
                        if ( allGood )
                        {
                            if ( !newRoad.Finish() )
                            {
                                newRoad.Remove();
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
                if ( flag )
                    flag.Remove();

                return Create().SetupAsCreateFlag( flag.node, flag.team, flag.crossing );
            }
            case Type.createFlag:
            {
                Flag newFlag = location.flag;
                if ( !newFlag )
                    newFlag = Flag.Create().Setup( location, team, false, crossing, Resource.BlockHandling.remove );
                else
                {
                    newFlag.assert.IsTrue( newFlag.blueprintOnly );
                    newFlag.Materialize();
                }
                    
                if ( newFlag == null )
                    return null;
                return Create().SetupAsRemoveFlag( newFlag );
            }
            case Type.removeResource:
            {
                var resource = this.resource;
                if ( resource )
                    resource.Remove();

                return Create().SetupAsCreateResource( resource.node, resource.type );
            }
            case Type.createResource:
            {
                var newResource = Resource.Create().Setup( location, resourceType );
                    
                if ( newResource == null )
                    return null;
                return Create().SetupAsRemoveResource( newResource );
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
                route.start.team.UpdateStockRoutes();
                return Create().SetupAsChangePriority( route, direction * -1 );
            }
            case Type.changeWorkshopRunningMode:
            {
                if ( workshop == null )
                    return null;
                var prev = workshop.mode;
                workshop.SetMode( workshopMode );
                return Create().SetupAsChangeWorkshopRunningMode( workshop, prev );
            }
            case Type.moveRoad:
            {
                var road = this.road;
                if ( road == null )
                    return null;
                var index = road.nodes.IndexOf( location );
                var newRoad = road.Move( index, direction );
                if ( !newRoad )
                    return null;
                var newLocation = location.Neighbour( direction );

                return Create().SetupAsMoveRoad( newRoad, newRoad.nodes.IndexOf( newLocation), ( direction + Constants.Node.neighbourCount / 2 ) % Constants.Node.neighbourCount );
            }
            case Type.stockAdjustment:
            {
                if ( building is Stock stock )
                {
                    var oldValue = stock.itemData[(int)itemType].ChangeChannelValue( stockChannel, weight );
                    return Create().SetupAsStockAdjustment( building as Stock, itemType, stockChannel, oldValue );
                }
                return null;
            }
            case Type.inputWeightChange:
            {
                var workshopType = (Workshop.Type)buildingType;
                var prev = team.SetInputWeight( workshopType, itemType, weight );
                return Create().SetupAsInputWeightChange( team, workshopType, itemType, prev );

            }
            case Type.attack:
            {
                team.Attack( building as Attackable, unitCount );
                return null;

            }
            case Type.createPlayer:
            {
                Team team = null;
                foreach ( var existingTeam in HiveCommon.game.teams )
                {
                    if ( existingTeam.name == teamName )
                        team = existingTeam;
                }
                if ( team == null )
                {
                    team = Team.Create().Setup( HiveCommon.game, teamName, Constants.Player.teamColors[HiveCommon.game.teams.Count%Constants.Player.teamColors.Length] );
                    if ( team == null )
                    {
                        Interface.Display( "No room for a new headquarter", closeAfter:int.MaxValue );
                        return null;
                    }
                    HiveCommon.game.teams.Add( team );
                }
                var newPlayer = Simpleton.Create().Setup( playerName, team );
                HiveCommon.game.players.Add( newPlayer );
                Interface.Display( $"{playerName} (team {teamName}) joined the game" );
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
                var previous = workshop.buffers[bufferIndex].usagePriority;
                workshop.ChangeBufferPriority( workshop.buffers[bufferIndex], useBuffer );
                return Create().SetupAsChangeBufferUsage( workshop, workshop.buffers[bufferIndex], previous );
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
            case Type.toggleEmergencyConstruction:
            {
                float normal = team.constructionFactors[(int)Building.Type.stock] == 1 ? 0 : 1;
                var allowed = new List<Building.Type>
                {
                    Building.Type.guardHouse,
                    (Building.Type)Workshop.Type.forester,
                    (Building.Type)Workshop.Type.woodcutter,
                    (Building.Type)Workshop.Type.stonemason,
                    (Building.Type)Workshop.Type.sawmill
                };
                for ( int i = 0; i < (int)Building.Type.total; i++ )
                {
                    if ( !allowed.Contains( (Building.Type)i ) )
                        team.constructionFactors[i] = normal;
                }
                return Create().SetupAsToggleEmergencyConstruction( team );
            }
        }
        return null;
    }
}
