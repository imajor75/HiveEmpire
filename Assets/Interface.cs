using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class Interface : MonoBehaviour
{
	public World world;
	public GameObject cursor;
	Canvas canvas;
	public GroundNode selectedNode;
	public Workshop.Type selectedWorkshopType = Workshop.Type.unknown;

	void Start()
	{
		Item.Initialize();
		Panel.Initialize();
		Building.Initialize();
		Road.Initialize();
		Worker.Initialize();
		Flag.Initialize();
		Resource.Initialize();

		cursor = GameObject.CreatePrimitive( PrimitiveType.Cube );
		cursor.name = "Cursor";
		cursor.GetComponent<MeshRenderer>().material = Resources.Load<Material>( "Cursor" );
		cursor.transform.localScale *= 0.25f;
		cursor.transform.SetParent( transform );

		world = ScriptableObject.CreateInstance<World>();
		world.NewGame( 792469403 );
	}

	void Update()
	{
		GroundNode node = world.eye.FindNodeAt( Input.mousePosition );
		if ( node != null )
		{
			cursor.transform.localPosition = node.Position();
			CheckNodeContext( node );
		};

		if ( Input.GetKey( KeyCode.Space ) )
			world.speedModifier = 5;
		else
			world.speedModifier = 1;
		if ( Input.GetKeyDown( KeyCode.P ) )
		{
			string fileName = "test.json";
			world.Save( fileName );
			Debug.Log( fileName + " is saved" );
		}
		if ( Input.GetKeyDown( KeyCode.L ) )
		{
			string fileName = "test.json";
			world.Load( "test.json" );
			Debug.Log( fileName + " is loaded" );
		}
		if ( Input.GetKeyDown( KeyCode.N ) )
		{
			world.NewGame( new System.Random().Next() );
			Debug.Log( "New game created" );
		}

	}

	void CheckNodeContext( GroundNode node )
	{
		Player player = world.mainPlayer;
		if ( Input.GetKeyDown( KeyCode.F ) )
		{
			Flag flag = Flag.Create();
			if ( !flag.Setup( world.ground, node, player ) )
				Destroy( flag );
		};
		if ( Input.GetKeyDown( KeyCode.R ) )
			Road.AddNodeToNew( world.ground, node, player );
		if ( Input.GetKeyDown( KeyCode.V ) )
			Dialog.Open( Dialog.Type.selectBuildingType );
		if ( Input.GetKeyDown( KeyCode.B ) && selectedWorkshopType != Workshop.Type.unknown )
			Workshop.Create().Setup( world.ground, node, player, selectedWorkshopType );
		if ( Input.GetMouseButtonDown( 0 ) )
		{
			if ( node.building )
				node.building.OnClicked();
			if ( node.flag )
				node.flag.OnClicked();
			if ( node.road )
				node.road.OnClicked();
		}
		if ( Input.GetKeyDown( KeyCode.O ) )
		{
			selectedNode = node;
			Debug.Log( "Current pos: " + node.x + ", " + node.y );
			Debug.Log( "Distance from main building: " + node.DistanceFrom( world.mainBuilding.node ) );
		}
		if ( Input.GetKeyDown( KeyCode.K ) )
		{
			if ( node.road )
				node.road.Remove();
			if ( node.building )
				node.building.Remove();
			if ( node.flag )
				node.flag.Remove();
		}
	}
}
