using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Flag : MonoBehaviour
{
    public static bool CreateNew(Ground ground, GroundNode node)
    {
        if (node.flag)
        {
            UnityEngine.Debug.Log("There is a flag there already");
            return false;
        }
        bool hasAdjacentFlag = false;
        foreach (var adjacentNode in node.neighbours)
            if (adjacentNode.flag)
                hasAdjacentFlag = true;
        if (hasAdjacentFlag)
        {
            UnityEngine.Debug.Log("Another flag is too close");
            return false;
        }
        UnityEngine.Debug.Log("New flag at " + node.x + ", " + node.y);
        GameObject flagObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flagObject.name = "Flag "+node.x+", "+node.y;
        flagObject.transform.SetParent(ground.transform);
        flagObject.transform.localPosition = node.Position();
        flagObject.transform.localScale *= 0.3f;
        UnityEngine.Debug.Log("Height: " + node.Position().y);
        node.flag = (Flag)flagObject.AddComponent(typeof(Flag));
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
}
