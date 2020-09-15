using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SocialPlatforms.GameCenter;

[RequireComponent( typeof( MeshFilter ), typeof( MeshRenderer ), typeof( MeshCollider ) )]
public class Ground : MonoBehaviour
{
	[JsonIgnore]
	public float speedModifier = 1;
	public int width = 50, height = 50;
	public GroundNode[] nodes;
	public int layoutVersion = 1;
	[JsonIgnore]
	public int currentRow, currentColumn;
	[JsonIgnore]
	public GameObject cursor;
	[JsonIgnore]
	public GroundNode selectedNode;
	public int meshVersion = 0;
	[JsonIgnore]
	public Mesh mesh;
	[JsonIgnore]
	public new MeshCollider collider;
	public Stock mainBuilding;
	public GroundNode zero;
	public List<Building> influencers = new List<Building>();
	static public System.Random rnd = new System.Random( 2 );
	int reservedCount, reservationCount;
	static int maxArea = 10;
	public static List<Offset>[] areas = new List<Offset>[maxArea];

	public static Ground Create()
	{
		var groundObject = new GameObject();
		return groundObject.AddComponent<Ground>();
	}

	public class Offset
	{
		public Offset( int x, int y )
		{
			this.x = x;
			this.y = y;
		}
		public int x;
		public int y;
	}

	void Start()
	{
		if ( zero == null )
			zero = Worker.zero;
		else
			Worker.zero = zero;
		gameObject.name = "Ground";

		MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();
		collider = gameObject.GetComponent<MeshCollider>();
		mesh = meshFilter.mesh = new Mesh();
		mesh.name = "GroundMesh";
		GetComponent<MeshRenderer>().material = Resources.Load<Material>( "GroundMaterial" );

		mesh = meshFilter.mesh = new Mesh();
		mesh.name = "GroundMesh";
		cursor = GameObject.CreatePrimitive( PrimitiveType.Cube );
		cursor.name = "Cursor";
		cursor.GetComponent<MeshRenderer>().material = Resources.Load<Material>( "Cursor" );
		cursor.transform.localScale *= 0.25f;
		cursor.transform.SetParent( transform );
	}

	public Ground Setup()
	{
		gameObject.name = "Ground";
		width = 50;
		height = 50;

		if ( nodes == null )
			nodes = new GroundNode[( width + 1 ) * ( height + 1 )];
		FinishLayout();
		SetHeights();
		CreateAreas();

		Player mainPlayer = GameObject.FindObjectOfType<Mission>().mainPlayer;
		GroundNode center = GetNode( 28, 19 );
		if ( mainBuilding == null )
		{
			mainBuilding = Stock.Create();
			mainBuilding.SetupMain( this, center, mainPlayer );
		}

		Camera.main.transform.position = transform.TransformPoint( center.Position() ) - new Vector3( 0, -4, 8 );

		GenerateResources();
		return this;
    }

	void CreateAreas()
	{
		for ( int i = 0; i < areas.Length; i++ )
			areas[i] = new List<Offset>();

		GroundNode center = GetNode( width/2, height/2 );
		for ( int x = -maxArea; x < maxArea; x++ )
		{
			for ( int y = -maxArea; y < maxArea; y++ )
			{
				if ( x == 0 && y == 0 )
					continue;
				int distance = GetNode( width / 2 + x, height / 2 + y ).DistanceFrom( center );
				for ( int j = 1; j < maxArea; j++ )
				{
					if ( distance > j )
						continue;
					areas[j].Add( new Offset( x, y ) );
				}
			}
		}
		int nodeCount = 0;
		for ( int i = 0; i < areas.Length; i++ )
		{
			Assert.AreEqual( areas[i].Count, nodeCount );
			nodeCount += ( i + 1 ) * 6;
		}
	}

	public void GenerateResources()
	{
		foreach ( var node in nodes )
		{
			int i = rnd.Next( 1000 );
			if ( i < 4 )
				node.AddResourcePatch( Resource.Type.tree, 7, 0.5f );
			if ( i >= 5 && i < 6 )
				node.AddResourcePatch( Resource.Type.rock, 5, 0.5f );
		}
	}

	public void FinishLayout()
	{
		for ( int x = 0; x <= width; x++ )
		{
			for ( int y = 0; y <= height; y++ )
			{
				if ( nodes[y * ( width + 1 ) + x] == null )
					nodes[y * ( width + 1 ) + x] = new GroundNode();
			}
		}
		for ( int x = 0; x <= width; x++ )
			for ( int y = 0; y <= height; y++ )
				GetNode( x, y ).Initialize( this, x, y );
	}

	public void SetHeights()
	{
		var t = Resources.Load<Texture2D>( "height" );
		Color[] colors = t.GetPixels();
		foreach ( var n in nodes )
		{
			float d = colors[(t.width*n.x/(width+1))+(t.height*n.y/(height+1))*t.width].g;
			n.height = d*10;
			if ( d > 0.35f )
				n.type = GroundNode.Type.hill;
			if ( d > 0.5f )
				n.type = GroundNode.Type.mountain;
		}
	}

	void Update()
	{
		if ( layoutVersion != meshVersion || mesh.vertexCount == 0 )
		{
			UpdateMesh();
			meshVersion = layoutVersion;
		}
		CheckMouse();
		CheckUserInput();
	}

	void LateUpdate()
	{
		Validate();
	}

	public GroundNode GetNode( int x, int y )
	{
		if ( x < 0 )
			x += width + 1;
		if ( y < 0 )
			y += height + 1;
		if ( x > width )
			x -= width + 1;
		if ( y > height )
			y -= height + 1;
		return nodes[y * ( width + 1 ) + x];
	}

	public void SetNode( int x, int y, GroundNode node )
	{
		if ( nodes == null )
			nodes = new GroundNode[( width + 1 ) * ( height + 1 )];

		nodes[y * ( width + 1 ) + x] = node;
	}
	void CheckMouse()
	{
		Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
		RaycastHit hit;
		var size = GroundNode.size;
		if ( collider.Raycast( ray, out hit, size * ( width + height ) ) )
		{
			Vector3 localPosition = transform.InverseTransformPoint(hit.point);
			var node = GroundNode.FromPosition( localPosition, this );
			currentColumn = node.x;
			currentRow = node.y;
			cursor.transform.localPosition = node.Position();
		}
	}

	void CheckUserInput()
	{
		Player player = GameObject.FindObjectOfType<Mission>().mainPlayer;
		var currentNode = GetNode(currentColumn, currentRow);
		if ( Input.GetKey( KeyCode.Z ) )
			speedModifier = 5;
		else
			speedModifier = 1;
		if ( Input.GetKeyDown( KeyCode.F ) )
		{
			Flag flag = Flag.Create();
			if ( !flag.Setup( this, currentNode, player ) )
				Destroy( flag );
		};
		if ( Input.GetKeyDown( KeyCode.R ) )
			Road.AddNodeToNew( this, currentNode, player );
		if ( Input.GetKeyDown( KeyCode.B ) )
			Workshop.Create().Setup( this, currentNode, player, Workshop.Type.woodcutter );
		if ( Input.GetKeyDown( KeyCode.V ) )
			Workshop.Create().Setup( this, currentNode, player, Workshop.Type.sawmill );
		if ( Input.GetMouseButtonDown( 0 ) )
		{
			if ( currentNode.building )
				currentNode.building.OnClicked();
			if ( currentNode.flag )
				currentNode.flag.OnClicked();
			if ( currentNode.road )
				currentNode.road.OnClicked();
		}
		if ( Input.GetKeyDown( KeyCode.O ) )
		{
			selectedNode = currentNode;
			Debug.Log( "Current pos: " + currentNode.x + ", " + currentNode.y );
			Debug.Log( "Distance from main building: " + currentNode.DistanceFrom( mainBuilding.node ) );
		}
		if ( Input.GetKeyDown( KeyCode.K ) )
		{
			if ( currentNode.road )
				currentNode.road.Remove();
			if ( currentNode.building )
				currentNode.building.Remove();
			if ( currentNode.flag )
				currentNode.flag.Remove();
		}

	}

	void UpdateMesh()
	{
		if ( mesh == null )
			return;

		if ( mesh.vertices == null || mesh.vertices.Length == 0 )
		{
			var vertices = new Vector3[(width+1)*(height+1)];
			var uvs = new Vector2[(width+1)*(height+1)];
			var colors = new Color[(width+1)*(height+1)];

			for ( int i = 0; i < ( width + 1 ) * ( height + 1 ); i++ )
			{
				var p = nodes[i].Position();
				vertices[i] = p;
				uvs[i] = new Vector2( p.x, p.z );
				switch ( nodes[i].type )
				{
					case GroundNode.Type.grass:
					{
						colors[i] = Color.red;
						break;
					}
					case GroundNode.Type.hill:
					{
						colors[i] = Color.green;
						break;
					}
					case GroundNode.Type.mountain:
					{
						colors[i] = Color.blue;
						break;
					}
				}
			}
			mesh.vertices = vertices;
			mesh.uv = uvs;
			mesh.colors = colors;

			var triangles = new int[width*height*2*3];
			for ( int x = 0; x < width; x++ )
			{
				for ( int y = 0; y < height; y++ )
				{
					var i = (y*width+x)*2*3;
					triangles[i + 0] = ( y + 0 ) * ( width + 1 ) + ( x + 0 );
					triangles[i + 1] = ( y + 1 ) * ( width + 1 ) + ( x + 0 );
					triangles[i + 2] = ( y + 0 ) * ( width + 1 ) + ( x + 1 );
					triangles[i + 3] = ( y + 0 ) * ( width + 1 ) + ( x + 1 );
					triangles[i + 4] = ( y + 1 ) * ( width + 1 ) + ( x + 0 );
					triangles[i + 5] = ( y + 1 ) * ( width + 1 ) + ( x + 1 );
				}
			}
			mesh.triangles = triangles;
			mesh.RecalculateNormals();
			collider.sharedMesh = mesh;
		}
		else
		{
			var vertices = mesh.vertices;
			for ( int i = 0; i < ( width + 1 ) * ( height + 1 ); i++ )
				vertices[i] = nodes[i].Position();
			mesh.vertices = vertices;
			collider.sharedMesh = mesh;
		}
	}

	class InfluenceChange
	{
		GroundNode node;
		int newValue;
	}

	public void RegisterInfluence( Building building )
	{
		influencers.Add( building );
		RecalculateOwnership();
	}

	public void UnregisterInfuence( Building building )
	{
		influencers.Remove( building );
		RecalculateOwnership();
	}

	void RecalculateOwnership()
	{
		foreach ( var n in nodes )
		{
			n.owner = null;
			n.influence = 0;
		}

		foreach ( var building in influencers )
		{
			List<GroundNode> touched = new List<GroundNode>();
			touched.Add( building.node );
			for ( int i = 0; i < touched.Count; i++ )
			{
				int influence = building.Influence( touched[i] );
				if ( influence <= 0 )
					continue;
				if ( touched[i].influence < influence )
				{
					touched[i].influence = influence;
					touched[i].owner = building.owner;
				}
				for ( int j = 0; j < GroundNode.neighbourCount; j++ )
				{
					GroundNode neighbour = touched[i].Neighbour( j );
					if ( neighbour.index >= 0 && neighbour.index < touched.Count && touched[neighbour.index] == neighbour )
						continue;
					neighbour.index = touched.Count;
					touched.Add( neighbour );
				}
			}
		}

		foreach ( var node in nodes )
		{
			for ( int j = 0; j < GroundNode.neighbourCount; j++ )
			{
				GroundNode neighbour = node.Neighbour( j );
				if ( node.owner == neighbour.owner )
				{
					if ( node.borders[j] )
					{
						Destroy( node.borders[j].gameObject );
						node.borders[j] = null;
					}
				}
				else
				{
					if ( node.owner != null )
						node.borders[j] = BorderEdge.Create().Setup( node, j );
				}
			}
		}
	}

    public void Validate()
 	{
		reservationCount = reservedCount = 0;
        Assert.IsTrue( width > 0 && height > 0, "Map size is not correct (" + width + ", " + height );
        Assert.AreEqual( ( width + 1 ) * ( height + 1 ), nodes.Length, "Map layout size is incorrect" );
        foreach ( var node in nodes )
            node.Validate();
		Assert.AreEqual( reservedCount, reservationCount, "Reservation numbers are wrong" );
    }
}