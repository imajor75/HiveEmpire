using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Settings
{
    public bool grass = true;
    public int fullscreenWidth = -1;
    public int fullscreenHeight = -1;
    public bool fullscreen = true;
    public bool enableSideCameras = true;

    public void Apply()
    {
        if ( fullscreenHeight != -1 && fullscreenWidth != -1 )
            Screen.SetResolution( fullscreenWidth, fullscreenHeight, true );
        else
        {
            fullscreenWidth = Screen.currentResolution.width;
            fullscreenHeight = Screen.currentResolution.height;
        }
        Screen.fullScreen = fullscreen;
		Serializer.Write( Application.persistentDataPath + "/Settings/options.json", this, true, true );
    }
}
