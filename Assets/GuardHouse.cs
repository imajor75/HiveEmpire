using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GuardHouse : Building
{
	// TODO Guardhouses are sometimes not visible, only after reload
	// Somehow they are offset, but they are linked to the correct block.
	// TODO They are completely built even before the construction
	public List<Worker> soldiers = new List<Worker>();
	public int influence = Constants.GuardHouse.defaultInfluence;
	public bool ready;
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
		if ( construction.done && soldiers.Count == 0 && !blueprintOnly && team.soldierCount > 0 )
		{
			team.soldierCount--;
			Worker soldier = Worker.Create().SetupAsSoldier( this );
			if ( soldier == null )
				return;

			soldiers.Add( soldier );
		}
		if ( !ready && soldiers.Count > 0 && soldiers[0].IsIdle( true ) )
		{
			ready = true;
			team.RegisterInfluence( this );
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
