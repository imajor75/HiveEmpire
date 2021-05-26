using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimationEventForwarder : MonoBehaviour
{
    Worker target;

    public void Start()
    {
        target = transform.parent.GetComponent<Worker>();
    }

    void MakeSound( int toolID )
    {
        target?.MakeSound( toolID );
    }

}
