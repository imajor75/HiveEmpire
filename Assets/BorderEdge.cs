using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BorderEdge : MonoBehaviour
{
    public GroundNode node;
    public int direction;

    public void Setup(GroundNode node, int direction )
    {
        this.node = node;
        this.direction = direction;
		Assert.AreNotEqual( node.owner, node.Neighbour( direction ).owner );
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
