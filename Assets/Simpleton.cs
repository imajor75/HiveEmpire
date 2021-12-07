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
            if ( best != null )
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
        public float problemWeight, solutionEfficiency;
        public float importance
        {
            get
            {
                return solutionEfficiency * problemWeight;
            }
        }
        public Simpleton boss;
    }

    public class GlobalTask : Task
    {
        public GlobalTask( Simpleton boss ) : base( boss ) {}
        public override bool Analyze()
        {
            float soldierYield = 0;
            var workshops = Resources.FindObjectsOfTypeAll<Workshop>(); // TODO Keep an array of the current buildings instead of always collecting then using the Resources class
            foreach ( var workshop in workshops )
            {
                if ( workshop.type == Workshop.Type.barrack && workshop.team == boss.team )
                    soldierYield += workshop.maxOutput;

            }
            boss.tasks.Add( new YieldTask( boss, Workshop.Type.woodcutter, Math.Max( soldierYield * 2, 1 ) ) );
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
        public int bestScore;

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
                currentYield = 0;
                foreach ( var workshop in workshops )
                {
                    if ( workshop.type == workshopType && workshop.team == boss.team )
                        currentYield += workshop.maxOutput;
                }

                if ( currentYield >= target )
                    problemWeight = 0;
                else
                    problemWeight = 1 - ( (float)currentYield / target );

                nodeRow = -1;
                return problemWeight > 0 ? needMoreTime : finished;
            }

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
                foreach ( var offset in Ground.areas[6] )
                {
                    var nearby = node + offset;
                    bool isThisGood = workshopType switch
                    {
                        Workshop.Type.woodcutter => nearby.HasResource( Resource.Type.tree ),
                        _ => true
                    };
                    if ( isThisGood && Workshop.IsNodeGoodForRelax( nearby ) )
                        resources++;
                }

                if ( resources > bestScore )
                {
                    bestScore = resources - node.DistanceFrom( boss.team.mainBuilding.node );
                    bestLocation = node;
                    bestFlagDirection = workingFlagDirection;
                    solutionEfficiency = (float)resources / Ground.areas[6].Count;
                }
            }
        }

        public override void ApplySolution()
        {
            HiveCommon.oh.ScheduleCreateBuilding( bestLocation, bestFlagDirection, (Building.Type)workshopType );
        }
    }
}
