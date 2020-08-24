using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Eye : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        camera = GetComponent<Camera>();
    }

    // Update is called once per frame
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
    new Camera camera;
}
