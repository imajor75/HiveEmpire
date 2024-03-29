using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

public abstract class Attackable : Building
{
	public List<Unit> attackers = new ();
	public Unit aggressor, assassin, defender;
	public int lastSpot;
	public bool takeoverInProgress;
	public Versioned attackedStatus = new ();

	List<GameObject> trash = new ();
	List<Material> trashMaterials = new ();
	Game.Timer trashTimer = new ();

	public Team attackerTeam
	{
		get
		{
			if ( attackers.Count > 0 )
				return attackers.First().team;
			return aggressor?.team;
		}
	}

	public bool readyForAttacker
	{
		get
		{
			if ( !aggressor )
				return true;

			assert.IsTrue( aggressor.taskQueue.Count > 0 );

			if ( assassin )
				return false;

			var fightAct = aggressor.taskQueue.First() as Unit.DoAct;

			if ( fightAct == null || fightAct.timeSinceStarted.age < Constants.GuardHouse.fightDuration )
				return false;

			return true;
		}
	}
	
	public abstract int defenderCount { get; }
	public abstract Unit GetDefender();

	public int attackerCount
	{
		get
		{
			int c = attackers.Count;
			if ( aggressor )
				c++;
			if ( assassin )
				c++;
			return c;
		}
	}

	public override void GameLogicUpdate( UpdateStage stage )
	{
		base.GameLogicUpdate( stage );
		if ( blueprintOnly || !construction.done )
			return;

		if ( readyForAttacker && attackers.Count > 0 && !takeoverInProgress && attackers.First().IsIdle() )
			ProcessAttacker( attackers.First() );

		if ( trashTimer.inProgress )
		{
			var alpha = -(float)trashTimer.age / Constants.GuardHouse.deathFadeTime;
			if ( alpha < 0 )
				alpha = 0;
			foreach ( var m in trashMaterials )
				m.SetFloat( "_Alpha", alpha );
		}
		if ( trashTimer.done )
		{
			foreach ( var g in trash )
				Eradicate( g );
		}
	}

	void ProcessAttacker( Unit attacker )
	{
		if ( defenderCount == 0 && aggressor == null )
		{
			attacker.ResetTasks();
			attacker.ScheduleWalkToNode( flag.node );
			attacker.ScheduleCall( this );
			takeoverInProgress = true;
			return;
		}
		if ( aggressor )
		{
			assert.AreEqual( attacker.team, aggressor.team );
			if ( assassin )
				return;
			attacker.ResetTasks();
			attacker.ScheduleWalkToNode( flag.node.Neighbour( 0 ), avoid:flag.node );
			attacker.ScheduleWalkToNeighbour( flag.node, false, Unit.stabInTheBackAct );
			assassin = attacker;
			attackers.Remove( assassin );
			return;
		}

		defender = GetDefender();
		defender.ResetTasks();
		defender.ScheduleWalkToNeighbour( flag.node );
		defender.ScheduleWait( attacker );
		defender.ScheduleWalkToNeighbour( flag.node.Neighbour( 0 ), false, Unit.defendingAct );
		attacker.ResetTasks();
		attacker.ScheduleWalkToNode( flag.node );
		attacker.ScheduleWait( defender );
		attacker.ScheduleWalkToNeighbour( flag.node.Neighbour( 3 ), false, Unit.fightingAct );

		aggressor = attacker;
		attackers.Remove( aggressor );
	}

	public void DefenderStabbed( Unit assassin )
	{
		assert.IsNotNull( aggressor );

		void Trash( Unit soldier )
		{
			var m = Instantiate( soldier.team.Get01AMaterial() );
			soldier.body.transform.SetParent( transform, false );
			trash.Add( soldier.body );
			trashMaterials.Add( m );
			World.SetMaterialRecursive( soldier.body, m );
			soldier.animator.speed = 0;
			soldier.Remove();
		}

		Trash( defender );
		Trash( aggressor );
		Trash( assassin );
		defender = this.assassin = aggressor = null;
		trashTimer.Start( Constants.GuardHouse.deathFadeTime );
	}

    public override void Remove()
    {
		RemoveElements( attackers );
		aggressor?.Remove();
		defender?.Remove();
		assassin?.Remove();
        base.Remove();
    }
}

public class GuardHouse : Attackable
{
	// TODO Guardhouses are sometimes not visible, only after reload
	// Somehow they are offset, but they are linked to the correct block.
	// TODO They are completely built even before the construction
	public List<Unit> soldiers = new ();
	public int influence = Constants.GuardHouse.defaultInfluence;
	public bool ready;
	public int optimalSoldierCount;
	public static GameObject template;
	public static readonly Configuration guardHouseConfiguration = new ();
	bool removing;
	public override int defenderCount
	{
		get
		{
			int defenderCount = 0;
			foreach ( var defender in soldiers )
				if ( defender.IsIdle( true, true ) )
					defenderCount++;
			return defenderCount;
		}
	}
	override public UpdateStage updateMode => UpdateStage.turtle;

	public override Unit GetDefender()
	{
		var defender = soldiers[0];
		soldiers.Remove( defender );
		contentChange.Trigger();
		return defender;
	}

    public override void UnitCallback( Unit unit, float floatData, bool boolData )
	{
		assert.IsTrue( attackers.Contains( unit ) );
		foreach ( var soldier in soldiers )
			soldier.building = null;
		soldiers.Clear();
		foreach ( var soldier in attackers )
		{
			soldier.standingOffsetInsideBuilding = GetNextSoldierSpot();
			soldiers.Add( soldier );
			contentChange.Trigger();
		}
		attackers.Clear();
		team.guardHouses.Remove( this );
		SetTeam( soldiers.First().team );
		team.guardHouses.Add( this );
		takeoverInProgress = false;
	}

	public override bool wantFoeClicks { get { return true; } }

	public static new void Initialize()
	{
		template = (GameObject)Resources.Load( "prefabs/buildings/guardhouse" );
		guardHouseConfiguration.plankNeeded = 2;
		guardHouseConfiguration.stoneNeeded = 2;
		guardHouseConfiguration.flatteningNeeded = false;
		guardHouseConfiguration.constructionTime = Constants.GuardHouse.constructionTime;
	}

	public static SiteTestResult IsNodeSuitable( Node placeToBuild, Team owner, int flagDirection, bool ignoreTreesAndRocks = true )
	{
		return Building.IsNodeSuitable( placeToBuild, owner, guardHouseConfiguration, flagDirection, ignoreTreesAndRocks );
	}

	public static GuardHouse Create()
	{
		return new GameObject().AddComponent<GuardHouse>();
	}

	override public string title { get { return "Guard House"; } set {} }

	public GuardHouse Setup( Node node, Team owner, int flagDirection, bool blueprintOnly = false, Resource.BlockHandling block = Resource.BlockHandling.block )
	{
		height = 1.2f;
		optimalSoldierCount = 1;
		if ( base.Setup( node, owner, guardHouseConfiguration, flagDirection, blueprintOnly, block ) == null )
			return null;

		if ( !blueprintOnly )
			owner.guardHouses.Add( this );

		return this;
	}

	override public void Materialize()
	{
		team.guardHouses.Add( this );
		base.Materialize();
	}

	new void Start()
	{
		assert.IsNotNull( node, "GuardHouse without node" );
		if ( node == null )
			return;
		base.Start();
		name = $"Guardhouse {node.x}:{node.y}";

		// Not sure why these are needed, but it happened that the assassin reference was unity null
		if ( assassin == null || assassin.destroyed )
			assassin = null;
		if ( aggressor == null || aggressor.destroyed )
			aggressor = null;
	}

	public override GameObject Template()
	{
		return template;
	}

	public override void GameLogicUpdate( UpdateStage stage )
	{
		base.GameLogicUpdate( stage );
		if ( blueprintOnly || !construction.done )
			return;
		if ( !ready && soldiers.Count > 0 && soldiers.First().node == node )
		{
			ready = true;
			team.RegisterInfluence( this );
		}
		if ( !construction.done )
			return;

		if ( team )
		{
			while ( soldiers.Count > optimalSoldierCount )
			{
				var soldierToRelease = soldiers.Last();
				soldiers.Remove( soldierToRelease );
				soldierToRelease.building = null;
				if ( soldierToRelease.IsIdle() )
					soldierToRelease.ScheduleWalkToNode( flag.node );
			}
			while ( soldiers.Count < optimalSoldierCount && team.soldierCount > 0 )
			{
				var newSoldier = Unit.Create().SetupAsSoldier( this );
				team.soldierCount--;
				newSoldier.standingOffsetInsideBuilding = GetNextSoldierSpot();
				soldiers.Add( newSoldier );
				contentChange.Trigger();
			}
		}
	}

	public int lastDefenderSpot;
	public Vector3 GetNextSoldierSpot()
	{
		var a = Math.PI * lastDefenderSpot++ * 2 / 3.2f;
		return new Vector3( (float)Math.Sin( a ) * 0.15f, 0.375f, (float)Math.Cos( a ) * 0.15f );
	}

	public override void OnClicked( Interface.MouseButton button, bool show = false )
	{
		base.OnClicked( button, show );
		if ( button == Interface.MouseButton.left && construction.done )
			Interface.GuardHousePanel.Create().Open( this, show );
	}

	public override void Remove()
	{
		RemoveElements( soldiers );

		if ( removing )
			return;

		removing = true;
		team.UnregisterInfuence( this );
		team.guardHouses.Remove( this );
		base.Remove();
	}

	public override int Influence( Node node )
	{
		return influence - node.DistanceFrom( this.node );
	}

	public override void Validate( bool chain )
	{
		if ( blueprintOnly )
			return;
		assert.IsNotNull( soldiers, "GuardHouse has no soldiers" );
		if ( soldiers == null )
			return;

		foreach ( var soldier in soldiers )
			assert.AreEqual( team, soldier.team );
		if ( attackers.Count > 0 )
		{
			var enemy = attackers.First().team;
			assert.AreNotEqual( enemy, team, "Attacking own guardhouse" );
			foreach ( var soldier in attackers )
				assert.AreEqual( soldier.team, enemy );
		}
		assert.IsTrue( blueprintOnly || team.guardHouses.Contains( this ) );
		base.Validate( chain );
	}
}
