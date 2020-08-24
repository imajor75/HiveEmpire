using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

public class Building : MonoBehaviour
{
    public Flag flag;
    public Ground ground;
    public GroundNode node;
    static public Material material;

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

        var buildingObject = GameObject.CreatePrimitive( PrimitiveType.Capsule );
        buildingObject.name = "Building " + node.x + ", " + node.y;
        buildingObject.transform.SetParent( ground.transform );
        buildingObject.transform.localPosition = node.Position();
        buildingObject.transform.localScale *= 0.3f;
        var newBuilding = buildingObject.AddComponent<Building>();
        newBuilding.ground = ground;
        newBuilding.flag = flag;
        newBuilding.node = node;
        node.building = newBuilding;
        return true;
    }

    // Start is called before the first frame update
    void Start()
    {
        var renderer = gameObject.GetComponent<MeshRenderer>();
        renderer.material = material;
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
