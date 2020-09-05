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
	static public GameObject prefab;
	static public Shader shader;
	[JsonIgnore]
	new public MeshRenderer renderer;
	int sliceLevelID;

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
		public void FixedUpdate( Building building )
		{
			if ( done )
				return;

			int plankMissing = plankNeeded - plankOnTheWay - plankArrived;
			ItemDispatcher.lastInstance.RegisterRequest( building, Item.Type.plank, plankMissing, ItemDispatcher.Priority.high );
			int stoneMissing = stoneNeeded - stoneOnTheWay - stoneArrived;
			ItemDispatcher.lastInstance.RegisterRequest( building, Item.Type.stone, stoneMissing, ItemDispatcher.Priority.high );

			progress += 0.01f;
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
	public Construction construction = new Construction();

	public enum Type
	{
		stock,
		workshop
	}

	public static void Initialize()
	{
		prefab = (GameObject)Resources.Load( "constructedHouse" );
		Assert.IsNotNull( prefab );
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
		var flag = flagNode.flag;

		var buildingObject = (GameObject)GameObject.Instantiate( prefab );
		buildingObject.name = "Building " + node.x + ", " + node.y;
		buildingObject.transform.SetParent( ground.transform );
		buildingObject.transform.localPosition = node.Position();
		buildingObject.transform.localScale = new Vector3( 40, 40, 40 );
		Building newBuilding = null;
		if ( type == Type.stock )
			newBuilding = buildingObject.AddComponent<Stock>();
		if ( type == Type.workshop )
			newBuilding = buildingObject.AddComponent<Workshop>();
		newBuilding.ground = ground;
		newBuilding.flag = flag;
		newBuilding.node = node;
		node.building = newBuilding;
		return true;
	}

	public void Start()
	{
		transform.SetParent( ground.transform );
		transform.localPosition = node.Position();
		transform.localScale = new Vector3( 40, 40, 40 );
		transform.Rotate( Vector3.back * 90 );
		renderer = GetComponent<MeshRenderer>();
		sliceLevelID = Shader.PropertyToID( "_SliceLevel" );
		foreach ( var m in renderer.materials )
			m.shader = shader;
	}

	public void FixedUpdate()
	{
		construction.FixedUpdate( this );
	}

	public void Update()
	{
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
		float upperLimit = lowerLimit+1;
		float level = upperLimit;
		if ( !construction.done )
			level = lowerLimit+(upperLimit-lowerLimit)*construction.progress;

		foreach ( var m in renderer.materials )
			m.SetFloat( sliceLevelID, level );
	}
}
