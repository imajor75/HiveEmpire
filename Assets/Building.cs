using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

abstract public class Building : MonoBehaviour
{
	public Flag flag;
	public Ground ground;
	public GroundNode node;
	[JsonIgnore]
	public Road exit;
	static public GameObject prefab;
	static public GameObject prefab2;
	static public Shader shader;
	[JsonIgnore]
	public List<MeshRenderer> renderers;
	int sliceLevelID;
	public Construction construction = new Construction();

	[System.Serializable]
	public class Construction
	{
		public bool done;
		public float progress;
		public int plankNeeded;
		public int plankOnTheWay;
		public int plankArrived;
		public int stoneNeeded;
		public int stoneOnTheWay;
		public int stoneArrived;

		public void Update( Building building )
		{
			if ( done )
				return;

			int plankMissing = plankNeeded - plankOnTheWay - plankArrived;
			ItemDispatcher.lastInstance.RegisterRequest( building, Item.Type.plank, plankMissing, ItemDispatcher.Priority.high );
			int stoneMissing = stoneNeeded - stoneOnTheWay - stoneArrived;
			ItemDispatcher.lastInstance.RegisterRequest( building, Item.Type.stone, stoneMissing, ItemDispatcher.Priority.high );
		}

		public void FixedUpdate()
		{
			if ( done )
				return;

			progress += 0.001f;
			float maxProgress = ((float)plankArrived+stoneArrived)/(plankNeeded+stoneNeeded);
			if ( progress > maxProgress )
				progress = maxProgress;

			if ( progress >= 1 )
				done = true;
		}
		public bool ItemOnTheWay( Item item )
		{
			if ( done )
				return false;

			if ( item.type == Item.Type.plank )
			{
				plankOnTheWay++;
				Assert.IsTrue( plankArrived + plankOnTheWay <= plankNeeded );
				return true;
			}
			if ( item.type == Item.Type.stone )
			{
				stoneOnTheWay++;
				Assert.IsTrue( stoneArrived + stoneOnTheWay <= stoneNeeded );
				return true;
			}

			Assert.IsTrue( false );
			return false;
		}

		public virtual bool ItemArrived( Item item )
		{
			if ( done )
				return false;

			if ( item.type == Item.Type.plank )
			{
				Assert.IsTrue( plankOnTheWay > 0 );
				plankOnTheWay--;
				plankArrived++;
				Assert.IsTrue( plankArrived + plankOnTheWay <= plankNeeded );
				return true;
			}
			if ( item.type == Item.Type.stone )
			{
				Assert.IsTrue( stoneOnTheWay > 0 );
				stoneOnTheWay--;
				stoneArrived++;
				Assert.IsTrue( stoneArrived + stoneOnTheWay <= stoneNeeded );
				return true;
			}

			Assert.IsTrue( false );
			return false;
		}
	}

	public enum Type
	{
		stock,
		workshop
	}

	public static void Initialize()
	{
		prefab = (GameObject)Resources.Load( "constructedHouse" );
		Assert.IsNotNull( prefab );
		prefab2 = (GameObject)Resources.Load( "Medieval fantasy house/Medieva_fantasy_house" );
		Assert.IsNotNull( prefab2 );
		shader = (Shader)Resources.Load( "Construction" );
		Assert.IsNotNull( shader );
	}

	public static bool CreateNew( Ground ground, GroundNode node, Type type )
	{
		if ( node.flag || node.building || node.road )
		{
			Debug.Log( "Node is already occupied" );
			return false;
		}
		var flagNode = ground.GetNode( node.x + 1, node.y - 1 );
		if ( !Flag.Create( ground, flagNode ) )
		{
			Debug.Log( "Flag couldn't be created" );
			return false;
		}

		var buildingObject = (GameObject)GameObject.Instantiate( prefab2 );
		Building newBuilding = null;
		if ( type == Type.stock )
			newBuilding = buildingObject.AddComponent<Stock>();
		if ( type == Type.workshop )
			newBuilding = buildingObject.AddComponent<Workshop>();
		newBuilding.ground = ground;
		newBuilding.flag = flagNode.flag;
		newBuilding.node = node;
		node.building = newBuilding;
		return true;
	}

	public void Start()
	{
		name = "Building " + node.x + ", " + node.y;
		transform.SetParent( ground.transform );
		transform.localPosition = node.Position();
		transform.localScale = new Vector3( 0.09f, 0.09f, 0.09f );
		transform.Rotate( Vector3.up * -90 );
		renderers = new List<MeshRenderer>();
		ScanChildObject( transform );
		sliceLevelID = Shader.PropertyToID( "_SliceLevel" );
		foreach( var renderer in renderers )
			foreach ( var m in renderer.materials )
				m.shader = shader;

		Assert.IsNull( exit );
		exit = Road.Create();
		exit.SetupAsBuildingExit( this );
	}

	void ScanChildObject( Transform transform )
	{
		var renderer = transform.GetComponent<MeshRenderer>();
		if ( renderer != null )
			renderers.Add( renderer );
		for ( int i = 0; i < transform.childCount; i++ )
			ScanChildObject( transform.GetChild( i ) );
	}

	public void FixedUpdate()
	{
		construction.FixedUpdate();
	}

	public void Update()
	{
		construction.Update( this );
		UpdateLook();
	}

	public virtual bool SendItem( Item.Type itemType, Building destination )
	{
		Assert.IsTrue( false );
		return false;
	}

	public virtual void ItemOnTheWay( Item item )
	{
		Assert.IsTrue( false );
	}

	public virtual void ItemArrived( Item item )
	{
		Assert.IsTrue( false );
	}

	virtual public void OnClicked()
	{
		Assert.IsTrue( false );
	}

    virtual public void Validate()
    {
        Assert.AreEqual( this, node.building );
        Assert.AreEqual( flag, ground.GetNode( node.x + 1, node.y - 1 ).flag );
    }

	public void UpdateLook()
	{
		float lowerLimit = transform.position.y;
		float upperLimit = lowerLimit + 1.5f;
		float level = upperLimit;
		if ( !construction.done )
			level = lowerLimit+(upperLimit-lowerLimit)*construction.progress;

		foreach ( var r in renderers )
			foreach ( var m in r.materials )
				m.SetFloat( sliceLevelID, level );
	}
}
