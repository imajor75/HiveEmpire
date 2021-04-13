using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GuardHouse : Building
{
	public List<Worker> soldiers = new List<Worker>();
	public bool ready;
	public int influence = 8;
	public static GameObject template;
	static readonly Configuration configuration = new Configuration();

	public static new void Initialize()
	{
		template = (GameObject)Resources.Load( "prefabs/buildings/guardhouse" );
		configuration.plankNeeded = 2;
		configuration.stoneNeeded = 2;
		configuration.flatteningNeeded = false;
	}

	public static bool IsNodeSuitable( GroundNode placeToBuild, Player owner, int flagDirection )
	{
		return Building.IsNodeSuitable( placeToBuild, owner, configuration, flagDirection );
	}

	public static GuardHouse Create()
	{
		return new GameObject().AddComponent<GuardHouse>();
	}

	public GuardHouse Setup( GroundNode node, Player owner, int flagDirection, bool blueprintOnly = false )
	{
		construction.plankNeeded = 2;
		title = "guardhouse";
		height = 2;
		if ( base.Setup( node, owner, configuration, flagDirection, blueprintOnly ) == null )
			return null;

		return this;
	}

	new void Start()
	{
		var body = Instantiate( template, transform );
		body.layer = World.layerIndexPickable;
		body.transform.RotateAround( node.Position, Vector3.up, 60 * ( 1 - flagDirection ) );
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
