using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

public class Building : MonoBehaviour
{
    public Flag flag;
    public Ground ground;
    public GroundNode node;
    public int[] inventory = new int[(int)Item.Type.total];
    public bool main = false;
    static public GameObject prefab;

    public static bool CreateNew( Ground ground, GroundNode node )
    {
        if ( node.flag || node.building || node.road )
        {
            Debug.Log( "Node is already occupied" );
            return false;
        }
        var flagNode = ground.GetNode( node.x + 1, node.y - 1 );
        if ( !Flag.CreateNew( ground, flagNode ) )
        {
            Debug.Log( "Flag couldn't be created" );
            return false;
        }
        var flag = flagNode.flag;

        var buildingObject = (GameObject)GameObject.Instantiate( prefab );
        buildingObject.name = "Building " + node.x + ", " + node.y;
        buildingObject.transform.SetParent( ground.transform );
        buildingObject.transform.localPosition = node.Position();
        Vector3 scale = new Vector3(); scale.Set( 40, 40, 40 );
        buildingObject.transform.localScale = scale;
        buildingObject.transform.Rotate( Vector3.back * 90 );
        var newBuilding = buildingObject.AddComponent<Building>();
        newBuilding.ground = ground;
        newBuilding.flag = flag;
        newBuilding.node = node;
        node.building = newBuilding;
        return true;
    }
    public static bool SetupMain( Ground ground, GroundNode node )
    {
        if ( !CreateNew( ground, node ) )
            return false;

        var mainBuilding = node.building;
        mainBuilding.main = true;
        mainBuilding.inventory[(int)Item.Type.wood] = 10;
        return true;
    }

    // Start is called before the first frame update
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void Validate()
    {
        Assert.AreEqual( this, node.building );
        Assert.AreEqual( flag, ground.GetNode( node.x + 1, node.y - 1 ).flag );
    }
}
