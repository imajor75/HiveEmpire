using System;
using UnityEngine;

public static class Constants
{
    public static class Interface
    {
        public const int showNextActionDuringReplay = Constants.World.normalSpeedPerSecond * 2;
    }

    public static class World
    {
    	public const float autoSaveInterval = Constants.World.normalSpeedPerSecond * 60 * 5;
        public const float fastSpeedFactor = 8;
        public const int normalSpeedPerSecond = 50;
        public const int allowedAdvancePerFrame = 5;
    }

    public static class Network
    {
        public const int bufferSize = 1400;
        public const int defaultPort = 5000;
        public const float lagTolerance = 0.5f;
        public const int broadcastPort = 35147;
    }

    public static class Building
    {
        public const int flatteningTime = 220;
        public const int defaultPlankNeeded = 2;
        public const int defaultStoneNeeded = 0;
    }

    public static class Eye
    {
        public const float defaultAltitude = 4.0f;
        public const float minAltitude = 2.0f;
        public const float maxAltitude = 15.0f;
        public const float defaultViewDistance = 5.0f;
        public const float defaultViewDistanceWithDirector = 2.0f;
        public const float forwardForGroundBlocks = 10;
        public const float heightFollowSpeed = 2;
        public const float moveSpeed = 5;
        public const float rotateSpeed = 1.5f;
        public const float autoRotateSpeed = 0.15f;
        public const float altitudeChangeSpeed = 5;
        public const float altitudeSmoothness = 1;
        public const float altitudeChangeSpeedWithMouseWheel = 25;
        public const float autoStorePositionAfter = 2;
        public const int maxNumberOfSavedPositions = 10;
    }

    public static class Flag
    {
        public const int maxItems = 6;
        public const float tilesHeight = 0.03f;
        public const float itemSpread = 0.25f;
    }

    public static class Item
    {
    	public const bool creditOnRemove = true;
        public static float[] bottomHeights = new float[(int)(global::Item.Type.total)]
        {
		    float.MaxValue,     //log
		    float.MaxValue,     //stone
		    float.MaxValue,     //plank
		    float.MaxValue,     //fish
		    float.MaxValue,     //grain
		    float.MaxValue,     //flour
		    float.MaxValue,     //salt
		    float.MaxValue,     //pretzel
		    -0.2f,     //hide
		    float.MaxValue,     //iron
		    float.MaxValue,     //coal
		    float.MaxValue,     //gold
		    float.MaxValue,     //bow
		    float.MaxValue,     //steel
		    float.MaxValue,     //weapon
		    float.MaxValue,     //water
		    float.MaxValue,     //beer
		    float.MaxValue,     //pork
		    float.MaxValue,     //goldBar
		    float.MaxValue      //soldier
        };
        public static float[] yawAtFlag = new float[(int)(global::Item.Type.total)]
        {
		    0,     //log
		    0,     //stone
		    0,     //plank
		    0,     //fish
		    0,     //grain
		    0,     //flour
		    0,     //salt
		    0,     //pretzel
		    -100,    //hide
		    0,     //iron
		    0,     //coal
		    0,     //gold
		    0,     //bow
		    0,     //steel
		    0,     //weapon
		    0,     //water
		    0,     //beer
		    0,     //pork
		    0,     //goldBar
		    0      //soldier
        };
        public static Vector3[] secondItemOffset = new Vector3[(int)(global::Item.Type.total)]
        {
		    new Vector3( 0, 0.45f, 0 ),     //log
		    new Vector3( 0, 1.0f, 0 ),     //stone
		    new Vector3( 0, 0, -0.25f ),     //plank
		    new Vector3( 0, 0, -0.35f ),     //fish
		    new Vector3( 0, 0.7f, 0 ),     //grain
		    new Vector3( 0, 0.9f, 0 ),     //flour
		    new Vector3( 0, 1.0f, 0 ),     //salt
		    new Vector3( 0, 0.3f, 0 ),     //pretzel
		    new Vector3( 0, 0, 0 ),    //hide
		    new Vector3( 0, 0, -0.25f ),     //iron
		    new Vector3( 0, 0.8f, 0 ),     //coal
		    new Vector3( 0, 0.7f, 0 ),     //gold
		    new Vector3( 0, 0, -0.25f ),     //bow
		    new Vector3( 0, 0.3f, 0 ),     //steel
		    new Vector3( 0, 0, -0.25f ),     //weapon
		    new Vector3( 0, 0.7f, 0 ),     //water
		    new Vector3( 0, 0.7f, 0 ),     //beer
		    new Vector3( 0, 0.4f, 0 ),     //pork
		    new Vector3( 0, 0.3f, 0 ),     //goldBar
		    new Vector3( 0, 0, 0 )      //soldier
        };
    }

    public static class Ground
    {
    	public const int maxArea = 10;
    	public const float defaultSharpRendering = 0.5f;
        public const int grassLevels = 30;
        public const int grassMaskDimension = 256;
        public const float grassDensity = 0.4f;
    }

    public static class GuardHouse
    {
    	public const int defaultInfluence = 8;
        public const int deathFadeTime = 100;
        public const int fightLoopLength = 200;
        public const int hitTime = 0;
        public const int sufferTime = 120;
        public const int fightDuration = 300;
        public const int constructionTime = Constants.World.normalSpeedPerSecond * 60;
        public const int attackMaxDistance = 8;
    }

    public static class HeightMap
    {
        public const int defaultSize = 9;
        public const bool defaultTileable = true;
        public const bool defaultIsland = false;
    	public const float defaultBorderLevel = 0.5f;
    	public const float defaultRandomness = 1.2f;
    	public const float defaultNoise = -0.15f;
    	public const float defaultRandomnessDistribution = -0.3f;
        public const bool defaultNormalize = true;
    	public const float defaultAdjustment = 0;
    	public const float defaultSquareDiamondRatio = 1;
    }

    public static class Map
    {
        public const float defaultZoom = 6;
        public const float zoomMin = 1;
        public const float zoomMax = 20;
        public const float zoomSpeed = 0.03f;
        public const float zoomSpeedWithMouseWheel = 0.2f;
    }

    public static class Node
    {
    	public const float size = 1;
    	public const int neighbourCount = 6;
	    public const float decorationSpreadMin = 0.3f;
	    public const float decorationSpreadMax = 0.6f;
	    public const float decorationDensity = 0.4f;
    }

    public static class Player
    {
    	public const int productivityAdvanceTime = Constants.World.normalSpeedPerSecond * 60;
    	public const int productivityUpdateTime = 50;
		public static float productionUpdateFactor = (float)Math.Pow( 0.9, 1.0/60 );
		public const float defaultInputWeight = 0.5f;
        public const int attackPeriod = 200;
        public static string[] names = 
        {
            "Ahmose",
            "Ammeris",
            "Baufra",
            "Benerib",
            "Dedumose",
            "Duaenre",
            "Gautseshen",
            "Gilukhipa",
            "Harwa",
            "Horemheb",
            "Inetkawes",
            "Iuwelot",
            "Kaemqed",
            "Khenthap",
            "Lysimachus",
            "Maatkare",
            "Merneptah",
            "Nebiriau",
            "Nitocris",
            "Osorkon",
            "Paanchi",
            "Psusennes",
            "Qalhata",
            "Raneb",
            "Renseneb",
            "Sahure",
            "Senebkay",
            "Sihathor",
            "Taharqa",
            "Twosret",
            "Userkaf",
            "Weneg",
            "Yaqub-Har",
            "Zoser"
        };
        public static string[] teamNames =
        {
            "Atlas",
            "Cecropia",
            "Chimabachidae",
            "Choreutidae",
            "Castniidae",
            "Aididae",
            "Himantopteridae",
            "Hesperiidae",
            "Pyralidae",
            "Apatelodidae",
            "Sematuridae",
            "Erebidae"
        };
        public static Color[] teamColors = 
        {
            new Color( 0.7f, 0.4f, 0.2f ),
            new Color( 0.6f, 0.8f, 0.4f ),
            new Color( 0, 0.5f, 0.8f ),
            new Color( 0.6f, 0.5f, 0.4f )
        };
    }

    public static class Resource
    {
        public const int treeGrowthTime = 15000;    // 5 minutes
        public const int cornfieldGrowthTime = 20000;
        public const int animalSpawnTime = 1000;
        public const int treeSoundTime = 60000;
    }

    public static class Road
    {
    	public const int timeBetweenHaulersAdded = Constants.World.normalSpeedPerSecond * 60;
    	public const float bodyHeight = 1.0f/20;
    	public const int blocksInSection = 8;
    }

    public static class Stock
    {
    	public const int influenceRange = 10;
	    public const int defaultmaxItems = 200;
	    public const int defaultmaxItemsForMain = 400;
	    public const int defaultInputMin = 0;
	    public const int defaultInputMax = 0;
	    public const int defaultOutputMin = 0;
	    public const int defaultOutputMax = 50;
	    public const int defaultCartInput = 0;
	    public const int defaultCartOutput = 0;
        public const int plankNeeded = 3;
        public const int stoneNeeded = 3;
        public const bool flatteningNeeded = true;
        public const int constructionTime = Constants.World.normalSpeedPerSecond * 120;
        public const global::Node.Type groundTypeNeeded = global::Node.Type.aboveWater;
		public const int cartCapacity = 25;
        public const float cartSpeed = 1.25f;
        public const int startPlankCount = 10;
        public const int startStoneCount = 5;
        public const int startSoldierCount = 15;
    }

    public static class Unit
    {
       	public const int stuckTimeout = Constants.World.normalSpeedPerSecond * 60;
    	public const int boredTimeBeforeRemove = Constants.World.normalSpeedPerSecond * 120;
        public const int flagSearchDistance = 6;
    }

    public static class Workshop
    {
	    public const int mineOreRestTime = Constants.World.normalSpeedPerSecond * 400;
	    public const int fishRestTime = Constants.World.normalSpeedPerSecond * 320;
	    public const int pasturingTime = Constants.World.normalSpeedPerSecond * 2;
	    public const float pasturingPrayChance = 0.2f;  
    	public const int maxSavedStatusTime = Constants.World.normalSpeedPerSecond * 60 * 60 * 10;
        public const int productivityTimingLength = 300;
        public const float productivityWeight = 0.05f;
        public const int defaultProductionTime = Constants.World.normalSpeedPerSecond * 30;
        public const int defaultMaxRestTime = Constants.World.normalSpeedPerSecond * 60;
        public const int defaultRelaxSpotNeeded = 20;
        public const int defaultOutputMax = 4;
        public const int defaultGatheringRange = 6;
        public const int defaultBufferSize = 4;
        public const int defaultImportantInBuffer = 3;
        public const int relaxAreaSize = 3;
        public const int gathererMaxOutputRecalculationPeriod = Constants.World.normalSpeedPerSecond * 60 * 30;
    }

    public static class Simpleton
    {
        public const int roadMaxLength = 7;
        public const int flagConnectionRange = 7;
        public const float defaultConfidence = 0.1f;
        public const float minimumConfidence = 0.0f;
        public const int inabilityTolerance = Constants.World.normalSpeedPerSecond * 60;
        public const float confidenceLevel = 0.1f;
        public const int sourceSearchRange = 6;
        public const float extensionImportance = 0.8f;
        public const float flagCaptureImportance = 0.3f;
        public const float relaxTolerance = 0.8f;
        public static int cleanupPeriod = 0;//60 * 60 * Constants.World.normalSpeedPerSecond;
        public const int stockCoverage = 8;
        public const int workshopCoverage = 8;
        public const int stockSave = Constants.Stock.cartCapacity;
        public const int cartMin = 5;
        public const int itemTypesPerStock = 6;
        public const int dealCheckPeriod = Constants.World.normalSpeedPerSecond * 60 * 10;
        public const float guardHouseWorkshopRatio = 1.5f;
        public const int roadLastUsedMin = Constants.World.normalSpeedPerSecond * 60 * 20;
        public const int roadLastUsedMax = Constants.World.normalSpeedPerSecond * 60 * 120;
        public const int soldiersReserved = 15;
    }
}

