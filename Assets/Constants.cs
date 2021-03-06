public static class Constants
{
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
        public const float heightFollowSpeed = 0.04f;
        public const float moveSpeed = 0.1f;
        public const float rotateSpeed = 0.03f;
        public const float autoRotateSpeed = 0.001f;
        public const float altitudeChangeSpeed = 1.01f;
        public const float altitudeSmoothness = 0.1f;
        public const float altitudeChangeSpeedWithMouseWheel = 0.5f;
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

    }

    public static class Ground
    {
    	public const int maxArea = 10;
    	public const float defaultSharpRendering = 0.5f;
    }

    public static class GuardHouse
    {
    	public const int defaultInfluence = 8;
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
    	public const int productivityUpdateTime = 3000;
		public const float productionUpdateFactor = 0.1f;
		public const float defaultInputWeight = 0.5f;
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
    	public const int timeBetweenWorkersAdded = 3000;
    	public const float bodyHeight = 1.0f/20;
    }

    public static class Stock
    {
    	public const int influenceRange = 10;
	    public const int defaultmaxItems = 200;
	    public const int defaultmaxItemsForMain = 400;
        public const int plankNeeded = 3;
        public const int stoneNeeded = 3;
        public const bool flatteningNeeded = true;
        public const int constructionTime = 6000;
        public const global::Node.Type groundTypeNeeded = global::Node.Type.aboveWater;
		public const int cartCapacity = 25;
        public const float cartSpeed = 1.25f;
    }

    public static class Worker
    {
       	public const int stuckTimeout = 3000;
    	public const int boredTimeBeforeRemove = 6000;
    }

    public static class Workshop
    {
	    public const int mineOreRestTime = 8000;
	    public const int fishRestTime = 8000;
	    public const int pasturingTime = 100;
	    public const float pasturingPrayChance = 0.2f;  
    	public const int maxSavedStatusTime = 50 * 60 * 60 * 10;
        public const int productivityTimingLength = 300;
        public const float productivityWeight = 0.05f;
        public const int defaultProductionTime = 1500;
        public const int defaultMaxRestTime = 3000;
        public const int defaultRelaxSpotNeeded = 20;
        public const int defaultOutputMax = 6;
        public const int defaultGatheringRange = 6;
        public const int defaultBufferSize = 6;
        public const int defaultImportantInBuffer = 3;
    }
}

