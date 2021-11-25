using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimationEventForwarder : MonoBehaviour
{
    Unit target;

    public void Start()
    {
        if ( transform.parent )
            target = transform.parent.GetComponent<Unit>();
    }

    void MakeSound( int soundID )
    {
        target?.MakeSound( soundID );
    }

}
