using System;
using UnityEngine;

public static class Constants
{
    public static class Interface
    {
        public const int showNextActionDuringReplay = Constants.Game.normalSpeedPerSecond * 2;
    	public const float autoSaveIntervalInSecond = 60 * 5;
        public const int maxMessages = 50;
        public const int iconSize = 20;
        public const float uiScale = 1.5f;
        public const float highightedMenuItemScale = 1.2f;
        public const float menuHighlightChangeSpeed = 0.1f;
        public const int prerunPerFrame = 500;
        public static class ProductionChainPanel
        {
    		public const int maxWorkshopsPerLine = 5;
            public const int width = 400;
            public const int pixelsPerRow = 120;
            public const int pixelsPerFlow = 20;
            public static Color[] flowColors = new Color[4]{ Color.black.Wash(), Color.red.Wash(), Color.blue.Wash(), Color.green.Wash() };
            public const float flowWidthInPixel = 2;
            public const float flowSegmentLength = 0.05f;
            public const int flowHorizontalSpace = 10;
            public const int backGroundWidth = 1024;
            public const int backGroundHeight = 2048;
        }
    }

    public static class World
    {
        public const float oreCountPerNode = 0.02f;
        public const int soundMaxDistance = 7;
        public const int layerIndexResources = 3;
        public const int layerIndexWater = 4;
        public const int layerIndexHighlightVolume = 6;
        public const int layerIndexGround = 7;
        public const int layerIndexMap = 8;
        public const int layerIndexItems = 9;
        public const int layerIndexDecorations = 10;
        public const int layerIndexPPVolume = 11;
        public const int layerIndexGrass = 12;
        public const int layerIndexBuildings = 13;
        public const int layerIndexUnits = 14;
        public const int layerIndexRoads = 15;
        public const int layerIndexTrees = 16;
        public const int layerIndexSprites = 17;
        public const int layerIndexUI = 18;
    }

    public static class Game
    {
        public const float fastSpeedFactor = 8;
        public const int normalSpeedPerSecond = 50;
        public const int allowedAdvancePerFrame = 5;
        public const float lazyUpdateSpeed = 0.1f;
        public const float turtleUpdateSpeed = 0.01f;
        public const int improveChallengeSampleRange = normalSpeedPerSecond * 60 * 30;
    }

    public static class Network
    {
        public const int bufferSize = 1024;
        public const int defaultPort = 5000;
        public const float lagTolerance = 0.5f;
        public const int broadcastPort = 35147;
    }

    public static class Building
    {
        public const int flatteningTime = 220;
        public const int defaultPlankNeeded = 2;
        public const int defaultStoneNeeded = 0;
        public const float importantBuildingConstructionWeight = 2;
    }

    public static class Eye
    {
        public const float defaultAltitude = 4.0f;
        public const float minAltitude = 2.0f;
        public const float maxAltitude = 15.0f;
        public const float defaultAltitudeDirection = (float)Math.PI / 4;
        public const float minAltitudeDirection = 0.6f;
        public const float maxAltitudeDirection = 1.2f;
        public const float defaultViewDistanceWithDirector = 2.0f;
        public const float groundHeightDefault = 3;
        public const float heightFollowSpeed = 2;
        public const float moveSpeed = 5;
        public const float rotateSpeed = 1.5f;
        public const float autoRotateSpeed = 0.15f;
        public const float altitudeChangeSpeed = 5;
        public const float altitudeDirectopmChangeSpeed = 0.25f;
        public const float altitudeSmoothness = 1;
        public const float altitudeChangeStep = 25;
        public const float autoStorePositionAfter = 2;
        public const int maxNumberOfSavedPositions = 10;
        public const float maxDistance = 0.35f;
        public const bool depthOfField = false;
        public const int highlightEffectLevels = 1;
        public const int highlightEffectGlowSize = 2;
        public const float highlightVolumeHeight = 20 * Node.size;
        public static Color highlightEffectGlowColor = Color.white;
        public const float highlightSwitchTime = 0.5f;
        public const float clipDistance = 1.5f;
        public const float fogDistance = 1.2f;
        public static Color fogColor = new Color( 0.6f, 0.75f, 0.63f );
        public const int spriteAlphaPatchSize = 250;
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
		    float.MaxValue,     //copper
		    float.MaxValue,     //coal
		    float.MaxValue,     //gold
		    float.MaxValue,     //silver
		    float.MaxValue,     //bow
		    float.MaxValue,     //steel
		    float.MaxValue,     //weapon
		    float.MaxValue,     //water
		    float.MaxValue,     //beer
		    float.MaxValue,     //pork
		    float.MaxValue,     //jewelry
		    float.MaxValue,     //soldier
		    float.MaxValue,     //apple
		    float.MaxValue,     //corn
		    float.MaxValue,     //cornFlour
		    float.MaxValue,     //dung
		    float.MaxValue,     //charcoal
		    float.MaxValue,     //pie
		    float.MaxValue,     //milk
		    float.MaxValue,     //egg
		    float.MaxValue,     //cheese
		    float.MaxValue,     //sling
		    float.MaxValue,     //friedFish
		    float.MaxValue      //sterling
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
		    0,     //copper
		    0,     //coal
		    0,     //gold
		    0,     //silver
		    0,     //bow
		    0,     //steel
		    0,     //weapon
		    0,     //water
		    0,     //beer
		    0,     //pork
		    0,     //jewelry
		    0,     //soldier
		    0,     //apple
		    0,     //corn
		    0,     //cornFlour
		    0,     //dung
		    0,     //charcoal
		    0,     //pie
		    0,     //milk
		    0,     //egg
		    0,     //cheese
		    0,     //sling
		    0,     //friedFish
		    0      //sterling
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
		    new Vector3( 0, 0.7f, 0 ),     //copper
		    new Vector3( 0, 0.8f, 0 ),     //coal
		    new Vector3( 0, 0.7f, 0 ),     //gold
		    new Vector3( 0, 0.7f, 0 ),     //silver
		    new Vector3( 0, 0, -0.25f ),     //bow
		    new Vector3( 0, 0.3f, 0 ),     //steel
		    new Vector3( 0, 0, -0.25f ),     //weapon
		    new Vector3( 0, 1.3f, 0 ),     //water
		    new Vector3( 0, 0.7f, 0 ),     //beer
		    new Vector3( 0, 0.4f, 0 ),     //pork
		    new Vector3( 0, 0.8f, 0 ),     //jewelry
		    new Vector3( 0, 0, 0 ),      //soldier
		    new Vector3( 0, 0.8f, 0 ),    //apple
		    new Vector3( 0, 0.6f, 0 ),      //corn
		    new Vector3( 0, 1.3f, 0 ),    //cornFlour
		    new Vector3( 0, 1.2f, 0 ),    //dung
		    new Vector3( 0, 1.0f, 0 ),    //charcoal
		    new Vector3( 0, 0.2f, 0 ),    //pie
		    new Vector3( 0, 1.2f, 0 ),    //milk
		    new Vector3( 0, 0.8f, 0 ),    //egg
		    new Vector3( 0, 0.5f, 0 ),    //cheese
		    new Vector3( 0, 0, -0.3f ),   //sling
		    new Vector3( 0, 0.4f, 0 ),    //friedFish
		    new Vector3( 0, 0.3f, 0 )     //sterling
        };
    }

    public static class Ground
    {
    	public const int maxArea = 10;
    	public const float defaultSharpRendering = 0.5f;
        public const int grassLevels = 30;
        public const int grassMaskDimension = 256;
        public const float grassDensity = 0.4f;
		public const int blockCount = 4;
    }

    public static class GuardHouse
    {
    	public const int defaultInfluence = 8;
        public const int deathFadeTime = 100;
        public const int fightLoopLength = 200;
        public const int hitTime = 0;
        public const int sufferTime = 120;
        public const int fightDuration = 300;
        public const int constructionTime = Constants.Game.normalSpeedPerSecond * 60;
        public const int attackMaxDistance = 8;
    }

    public static class HeightMap
    {
        public const int defaultSize = 9;
        public const bool defaultTileable = true;
        public const bool defaultIsland = false;
    	public const float defaultBorderLevel = 0.0f;
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
        public static class Chart
        {
            public const int advanceTime = Constants.Game.normalSpeedPerSecond * 60;
            public const float pastWeight = 0.95f;
        }
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
        public const int cornFieldGrowthTime = 22000;
        public const int wheatFieldGrowthTime = 10000;
        public const int animalSpawnTime = 1000;
        public const int treeSoundTime = 60000;
        public const int rockCharges = 7;
        public const int oreChargePerNodeDefault = 10;
        public const int scaleUpdatePeriod = Constants.Game.normalSpeedPerSecond * 5;
    }

    public static class Road
    {
    	public const int timeBetweenHaulersAdded = Constants.Game.normalSpeedPerSecond * 60;
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
        public const int constructionTime = Constants.Game.normalSpeedPerSecond * 120;
        public const global::Node.Type groundTypeNeeded = global::Node.Type.aboveWater;
		public const int cartCapacity = 25;
        public const float cartSpeed = 1.25f;
        public const int startPlankCount = 10;
        public const int startStoneCount = 5;
        public const int startSoldierCount = 15;
        public const int resupplyPeriod = Constants.Game.normalSpeedPerSecond * 60 * 30;
        public const int minimumPlank = 2;
        public const int fullTolerance = 20;
    }

    public static class Unit
    {
       	public const int stuckTimeout = Constants.Game.normalSpeedPerSecond * 60;
    	public const int boredTimeBeforeRemove = Constants.Game.normalSpeedPerSecond * 120;
        public const int flagSearchDistance = 6;
        public const float itemsInHandsSpriteHorizontalOffset = 0.2f * Node.size;
        public const float itemsInHandsSpriteVerticalOffset = 0.4f * Node.size;
    }

    public static class Workshop
    {
	    public const int mineOreRestTime = Constants.Game.normalSpeedPerSecond * 400;
	    public const int fishRestTime = Constants.Game.normalSpeedPerSecond * 600;
	    public const int appleGrowTime = Constants.Game.normalSpeedPerSecond * 600;
	    public const int dungRestTime = Constants.Game.normalSpeedPerSecond * 25;
	    public const int pasturingTime = Constants.Game.normalSpeedPerSecond * 2;
	    public const float pasturingPrayChance = 0.2f;  
    	public const int maxSavedStatusTime = Constants.Game.normalSpeedPerSecond * 60 * 60;
        public const int defaultProductionTime = Constants.Game.normalSpeedPerSecond * 30;
        public const int defaultMaxRestTime = Constants.Game.normalSpeedPerSecond * 60;
        public const int defaultRelaxSpotNeeded = 20;
        public const int defaultOutputMax = 4;
        public const int defaultGatheringRange = 6;
        public const int defaultBufferSize = 4;
        public const int defaultImportantInBuffer = 3;
        public const int relaxAreaSize = 3;
        public const int productivityPeriod = Constants.Game.normalSpeedPerSecond * 60 * 5;
        public const int gathererSleepTimeAfterFail = Constants.Game.normalSpeedPerSecond * 6;
        public const int gathererHarvestTime = Constants.Game.normalSpeedPerSecond * 6;
        public const int freeStoneTimePeriod = Constants.Game.normalSpeedPerSecond * 60 * 10;
        public const int keepAwayOnNoPath = Constants.Game.normalSpeedPerSecond * 60 * 3;
    }

    public static class Simpleton
    {
        public const int roadMaxLength = 7;
        public const int flagConnectionRange = 7;
        public const float defaultConfidence = 0.1f;
        public const float minimumConfidence = 0.0f;
        public const int inabilityTolerance = Constants.Game.normalSpeedPerSecond * 60;
        public const float confidenceLevel = 0.1f;
        public const int sourceSearchRange = 6;
        public const float extensionImportance = 0.2f;
        public const float flagCaptureImportance = 0.3f;
        public const float relaxTolerance = 0.8f;
        public static int cleanupPeriod = 0;//60 * 60 * Constants.Game.normalSpeedPerSecond;
        public const int stockCoverage = 8;
        public const int workshopCoverage = 8;
        public const int stockSave = Constants.Stock.cartCapacity;
        public const int cartMin = 5;
        public const int itemTypesPerStock = 6;
        public const int itemTypesPerMainStock = 10;
        public const int dealCheckPeriod = Constants.Game.normalSpeedPerSecond * 60 * 2;
        public const float guardHouseWorkshopRatio = 1.5f;
        public const int roadLastUsedMin = Constants.Game.normalSpeedPerSecond * 60 * 20;
        public const int roadLastUsedMax = Constants.Game.normalSpeedPerSecond * 60 * 120;
        public const int soldiersReserved = 15;
        public const float deadEndProblemFactor = 0.8f;
        public const int noResourceTolerance = Constants.Game.normalSpeedPerSecond * 60 * 20;
        public const float abandonedFlagWeight = 0.25f;
        public const float isolatedBuildingWeight = 0.5f;
        public const float isolatedFlagWeight = 0.5f;
        public const float blockedFlagWeight = 0.75f;
        public const float badConnectionWeight = 0.75f;
        public const float bridgeFlagEfficiency = 0.1f;
        public const float resourceWeight = 0.6f;
        public const float relaxImportance = 0.2f;
        public const float sourceImportance = 0.2f;
        public const float redundancyWeight = 0.5f;
        public const int forestNodeCountForRenew = 15;
        public const int expectedLogFromRenewWoodcutter = 50;
        public const int expectedPlankPanic = 40;
        public const float nodeWithRockPrice = 3;
        public const float nodeAtFarmPrice = 1.5f;
        public const float nodeAtForesterPrice = 1.25f;
        public const float alreadyHasFlagBonus = 1.2f;
        public const int maximumProductionCalculatingPeriod = Constants.Game.normalSpeedPerSecond * 60 * 5;
        public const float resourcesAroundFarmProblem = 0.3f;
        public const float solutionToRemoveTreesAroundFarm = 0.8f;
        public const float solutionToRemoveRocksAroundFarm = 0.5f;
        public const float enoughPreparation = 0.9f;
        public const float importanceReduction = 0.01f;
    }
}

