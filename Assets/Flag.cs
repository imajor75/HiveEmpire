using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

public class Flag : MonoBehaviour
{
    public static bool CreateNew(Ground ground, GroundNode node)
    {
        if (node.flag)
        {
            Debug.Log("There is a flag there already");
            return false;
        }
        bool hasAdjacentFlag = false;
        foreach (var adjacentNode in node.neighbours)
            if (adjacentNode.flag)
                hasAdjacentFlag = true;
        if (hasAdjacentFlag)
        {
            Debug.Log("Another flag is too close");
            return false;
        }
        GameObject flagObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flagObject.name = "Flag "+node.x+", "+node.y;
        flagObject.transform.SetParent(ground.transform);
        flagObject.transform.localPosition = node.Position();
        flagObject.transform.localScale *= 0.3f;
        node.flag = (Flag)flagObject.AddComponent(typeof(Flag));
        node.flag.node = node;
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
        Assert.AreEqual( this, node.flag );
        for ( int i = 0; i < 6; i++ )
            Assert.IsNull( node.neighbours[i].flag );
    }

    GroundNode node;
}
