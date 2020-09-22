	using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using UnityEditor.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Mission : MonoBehaviour
{
	public World world;

    void Start()
    {
		Item.Initialize();
		Panel.Initialize();
		Building.Initialize();
		Road.Initialize();
		Worker.Initialize();
		Flag.Initialize();
		Resource.Initialize();
		world = ScriptableObject.CreateInstance<World>();
		world.NewGame( 792469403 );
	}

	void Update()
    {
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
}
