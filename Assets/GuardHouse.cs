using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GuardHouse : Building
{
	public List<Worker> soldiers = new List<Worker>();
	public bool ready;
	public int influence = 8;
	public static GameObject template;
	static Configuration configuration = new Configuration();

	public static new void Initialize()
	{
		template = (GameObject)Resources.Load( "WatchTower/Tower" );
		configuration.plankNeeded = 2;
		configuration.stoneNeeded = 2;
		configuration.flatteningNeeded = false;
	}

	public static bool IsNodeSuitable( GroundNode placeToBuild, Player owner )
	{
		return Building.IsNodeSuitable( placeToBuild, owner, configuration );
	}

	public static GuardHouse Create()
	{
		return new GameObject().AddComponent<GuardHouse>();
	}

	public GuardHouse Setup( GroundNode node, Player owner, bool blueprintOnly = false )
	{
		construction.plankNeeded = 2;
		title = "guardhouse";
		height = 2;
		if ( base.Setup( node, owner, configuration, blueprintOnly ) == null )
			return null;

		return this;
	}

	new void Start()
	{
		Instantiate( template, transform ).layer = World.layerIndexPickable;
		base.Start();
	}

	public new void Update()
	{
		base.Update();
		if ( construction.done && soldiers.Count == 0 && !blueprintOnly )
		{
			Worker soldier = Worker.Create().SetupAsSoldier( this );
			if ( soldier == null )
				return;

			soldiers.Add( soldier );
		}
		if ( !ready && soldiers.Count > 0 && soldiers[0].IsIdle( true ) )
		{
			ready = true;
			owner.RegisterInfluence( this );
		}
	}

	public override bool Remove( bool takeYourTime )
	{
		owner.UnregisterInfuence( this );
		return base.Remove( takeYourTime );
	}

	public override int Influence( GroundNode node )
	{
		return influence - node.DistanceFrom( this.node );
	}

}
