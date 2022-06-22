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
    public List<Node> blockedNodes = new List<Node>();
    public List<Item.Type> lackingProductions = new List<Item.Type>();
    public int reservedPlank, reservedStone;
    public int expectedLog, expectedPlank;
    public bool active;
	public bool showActions;
    public bool peaceful;
    public bool noRoom;
    public bool dumpTasks, dumpYields;

   	[Obsolete( "Compatibility with old files", true )]
    List<Node> isolatedNodes { set { blockedNodes = value; } }
   	[Obsolete( "Compatibility with old files", true )]
	bool insideCriticalSection { set {} }
   	[Obsolete( "Compatibility with old files", true )]
    bool hasSawmill { set {} }
   	[Obsolete( "Compatibility with old files", true )]
    bool hasWoodcutter { set {} }


    public static Simpleton Create()
    {
        return new GameObject( "Simpleton" ).AddComponent<Simpleton>();
    }

    public new Simpleton Setup( string name, Team team )
    {
        if ( base.Setup( name, team ) == null )
            return null;

        return this;
    }

    void Log( string text )
    {
        HiveCommon.Log( $"[{name}]: {text}" );
    }

    public override void Defeat()
    {
        if ( active )
            world.defeatedSimpletonCount++;
        base.Defeat();
    }

    public override void GameLogicUpdate()
    {
        if ( team.mainBuilding == null )
            return;

        if ( tasks == null && active )
        {
            tasks = new List<Task>();
            tasks.Add( new GlobalTask( this ) );
            currentProblem = 0;
            return;
        }
        if ( tasks == null )
            return;
        if ( currentProblem < tasks.Count )
        {
            tasks[currentProblem].boss = this;
            var current = tasks[currentProblem];
            if ( tasks[currentProblem].Analyze() == Task.finished )
                currentProblem++;
        }
        else
        {
            if ( dumpTasks )
            {
                tasks.Sort( ( a, b ) => b.importance.CompareTo( a.importance ) );
                Log( "==================" );
                Log( $"{name} tasks:" );
                for ( int i = 0; i < tasks.Count && i < 30; i++ )
                {
                    var task = tasks[i];
                    Log( $"{i}. {task.importance:F2} ({task.problemWeight:F2}, {task.solutionEfficiency:F2}) {task.description}" );
                }            
                dumpTasks = false;
            }

            if ( dumpYields )
            {
                Log( "==================" );
                foreach ( var task in tasks )
                {
                    if ( task is YieldTask yieldTask )
                    {
                        var currentYield = yieldTask.surplus ? "surplus" : yieldTask.currentYield.ToString();
                        Log( $"{yieldTask.workshopType} - target: {yieldTask.target}, current: {currentYield}, best location: {yieldTask.bestLocation}" );
                    }
                }
                dumpYields = false;
            }

            Task best = null;
            foreach ( var task in tasks )
            {
                if ( best == null || task.importance > best.importance )
                    best = task;
            }
            if ( best != null && best.importance >= confidence )
            {
                Log( $"Applying solution {best.ToString()} (problem: {best.problemWeight}, solution: {best.solutionEfficiency})" );
                best.ApplySolution();
                inability.Start( Constants.Simpleton.inabilityTolerance );
                confidence = Constants.Simpleton.defaultConfidence;
            }
            if ( inability.done && confidence > Constants.Simpleton.minimumConfidence )
            {
                confidence -= Constants.Simpleton.confidenceLevel;
                Log( $"No good solution (best: {best.importance}), reducing confidence to {confidence}" );
                inability.Start( Constants.Simpleton.inabilityTolerance );
            }

            tasks = null;
        }
    }

    [Serializable]
    public class Data
    {
        public bool isolated;
        public World.Timer lastCleanup = new World.Timer();
        public List<Deal> deals = new List<Deal>();
        public List<Item.Type> managedItemTypes = new List<Item.Type>();
        public World.Timer lastDealCheck = new World.Timer();
        public World.Timer lastTimeHadResources = new World.Timer();
        public HiveObject hiveObject;
        public Building possiblePartner;
        public Item.Type possiblePartnerItemType;
        public bool hasOutputStock;
        public List<Flag> failedConnections = new List<Flag>();

       	[Obsolete( "Compatibility with old files", true )]
        Building possibleDealer { set {} }
       	[Obsolete( "Compatibility with old files", true )]
        Workshop.Buffer possibleDealerBuffer { set {} }

        public Data() {}

        public Data( HiveObject hiveObject )
        {
            this.hiveObject = hiveObject;
        }

        public bool RegisterManagedItemType( Item.Type itemType )
        {
            if ( managedItemTypes.Contains( itemType ) )
                return false;

            managedItemTypes.Add( itemType );
            return true;
        }

        public bool RegisterPartner( Building partner, Item.Type itemType )
        {
            if ( partner == null )
                return false;
            foreach ( var deal in deals )
            {
                if ( deal.partner == partner && deal.itemType == itemType )
                    return false;
            }
            deals.Add( new Deal { partner = partner, itemType = itemType } );
            if ( hiveObject is Workshop workshop )
            {
                var area = workshop.outputArea;
                foreach ( var buffer in workshop.buffers )
                    if ( buffer.itemType == itemType )
                        area = buffer.area;
                var offset = new Ground.Offset( 0, 0, 0 );
                int dealCount = 0;
                foreach ( var deal in deals )
                {
                    if ( deal.itemType != itemType )
                        continue;
                    
                    offset += deal.partner.node - workshop.node;
                    dealCount++;
                }
                offset.x /= dealCount;
                offset.y /= dealCount;
                var center = workshop.node + offset;
                int radius = 0;
                bool hasStock = false;
                foreach ( var deal in deals )
                {
                    if ( deal.itemType == itemType )
                        radius = Math.Max( radius, center.DistanceFrom( deal.partner.node ) );
                    if ( deal.partner is Stock )
                        hasStock = true;
                }
                if ( workshop.type != Workshop.Type.woodcutter || hasStock )
                    HiveObject.oh.ScheduleChangeArea( workshop, area, center, radius, false, Operation.Source.computer );
            }
            partner.simpletonDataSafe.RegisterPartner( hiveObject as Building, itemType );
            return true;
        }
    }

    [Serializable]
    public class Deal
    {
        public Item.Type itemType;
        public Building partner;
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
        virtual public string description { get { return ToString(); } }
        public Simpleton boss;
    }

    public class GlobalTask : Task
    {
        public enum Action
        {   
            toggleEmergency,
            disableNonConstruction,
            enableNonConstruction,
        }
        public Action action;

        public GlobalTask( Simpleton boss ) : base( boss ) {}
        public override bool Analyze()
        {
            float soldierYield = 0;
            foreach ( var workshop in boss.team.workshops )
            {
                if ( !workshop.construction.done )
                    continue;
                if ( workshop.type == Workshop.Type.barrack && workshop.team == boss.team )
                    soldierYield += workshop.maxOutput;
                boss.tasks.Add( new MaintenanceTask( boss, workshop ) );
            }

            boss.reservedPlank = boss.reservedStone = 0;
            void CheckBuilding( Building building )
            {
                if ( !building.construction.done )
                {
                    boss.reservedPlank += building.construction.plankMissing;
                    boss.reservedStone += building.construction.stoneMissing;
                }
                foreach ( var deal in building.simpletonDataSafe.deals )
                {
                    bool valid = false;
                    if ( deal.partner is Workshop || deal.partner is Stock )
                        valid = true;
                    if ( deal.partner == null )
                        valid = false;
                    if ( !valid )
                    {
                        building.simpletonDataSafe.deals.Remove( deal );
                        return;
                    }
                }
            }

            bool hasSawmill = false;
            boss.expectedLog = 0;
            List<Resource> countedTrees = new List<Resource>();
            foreach ( var workshop in boss.team.workshops )
            {
                if ( workshop.type == Workshop.Type.woodcutter && workshop.construction.done )
                {
                    bool hasForester = false;
                    int forestNodeCount = 0, treeCount = 0;
                    foreach ( var offset in Ground.areas[Workshop.GetConfiguration( Workshop.Type.woodcutter ).gatheringRange] )
                    {
                        var node = workshop.node + offset;
                        if ( node.building && node.building.type == (Building.Type)Workshop.Type.forester && node.building.team == boss.team && node.building.construction.done )
                            hasForester = true;
                        if ( node.type == Node.Type.forest )
                            forestNodeCount++;
                        foreach ( var resource in node.resources )
                        {
                            if ( resource.type != Resource.Type.tree )
                                continue;
                            if ( resource.charges > 0 )
                            {
                                resource.charges = 0;   // TODO Not so nice
                                countedTrees.Add( resource );
                                treeCount++;
                            }
                        }
                    }
                    int expectedLocalLog = treeCount;
                    if ( hasForester && forestNodeCount >= Constants.Simpleton.forestNodeCountForRenew )
                        expectedLocalLog = Constants.Simpleton.expectedLogFromRenewWoodcutter;
                    boss.expectedLog += expectedLocalLog;
                }
                if ( workshop.type == Workshop.Type.sawmill && workshop.construction.done )
                    hasSawmill = true;
            }
            foreach ( var tree in countedTrees )
                tree.charges = 1;
            boss.expectedPlank = hasSawmill ? boss.expectedLog : 0;

            foreach ( var stock in boss.team.stocks )
                CheckBuilding( stock );
            foreach ( var workshop in boss.team.workshops )
                CheckBuilding( workshop );
            foreach ( var guardHouse in boss.team.guardHouses )
                CheckBuilding( guardHouse );

            if ( boss.reservedPlank > 0 && boss.team.FindInputWeight( Workshop.Type.bowMaker, Item.Type.plank ).weight > 0 )
            {
                action = Action.disableNonConstruction;
                problemWeight = solutionEfficiency = 1;
            }

            if ( boss.reservedPlank == 0 && boss.team.FindInputWeight( Workshop.Type.bowMaker, Item.Type.plank ).weight == 0 )
            {
                action = Action.enableNonConstruction;
                problemWeight = solutionEfficiency = 1;
            }
            
            if ( boss.expectedPlank < Constants.Simpleton.expectedPlankPanic && boss.team.constructionFactors[(int)Building.Type.stock] != 0 )
            {
                action = Action.toggleEmergency;
                problemWeight = solutionEfficiency = 1;
            }
            if ( boss.expectedPlank >= Constants.Simpleton.expectedPlankPanic && boss.team.constructionFactors[(int)Building.Type.stock] == 0 )
            {
                action = Action.toggleEmergency;
                problemWeight = solutionEfficiency = 1;
            }
            boss.noRoom = false;

            boss.tasks.Add( new YieldTask( boss, Workshop.Type.woodcutter, Math.Max( soldierYield * 2, 3 ) ) );
            boss.tasks.Add( new YieldTask( boss, Workshop.Type.sawmill, Math.Max( soldierYield, 3 ) ) );
            boss.tasks.Add( new YieldTask( boss, Workshop.Type.stonemason, 1 ) );
            boss.tasks.Add( new YieldTask( boss, Workshop.Type.bakery, soldierYield * 2 ) );
            if ( boss.lackingProductions.Count == 0 )
                boss.tasks.Add( new YieldTask( boss, Workshop.Type.barrack, soldierYield + 0.1f ) );
            else
                boss.lackingProductions.Clear();
            boss.tasks.Add( new YieldTask( boss, Workshop.Type.bowMaker, soldierYield ) );
            boss.tasks.Add( new YieldTask( boss, Workshop.Type.brewery, soldierYield * 2 ) );
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
                if ( flag.team == boss.team && !flag.blueprintOnly )
                    boss.tasks.Add( new FlagTask( boss, flag ) );
            }

            foreach ( var road in boss.team.roads )
            {
                if ( road.nodes.Count >= Constants.Simpleton.roadMaxLength && road.team == boss.team && road.ready )
                    boss.tasks.Add( new SplitRoadTask( boss, road ) );
            }

            if ( !boss.peaceful )
            {
                foreach ( var enemy in world.teams )
                {
                    if ( enemy == boss.team )
                        continue;
                    boss.tasks.Add( new AttackTask( boss, enemy.mainBuilding ) );
                    foreach ( var guardHouse in enemy.guardHouses )
                        boss.tasks.Add( new AttackTask( boss, guardHouse ) );
                }
            }

            return finished;
        }

        public override void ApplySolution()
        {
            switch ( action )
            {
                case Action.toggleEmergency:
                    HiveCommon.oh.ScheduleToggleEmergencyConstruction( boss.team, true, Operation.Source.computer );
                    break;
                case Action.disableNonConstruction:
                    HiveCommon.oh.ScheduleInputWeightChange( boss.team, Workshop.Type.goldBarMaker, Item.Type.log, 0 );
                    HiveCommon.oh.ScheduleInputWeightChange( boss.team, Workshop.Type.bowMaker, Item.Type.plank, 0 );
                    break;
                case Action.enableNonConstruction:
                    HiveCommon.oh.ScheduleInputWeightChange( boss.team, Workshop.Type.goldBarMaker, Item.Type.log, 0.5f );
                    HiveCommon.oh.ScheduleInputWeightChange( boss.team, Workshop.Type.bowMaker, Item.Type.plank, 0.5f );
                    break;
            }
        }

        public override string description
        {
            get
            {
                string d = "GlobalTask: ";
                d += action switch
                {
                    Action.toggleEmergency => "toggle emergency",
                    Action.disableNonConstruction => "disable base material usage for economy",
                    Action.enableNonConstruction => "enable base material usage for economy",
                    _ => "unknown"
                };
                return d;
            }
        }
    }

    public class AttackTask : Task
    {
        public Attackable target;

        public AttackTask( Simpleton boss, Attackable target ) : base( boss )
        {
            this.target = target;
        }

        public override bool Analyze()
        {
            if ( target == null )
                return finished;
                
            problemWeight = 0.5f;

            if ( boss.team.Attack( target, 1, true ) != Team.AttackStatus.available || target.attackerTeam )
                return finished;

            int soldiersNeeded = target.defenderCount * 2 + 1;
            if ( boss.team.soldierCount < soldiersNeeded + Constants.Simpleton.soldiersReserved )
                return finished;

            solutionEfficiency = ( boss.team.soldierCount - Constants.Simpleton.soldiersReserved ) * 0.01f;
            return finished;
        }

        public override void ApplySolution()
        {
            HiveCommon.oh.ScheduleAttack( boss.team, target, target.defenderCount * 2 + 1, true, Operation.Source.computer );
        }
    }

    public class YieldTask : Task
    {
        public Workshop.Type workshopType;
        public float target;
        public float currentYield = -1;
        public bool surplus;
        public int nodeRow;
        public Node bestLocation;
        public int bestFlagDirection;
        public float bestScore = float.MinValue;
        public List<Workshop.Type> dependencies = new List<Workshop.Type>();
        public Workshop.Configuration configuration;
        public int reservedPlank, reservedStone;
        public int currentPlank, currentStone;

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
                    {
                        if ( workshop.output > 0 )
                        {
                            surplus = true;
                            return finished;
                        }
                        currentYield += workshop.CalculateMaxOutput();
                    }
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
                if ( HiveCommon.world.challenge.buildingMax != null && currentWorkshopCount >= HiveCommon.world.challenge.buildingMax[(int)workshopType] )
                    return finished;

                nodeRow = -1;
                if ( problemWeight > 0 )
                {
                    GatherDependencies();
                    boss.lackingProductions.Add( outputType );
                }
                return problemWeight > 0 ? needMoreTime : finished;
            }

            configuration = Workshop.GetConfiguration( workshopType );
            reservedPlank = boss.reservedPlank;
            reservedStone = boss.reservedStone;
            if ( workshopType != Workshop.Type.woodcutter && workshopType != Workshop.Type.sawmill && workshopType != Workshop.Type.forester )
                reservedPlank += 4;

            currentPlank = boss.team.Stockpile( Item.Type.plank ) + boss.expectedPlank;
            currentStone = boss.team.Stockpile( Item.Type.stone );

            if ( configuration.plankNeeded + reservedPlank > currentPlank )
                return finished;
            if ( configuration.stoneNeeded > 0 && configuration.stoneNeeded + reservedStone > currentStone )
                return finished;

            ScanRow( nodeRow++ );

            if ( nodeRow == HiveCommon.ground.dimension )
            {
                if ( bestLocation == null )
                {
                    boss.Log( $"A new {workshopType} would be good, but there is no room" );
                    boss.noRoom = true;
                }
                return finished;
            }

            return needMoreTime;
        }

        void GatherDependencies()
        {
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
        }

        void ScanRow( int row )
        {
            for ( int x = 0; x < HiveCommon.ground.dimension; x++ )
            {
                var node = HiveCommon.ground.GetNode( x, nodeRow );
                int workingFlagDirection = -1;
                Node site = null;
                for ( int flagDirection = 0; flagDirection < Constants.Node.neighbourCount; flagDirection++ )
                {
                    int o = ( flagDirection + ( Constants.Node.neighbourCount / 2 ) ) % Constants.Node.neighbourCount;
                    site = node.Neighbour( o );
                    if ( boss.blockedNodes.Contains( site ) )
                        continue;
                    if ( Workshop.IsNodeSuitable( site, boss.team, configuration, flagDirection ) )
                    {
                        workingFlagDirection = flagDirection;
                        break;
                    }
                }

                if ( workingFlagDirection < 0 )
                    continue;

                float score = CalculateAvailaibily( site );
                if ( score > bestScore )
                {
                    bestScore = score;
                    bestLocation = site;
                    bestFlagDirection = workingFlagDirection;
                    solutionEfficiency = score;
                }
            }
        }

        float CalculateAvailaibily( Node node )
        {
            float resources = 0;
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
                if ( workshopType == Workshop.Type.woodcutter && nearby.type == Node.Type.forest )
                    resources += 0.5f;
            }
            float expectedResourceCoverage = workshopType switch
            {
                Workshop.Type.stonemason => 0.05f,
                Workshop.Type.ironMine => 0.5f,
                Workshop.Type.goldMine => 0.5f,
                Workshop.Type.coalMine => 0.5f,
                Workshop.Type.stoneMine => 0.01f,
                Workshop.Type.saltMine => 0.5f,
                Workshop.Type.hunter => 0.01f,
                Workshop.Type.fishingHut => 0.1f,
                Workshop.Type.woodcutter => 0.5f,
                Workshop.Type.forester => 0.5f,
                _ => 2f
            };
            if ( resources == 0 )
                return 0;

            float score = ( resources > expectedResourceCoverage * Ground.areas[configuration.gatheringRange].Count ) ? Constants.Simpleton.highResourceEfficiency : Constants.Simpleton.lowResourceEfficiency;

            int relaxSpotCount = 0;
            foreach ( var relaxOffset in Ground.areas[Constants.Workshop.relaxAreaSize] )
            {
                if ( Workshop.IsNodeGoodForRelax( node + relaxOffset ) )
                    relaxSpotCount++;

            }
            float relaxAvailability = (float)relaxSpotCount / configuration.relaxSpotCountNeeded;
            if ( relaxAvailability > 1 )
                relaxAvailability = 1;
            score += relaxAvailability * Constants.Simpleton.relaxImportance;

            float sourceAvailability = 0.5f;
            int buildingCount = 0;
            if ( dependencies.Count > 0 )
            {
                int sourceScore = 0;
                foreach ( var sourceOffset in Ground.areas[Constants.Simpleton.sourceSearchRange] )
                {
                    var buildingNode = node.Add( sourceOffset );
                    var building = buildingNode.building;
                    if ( building == null || building.node != buildingNode || building.team != boss.team )
                        continue;
                    buildingCount++;
                    if ( dependencies.Contains( (Workshop.Type)building.type ) )
                        sourceScore += Constants.Simpleton.sourceSearchRange - buildingNode.DistanceFrom( node );
                }
                sourceAvailability = (float)sourceScore / ( dependencies.Count * Constants.Simpleton.sourceSearchRange );
            }
            score += sourceAvailability * Constants.Simpleton.sourceImportance;

            if ( node.valuable )
                score *= Constants.Simpleton.valuableNodePenalty;

            return score;
        }

        public override string description
        {
            get
            {
                return $"YieldTask: building {workshopType} at {bestLocation}";
            }
        }

        public override void ApplySolution()
        {
            boss.Log( $"Building a {workshopType} at {bestLocation}" );
            boss.Log( $" plank: {currentPlank} ({reservedPlank} reserved), stone: {currentStone} ({reservedStone} reserved)" );
            HiveCommon.oh.ScheduleCreateBuilding( bestLocation, bestFlagDirection, (Building.Type)workshopType, boss.team, true, Operation.Source.computer );
        }
    }

    public class FlagTask : Task
    {
        public enum Action
        {
            connect,
            remove,
            removeBlocked,
            removeRoad,
            capture
        }
        public Action action;
        public PathFinder path = PathFinder.Create();
        public Flag flag;
        public Road road;
        override public string description
        { 
            get 
            {
                string d = "FlagTask: ";
                switch ( action )
                {   
                    case Action.connect:
                    {
                        if ( path == null || path.path == null || path.path.Count < 2 || flag == null )
                            d += $"failed connect attempt of {flag.node.x}:{flag.node.y}";
                        else
                        {
                            var lastNode = path.path.Last();
                            d += $"connecting {flag.node.x}:{flag.node.y} to {lastNode.x}:{lastNode.y}";
                        }
                        break;
                    }
                    case Action.remove:
                        d += $"removing flag at {flag.node}";
                        break;
                    case Action.removeBlocked:
                        d += $"removing blocked flag at {flag.node}";
                        break;
                    case Action.removeRoad:
                        d += $"removing {road}";
                        break;
                    case Action.capture:
                        d += $"capturing roads around flag at {flag.node}";
                        break;
                    default:
                        d += "unknown";
                        break;
                };
                return d;
            }
        }
        public FlagTask( Simpleton boss, Flag flag ) : base( boss )
        {
            this.flag = flag;
        }
        public override bool Analyze()
        {
            foreach ( var fail in flag.simpletonDataSafe.failedConnections )
            {
                if ( fail == null )
                {
                    flag.simpletonDataSafe.failedConnections.Remove( fail );
                    break;
                }
            }

            int roadCount = flag.roadsStartingHereCount;
            if ( ( roadCount == 0 && flag != boss.team.mainBuilding.flag ) || !path.FindPathBetween( flag.node, boss.team.mainBuilding.flag.node, PathFinder.Mode.onRoad ) )
            {
                flag.simpletonDataSafe.isolated = true;

                var buildingCount = flag.Buildings().Count;
                if ( buildingCount > 0 )
                    problemWeight = Constants.Simpleton.isolatedBuildingWeight;
                else
                {
                    if ( roadCount == 0 )
                        problemWeight = Constants.Simpleton.abandonedFlagWeight;
                    else
                        problemWeight = Constants.Simpleton.isolatedFlagWeight;
                }

                if ( flag.CaptureRoads( true ) )
                {
                    solutionEfficiency = 1;
                    action = Action.capture;
                    return finished;
                }

                foreach ( var offset in Ground.areas[Constants.Ground.maxArea-1] )
                {
                    var node = flag.node + offset;
                    if ( node.team != boss.team || node.flag == null || node.flag.simpletonDataSafe.isolated )
                        continue;
                    if ( !path.FindPathBetween( flag.node, node, PathFinder.Mode.forRoads, true ) )
                        continue;

                    solutionEfficiency = (float)Math.Pow( 1f/path.path.Count, 0.25f );
                    action = Action.connect;
                    return finished;
                }

                problemWeight = Constants.Simpleton.blockedFlagWeight;
                solutionEfficiency = 1;
                action = Action.removeBlocked;
                return finished;
            }
            else
                flag.simpletonDataSafe.isolated = false;

            if ( flag.CaptureRoads( true ) )
            {
                action = Action.capture;
                problemWeight = Constants.Simpleton.flagCaptureImportance;
                solutionEfficiency = 1;
                return finished;
            }

            List<Flag> connectedFlags = new List<Flag>();
            List<Road> connections = new List<Road>();
            foreach ( var road in flag.roadsStartingHere )
            {
                if ( road == null )
                    continue;

                var otherFlag = road.OtherEnd( flag );
                if ( otherFlag == flag )
                {
                    problemWeight = 0.5f;
                    solutionEfficiency = 1.0f;
                    this.road = road;
                    action = Action.removeRoad;
                    return finished;
                }
                if ( !connectedFlags.Contains( otherFlag ) && otherFlag != flag )
                {
                    connectedFlags.Add( otherFlag );
                    connections.Add( road );
                }
                else
                {
                    var alternativeRoad = connections[connectedFlags.IndexOf( otherFlag )];
                    problemWeight = 0.5f;
                    solutionEfficiency = 1;
                    action = Action.removeRoad;
                    this.road = road.nodes.Count > alternativeRoad.nodes.Count ? road : alternativeRoad;
                    return finished;
                }
            }

            if ( connectedFlags.Count < 2 && flag.Buildings().Count == 0 )
            {
                problemWeight = Constants.Simpleton.deadEndProblemFactor;
                solutionEfficiency = 1;
                action = Action.remove;
                return finished;
            }

            foreach ( var road in flag.roadsStartingHere )
            {
                if ( road == null )
                    continue;

                problemWeight = ( (float)road.lastUsed.age - Constants.Simpleton.roadLastUsedMin ) / ( Constants.Simpleton.roadLastUsedMax - Constants.Simpleton.roadLastUsedMin );
                if ( problemWeight < 0 )
                    problemWeight = 0;
                if ( problemWeight > 1 )
                    problemWeight = 1;
                if ( problemWeight != 0 )
                {
                    action = Action.removeRoad;
                    solutionEfficiency = 1;
                    this.road = road;
                    return finished;
                }
            }

            foreach ( var offset in Ground.areas[Constants.Simpleton.flagConnectionRange] )
            {
                var nearbyNode = flag.node + offset;
                if ( 
                    nearbyNode.flag && 
                    nearbyNode.flag.team == boss.team && 
                    nearbyNode.flag.id < flag.id && 
                    !nearbyNode.flag.blueprintOnly && 
                    !flag.simpletonDataSafe.failedConnections.Contains( nearbyNode.flag ) )
                        boss.tasks.Add( new ConnectionTask( boss, flag, nearbyNode.flag ) );
            }
            return finished;
        }

        public override void ApplySolution()
        {
            switch ( action )
            {
                case Action.connect:
                {
                    if ( path == null || path.path == null || path.path.Count < 2 || flag == null ) // TODO path.Count was 0
                        return;
                    boss.Log( $"Connecting {flag.name} to the road network at {path.path.Last().name}" );
                    HiveCommon.oh.ScheduleCreateRoad( path.path, boss.team, true, Operation.Source.computer );
                    boss.blockedNodes.Clear();
                    break;
                }
                case Action.removeBlocked:
                {
                    foreach ( var building in flag.Buildings() )
                        boss.blockedNodes.Add( building.node );
                    boss.Log( $"Removing blocked flag at {flag.node.x}:{flag.node.y}" );
                    HiveCommon.oh.ScheduleRemoveFlag( flag, true, Operation.Source.computer );
                    break;
                }
                case Action.remove:
                {
                    boss.Log( $"Removing flag at {flag.node.x}:{flag.node.y}" );
                    HiveCommon.oh.ScheduleRemoveFlag( flag, true, Operation.Source.computer );
                    break;
                }
                case Action.capture:
                {
                    boss.Log( $"Capturing roads around {flag}" );
                    HiveCommon.oh.ScheduleCaptureRoad( flag, true, Operation.Source.computer );
                    break;
                }
                case Action.removeRoad:
                {
                    boss.Log( $"Removing road {road}" );
                    HiveCommon.oh.ScheduleRemoveRoad( road, true, Operation.Source.computer );
                    if ( road.ends[0] != road.ends[1] )
                    {
                        road.ends[0].simpletonDataSafe.failedConnections.Add( road.ends[1] );
                        road.ends[1].simpletonDataSafe.failedConnections.Add( road.ends[0] );
                    }
                    break;
                }
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
            int onRoadLength = 0;
            if ( !path.FindPathBetween( flagA.node, flagB.node, PathFinder.Mode.onRoad ) )
                problemWeight = Constants.Simpleton.isolatedFlagWeight;
            else
            {
                foreach ( var road in path.roadPath )
                    onRoadLength += road.nodes.Count - 1;
                problemWeight = Constants.Simpleton.badConnectionWeight - (float)flagA.node.DistanceFrom( flagB.node ) / onRoadLength;
            }
            if ( problemWeight < 0.5 )
                return finished;

            if ( !path.FindPathBetween( flagA.node, flagB.node, PathFinder.Mode.forRoads, true, tryToAvoidValuableNodes:true ) )
                return finished;

            solutionEfficiency = 1 - (float)(path.path.Count - 1) / onRoadLength;
            return finished;
        }

        public override void ApplySolution()
        {
            boss.Log( $"Creating new road between {path.path.First().name} and {path.path.Last().name}" );
            HiveCommon.oh.ScheduleCreateRoad( path.path, boss.team, true, Operation.Source.computer );
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
            boss.Log( $"Spliting road at {road.nodes[best].name}" );
            HiveCommon.oh.ScheduleCreateFlag( road.nodes[best], boss.team, false, true, Operation.Source.computer );
        }
    }

    public class ExtendBorderTask : Task
    {
        public Node best;
        public int flagDirection;

        public ExtendBorderTask( Simpleton boss ) : base( boss ) {}
        public override bool Analyze()
        {
            if ( boss.team.Stockpile( Item.Type.plank ) + boss.expectedPlank < GuardHouse.guardHouseConfiguration.plankNeeded + boss.reservedPlank )
                return finished;
            if ( boss.team.Stockpile( Item.Type.stone ) < GuardHouse.guardHouseConfiguration.stoneNeeded + boss.reservedStone )
                return finished;
            if ( boss.team.guardHouses.Count * Constants.Simpleton.guardHouseWorkshopRatio > boss.team.workshops.Count && !boss.noRoom )
                return finished;

            problemWeight = boss.noRoom ? 1 : Constants.Simpleton.extensionImportance;
            
            foreach ( var node in HiveCommon.ground.nodes )
            {
                if ( boss.blockedNodes.Contains( node ) )
                    continue;

                if ( node.team != boss.team )
                    continue;
                bool isNodeAtBorder = false;
                foreach ( var offset in Ground.areas[1] )
                {
                    if ( offset && node.Add( offset ).team != boss.team )
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
            boss.Log( "No room for a new guardhouse" );
            return finished;
        }

        public override void ApplySolution()
        {
            boss.Log( $"Building guard house at {best.name}" );
            HiveCommon.oh.ScheduleCreateBuilding( best, flagDirection, Building.Type.guardHouse, boss.team, true, Operation.Source.computer );
        }
    }

    public class MaintenanceTask : Task
    {
        public Workshop workshop;
        public enum Action
        {
            remove,
            disableFish,
            cleanup,
            linkToPartner
        }
        public Action action;
        public Building partner;
        public Item.Type itemTypeToLink;
        public List<Road> cleanupRoads = new List<Road>();
        public List<Flag> cleanupFlags = new List<Flag>();
        public MaintenanceTask( Simpleton boss, Workshop workshop ) : base( boss )
        {
            this.workshop = workshop;
        }

        public override bool Analyze()
        {
            var data = workshop.simpletonDataSafe;

            if ( workshop.type == Workshop.Type.woodcutter || workshop.type == Workshop.Type.stonemason )
            {
                if ( workshop.ResourcesLeft() != 0 )
                    data.lastTimeHadResources.Start( Constants.Simpleton.noResourceTolerance );

                if ( data.lastTimeHadResources.done )
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

            if ( Constants.Simpleton.cleanupPeriod != 0 )
            {
                var relaxState = (float)workshop.relaxSpotCount / workshop.productionConfiguration.relaxSpotCountNeeded;
                if ( relaxState < Constants.Simpleton.relaxTolerance && ( workshop.simpletonDataSafe.lastCleanup.empty || workshop.simpletonDataSafe.lastCleanup.age > Constants.Simpleton.cleanupPeriod ) )
                {
                    problemWeight = 1 - relaxState;
                    foreach ( var offset in Ground.areas[Constants.Workshop.relaxAreaSize] )
                    {
                        Node offsetedNode = workshop.node + offset;
                        if ( offsetedNode.team != workshop.team )
                            continue;
                        if ( offsetedNode.flag && offsetedNode.flag.Buildings().Count == 0 )
                            cleanupFlags.Add( offsetedNode.flag );
                        if ( offsetedNode.road && !cleanupRoads.Contains( offsetedNode.road ) )
                            cleanupRoads.Add( offsetedNode.road );
                    }
                    solutionEfficiency = 0.1f * ( cleanupRoads.Count + cleanupFlags.Count );
                    action = Action.cleanup;
                    return finished;
                }
            }

            if ( data.possiblePartner == null && workshop.simpletonDataSafe.lastDealCheck.ageinf > Constants.Simpleton.dealCheckPeriod )
            {
                workshop.simpletonDataSafe.lastDealCheck.Start();
                
                foreach ( var buffer in workshop.buffers )
                {
                    if ( workshop.type == Workshop.Type.stoneMine && buffer.itemType == Item.Type.fish )
                        continue;
                        
                    foreach ( var offset in Ground.areas[Constants.Simpleton.workshopCoverage] )
                    {
                        var building = workshop.node.Add( offset ).building;
                        if ( building == null || building.team != boss.team || !building.construction.done )
                            continue;
                        if ( building is Workshop partner )
                        {
                            if ( partner.productionConfiguration.outputType != buffer.itemType )
                                continue;

                            if ( ConsiderPartner( building, buffer.itemType ) )
                                break;
                        }
                    }

                    if ( data.possiblePartner )
                        break;

                    Stock stock = GetStock( workshop, buffer.itemType );
                    if ( stock == null )
                        continue;

                    if ( ConsiderPartner( stock, buffer.itemType ) )
                        break;
                }

                if ( !data.hasOutputStock && workshop.productionConfiguration.outputType >= 0 && workshop.productionConfiguration.outputType < Item.Type.total )
                {
                    Stock stock = GetStock( workshop, workshop.productionConfiguration.outputType );
                    if ( stock )
                    {
                        data.possiblePartner = stock;
                        data.possiblePartnerItemType = workshop.productionConfiguration.outputType;
                    }
                }
            }

            if ( data.possiblePartner )
            {
                action = Action.linkToPartner;
                problemWeight = solutionEfficiency = 0.5f;
                this.itemTypeToLink = data.possiblePartnerItemType;
                this.partner = data.possiblePartner;
            }
            return finished;
        }

        bool ConsiderPartner( Building partner, Item.Type itemType )
        {
            foreach ( var deal in workshop.simpletonDataSafe.deals )
            {
                if ( deal.partner == partner && itemType == deal.itemType )
                    return false;
            }

            workshop.simpletonDataSafe.possiblePartner = partner;
            workshop.simpletonDataSafe.possiblePartnerItemType = itemType;
            return true;
        }

        Stock GetStock( Workshop workshop, Item.Type itemType )
        {
            Stock best = null;
            float bestScore = 0;
            bool constructionInProgress = false;
            foreach ( var stock in boss.team.stocks )
            {
                float score = 1f / stock.node.DistanceFrom( workshop.node );
                if ( stock.node.DistanceFrom( workshop.node ) > Constants.Simpleton.stockCoverage )
                    continue;
                if ( stock.simpletonDataSafe.managedItemTypes.Contains( itemType ) )
                    score *= 5;
                else
                    if ( stock.simpletonDataSafe.managedItemTypes.Count >= ( stock.main ? Constants.Simpleton.itemTypesPerMainStock : Constants.Simpleton.itemTypesPerStock ) )
                        continue;
                if ( score > bestScore )
                {
                    if ( stock.construction.done )
                    {
                        bestScore = score;
                        best = stock;
                    }
                    else
                        constructionInProgress = true;
                }
            }
            if ( best == null && !constructionInProgress )
                boss.tasks.Add( new BuildStockTask( boss, workshop.node ) );
            return best;
        }

        public int DealCount( Workshop workshop, Item.Type itemType )
        {
            int count = 0;
            foreach ( var deal in workshop.simpletonDataSafe.deals )
            {
                if ( deal.itemType == itemType )
                    count++;
            }
            return count;
        }

        public override void ApplySolution()
        {
            if ( workshop == null )
                return;

            switch ( action )
            {
                case Action.remove:
                boss.Log( $"Removing {workshop.name}" );
                if ( workshop.flag.roadsStartingHereCount == 1 )
                    HiveCommon.oh.ScheduleRemoveFlag( workshop.flag, true, Operation.Source.computer );
                else
                    HiveObject.oh.ScheduleRemoveBuilding( workshop, true, Operation.Source.computer );
                break;

                case Action.disableFish:
                boss.Log( $"Disabling fish input for {workshop.name}" );
                foreach ( var input in workshop.buffers )
                {
                    if ( input.itemType == Item.Type.fish )
                        HiveCommon.oh.ScheduleChangeBufferUsage( workshop, input, false, true, Operation.Source.computer );
                }
                break;

                case Action.cleanup:
                boss.Log( $"Cleaning up around {workshop.name}" );
                workshop.simpletonDataSafe.lastCleanup.Start();
                HiveCommon.oh.StartGroup( $"Cleaning up roads and junctions in the area" );
                foreach ( var road in cleanupRoads )
                    HiveCommon.oh.ScheduleRemoveRoad( road, false, Operation.Source.computer );
                foreach ( var flag in cleanupFlags )
                    HiveCommon.oh.ScheduleRemoveFlag( flag, false, Operation.Source.computer );
                break;

                case Action.linkToPartner:
                boss.Log( $"Linking {itemTypeToLink} at {workshop} to {partner}" );
                Assert.global.IsTrue( itemTypeToLink >= 0 && itemTypeToLink < Item.Type.total );
                HiveCommon.oh.StartGroup( $"Linkink {workshop.moniker} to partner" );
                workshop.simpletonDataSafe.RegisterPartner( partner, itemTypeToLink );
                workshop.simpletonDataSafe.possiblePartner = null;
                if ( partner is Stock stock )
                {
                    stock.simpletonDataSafe.RegisterManagedItemType( itemTypeToLink );
                    if ( workshop.productionConfiguration.outputType == itemTypeToLink )
                    {
                        workshop.simpletonDataSafe.hasOutputStock = true;
                        HiveCommon.oh.ScheduleStockAdjustment( stock, itemTypeToLink, Stock.Channel.cartOutput, Constants.Stock.cartCapacity, false, Operation.Source.computer );
                        HiveCommon.oh.ScheduleStockAdjustment( stock, itemTypeToLink, Stock.Channel.inputMax, Constants.Stock.cartCapacity + 5, false, Operation.Source.computer );
                    }
                    else
                    {
                        HiveCommon.oh.ScheduleStockAdjustment( stock, itemTypeToLink, Stock.Channel.cartInput, Constants.Simpleton.cartMin, false, Operation.Source.computer );
                        HiveCommon.oh.ScheduleStockAdjustment( stock, itemTypeToLink, Stock.Channel.inputMax, Constants.Simpleton.stockSave, false, Operation.Source.computer );
                    }
                }
                if ( partner is Workshop )
                    partner.simpletonDataSafe.RegisterPartner( workshop, itemTypeToLink );
                break;
            }
        }
    }

    public class BuildStockTask : Task
    {
        public Node center;
        public Node site;
        public int flagDirection;

        public BuildStockTask( Simpleton boss, Node center ) : base( boss )
        {
            this.center = center;
            problemWeight = 0.5f;
        }

        public override bool Analyze()
        {
            foreach ( var offset in Ground.areas[Constants.Simpleton.stockCoverage] )
            {
                site = center + offset;
                for ( flagDirection = 0; flagDirection < Constants.Node.neighbourCount; flagDirection++ )
                {
                    if ( !Stock.IsNodeSuitable( site, boss.team, flagDirection ) )
                        continue;
                    solutionEfficiency = 1 - ( (float)offset.d / Constants.Simpleton.stockCoverage );
                    return finished;
                }
            }
            return finished;
        }

        public override void ApplySolution()
        {
            boss.Log( $"Building stock at {site.name}" );
            HiveCommon.oh.ScheduleCreateBuilding( site, flagDirection, Building.Type.stock, boss.team, true, Operation.Source.computer );
        }
    }
}
