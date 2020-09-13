using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Resource : MonoBehaviour
{
	public Type type;
	public int charges;
	public GameObject body;

	public enum Type
	{
		tree,
		rock,
		other
	}

	static public Resource Create()
	{
		GameObject obj = new GameObject();
		return obj.AddComponent<Resource>();
	}

	public Resource Setup( Type type, int charges = 1 )
	{
		this.type = type;
		this.charges = charges;
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
}
