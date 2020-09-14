using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Resource : MonoBehaviour
{
	public GroundNode node;
	public Type type;
	public int charges;
	GameObject body;
	static GameObject[] templateTree;

	public enum Type
	{
		tree,
		rock,
		other
	}

	public static void Initialize()
	{
		templateTree = new GameObject[4];
		templateTree[0] = (GameObject)Resources.Load( "Tree9/Tree9_2" );
		templateTree[1] = (GameObject)Resources.Load( "Tree9/Tree9_3" );
		templateTree[2] = (GameObject)Resources.Load( "Tree9/Tree9_4" );
		templateTree[3] = (GameObject)Resources.Load( "Tree9/Tree9_5" );
	}

	static public Resource Create()
	{
		GameObject obj = new GameObject();
		return obj.AddComponent<Resource>();
	}

	public Resource Setup( GroundNode node, Type type, int charges = 1 )
	{
		if ( node.building || node.flag )
		{
			Destroy( gameObject );
			return null;
		}

		this.type = type;
		this.charges = charges;
		this.node = node;
		transform.SetParent( node.ground.transform );
		if ( type == Type.tree )
		{
			name = "Tree";
			body = GameObject.Instantiate( templateTree[Ground.rnd.Next( 4 )] );
			body.transform.Rotate( Vector3.up * Ground.rnd.Next( 360 ) );
		}
		if ( type == Type.rock )
			name = "Rock";
		if ( type == Type.other )
			name = "Decoration";
		if ( body != null )
			body.transform.SetParent( transform );

		transform.localPosition = node.Position();
		return this;
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
		Assert.IsNotNull( body );
	}
}
