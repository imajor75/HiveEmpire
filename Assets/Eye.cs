using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent( typeof( Camera ))]
public class Eye : MonoBehaviour
{
	new Camera camera;

	void Start()
    {
		tag = "MainCamera";
		name = "Eye";
        camera = GetComponent<Camera>();
		transform.position = new Vector3( 0, 4, -7 );
		transform.Rotate( Vector3.right * 40 );
    }

    void FixedUpdate()
    {
        if ( Input.GetKey( KeyCode.A ) )
            camera.transform.localPosition += Vector3.left * 0.1f;
        if ( Input.GetKey( KeyCode.D ) )
            camera.transform.localPosition += Vector3.right * 0.1f;
        if ( Input.GetKey( KeyCode.W ) )
            camera.transform.position += Vector3.forward * 0.1f;
        if ( Input.GetKey( KeyCode.S ) )
            camera.transform.position += Vector3.back * 0.1f;
    }
}
