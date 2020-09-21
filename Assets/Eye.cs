using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Assertions.Must;

[RequireComponent( typeof( Camera ))]
public class Eye : MonoBehaviour
{
	public float altitude = 0.3f;
	static public float minAltitude = 0.2f;
	static public float maxAltitude = 1.0f;
	public Ground ground;

	public static void Create()
	{
		var eyeObject = new GameObject();
		Eye eye = eyeObject.AddComponent<Eye>();
		eye.tag = "MainCamera";
		eye.name = "Eye";
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
		{
			Vector3 position = hit.point;
			if ( position.y < Ground.waterLevel * Ground.maxHeight )
				position.y = Ground.waterLevel * Ground.maxHeight;
			transform.position = position + Vector3.up * altitude * Ground.maxHeight;
		}
	}

	public void FocusOn( GroundNode node )
	{
		transform.position = node.Position() - new Vector3( 0, 4, 8 );
	}

	void FixedUpdate()
    {
        if ( Input.GetKey( KeyCode.A ) )
            transform.localPosition += Vector3.left * 0.1f;
        if ( Input.GetKey( KeyCode.D ) )
            transform.localPosition += Vector3.right * 0.1f;
        if ( Input.GetKey( KeyCode.W ) )
            transform.position += Vector3.forward * 0.1f;
		if ( Input.GetKey( KeyCode.S ) )
			transform.position += Vector3.back * 0.1f;
		if ( Input.GetKey( KeyCode.Q ) )
			transform.Rotate( Vector3.up, 2, Space.World );
		if ( Input.GetKey( KeyCode.E ) )
			transform.Rotate( Vector3.up, -2, Space.World );
		if ( Input.GetKey( KeyCode.Z ) && altitude < maxAltitude )
			altitude *= 1.01f;
		if ( Input.GetKey( KeyCode.X ) && altitude > minAltitude )
			altitude *= 0.99f;
	}
}
