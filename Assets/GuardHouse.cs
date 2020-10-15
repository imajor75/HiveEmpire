﻿using System.Collections;
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

	public static GuardHouse Create()
	{
		return new GameObject().AddComponent<GuardHouse>();
	}

	public GuardHouse Setup( GroundNode node, Player owner )
	{
		construction.plankNeeded = 2;
		title = "guardhouse";
		height = 2;
		if ( base.Setup( node, owner, configuration ) == null )
			return null;

		return this;
	}

	new void Start()
	{
		GameObject.Instantiate( template, transform );
		base.Start();
	}

	public new void Update()
	{
		base.Update();
		if ( construction.done && soldiers.Count == 0 )
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

	void OnDestroy()
	{
		owner.UnregisterInfuence( this );
	}

	public override int Influence( GroundNode node )
	{
		return influence - node.DistanceFrom( this.node );
	}

}
