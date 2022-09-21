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
	public bool timedValidate = true;
	public bool frameValidate = true;
    public float masterVolume = 1;
    public bool autoSave = true;
    public float autoSaveInterval = Constants.Interface.autoSaveIntervalInSecond;
    public bool saveOnExit = true;
    public bool showFPS = false;

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
        AudioListener.volume = masterVolume;
		Serializer.Write( Application.persistentDataPath + "/Settings/options.json", this, true, true );
    }
}
