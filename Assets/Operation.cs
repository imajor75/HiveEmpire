using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class OperationHandler : HiveObject
{
	public List<Operation> undoQueue = new List<Operation>(), redoQueue = new List<Operation>(), repeatBuffer = new List<Operation>();

    public override Node location => throw new System.NotImplementedException();

    public void ExecuteOperation( Operation operation, bool doneAlready = false )
	{
        operation.scheduleAt = World.instance.time;
        repeatBuffer.Add( operation );
		var inverse = operation.ExecuteAndInvert( doneAlready );
        if ( inverse )
        {
            undoQueue.Add( inverse );
            redoQueue.Clear();
        }
        else
            assert.Fail( "Not invertible operation" );
	}

	void RepeatOperation( List<Operation> from, List<Operation> to, bool merge = false )
	{
		if ( from.Count == 0 )
			return;
		var operation = from.Last();
        var hadMerge = operation.merge;
		from.Remove( operation );
		var inverted = operation.ExecuteAndInvert( false );
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

	public void RegisterCreateBuilding( Building building, bool merge = false )
	{
		ExecuteOperation( Operation.Create().SetupAsRemoveBuilding( building, merge ), true );
	}

	public void ExecuteRemoveRoad( Road road, bool merge = false )
	{
        if ( road )
		    ExecuteOperation( Operation.Create().SetupAsRemoveRoad( road, merge ) );
	}

	public void RegisterCreateRoad( Road road )
	{
		ExecuteOperation( Operation.Create().SetupAsCreateRoad( road.nodes ), true );
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

	public void RegisterCreateFlag( Flag flag, bool merge = false )
	{
		ExecuteOperation( Operation.Create().SetupAsRemoveFlag( flag, merge ), true );
	}

	public void RegisterChangeArea( Ground.Area area, Node oldCenter, int oldRadius )
	{
        if ( area != null )
		    ExecuteOperation( Operation.Create().SetupAsChangeArea( area, oldCenter, oldRadius ), true );
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

}

public class Operation : ScriptableObject
{
    public Type type;
    public int workerCount;
    public Road road;
    public Building building;
    public Node location;
    public Workshop.Type workshopType;
    public BuildingType buildingType;
    public int direction;
    public List<Node> roadPath;
    public Flag flag;
    public bool crossing;
    public bool merge;
    public Ground.Area area;
    public int radius;
    public Stock.Route route;
    public Stock start, end;
    public Item.Type itemType;

    public enum BuildingType
    {
        workshop,
        stock,
        guardHouse
    }

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

    public Operation SetupAsCreateBuilding( Node location, int direction, BuildingType buildingType, Workshop.Type workshopType = Workshop.Type.unknown )
    {
        type = Type.createBuilding;
        this.location = location;
        this.direction = direction;
        this.buildingType = buildingType;
        this.workshopType = workshopType;
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

    public Operation ExecuteAndInvert( bool doneAlready )
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
                    inverse.SetupAsCreateBuilding( building.node, building.flagDirection, BuildingType.workshop, workshop.type );
                if ( building is Stock )
                    inverse.SetupAsCreateBuilding( building.node, building.flagDirection, BuildingType.stock );
                if ( building is GuardHouse )
                    inverse.SetupAsCreateBuilding( building.node, building.flagDirection, BuildingType.guardHouse );

                if ( !doneAlready )
                    building.Remove( true );
                return inverse;
            }
            case Type.createBuilding:
            {
                Building newBuilding = null;
                if ( !doneAlready )
                {
                    if ( buildingType == BuildingType.workshop )
                        newBuilding = Workshop.Create().Setup( location, Interface.root.mainPlayer, workshopType, direction );
                    if ( buildingType == BuildingType.stock )
                        newBuilding = Stock.Create().Setup( location, Interface.root.mainPlayer, direction );
                    if ( buildingType == BuildingType.guardHouse )
                        newBuilding = GuardHouse.Create().Setup( location, Interface.root.mainPlayer, direction );
                }
                else
                    newBuilding = location.building;

                if ( newBuilding )
                    return Create().SetupAsRemoveBuilding( newBuilding );
                else
                    return null;
            }
            case Type.removeRoad:
            {
                if ( !doneAlready )
                {
                    if ( road == null || !road.Remove( true ) )
                        return null;
                }
                return Create().SetupAsCreateRoad( road.nodes );    // TODO Seems to be dangerous to use the road after it was removed
            }
            case Type.createRoad:
            {
                Road newRoad = null;
                if ( !doneAlready )
                {
                    newRoad = Road.Create().Setup( roadPath[0].flag );
                    if ( newRoad )
                    {
                        bool allGood = true;
                        for ( int i = 1; i < roadPath.Count && allGood; i++ )
                            allGood &= newRoad.AddNode( roadPath[i] );
                        if ( allGood )
                            newRoad.Finish();
                    }
                }
                else
                    newRoad = roadPath[1].road;
                if ( newRoad )
                    return Create().SetupAsRemoveRoad( newRoad );
                else
                    return null;
            }
            case Type.removeFlag:
            {
                if ( !doneAlready )
                {
                    if ( flag == null || !flag.Remove( true ) )
                        return null;
                }

                return Create().SetupAsCreateFlag( flag.node, flag.crossing );
            }
            case Type.createFlag:
            {
                Flag newFlag = null;
                if ( !doneAlready )
                    newFlag = Flag.Create().Setup( location, Interface.root.mainPlayer, false, crossing );
                else
                    newFlag = location.flag;
                if ( newFlag == null )
                    return null;
                return Create().SetupAsRemoveFlag( newFlag );
            }
            case Type.moveFlag:
            {
                if ( !doneAlready && !flag.Move( direction ) )
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
                if ( !doneAlready )
                {
                    route.priority += direction;
                    route.start.owner.UpdateStockRoutes( route.itemType );
                }
                return Create().SetupAsChangePriority( route, direction * -1 );
            }
            case Type.moveRoad:
            {
                var index = location.road.nodes.IndexOf( location );
                if ( !location.road.Move( index, direction ) )
                    return null;
                return Create().SetupAsMoveRoad( road, index, ( direction + Constants.Node.neighbourCount / 2 ) % Constants.Node.neighbourCount );
            }
            case Type.CRC:
                break;
            }
        }
        return null;
    }
}
