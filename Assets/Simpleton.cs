using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Newtonsoft.Json;
using System.Linq;

public class Simpleton : Player
{
    [JsonIgnore]
    public List<Task> tasks;
    public int currentProblem;
    public World.Timer inability = new World.Timer();
    public float confidence = Constants.Simpleton.defaultConfidence;
    public List<Node> isolatedNodes = new List<Node>();
    public int reservedPlank, reservedStone;

    public static new Simpleton Create()
    {
        return new GameObject( "Simpleton" ).AddComponent<Simpleton>();
    }

    public new Simpleton Setup( string name, Team team )
    {
        if ( base.Setup( name, team ) == null )
            return null;

        return this;
    }

    void FixedUpdate()
    {
        if ( tasks == null )
        {
            tasks = new List<Task>();
            tasks.Add( new GlobalTask( this ) );
            currentProblem = 0;
            return;
        }
        if ( currentProblem < tasks.Count )
        {
            tasks[currentProblem].boss = this;
            var current = tasks[currentProblem];
            if ( tasks[currentProblem].Analyze() == Task.finished )
                currentProblem++;
        }
        else
        {
            Task best = null;
            foreach ( var task in tasks )
            {
                if ( best == null || task.importance > best.importance )
                    best = task;
            }
            if ( best != null && best.importance >= confidence )
            {
                Log( $"[{name}]: Applying solution {best.ToString()} (problem: {best.problemWeight}, solution: {best.solutionEfficiency})" );
                best.ApplySolution();
                inability.Start( Constants.Simpleton.inabilityTolerance );
                confidence = Constants.Simpleton.defaultConfidence;
            }
            if ( inability.done && confidence > Constants.Simpleton.minimumConfidence )
            {
                confidence -= Constants.Simpleton.confidenceLevel;
                inability.Start( Constants.Simpleton.inabilityTolerance );
            }

            tasks = null;
        }
    }

    [Serializable]
    public class Data
    {
        public bool isolated;
    }

    public abstract class Task
    {
        public const bool finished = false;
        public const bool needMoreTime = true;

        public Task( Simpleton boss )
        {
            this.boss = boss;
        }

        public abstract bool Analyze();
        public virtual void ApplySolution() {}
        public float problemWeight, solutionEfficiency, priority = 1;
        public float importance { get { return solutionEfficiency * problemWeight * priority; } }
        public Simpleton boss;
    }

    public class GlobalTask : Task
    {
        public GlobalTask( Simpleton boss ) : base( boss ) {}
        public override bool Analyze()
        {
            float soldierYield = 0;
            foreach ( var workshop in boss.team.workshops )
            {
                if ( workshop.type == Workshop.Type.barrack && workshop.team == boss.team )
                    soldierYield += workshop.maxOutput;
                boss.tasks.Add( new MaintenanceTask( boss, workshop ) );
            }

            boss.reservedPlank = boss.reservedStone = 0;
            void CheckBuilding( Building building )
            {
                boss.reservedPlank += building.construction.plankMissing;
                boss.reservedStone += building.construction.stoneMissing;
            }

            foreach ( var stock in boss.team.stocks )
                CheckBuilding( stock );
            foreach ( var workshop in boss.team.workshops )
                CheckBuilding( workshop );
            foreach ( var guardHouse in boss.team.guardHouses )
                CheckBuilding( guardHouse );

            boss.tasks.Add( new YieldTask( boss, Workshop.Type.woodcutter, Math.Max( soldierYield * 2, 3 ) ) );
            boss.tasks.Add( new YieldTask( boss, Workshop.Type.sawmill, Math.Max( soldierYield, 3 ) ) );
            boss.tasks.Add( new YieldTask( boss, Workshop.Type.stonemason, 1 ) );
            boss.tasks.Add( new YieldTask( boss, Workshop.Type.bakery, soldierYield * 2 ) );
            boss.tasks.Add( new YieldTask( boss, Workshop.Type.barrack, soldierYield + 0.1f ) );
            boss.tasks.Add( new YieldTask( boss, Workshop.Type.bowMaker, soldierYield ) );
            boss.tasks.Add( new YieldTask( boss, Workshop.Type.brewery, soldierYield * 2 ) );
            boss.tasks.Add( new YieldTask( boss, Workshop.Type.bowMaker, soldierYield ) );
            boss.tasks.Add( new YieldTask( boss, Workshop.Type.butcher, soldierYield * 2 ) );
            boss.tasks.Add( new YieldTask( boss, Workshop.Type.coalMine, soldierYield * 2 ) );
            boss.tasks.Add( new YieldTask( boss, Workshop.Type.farm, soldierYield * 3 ) );
            boss.tasks.Add( new YieldTask( boss, Workshop.Type.fishingHut, Math.Max( soldierYield, 1 ) ) );
            boss.tasks.Add( new YieldTask( boss, Workshop.Type.forester, Math.Max( soldierYield * 2, 2 ) ) );
            boss.tasks.Add( new YieldTask( boss, Workshop.Type.goldBarMaker, soldierYield ) );
            boss.tasks.Add( new YieldTask( boss, Workshop.Type.goldMine, soldierYield ) );
            boss.tasks.Add( new YieldTask( boss, Workshop.Type.hunter, soldierYield ) );
            boss.tasks.Add( new YieldTask( boss, Workshop.Type.ironMine, soldierYield ) );
            boss.tasks.Add( new YieldTask( boss, Workshop.Type.mill, soldierYield ) );
            boss.tasks.Add( new YieldTask( boss, Workshop.Type.saltMine, soldierYield ) );
            boss.tasks.Add( new YieldTask( boss, Workshop.Type.smelter, soldierYield ) );
            boss.tasks.Add( new YieldTask( boss, Workshop.Type.stoneMine, 1 ) );
            boss.tasks.Add( new YieldTask( boss, Workshop.Type.weaponMaker, soldierYield ) );
            boss.tasks.Add( new YieldTask( boss, Workshop.Type.well, soldierYield * 2 ) );
            boss.tasks.Add( new ExtendBorderTask( boss ) );

            foreach ( var flag in boss.team.flags )
            {
                if ( flag.team == boss.team )
                    boss.tasks.Add( new FlagTask( boss, flag ) );
            }

            foreach ( var road in boss.team.roads )
            {
                if ( road.nodes.Count >= Constants.Simpleton.roadMaxLength && road.team == boss.team )
                    boss.tasks.Add( new SplitRoadTask( boss, road ) );
            }

            return finished;
        }
    }

    public class YieldTask : Task
    {
        public Workshop.Type workshopType;
        public float target;
        public float currentYield = -1;
        public int nodeRow;
        public Node bestLocation;
        public int bestFlagDirection;
        public float bestScore = float.MinValue;
        public float bestResourceAvailability, bestRelaxAvailability, bestSourceAvailability;
        public List<Workshop.Type> dependencies = new List<Workshop.Type>();
        public Workshop.Configuration configuration;

        public YieldTask( Simpleton boss, Workshop.Type workshopType, float target ) : base( boss ) 
        {
            this.workshopType = workshopType;
            this.target = target;
            if ( workshopType == Workshop.Type.barrack )
                priority = 0.5f;
        }
        public override bool Analyze()
        {
            if ( currentYield < 0 )
            {
                int currentWorkshopCount = 0;
                var outputType = Workshop.GetConfiguration( workshopType ).outputType;
                currentYield = 0;
                foreach ( var workshop in boss.team.workshops )
                {
                    if ( workshop.productionConfiguration.outputType == outputType && workshop.team == boss.team )
                        currentYield += workshop.maxOutput;
                    if ( workshop.type == workshopType )
                        currentWorkshopCount++;
                }

                if ( currentYield >= target )
                    problemWeight = 0;
                else
                    problemWeight = 1 - 0.5f * ( (float)currentYield / target );

                if ( currentYield == 0 )
                {
                    priority = workshopType switch
                    {
                        Workshop.Type.woodcutter => 4,
                        Workshop.Type.stonemason => 2,
                        Workshop.Type.stoneMine => 3,
                        Workshop.Type.sawmill => 3,
                        _ => 1
                    };
                };
                if ( currentWorkshopCount >= HiveCommon.world.challenge.buildingMax[(int)workshopType] )
                    return finished;

                nodeRow = -1;
                return problemWeight > 0 ? needMoreTime : finished;
            }

            configuration = Workshop.GetConfiguration( workshopType );
            int reservedPlank = boss.reservedPlank + 4, reservedStone = boss.reservedStone;
            if ( workshopType == Workshop.Type.woodcutter || workshopType == Workshop.Type.sawmill )
                reservedPlank = 0;
            if ( configuration.plankNeeded + reservedPlank > boss.team.mainBuilding.itemData[(int)Item.Type.plank].content )
                return finished;
            if ( configuration.stoneNeeded + reservedStone > boss.team.mainBuilding.itemData[(int)Item.Type.stone].content )
                return finished;

            if ( workshopType == Workshop.Type.woodcutter )
                dependencies.Add( Workshop.Type.forester );
            if ( workshopType == Workshop.Type.sawmill )
                dependencies.Add( Workshop.Type.woodcutter );
            if ( workshopType == Workshop.Type.mill )
                dependencies.Add( Workshop.Type.farm );
            if ( workshopType == Workshop.Type.bowMaker )
            {
                dependencies.Add( Workshop.Type.sawmill );
                dependencies.Add( Workshop.Type.hunter );
            }
            if ( workshopType == Workshop.Type.bakery )
            {
                dependencies.Add( Workshop.Type.mill );
                dependencies.Add( Workshop.Type.saltMine );
            }
            if ( workshopType == Workshop.Type.smelter )
            {
                dependencies.Add( Workshop.Type.ironMine );
                dependencies.Add( Workshop.Type.coalMine );
            }
            if ( workshopType == Workshop.Type.weaponMaker )
            {
                dependencies.Add( Workshop.Type.smelter );
                dependencies.Add( Workshop.Type.coalMine );
            }
            if ( workshopType == Workshop.Type.brewery )
            {
                dependencies.Add( Workshop.Type.well );
                dependencies.Add( Workshop.Type.farm );
            }
            if ( workshopType == Workshop.Type.butcher )
            {
                dependencies.Add( Workshop.Type.brewery );
                dependencies.Add( Workshop.Type.farm );
            }
            if ( workshopType == Workshop.Type.barrack )
            {
                dependencies.Add( Workshop.Type.bowMaker );
                dependencies.Add( Workshop.Type.brewery );
                dependencies.Add( Workshop.Type.goldBarMaker );
                dependencies.Add( Workshop.Type.weaponMaker );
            }
            if ( workshopType == Workshop.Type.goldBarMaker )
            {
                dependencies.Add( Workshop.Type.goldMine );
                dependencies.Add( Workshop.Type.woodcutter );
            }

            ScanRow( nodeRow++ );

            if ( nodeRow == HiveCommon.ground.dimension - 1 )
                return finished;

            return needMoreTime;
        }

        void ScanRow( int row )
        {
            for ( int x = 0; x < HiveCommon.ground.dimension; x++ )
            {
                var node = HiveCommon.ground.GetNode( x, nodeRow );
                if ( boss.isolatedNodes.Contains( node ) )
                    continue;
                int workingFlagDirection = -1;
                for ( int flagDirection = 0; flagDirection < Constants.Node.neighbourCount; flagDirection++ )
                {
                    if ( Workshop.IsNodeSuitable( node, boss.team, configuration, flagDirection ) )
                        workingFlagDirection = flagDirection;
                }

                if ( workingFlagDirection < 0 )
                    continue;

                var availability = CalculateAvailaibily( node );
                float score = ( availability.resource + availability.relax + availability.source ) / 3;
                if ( availability.resource == 0 )
                    score = 0;
                if ( score > bestScore )
                {
                    bestScore = score;
                    bestSourceAvailability = availability.source;
                    bestRelaxAvailability = availability.relax;
                    bestResourceAvailability = availability.resource;
                    bestLocation = node;
                    bestFlagDirection = workingFlagDirection;
                    solutionEfficiency = score;
                }
            }
        }

        (float resource, float relax, float source) CalculateAvailaibily( Node node )
        {
            int resources = 0;
            foreach ( var offset in Ground.areas[configuration.gatheringRange] )
            {
                var nearby = node + offset;
                bool isThisGood = workshopType switch
                {
                    Workshop.Type.woodcutter => nearby.HasResource( Resource.Type.tree ),
                    Workshop.Type.stonemason => nearby.HasResource( Resource.Type.rock ),
                    Workshop.Type.coalMine => nearby.HasResource( Resource.Type.coal ),
                    Workshop.Type.ironMine => nearby.HasResource( Resource.Type.iron ),
                    Workshop.Type.stoneMine => nearby.HasResource( Resource.Type.stone ),
                    Workshop.Type.saltMine => nearby.HasResource( Resource.Type.salt ),
                    Workshop.Type.goldMine => nearby.HasResource( Resource.Type.gold ),
                    Workshop.Type.farm => nearby.type == Node.Type.grass,
                    Workshop.Type.forester => nearby.type == Node.Type.forest,
                    Workshop.Type.fishingHut => nearby.HasResource( Resource.Type.fish ),
                    Workshop.Type.hunter => nearby.HasResource( Resource.Type.animalSpawner ),
                    _ => true
                };
                if ( isThisGood )
                    resources++;
            }
            float expectedResourceCoverage = workshopType switch
            {
                Workshop.Type.stonemason => 0.05f,
                Workshop.Type.ironMine => 0.5f,
                Workshop.Type.goldMine => 0.5f,
                Workshop.Type.coalMine => 0.5f,
                Workshop.Type.stoneMine => 0.5f,
                Workshop.Type.saltMine => 0.5f,
                Workshop.Type.hunter => 0.01f,
                Workshop.Type.fishingHut => 0.1f,
                _ => 2f
            };
            var resourceAvailability = (float)resources / ( Ground.areas[configuration.gatheringRange].Count * expectedResourceCoverage );

            int relaxSpotCount = 0;
            foreach ( var relaxOffset in Ground.areas[Constants.Workshop.relaxAreaSize] )
            {
                if ( Workshop.IsNodeGoodForRelax( node + relaxOffset ) )
                    relaxSpotCount++;

            }
            float relaxAvailability = (float)relaxSpotCount / configuration.relaxSpotCountNeeded;
            if ( relaxAvailability > 1 )
                relaxAvailability = 1;

            float sourceAvailability = 0.5f;
            if ( dependencies.Count > 0 )
            {
                int sourceScore = 0;
                foreach ( var sourceOffset in Ground.areas[Constants.Simpleton.sourceSearchRange] )
                {
                    var source = node + sourceOffset;
                    if ( source.building && source.building.node == source && source.building.team == boss.team && dependencies.Contains( (Workshop.Type)source.building.type ) )
                        sourceScore += Constants.Simpleton.sourceSearchRange - source.DistanceFrom( node );
                }
                sourceAvailability = (float)sourceScore / ( dependencies.Count * Constants.Simpleton.sourceSearchRange );
            }

            return ( resourceAvailability, relaxAvailability, sourceAvailability );
        }

        public override void ApplySolution()
        {
            HiveCommon.Log( $"[{boss.name}]: Building a {workshopType} at {bestLocation.name} ({bestResourceAvailability}, {bestRelaxAvailability}, {bestSourceAvailability})" );
            HiveCommon.oh.ScheduleCreateBuilding( bestLocation, bestFlagDirection, (Building.Type)workshopType, true, Operation.Source.computer );
        }
    }

    public class FlagTask : Task
    {
        public PathFinder path = PathFinder.Create();
        public Flag flag;
        public FlagTask( Simpleton boss, Flag flag ) : base( boss )
        {
            this.flag = flag;
        }
        public override bool Analyze()
        {
            if ( ( flag.roadsStartingHereCount == 0 && flag != boss.team.mainBuilding.flag ) || !path.FindPathBetween( flag.node, boss.team.mainBuilding.flag.node, PathFinder.Mode.onRoad ) )
            {
                flag.simpletonDataSafe.isolated = true;
                problemWeight = 1;
                foreach ( var offset in Ground.areas[Constants.Ground.maxArea-1] )
                {
                    var node = flag.node + offset;
                    if ( node.team != boss.team || node.flag == null || node.flag.simpletonDataSafe.isolated )
                        continue;
                    if ( !path.FindPathBetween( flag.node, node, PathFinder.Mode.forRoads, true ) )
                    {
                        foreach ( var building in flag.Buildings() )
                            boss.isolatedNodes.Add( building.node );

                        solutionEfficiency = 0.5f;
                        continue;
                    }

                    boss.isolatedNodes.Clear();
                    solutionEfficiency = (float)Math.Pow( 1f/path.path.Count, 0.25f );
                    break; 
                }
                return finished;
            }
            else
                flag.simpletonDataSafe.isolated = false;
            foreach ( var offset in Ground.areas[Constants.Simpleton.flagConnectionRange] )
            {
                var nearbyNode = flag.node + offset;
                if ( nearbyNode.flag && nearbyNode.flag.team == boss.team && nearbyNode.flag.id < flag.id )
                    boss.tasks.Add( new ConnectionTask( boss, flag, nearbyNode.flag ) );
            }
            return finished;
        }

        public override void ApplySolution()
        {
            if ( path.ready )
            {
                HiveCommon.Log( $"[{boss.name}]: Connecting {flag.name} to the road network at {path.path.Last().name}" );
                HiveCommon.oh.ScheduleCreateRoad( path.path, true, Operation.Source.computer );
            }
            else
            {
                HiveCommon.Log( $"[{boss.name}]: Removing separated flag at {flag.node.x}:{flag.node.y}" );
                HiveCommon.oh.ScheduleRemoveFlag( flag, true, Operation.Source.computer );
            }
        }
    }

    public class ConnectionTask : Task
    {
        public Flag flagA, flagB;
        public PathFinder path = PathFinder.Create();

        public ConnectionTask( Simpleton boss, Flag flagA, Flag flagB ) : base( boss )
        {
            this.flagA = flagA;
            this.flagB = flagB;
        }

        public override bool Analyze()
        {
            if ( !path.FindPathBetween( flagA.node, flagB.node, PathFinder.Mode.onRoad ) )
                problemWeight = 1;
            else
            {
                int length = 0;
                foreach ( var road in path.roadPath )
                    length += road.nodes.Count - 1;
                problemWeight = 1 - (float)flagA.node.DistanceFrom( flagB.node ) / length;
            }
            if ( problemWeight < 0.5 )
                return finished;

            if ( !path.FindPathBetween( flagA.node, flagB.node, PathFinder.Mode.forRoads, true ) )
                return finished;

            solutionEfficiency = (float)flagA.node.DistanceFrom( flagB.node ) / (path.path.Count - 1);
            if ( path.path.Count > 5 )
                solutionEfficiency /= (path.path.Count - 5);
            return finished;
        }

        public override void ApplySolution()
        {
            HiveCommon.Log( $"[{boss.name}]: Creating new road between {path.path.First().name} and {path.path.Last().name}" );
            HiveCommon.oh.ScheduleCreateRoad( path.path, true, Operation.Source.computer );
        }
    }

    public class SplitRoadTask : Task
    {
        public Road road;
        public int best;

        public SplitRoadTask( Simpleton boss, Road road ) : base( boss )
        {
            this.road = road;
        }

        public override bool Analyze()
        {
            problemWeight = Math.Min( 1, (float)(road.nodes.Count - Constants.Simpleton.roadMaxLength ) / 2 * Constants.Simpleton.roadMaxLength );
            var center = road.nodes.Count / 2;
            best = 0;
            for ( int i = 2; i < road.nodes.Count - 2; i++ )
            {
                var node = road.nodes[i];
                if ( Flag.IsNodeSuitable( node, boss.team ) )
                    if ( Math.Abs( i - center ) < Math.Abs( best - center ) )
                        best = i;
            }
            solutionEfficiency = best > 0 ? 1 : 0;
            return finished;
        }

        public override void ApplySolution()
        {
            HiveCommon.Log( $"[{boss.name}]: Spliting road at {road.nodes[best].name}" );
            HiveCommon.oh.ScheduleCreateFlag( road.nodes[best], false, true, Operation.Source.computer );
        }
    }

    public class ExtendBorderTask : Task
    {
        public Node best;
        public int flagDirection;

        public ExtendBorderTask( Simpleton boss ) : base( boss ) {}
        public override bool Analyze()
        {
            if ( boss.team.mainBuilding.itemData[(int)Item.Type.plank].content < GuardHouse.guardHouseConfiguration.plankNeeded )
                return finished;
            if ( boss.team.mainBuilding.itemData[(int)Item.Type.stone].content < GuardHouse.guardHouseConfiguration.stoneNeeded + 2 )
                return finished;
            if ( boss.team.guardHouses.Count * 2 > boss.team.workshops.Count )
                return finished;

            problemWeight = Constants.Simpleton.extensionImportance;
            
            foreach ( var node in HiveCommon.ground.nodes )
            {
                if ( boss.isolatedNodes.Contains( node ) )
                    continue;

                if ( node.team != boss.team )
                    continue;
                bool isNodeAtBorder = false;
                foreach ( var offset in Ground.areas[1] )
                {
                    if ( node.Add( offset ).team != boss.team )
                    {
                        isNodeAtBorder = true;
                        break;
                    }
                }
                if ( !isNodeAtBorder )
                    continue;

                bool hasGuardHouseAround = false;
                foreach ( var offset in Ground.areas[6] )
                {
                    if ( node.Add( offset ).building is GuardHouse )
                    {
                        hasGuardHouseAround = true;
                        break;
                    }
                }
                if ( hasGuardHouseAround )
                    continue;

                for ( flagDirection = 0; flagDirection < Constants.Node.neighbourCount; flagDirection++ )
                {
                    if ( GuardHouse.IsNodeSuitable( node, boss.team, flagDirection ) )
                    {
                        best = node;
                        solutionEfficiency = 1;
                        return finished;
                    }
                }
            }
            return finished;
        }

        public override void ApplySolution()
        {
            HiveCommon.Log( $"[{boss.name}]: Building guard house at {best.name}" );
            HiveCommon.oh.ScheduleCreateBuilding( best, flagDirection, Building.Type.guardHouse, true, Operation.Source.computer );
        }
    }

    public class MaintenanceTask : Task
    {
        public Workshop workshop;
        public enum Action
        {
            remove,
            disableFish
        }
        public Action action;
        public MaintenanceTask( Simpleton boss, Workshop workshop ) : base( boss )
        {
            this.workshop = workshop;
        }

        public override bool Analyze()
        {
            if ( workshop.type == Workshop.Type.woodcutter || workshop.type == Workshop.Type.stonemason )
            {
                if ( workshop.ResourcesLeft() == 0 )
                {
                    action = Action.remove;
                    problemWeight = solutionEfficiency = 1;
                }
            }
            if ( workshop.type == Workshop.Type.ironMine || workshop.type == Workshop.Type.coalMine )
            {
                foreach ( var input in workshop.buffers )
                {
                    if ( input.itemType == Item.Type.fish && !input.disabled )
                    {
                        action = Action.disableFish;
                        problemWeight = solutionEfficiency = 1;
                    }
                }
            }
            return finished;
        }

        public override void ApplySolution()
        {
            switch ( action )
            {
                case Action.remove:
                HiveCommon.Log( $"[{boss.name}]: Removing {workshop.name}" );
                if ( workshop.flag.roadsStartingHereCount == 1 )
                    HiveCommon.oh.ScheduleRemoveFlag( workshop.flag, true, Operation.Source.computer );
                else
                    HiveObject.oh.ScheduleRemoveBuilding( workshop, true, Operation.Source.computer );
                break;

                case Action.disableFish:
                foreach ( var input in workshop.buffers )
                {
                    if ( input.itemType == Item.Type.fish )
                        HiveCommon.oh.ScheduleChangeBufferUsage( workshop, input, false, true, Operation.Source.computer );
                }
                break;
            }
        }
    }
}
