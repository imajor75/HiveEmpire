using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Resource : MonoBehaviour
{
	public GroundNode node;
	public Type type;
	public int charges;
	GameObject body;
	static List<GameObject> templateTree = new List<GameObject>();
	static List<GameObject> templateRock = new List<GameObject>();

	public enum Type
	{
		tree,
		rock,
		other
	}

	public static void Initialize()
	{
		templateTree.Add( (GameObject)Resources.Load( "BrokenVector/LowPolyTreePack/Prefabs/Tree Type0 05" ) );
		templateTree.Add( (GameObject)Resources.Load( "BrokenVector/LowPolyTreePack/Prefabs/Tree Type3 02" ) );
		templateTree.Add( (GameObject)Resources.Load( "BrokenVector/LowPolyTreePack/Prefabs/Tree Type3 05" ) );
		templateTree.Add( (GameObject)Resources.Load( "BrokenVector/LowPolyTreePack/Prefabs/Tree Type5 04" ) );
		templateTree.Add( (GameObject)Resources.Load( "BrokenVector/LowPolyTreePack/Prefabs/Tree Type0 02" ) );

		templateRock.Add( (GameObject)Resources.Load( "LowPoly Rocks/Prefabs/Rock1" ) );
		templateRock.Add( (GameObject)Resources.Load( "LowPoly Rocks/Prefabs/Rock3" ) );
		templateRock.Add( (GameObject)Resources.Load( "LowPoly Rocks/Prefabs/Rock9" ) );
		templateRock.Add( (GameObject)Resources.Load( "Rocks pack Lite/Prefabs/Rock1" ) );
		templateRock.Add( (GameObject)Resources.Load( "Rocks pack Lite/Prefabs/Rock2" ) );
		templateRock.Add( (GameObject)Resources.Load( "Rocks pack Lite/Prefabs/Rock3" ) );
	}

	static public Resource Create()
	{
		GameObject obj = new GameObject();
		return obj.AddComponent<Resource>();
	}

	public Resource Setup( GroundNode node, Type type, int charges = 1 )
	{
		if ( node.building || node.flag || node.resource )
		{
			Destroy( gameObject );
			return null;
		}

		node.resource = this;
		this.type = type;
		this.charges = charges;
		this.node = node;
		return this;
	}

    // Start is called before the first frame update
    void Start()
    {
		transform.SetParent( node.ground.transform );
		transform.localPosition = node.Position();

		if ( type == Type.tree )
		{
			name = "Tree";
			body = GameObject.Instantiate( templateTree[Ground.rnd.Next( templateTree.Count )] );
			body.transform.Rotate( Vector3.up * Ground.rnd.Next( 360 ) );
			body.transform.localScale = Vector3.one * 0.3f;
		}
		if ( type == Type.rock )
		{
			name = "Rock";
			body = GameObject.Instantiate( templateRock[Ground.rnd.Next( templateRock.Count )] );
			body.transform.Rotate( Vector3.up * Ground.rnd.Next( 360 ) );
		}
		if ( type == Type.other )
			name = "Decoration";
		if ( body != null )
		{
			body.transform.SetParent( transform );
			body.transform.localPosition = Vector3.zero;
		}
		Assert.IsNotNull( body );
	}

	// Update is called once per frame
	void Update()
    {
        
    }

	public void Validate()
	{
		//Assert.IsNotNull( body );
	}
}
