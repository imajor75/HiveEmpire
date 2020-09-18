using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Resource : MonoBehaviour
{
	public GroundNode node;
	public Type type;
	public int growth = 0;
	public int charges;
	GameObject body;
	public int keepAwayTimer = 0;
	public Worker hunter;
	static List<GameObject> templateTree = new List<GameObject>();
	static List<GameObject> templateRock = new List<GameObject>();
	static Material cornfieldMaterial;

	public enum Type
	{
		tree,
		rock,
		fish,
		cornfield,
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

		cornfieldMaterial = Resources.Load<Material>( "cornfield" );
	}

	static public Resource Create()
	{
		GameObject obj = new GameObject();
		return obj.AddComponent<Resource>();
	}

	public Resource Setup( GroundNode node, Type type, int charges = 1 )
	{
		if ( node.building || node.flag || node.resource || node.type != GroundNode.Type.grass )
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
		if ( type == Type.cornfield )
		{
			body = GameObject.CreatePrimitive( PrimitiveType.Capsule );
			body.transform.localScale = new Vector3( 0.5f, 0, 0.5f );
			body.GetComponent<MeshRenderer>().material = cornfieldMaterial;
			name = "Cornfield";
		}
		if ( body != null )
		{
			body.transform.SetParent( transform );
			body.transform.localPosition = Vector3.zero;
		}
		Assert.IsNotNull( body );
	}

	void Update()
	{
		if ( type == Type.cornfield )
		{
			float growth = (float)this.growth / Workshop.cornfieldGrowthMax;
			body.transform.localScale = new Vector3( 0.5f, growth / 2, 0.5f );
		}
	}

	void FixedUpdate()
	{
		growth++;
		keepAwayTimer--;
	}

	static public Item.Type ItemType( Type type )
	{
		switch ( type )
		{
			case Type.tree:
				return Item.Type.log;
			case Type.rock:
				return Item.Type.stone;
			case Type.fish:
				return Item.Type.fish;
			case Type.cornfield:
				return Item.Type.grain;
			default:
				return Item.Type.unknown;
		}
	}

	public void Remove()
	{
		Destroy( gameObject );
	}

	public void UpdateBody()
	{
		transform.localPosition = node.Position();
	}

	public void Validate()
	{
		//Assert.IsNotNull( body );
	}
}
