using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Settings
{
    public bool grass;

    public void Apply()
    {
		Serializer.Write( Application.persistentDataPath + "/Settings/options.json", this, true, true );
    }
}
