using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimationEventForwarder : MonoBehaviour
{
    Worker target;

    public void Start()
    {
        if ( transform.parent )
            target = transform.parent.GetComponent<Worker>();
    }

    void MakeSound( int soundID )
    {
        target?.MakeSound( soundID );
    }

}
