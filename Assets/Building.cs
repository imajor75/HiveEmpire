using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

abstract public class Building : MonoBehaviour
{
	public enum Type
	{
		stock,
		workshop
	}
    public Flag flag;
    public Ground ground;
    public GroundNode node;
    static public GameObject prefab;

    public static bool CreateNew( Ground ground, GroundNode node, Type type )
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
}
