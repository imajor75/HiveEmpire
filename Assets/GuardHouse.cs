using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GuardHouse : Building
{
	// TODO Guardhouses are sometimes not visible, only after reload
	// Somehow they are offset, but they are linked to the correct block.
	public List<Worker> soldiers = new List<Worker>();
	public bool ready;
	public int influence = 8;
	public static GameObject template;
	static readonly Configuration guardHouseConfiguration = new Configuration();

	public static new void Initialize()
	{
		template = (GameObject)Resources.Load( "prefabs/buildings/guardhouse" );
		guardHouseConfiguration.plankNeeded = 2;
		guardHouseConfiguration.stoneNeeded = 2;
		guardHouseConfiguration.flatteningNeeded = false;
		guardHouseConfiguration.constructionTime = 5000;
	}

	public static bool IsNodeSuitable( GroundNode placeToBuild, Player owner, int flagDirection )
	{
		return Building.IsNodeSuitable( placeToBuild, owner, guardHouseConfiguration, flagDirection );
	}

	public static GuardHouse Create()
	{
		return new GameObject().AddComponent<GuardHouse>();
	}

	public GuardHouse Setup( GroundNode node, Player owner, int flagDirection, bool blueprintOnly = false )
	{
		title = "guardhouse";
		height = 2;
		if ( base.Setup( node, owner, guardHouseConfiguration, flagDirection, blueprintOnly ) == null )
			return null;

		return this;
	}

	new void Start()
	{
		base.Start();
		name = $"Guardhouse {node.x}:{node.y}";
		var body = Instantiate( template, transform );
		body.layer = World.layerIndexPickable;
		body.transform.RotateAround( node.position, Vector3.up, 60 * ( 1 - flagDirection ) );
	}

	public new void FixedUpdate()
	{
		base.FixedUpdate();
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

	public override void OnClicked( bool show = false )
	{
		base.OnClicked( show );
		if ( construction.done )
			Interface.GuardHousePanel.Create().Open( this, show );
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
