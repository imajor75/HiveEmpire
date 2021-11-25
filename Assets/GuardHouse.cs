using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

public abstract class Attackable : Building
{
	public List<Unit> attackers = new List<Unit>();
	public Unit aggressor, assassin, defender;
	public int lastSpot;

	List<GameObject> trash = new List<GameObject>();
	List<Material> trashMaterials = new List<Material>();
	World.Timer trashTimer = new World.Timer();

	public Team attackerTeam
	{
		get
		{
			if ( attackers.Count > 0 )
				return attackers.First().team;
			return aggressor?.team;
		}
	}
	
	public abstract int defenderCount { get; }
	public abstract Unit GetDefender();
	public abstract void Occupy( List<Unit> attackers );

	public override void CriticalUpdate()
	{
		base.CriticalUpdate();
		if ( blueprintOnly || !construction.done )
			return;

		foreach ( var attacker in attackers )
		{
			if ( attacker.IsIdle() && attacker.node.DistanceFrom( flag.node ) <= 1 )
			{
				ProcessAttacker( attacker );
				break;
			}
		}

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
				Destroy( g );
		}
	}

	void ProcessAttacker( Unit attacker )
	{
		if ( defenderCount == 0 && aggressor == null )
		{
			Occupy( attackers );
			return;
		}
		if ( aggressor )
		{
			assert.AreEqual( attacker.team, aggressor.team );
			if ( assassin )
				return;
			attacker.ScheduleWalkToNeighbour( flag.node );
			attacker.ScheduleWalkToNeighbour( flag.node.Neighbour( 0 ), false, Unit.stabInTheBackAct );
			assassin = attacker;
			attackers.Remove( assassin );
			return;
		}

		defender = GetDefender();
		defender.ScheduleWalkToNeighbour( flag.node );
		defender.ScheduleWalkToNeighbour( flag.node.Neighbour( 0 ), false, Unit.fightingAct );
		attacker.ScheduleWalkToNeighbour( flag.node );
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
			soldier.body.transform.SetParent( transform );
			trash.Add( soldier.body );
			trashMaterials.Add( m );
			World.SetMaterialRecursive( soldier.body, m );
			soldier.animator.speed = 0;
			soldier.Remove( false );
		}

		Trash( defender );
		Trash( aggressor );
		Trash( assassin );
		trashTimer.Start( Constants.GuardHouse.deathFadeTime );
	}
}
public class GuardHouse : Attackable
{
	// TODO Guardhouses are sometimes not visible, only after reload
	// Somehow they are offset, but they are linked to the correct block.
	// TODO They are completely built even before the construction
	public List<Unit> soldiers = new List<Unit>();
	public int influence = Constants.GuardHouse.defaultInfluence;
	public bool ready;
	public int optimalSoldierCount;
	public static GameObject template;
	static readonly Configuration guardHouseConfiguration = new Configuration();
	bool removing;
	public override int defenderCount
	{
		get
		{
			int defenderCount = 0;
			foreach ( var defender in soldiers )
				if ( defender.IsIdle( true ) )
					defenderCount++;
			return defenderCount;
		}
	}

	public override Unit GetDefender()
	{
		var defender = soldiers[0];
		soldiers.Remove( defender );
		return defender;
	}

	public override void Occupy( List<Unit> attackers )
	{
		foreach ( var soldier in soldiers )
			soldier.building = null;
		soldiers.Clear();
		foreach ( var soldier in attackers )
			soldiers.Add( soldier );
		attackers.Clear();
		assassin = aggressor = null;
		SetTeam( soldiers.First().team );
	}

	public override bool wantFoeClicks { get { return true; } }

	public static new void Initialize()
	{
		template = (GameObject)Resources.Load( "prefabs/buildings/guardhouse" );
		guardHouseConfiguration.plankNeeded = 2;
		guardHouseConfiguration.stoneNeeded = 2;
		guardHouseConfiguration.flatteningNeeded = false;
		guardHouseConfiguration.constructionTime = 5000;
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

		return this;
	}

	new void Start()
	{
		base.Start();
		name = $"Guardhouse {node.x}:{node.y}";
	}

	public override GameObject Template()
	{
		return template;
	}

	public override void CriticalUpdate()
	{
		base.CriticalUpdate();
		if ( blueprintOnly || !construction.done )
			return;
		if ( !ready && soldiers.Count > 0 && soldiers.First().IsIdle( true ) )
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
				var a = Math.PI * soldiers.Count * 2 / 3;
				team.soldierCount--;
				newSoldier.standingOffset.Set( (float)Math.Sin( a ) * 0.15f, 0.375f, (float)Math.Cos( a ) * 0.15f );
				soldiers.Add( newSoldier );
			}
		}
	}


	public override void OnClicked( bool show = false )
	{
		base.OnClicked( show );
		if ( construction.done )
			Interface.GuardHousePanel.Create().Open( this, show );
	}

	public override bool Remove( bool takeYourTime )
	{
		if ( removing )
			return true;

		removing = true;
		team.UnregisterInfuence( this );
		return base.Remove( takeYourTime );
	}

	public override int Influence( Node node )
	{
		return influence - node.DistanceFrom( this.node );
	}

	public override void Validate( bool chain )
	{
		if ( blueprintOnly )
			return;
		foreach ( var soldier in soldiers )
			assert.AreEqual( team, soldier.team );
		if ( attackers.Count > 0 )
		{
			var enemy = attackers.First().team;
			assert.AreNotEqual( enemy, team );
			foreach ( var soldier in attackers )
				assert.AreEqual( soldier.team, enemy );
		}
		base.Validate( chain );
	}
}
