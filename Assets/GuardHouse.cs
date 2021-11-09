using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GuardHouse : Building
{
	// TODO Guardhouses are sometimes not visible, only after reload
	// Somehow they are offset, but they are linked to the correct block.
	// TODO They are completely built even before the construction
	public List<Worker> soldiers = new List<Worker>();
	public int influence = Constants.GuardHouse.defaultInfluence;
	public bool ready;
	public int optimalSoldierCount;
	public static GameObject template;
	static readonly Configuration guardHouseConfiguration = new Configuration();
	bool removing;

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
		if ( !ready && soldiers.Count > 0 && soldiers[0].IsIdle( true ) )
		{
			ready = true;
			team.RegisterInfluence( this );
		}
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
			var newSoldier = Worker.Create().SetupAsSoldier( this );
			var a = Math.PI * soldiers.Count * 2 / 3;
			newSoldier.standingOffset.Set( (float)Math.Sin( a ) * 0.15f, 0.375f, (float)Math.Cos( a ) * 0.15f );
			soldiers.Add( newSoldier );
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
}
