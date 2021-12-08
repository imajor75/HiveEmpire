using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Newtonsoft.Json;

public class Simpleton : Player
{
    [JsonIgnore]
    public List<Task> tasks;
    public int currentProblem;
    public float confidence = 0.5f;

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
            if ( best != null && best.importance > confidence )
                best.ApplySolution();
            tasks = null;
        }
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
            var buildings = Resources.FindObjectsOfTypeAll<Building>(); // TODO Keep an array of the current buildings instead of always collecting then using the Resources class
            bool isolatedBuildings = false;
            foreach ( var building in buildings )
            {
                if ( building is Workshop workshop )
                {
                    if ( workshop.type == Workshop.Type.barrack && workshop.team == boss.team )
                        soldierYield += workshop.maxOutput;
                    if ( workshop.type == Workshop.Type.woodcutter || workshop.type == Workshop.Type.stonemason )
                        boss.tasks.Add( new RemoveRunOutTask( boss, workshop ) );
                }
                if ( building.isolated )
                    isolatedBuildings = true;
            }

            if ( !isolatedBuildings )
            {
                boss.tasks.Add( new YieldTask( boss, Workshop.Type.woodcutter, Math.Max( soldierYield * 2, 1 ) ) );
                boss.tasks.Add( new YieldTask( boss, Workshop.Type.sawmill, Math.Max( soldierYield, 1 ) ) );
                boss.tasks.Add( new YieldTask( boss, Workshop.Type.stonemason, 1 ) );
                boss.tasks.Add( new YieldTask( boss, Workshop.Type.bakery, soldierYield * 2 ) );
                boss.tasks.Add( new YieldTask( boss, Workshop.Type.barrack, soldierYield + 0.1f ) );
                boss.tasks.Add( new YieldTask( boss, Workshop.Type.bowMaker, soldierYield ) );
                boss.tasks.Add( new YieldTask( boss, Workshop.Type.brewery, soldierYield * 2 ) );
                boss.tasks.Add( new YieldTask( boss, Workshop.Type.bowMaker, soldierYield ) );
                boss.tasks.Add( new YieldTask( boss, Workshop.Type.butcher, soldierYield * 2 ) );
                boss.tasks.Add( new YieldTask( boss, Workshop.Type.coalMine, soldierYield * 2 ) );
                boss.tasks.Add( new YieldTask( boss, Workshop.Type.farm, soldierYield * 3 ) );
                boss.tasks.Add( new YieldTask( boss, Workshop.Type.fishingHut, soldierYield ) );
                boss.tasks.Add( new YieldTask( boss, Workshop.Type.forester, soldierYield * 2 ) );
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
            }

            boss.tasks.Add( new ExtendBorderTask( boss ) );

            var flagList = Resources.FindObjectsOfTypeAll<Flag>();
            foreach ( var flag in flagList )
            {
                if ( flag.team == boss.team )
                    boss.tasks.Add( new FlagTask( boss, flag ) );
            }

            var roadList = Resources.FindObjectsOfTypeAll<Road>();
            foreach ( var road in roadList )
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
        public int bestScore = int.MinValue;

        public YieldTask( Simpleton boss, Workshop.Type workshopType, float target ) : base( boss ) 
        {
            this.workshopType = workshopType;
            this.target = target;
        }
        public override bool Analyze()
        {
            if ( currentYield < 0 )
            {
                var workshops = Resources.FindObjectsOfTypeAll<Workshop>(); // TODO Keep an array of the current buildings instead of always collecting then using the Resources class
                var outputType = Workshop.GetConfiguration( workshopType ).outputType;
                currentYield = 0;
                foreach ( var workshop in workshops )
                {
                    if ( workshop.productionConfiguration.outputType == outputType && workshop.team == boss.team )
                        currentYield += workshop.maxOutput;
                }

                if ( currentYield >= target )
                    problemWeight = 0;
                else
                    problemWeight = 1 - ( (float)currentYield / target );

                if ( currentYield == 0 && ( workshopType == Workshop.Type.woodcutter || workshopType == Workshop.Type.sawmill || workshopType == Workshop.Type.stonemason || workshopType == Workshop.Type.stoneMine ) )
                    priority = 100; // TODO ?

                nodeRow = -1;
                return problemWeight > 0 ? needMoreTime : finished;
            }

            var configuration = Workshop.GetConfiguration( workshopType );
            if ( configuration.plankNeeded > boss.team.mainBuilding.itemData[(int)Item.Type.plank].content )
                return finished;
            if ( configuration.stoneNeeded > boss.team.mainBuilding.itemData[(int)Item.Type.stone].content )
                return finished;

            ScanRow( nodeRow++ );

            if ( nodeRow == HiveCommon.ground.dimension - 1 )
                return finished;

            return needMoreTime;
        }

        void ScanRow( int row )
        {
            var configuration = Workshop.GetConfiguration( workshopType );
            for ( int x = 0; x < HiveCommon.ground.dimension; x++ )
            {
                var node = HiveCommon.ground.GetNode( x, nodeRow );
                int workingFlagDirection = -1;
                for ( int flagDirection = 0; flagDirection < Constants.Node.neighbourCount; flagDirection++ )
                {
                    if ( Workshop.IsNodeSuitable( node, boss.team, configuration, flagDirection ) )
                        workingFlagDirection = flagDirection;
                }

                if ( workingFlagDirection < 0 )
                    continue;
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
                    if ( isThisGood && Workshop.IsNodeGoodForRelax( nearby ) )
                        resources++;
                }

                if ( resources > bestScore )
                {
                    bestScore = resources;
                    bestLocation = node;
                    bestFlagDirection = workingFlagDirection;
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
                        _ => 1f
                    };
                    solutionEfficiency = (float)resources / ( Ground.areas[configuration.gatheringRange].Count * expectedResourceCoverage );
                }
            }
        }

        public override void ApplySolution()
        {
            HiveCommon.oh.ScheduleCreateBuilding( bestLocation, bestFlagDirection, (Building.Type)workshopType );
        }
    }

    public class FlagTask : Task
    {
        public PathFinder path = ScriptableObject.CreateInstance<PathFinder>();
        public Flag flag;
        public FlagTask( Simpleton boss, Flag flag ) : base( boss )
        {
            this.flag = flag;
        }
        public override bool Analyze()
        {
            if ( flag.roadsStartingHereCount == 0 )
            {
                problemWeight = 1;
                foreach ( var offset in Ground.areas[Constants.Ground.maxArea-1] )
                {
                    var node = flag.node + offset;
                    if ( node.team != boss.team || node.flag == null )
                        continue;
                    if ( !path.FindPathBetween( flag.node, node, PathFinder.Mode.forRoads, true ) )
                        continue;

                    solutionEfficiency = (float)Math.Pow( 1f/path.path.Count, 0.5f );
                    break;
                }
                return finished;
            }
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
            HiveCommon.oh.ScheduleCreateRoad( path.path );
        }
    }

    public class ConnectionTask : Task
    {
        public Flag flagA, flagB;
        public PathFinder path = ScriptableObject.CreateInstance<PathFinder>();

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
            HiveCommon.oh.ScheduleCreateRoad( path.path );
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
            HiveCommon.oh.ScheduleCreateFlag( road.nodes[best] );
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
            if ( boss.team.mainBuilding.itemData[(int)Item.Type.stone].content < GuardHouse.guardHouseConfiguration.stoneNeeded )
                return finished;

            int freeSpots = 0, usedSpots = 0;
            foreach ( var node in HiveCommon.ground.nodes )
            {
                if ( node.building is GuardHouse && !node.building.construction.done )
                    return finished;
                if ( node.team != boss.team )
                    continue;
                if ( node.building || node.flag || node.road )
                    usedSpots++;
                else
                    freeSpots++;
            }
            problemWeight = Math.Min( 2 * (float)usedSpots / (freeSpots+usedSpots), 1 );
            
            foreach ( var node in HiveCommon.ground.nodes )
            {
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
            HiveCommon.oh.ScheduleCreateBuilding( best, flagDirection, Building.Type.guardHouse );
        }
    }

    public class RemoveRunOutTask : Task
    {
        public Workshop workshop;
        public RemoveRunOutTask( Simpleton boss, Workshop workshop ) : base( boss )
        {
            this.workshop = workshop;
        }

        public override bool Analyze()
        {
            if ( workshop.ResourcesLeft() == 0 )
                problemWeight = solutionEfficiency = 1;
            return finished;
        }

        public override void ApplySolution()
        {
            if ( workshop.flag.roadsStartingHereCount == 1 )
                HiveCommon.oh.ScheduleRemoveFlag( workshop.flag );
            else
                HiveObject.oh.ScheduleRemoveBuilding( workshop );
        }
    }
}
