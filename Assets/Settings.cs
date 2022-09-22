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
    public bool shadows = true;
    public bool softShadows = true;
    public bool renderBuildings = true;
    public bool renderRoads = true;
    public bool renderResources = true;
    public bool renderGround = true;
    public bool renderUnits = true;
    public bool renderItems = true;
    public bool renderDecorations = true;
    public bool renderWater = true;

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
