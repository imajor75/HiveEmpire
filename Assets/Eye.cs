using UnityEngine;

[RequireComponent( typeof( Camera ))]
public class Eye : MonoBehaviour
{
	new Camera camera;

	public static void Create()
	{
		var eyeObject = new GameObject();
		Eye eye = eyeObject.AddComponent<Eye>();
		eye.tag = "MainCamera";
		eye.name = "Eye";
		eye.camera = eye.GetComponent<Camera>();
		eye.transform.position = new Vector3( 0, 4, -7 );
		eye.transform.Rotate( Vector3.right * 40 );
	}

	void Start()
    {
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
