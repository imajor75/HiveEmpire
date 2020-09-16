using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Assertions.Must;

[RequireComponent( typeof( Camera ))]
public class Eye : MonoBehaviour
{
	new Camera camera;
	public float altitude = 3;
	static public float minAltitude = 2;
	static public float maxAltitude = 10;
	public Ground ground;

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
		ground = Object.FindObjectOfType<Ground>();
    }

	private void Update()
	{
		Ray ray = new Ray( Camera.main.transform.localPosition+Vector3.up*50*GroundNode.size, Vector3.down );
		RaycastHit hit;
		var size = GroundNode.size;
		if ( ground.collider.Raycast( ray, out hit, GroundNode.size * 100 ) )
			transform.position = hit.point + Vector3.up * altitude;
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
		if ( Input.GetKey( KeyCode.Q ) )
			camera.transform.Rotate( Vector3.up, 2, Space.World );
		if ( Input.GetKey( KeyCode.E ) )
			camera.transform.Rotate( Vector3.up, -2, Space.World );
		if ( Input.GetKey( KeyCode.Z ) && altitude < maxAltitude )
			altitude *= 1.01f;
		if ( Input.GetKey( KeyCode.X ) && altitude > minAltitude )
			altitude *= 0.99f;
	}
}
