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
    public Game.Timer inability = new ();
    public float confidence = Constants.Simpleton.defaultConfidence;
    public List<Node> blockedNodes = new ();
    public List<Item.Type> lackingProductions = new ();
    public List<float> itemWeights;
    public int reservedPlank, reservedStone;
    public int expectedLog, expectedPlank;
    public bool emergencyPlank;
    public bool active;
	public bool showActions;
    public bool peaceful;
    public Workshop.Type debugPlacement = Workshop.Type.unknown;
    public bool noRoom;
    public bool dumpTasks, dumpYields;
    public List<ItemUsage> nonConstructionUsage;    // This could simply be a Tuple, but serialize doesn't work with that
    public bool preservingConstructionMaterial = false;
    public bool prepared => oh.challenge.preparation switch 
    {
        Game.Challenge.Preparation.construction => !emergencyPlank,
        Game.Challenge.Preparation.none or _ => true
    };

   	[Obsolete( "Compatibility with old files", true )]
    List<Node> isolatedNodes { set { blockedNodes = value; } }
   	[Obsolete( "Compatibility with old files", true )]
	bool insideCriticalSection { set {} }
   	[Obsolete( "Compatibility with old files", true )]
    bool hasSawmill { set {} }
   	[Obsolete( "Compatibility with old files", true )]
    bool hasWoodcutter { set {} }


    [Serializable]
    public struct ItemUsage
    {
        public Workshop.Type workshopType;
        public Item.Type itemType;
    }


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
            game.defeatedSimpletonCount++;
        base.Defeat();
    }

    public override void GameLogicUpdate()
    {
        Advance();
    }

    public enum AdvanceResult
    {
        needMoreCalls,
        done,
        confused

    }

    public AdvanceResult Advance()
    {
        if ( nonConstructionUsage == null && game.workshopConfigurations != null )
        {
            nonConstructionUsage = new ();
            foreach ( var workshopType in game.workshopConfigurations )
            {
                if ( workshopType.generatedInputs == null )
                    continue;
                foreach ( var input in workshopType.generatedInputs )
                {
                    if ( input != Item.Type.plank && input != Item.Type.stone && input != Item.Type.log )
                        continue;
                    if ( workshopType.type == Workshop.Type.sawmill )
                        continue;
                    nonConstructionUsage.Add( new ItemUsage { workshopType = workshopType.type, itemType = input } );
                }
            }
        }
        if ( team.mainBuilding == null )
            return AdvanceResult.needMoreCalls;

        if ( itemWeights == null )
        {
            itemWeights = new ();
            for ( int i = 0; i < (int)Item.Type.total; i++ )
                itemWeights.Add( 1 );
            void AddItemWeight( Item.Type itemType, float weight )
            {
                itemWeights[(int)itemType] *= weight;
                if ( weight < 1.05f && weight > 0.95f )
                    return;
                foreach ( var source in world.workshopConfigurations )
                {
                    if ( source.outputType != itemType || source.generatedInputs == null )
                        continue;
                    foreach ( var input in source.generatedInputs )
                        AddItemWeight( input, (float)Math.Pow( weight, 0.7f ) );
                }
            }
            AddItemWeight( Item.Type.stone, 2 );
        }

        if ( tasks == null && active )
        {
            tasks = new ();
            tasks.Add( new GlobalTask( this ) );
            currentProblem = 0;
            return AdvanceResult.needMoreCalls;
        }
        if ( tasks == null )
            return AdvanceResult.confused;
        if ( currentProblem < tasks.Count )
        {
            tasks[currentProblem].boss = this;
            var current = tasks[currentProblem];
            if ( tasks[currentProblem].Analyze() == Task.finished )
                currentProblem++;
            return AdvanceResult.needMoreCalls;
        }
        else
        {
            if ( dumpTasks )
            {
                tasks.Sort( ( a, b ) => b.importance.CompareTo( a.importance ) );
                Log( "==================" );
                Log( $"{name} tasks:" );
                for ( int i = 0; i < tasks.Count; i++ )
                {
                    var task = tasks[i];
                    Log( $"{i}. {task.importance:F2} ({task.problemWeight:F2}, {task.solutionEfficiency:F2}) {task}" );
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
            if ( best != null && best.importance >= confidence && best.importance > 0 )
            {
                Log( $"Applying solution {best.ToString()} (problem: {best.problemWeight}, solution: {best.solutionEfficiency})" );
                best.ApplySolution();
                inability.Start( Constants.Simpleton.inabilityTolerance );
                confidence = Constants.Simpleton.defaultConfidence;
            }
            if ( inability.done )
            {
                if ( confidence > Constants.Simpleton.minimumConfidence )
                {
                    confidence -= Constants.Simpleton.confidenceLevel;
                    Log( $"No good solution (best: {best.importance}), reducing confidence to {confidence}" );
                    inability.Start( Constants.Simpleton.inabilityTolerance );
                }
                else
                    Log( $"Confidence is already at minimum ({confidence}), don't know what to do" );
            }

            tasks = null;
            return AdvanceResult.done;
        }
    }

    public bool DoSomething()
    {
        AdvanceResult status = AdvanceResult.needMoreCalls;
        while ( status == AdvanceResult.needMoreCalls )
        {
            status = Advance();
            team.FinishConstructions();
        }

        return status == AdvanceResult.done;
    }

    [Serializable]
    public class Data
    {
        public bool isolated;
        public float price = 1;
        public Game.Timer lastCleanup = new ();
        public List<Deal> deals = new ();
        public List<Item.Type> managedItemTypes = new ();
        public Game.Timer lastDealCheck = new ();
        public Game.Timer lastTimeHadResources = new ();
        public HiveObject hiveObject;
        public Building possiblePartner;
        public Item.Type possiblePartnerItemType;
        public bool hasOutputStock;
        public List<Flag> failedConnections = new ();
        public YieldTask.Fit latestTestResult;
        public bool countedTree;

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
        public Simpleton boss;
    }

    public class GlobalTask : Task
    {
        public enum Action
        {   
            nothing,
            toggleEmergency,
            disableNonConstruction,
            enableNonConstruction
        }
        public Action action;
        public bool nodePricesCalculated;

        public GlobalTask( Simpleton boss ) : base( boss ) {}
        public override bool Analyze()
        {
            if ( !nodePricesCalculated )
            {
                nodePricesCalculated = true;
                foreach ( var node in ground.nodes )
                    node.simpletonDataSafe.price = node.HasResource( Resource.Type.rock ) ? Constants.Simpleton.nodeWithRockPrice : 1;
                foreach ( var workshop in boss.team.workshops )
                {
                    if ( workshop.type == Workshop.Type.wheatFarm || workshop.type == Workshop.Type.cornFarm )
                    {
                        foreach ( var offset in Ground.areas[workshop.productionConfiguration.gatheringRange] )
                        {
                            var data = workshop.node.Add( offset ).simpletonDataSafe;
                            if ( data.price < Constants.Simpleton.nodeAtFarmPrice )
                                data.price = Constants.Simpleton.nodeAtFarmPrice;
                        }
                    }
                    if ( workshop.type == Workshop.Type.forester )
                    {
                        foreach ( var offset in Ground.areas[workshop.productionConfiguration.gatheringRange] )
                        {
                            var nearby = workshop.node + offset;
                            if ( nearby.type != Node.Type.forest )
                                continue;
                            if ( nearby.simpletonDataSafe.price < Constants.Simpleton.nodeAtForesterPrice )
                                nearby.simpletonDataSafe.price = Constants.Simpleton.nodeAtForesterPrice;
                        }
                    }
                }
                return needMoreTime;
            }

            float soldierYield = 0;
            foreach ( var workshop in boss.team.workshops )
            {
                if ( workshop.type == Workshop.Type.barrack )
                    soldierYield += workshop.maxOutput;
                if ( !workshop.construction.done )
                    continue;
                boss.tasks.Add( new MaintenanceTask( boss, workshop ) );
            }

            boss.reservedPlank = boss.reservedStone = 0;
            void CheckBuilding( Building building )
            {
                if ( !building.construction.done && boss.team.constructionFactors[(int)building.type] == 1 )
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
            boss.expectedLog = boss.team.Stockpile( Item.Type.log );
            boss.expectedPlank = boss.team.Stockpile( Item.Type.plank );
            List<Resource> countedTrees = new ();
            foreach ( var workshop in boss.team.workshops )
            {
                if ( workshop.type == Workshop.Type.woodcutter && workshop.construction.done )
                {
                    bool hasForester = false;
                    int forestNodeCount = 0, treeCount = 0;
                    foreach ( var offset in Ground.areas[workshop.productionConfiguration.gatheringRange] )
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
                            if ( !resource.simpletonDataSafe.countedTree )
                            {
                                treeCount += resource.charges;
                                resource.simpletonDataSafe.countedTree = true;   // TODO Not so nice
                                countedTrees.Add( resource );
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
                tree.simpletonDataSafe.countedTree = false;
            boss.expectedPlank += hasSawmill ? boss.expectedLog : 0;

            foreach ( var stock in boss.team.stocks )
                CheckBuilding( stock );
            foreach ( var workshop in boss.team.workshops )
                CheckBuilding( workshop );
            foreach ( var guardHouse in boss.team.guardHouses )
                CheckBuilding( guardHouse );

            boss.emergencyPlank = boss.expectedPlank < Constants.Simpleton.expectedPlankPanic;

            if ( ( boss.emergencyPlank || boss.reservedPlank > 0 ) && !boss.preservingConstructionMaterial )
            {
                action = Action.disableNonConstruction;
                problemWeight = solutionEfficiency = 1;
            }

            if ( !boss.emergencyPlank && boss.reservedPlank == 0 && boss.preservingConstructionMaterial )
            {
                action = Action.enableNonConstruction;
                problemWeight = solutionEfficiency = 1;
            }
            
            if ( boss.emergencyPlank && boss.team.constructionFactors[(int)Building.Type.stock] != 0 )
            {
                action = Action.toggleEmergency;
                problemWeight = solutionEfficiency = 1;
            }
            if ( !boss.emergencyPlank && boss.team.constructionFactors[(int)Building.Type.stock] == 0 )
            {
                action = Action.toggleEmergency;
                problemWeight = solutionEfficiency = 1;
            }
            boss.noRoom = false;

            foreach ( var workshopType in game.workshopConfigurations )
            {
                if ( workshopType.type == Workshop.Type._geologistObsolete )
                    continue;

                if ( workshopType.type == Workshop.Type.barrack && boss.lackingProductions.Count != 0 )
                {
                    boss.lackingProductions.Clear();
                    continue;
                }
                float targetMinimum = workshopType.type switch
                {
                    Workshop.Type.barrack => soldierYield + 0.1f,
                    Workshop.Type.woodcutter => 3,
                    Workshop.Type.sawmill => 3,
                    Workshop.Type.forester => 2,
                    Workshop.Type.stoneMine => 1,
                    _ => 0
                };
                var outputType = workshopType.outputType;
                if ( workshopType.type == Workshop.Type.forester )
                    outputType = Item.Type.log;
                boss.tasks.Add( new YieldTask( boss, workshopType.type, Math.Max( soldierYield * game.itemTypeUsage[(int)outputType], targetMinimum ) ) );
            }

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
                foreach ( var enemy in game.teams )
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
                    foreach ( var usage in boss.nonConstructionUsage )
                        HiveCommon.oh.ScheduleInputWeightChange( boss.team, usage.workshopType, usage.itemType, 0 );
                    boss.preservingConstructionMaterial = true;
                    break;
                case Action.enableNonConstruction:
                    foreach ( var usage in boss.nonConstructionUsage )
                        HiveCommon.oh.ScheduleInputWeightChange( boss.team, usage.workshopType, usage.itemType, 0.5f );
                    boss.preservingConstructionMaterial = false;
                    break;
            }
        }

        public override string ToString()
        {
            string d = "GlobalTask: ";
            d += action switch
            {
                Action.toggleEmergency => "toggle emergency",
                Action.disableNonConstruction => "disable base material usage for economy",
                Action.enableNonConstruction => "enable base material usage for economy",
                _ => "nothing to do"
            };
            return d;
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
        public Fit bestScore;
        public List<Workshop.Type> dependencies = new ();
        public Workshop.Configuration configuration;
        public int reservedPlank, reservedStone;
        public int currentPlank, currentStone;
        public bool inspected => boss.debugPlacement == configuration.type;

        public enum State
        {
            inProgress,
            surplus,
            noProblem,
            noPlank,
            noStone,
            noSpace,
            possible
        };

        public State state;

        public YieldTask( Simpleton boss, Workshop.Type workshopType, float target ) : base( boss ) 
        {
            this.workshopType = workshopType;
            this.target = target;
            if ( workshopType == Workshop.Type.barrack )
                priority = 0.5f;
        }
        public override string ToString() => $"Yieldcheck for {workshopType} - target:{target}, current: {currentYield}, result: {state} (at {bestLocation})";
        public override bool Analyze()
        {
            if ( currentYield < 0 )
            {
                int currentWorkshopCount = 0;
                var outputType = Workshop.GetConfiguration( game, workshopType ).outputType;
                currentYield = 0;
                int stonemasonCount = 0;
                if ( workshopType == Workshop.Type.forester )
                {
                    foreach ( var workshop in boss.team.workshops )
                    {
                        if ( workshop.type == Workshop.Type.forester )
                        {
                            currentYield += workshop.productionConfiguration.productivity;
                            currentWorkshopCount++;
                        }
                    }
                }
                else
                {
                    foreach ( var workshop in boss.team.workshops )
                    {
                        if ( workshop.type == Workshop.Type.stonemason )
                            stonemasonCount++;
                        if ( workshop.productionConfiguration.outputType == outputType )
                        {
                            if ( workshop.output > 0 )
                            {
                                surplus = true;
                                state = State.surplus;
                                return finished;
                            }
                            currentYield += workshop.CalculateProductivity( true, Constants.Simpleton.maximumProductionCalculatingPeriod );
                        }
                        if ( workshop.type == workshopType )
                            currentWorkshopCount++;
                    }
                }

                if ( currentYield >= target )
                    problemWeight = 0;
                else
                    problemWeight = 0.75f - 0.5f * ( (float)currentYield / target );
                if ( workshopType == Workshop.Type.stonemason )
                    problemWeight = Math.Max( problemWeight, stonemasonCount switch { 0 => 1, 1 => 0.3f, _ => 0.1f } );
                if ( (int)outputType < boss.itemWeights.Count && (int)outputType >= 0 )
                    problemWeight = (float)Math.Pow( problemWeight, 1 / boss.itemWeights[(int)outputType] );

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
                if ( HiveCommon.game.challenge.buildingMax != null && currentWorkshopCount >= HiveCommon.game.challenge.buildingMax[(int)workshopType] )
                    return finished;

                nodeRow = -1;
                if ( problemWeight > 0 )
                {
                    GatherDependencies();
                    if ( outputType != Item.Type.unknown && outputType != Item.Type.soldier && outputType != Item.Type.stone )        // TODO Hacky special case for stone
                        boss.lackingProductions.Add( outputType );
                }
                if ( problemWeight == 0 )
                    state = State.noProblem;
                return problemWeight > 0 ? needMoreTime : finished;
            }

            configuration = Workshop.GetConfiguration( game, workshopType );
            reservedPlank = boss.reservedPlank;
            reservedStone = boss.reservedStone;
            if ( workshopType != Workshop.Type.woodcutter && workshopType != Workshop.Type.sawmill && workshopType != Workshop.Type.forester )
                reservedPlank += 4;

            currentPlank = boss.expectedPlank;
            currentStone = boss.team.Stockpile( Item.Type.stone );

            if ( configuration.plankNeeded + reservedPlank > currentPlank || ( boss.emergencyPlank && workshopType != Workshop.Type.woodcutter && workshopType != Workshop.Type.sawmill && workshopType != Workshop.Type.stonemason && workshopType != Workshop.Type.forester ) )
            {
                state = State.noPlank;
                return finished;
            }
            if ( configuration.stoneNeeded > 0 && configuration.stoneNeeded + reservedStone > currentStone )
            {
                state = State.noStone;
                return finished;
            }

            ScanRow( nodeRow++ );

            if ( nodeRow == HiveCommon.ground.dimension )
            {
                if ( bestLocation == null )
                {
                    boss.Log( $"A new {workshopType} would be good, but there is no room" );
                    boss.noRoom = true;
                    state = State.noSpace;
                }
                state = State.possible;
                return finished;
            }

            return needMoreTime;
        }

        void GatherDependencies()
        {
            if ( workshopType == Workshop.Type.woodcutter )
                dependencies.Add( Workshop.Type.forester );
            if ( workshopType == Workshop.Type.dungCollector )
            {
                dependencies.Add( Workshop.Type.butcher );
                dependencies.Add( Workshop.Type.dairy );
                dependencies.Add( Workshop.Type.poultryRun );
            }
            var configuration = Workshop.GetConfiguration( game, workshopType );
            foreach ( var otherWorkshopType in game.workshopConfigurations )
            {
                if ( configuration.generatedInputs == null )
                    continue;
                if ( configuration.generatedInputs.Contains( otherWorkshopType.outputType ) )
                    dependencies.Add( otherWorkshopType.type );
            }
        }

        void ScanRow( int row )
        {
            for ( int x = 0; x < HiveCommon.ground.dimension; x++ )
            {
                var node = HiveCommon.ground.GetNode( x, nodeRow );
                int workingFlagDirection = -1;
                float price = 1;
                Node site = null;
                for ( int flagDirection = 0; flagDirection < Constants.Node.neighbourCount; flagDirection++ )
                {
                    int o = ( flagDirection + ( Constants.Node.neighbourCount / 2 ) ) % Constants.Node.neighbourCount;
                    site = node.Neighbour( o );
                    if ( boss.blockedNodes.Contains( site ) )
                        continue;
                    price = 1;
                    if ( Workshop.IsNodeSuitable( site, boss.team, configuration, flagDirection, nodeAction:( node ) => price *= node.simpletonDataSafe.price ) )
                    {
                        workingFlagDirection = flagDirection;
                        break;
                    }
                }

                if ( workingFlagDirection < 0 )
                {
                    if ( inspected )
                        node.simpletonDataSafe.latestTestResult.notSuitable = true;
                    continue;
                }

                var score = CalculateAvailaibily( site );
                score.factor = (float)( 1 / Math.Pow( price, 2 ) );
                if ( node.validFlag )
                    score.factor *= Constants.Simpleton.alreadyHasFlagBonus;
                if ( inspected )
                    node.simpletonDataSafe.latestTestResult = score;
                if ( score.sum > bestScore.sum )
                {
                    bestScore = score;
                    bestLocation = site;
                    bestFlagDirection = workingFlagDirection;
                    solutionEfficiency = score.sum;
                }
            }
        }

        [Serializable]
        public struct Fit
        {
            public float resource, relax, dependency, factor;
            public float resourceCount;
            public float resourceCoverage;
            public float sum => ( resource + relax + dependency ) * factor;
            public bool notSuitable;
        }

        Fit CalculateAvailaibily( Node node )
        {
            Fit result = new ();
            result.factor = 1;
            float resources = 0;
            foreach ( var offset in Ground.areas[configuration.gatheringRange] )
            {
                var nearby = node + offset;
                bool isThisGood = workshopType switch
                {
                    Workshop.Type.woodcutter => nearby.HasResource( Resource.Type.tree ),
                    Workshop.Type.stonemason => nearby.HasResource( Resource.Type.rock ),
                    Workshop.Type.coalMine => nearby.HasResource( Resource.Type.coal, true ),
                    Workshop.Type.ironMine => nearby.HasResource( Resource.Type.iron, true ),
                    Workshop.Type.copperMine => nearby.HasResource( Resource.Type.copper, true ),
                    Workshop.Type.stoneMine => nearby.HasResource( Resource.Type.stone, true ),
                    Workshop.Type.saltMine => nearby.HasResource( Resource.Type.salt, true ),
                    Workshop.Type.goldMine => nearby.HasResource( Resource.Type.gold, true ),
                    Workshop.Type.silverMine => nearby.HasResource( Resource.Type.silver, true ),
                    Workshop.Type.wheatFarm => nearby.type == Node.Type.grass,
                    Workshop.Type.forester => nearby.type == Node.Type.forest,
                    Workshop.Type.fishingHut => nearby.HasResource( Resource.Type.fish ),
                    Workshop.Type.hunter => nearby.HasResource( Resource.Type.animalSpawner ),
                    Workshop.Type.dungCollector => nearby.HasResource( Resource.Type.dung ),
                    _ => true
                };
                if ( isThisGood )
                    resources++;
                if ( workshopType == Workshop.Type.woodcutter && nearby.type == Node.Type.forest && !boss.emergencyPlank && !nearby.block )
                    resources += 0.5f;
            }
            float idealResourceCoverage = workshopType switch
            {
                Workshop.Type.stonemason => 0.05f,
                Workshop.Type.dungCollector => 0.05f,
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
            float minimumResourceCoverage = workshopType switch
            {
                Workshop.Type.forester => 0.2f,
                _ => 0
            };
            if ( resources == 0 )
                return result;

            result.resourceCount = resources;
            result.resourceCoverage = resources / Ground.areas[configuration.gatheringRange].Count;
            float relativeResourceCoverage = ( result.resourceCoverage - minimumResourceCoverage ) / ( idealResourceCoverage - minimumResourceCoverage );
            if ( relativeResourceCoverage > 1 )
                result.resourceCoverage = 1;
            if ( relativeResourceCoverage < 0 )
                return result;
            result.resource = relativeResourceCoverage * Constants.Simpleton.resourceWeight;

            int relaxSpotCount = 0;
            foreach ( var relaxOffset in Ground.areas[Constants.Workshop.relaxAreaSize] )
            {
                if ( Workshop.IsNodeGoodForRelax( node + relaxOffset ) )
                    relaxSpotCount++;
            }
            float relaxAvailability = (float)relaxSpotCount / configuration.relaxSpotCountNeeded;
            if ( relaxAvailability > 1 )
                relaxAvailability = 1;
            result.relax = relaxAvailability * Constants.Simpleton.relaxImportance;

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
                if ( sourceAvailability > 1 )
                    sourceAvailability = 1;
            }

            float redundancyProblem = workshopType switch
            {
                Workshop.Type.stonemason => 1,
                Workshop.Type.woodcutter => 0.5f,
                Workshop.Type.forester => 0.5f,
                _ => 0
            };
            if ( configuration.gatheredResource != Resource.Type.unknown && redundancyProblem != 0 )
            {
                float redundancy = 0;
                foreach ( var offset in Ground.areas[configuration.gatheringRange] )
                {
                    var offsetedNode = node + offset;
                    if ( offsetedNode.building && offsetedNode.building.type == (Building.Type)workshopType )
                        redundancy += configuration.gatheringRange - offsetedNode.DistanceFrom( node );
                }
                float problem = Math.Min( 1, redundancy / configuration.gatheringRange * redundancyProblem );
                result.factor *= 1 - problem * Constants.Simpleton.redundancyWeight;
            }
            result.dependency = sourceAvailability * Constants.Simpleton.sourceImportance;
            return result;
        }

        public override void ApplySolution()
        {
            boss.Log( $"Building a {workshopType} at {bestLocation} (score: {bestScore.resource}, {bestScore.relax}, {bestScore.dependency}, {bestScore.resourceCount}, {bestScore.factor})" );
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
        override public string ToString()
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
                    if ( !path.FindPathBetween( flag.node, node, PathFinder.Mode.forRoads, true, appraiser: ( node ) => node.simpletonDataSafe.price ) )
                        continue;

                    float price = 0;
                    foreach ( var point in path.path )
                        price += point.simpletonDataSafe.price;
                    solutionEfficiency = (float)Math.Pow( 1f/price, 0.25f );
                    action = Action.connect;
                    return finished;
                }

                // The following three lines caused an infinite loop of (remove flag)-(split road) actions. It is not clear in what situation are these lines needed
                // problemWeight = Constants.Simpleton.blockedFlagWeight;
                // solutionEfficiency = 1;
                // action = Action.removeBlocked;
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

            List<Flag> connectedFlags = new ();
            List<Road> connections = new ();
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

        public override string ToString() => $"Connecting {flagA} and {flagB}";

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

            if ( !path.FindPathBetween( flagA.node, flagB.node, PathFinder.Mode.forRoads, true, appraiser:( node ) => node.simpletonDataSafe.price ) )
                return finished;

            float price = 0;
            foreach ( var point in path.path )
                price += point.simpletonDataSafe.price;
            solutionEfficiency = 1 - (price - 1) / onRoadLength;
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
        public float bestScore = 0;
        public int bestFlagDirection;

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

                float score = 0;
                foreach ( var offset in Ground.areas[Constants.GuardHouse.defaultInfluence] )
                {
                    var o = node + offset;
                    if ( o.team )
                        continue;
                    score += o.type switch { Node.Type.underWater => 0.5f, Node.Type.hill => 1.25f, _ => 1 };
                }

                if ( score < bestScore )
                    continue;

                for ( int flagDirection = 0; flagDirection < Constants.Node.neighbourCount; flagDirection++ )
                {
                    if ( GuardHouse.IsNodeSuitable( node, boss.team, flagDirection ) )
                    {
                        best = node;
                        bestScore = score;
                        bestFlagDirection = flagDirection;
                        solutionEfficiency = (float)score / Ground.areas[Constants.GuardHouse.defaultInfluence].Count;
                    }
                }
            }
            if ( best == null )
                boss.Log( "No room for a new guardhouse" );
            return finished;
        }

        public override void ApplySolution()
        {
            boss.Log( $"Building guard house at {best.name}" );
            HiveCommon.oh.ScheduleCreateBuilding( best, bestFlagDirection, Building.Type.guardHouse, boss.team, true, Operation.Source.computer );
        }

        public override string ToString() => $"Extend border (best location: {best}, best score: {bestScore}";
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
        public List<Road> cleanupRoads = new ();
        public List<Flag> cleanupFlags = new ();
        public MaintenanceTask( Simpleton boss, Workshop workshop ) : base( boss )
        {
            this.workshop = workshop;
        }

        public override string ToString() => $"Maintenance of {workshop}, action: {action}";

        class RemoveResources : Task
        {
            public List<Resource> resourcesToRemove = new ();
            public RemoveResources( Simpleton boss ) : base( boss ) {}
            override public bool Analyze() => finished;
            override public void ApplySolution()
            {
                foreach ( var resource in resourcesToRemove )
                    oh.ScheduleRemoveResource( resource, true, Operation.Source.computer );
            }
        }

        public override bool Analyze()
        {
            var data = workshop.simpletonDataSafe;

            if ( workshop.type == Workshop.Type.woodcutter || workshop.type == Workshop.Type.stonemason )
            {
                bool useless = false;
                if ( workshop.ResourcesLeft() != 0 || data.lastTimeHadResources.empty )
                    data.lastTimeHadResources.Start( Constants.Simpleton.noResourceTolerance );
                else
                    useless = workshop.tinkerer.IsIdle( true ) && workshop.tinkererMate.IsIdle( true ) && workshop.output == 0;

                if ( data.lastTimeHadResources.done || ( workshop.type == Workshop.Type.stonemason && useless ) )
                {
                    action = Action.remove;
                    problemWeight = solutionEfficiency = 1;
                    return finished;
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
                        return finished;
                    }
                }
            }
            if ( workshop.type == Workshop.Type.wheatFarm || workshop.type == Workshop.Type.cornFarm )
            {
                var resourcesToRemove = new List<Resource>();
                foreach ( var offset in Ground.areas[workshop.productionConfiguration.gatheringRange] )
                {
                    var sideNode = workshop.node.Add( offset );
                    if ( sideNode.type != Node.Type.grass )
                        continue;
                    foreach ( var resource in sideNode.resources )
                    {
                        if ( resource.type == Resource.Type.tree || resource.type == Resource.Type.rock )
                            resourcesToRemove.Add( resource );
                    }
                }
                if ( resourcesToRemove.Count > 0 )
                {
                    var solution = new RemoveResources( boss );
                    solution.problemWeight = (float)Math.Pow( Constants.Simpleton.resourcesAroundFarmProblem, 1 / resourcesToRemove.Count );
                    solution.solutionEfficiency = Constants.Simpleton.solutionToRemoveTreesAroundFarm;
                    foreach ( var resource in resourcesToRemove )
                        if ( resource.type == Resource.Type.rock )
                            solution.solutionEfficiency = Constants.Simpleton.solutionToRemoveRocksAroundFarm;
                    solution.resourcesToRemove = resourcesToRemove;
                    boss.tasks.Add( solution );
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
                int distance = stock.flag.PathDistanceFrom( workshop.flag );
                float score = 1f / distance;
                if ( distance > Constants.Simpleton.stockCoverage * 2 )
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
                        HiveCommon.oh.ScheduleChangeBufferUsage( workshop, input, Workshop.Buffer.Priority.disabled, true, Operation.Source.computer );
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
