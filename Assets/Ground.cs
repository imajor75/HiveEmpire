using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GroundNode
{
    int x, y;
    //Building building;
    //Flag flag;
    float height;
}

public class Ground : MonoBehaviour
{
    int width = 100, height = 100;
    GroundNode[] layout;

    // Start is called before the first frame update
    void Start()
    {

        Debug.Log( "Hello world!");
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
