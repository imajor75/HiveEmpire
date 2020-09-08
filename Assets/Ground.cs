using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Assertions;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class Ground : MonoBehaviour
{
    public int width = 50, height = 50;
	public GroundNode[] layout;
    public int layoutVersion = 1;
	[JsonIgnore]
	public int currentRow, currentColumn;
	[JsonIgnore]
	public GameObject currentNode;
	[JsonIgnore]
	public GroundNode selectedNode;
    public int meshVersion = 0;
	[JsonIgnore]
	public Mesh mesh;
	[JsonIgnore]
	public new MeshCollider collider;
	[JsonIgnore]
	public Stock mainBuilding;

	public static Ground Create()
	{
		var groundObject = new GameObject();
		return groundObject.AddComponent<Ground>();
	}

	void Start()
    {
		gameObject.name = "Ground";
		width = 50;
		height = 30;
        MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();
        collider = gameObject.GetComponent<MeshCollider>();

		currentNode = GameObject.CreatePrimitive( PrimitiveType.Cube );
		currentNode.name = "Cursor";
		currentNode.GetComponent<MeshRenderer>().material = Resources.Load<Material>( "Cursor" );
		currentNode.transform.localScale *= 0.25f;
		currentNode.transform.SetParent( transform );


		GetComponent<MeshRenderer>().material = Resources.Load<Material>( "GroundMaterial" );

		mesh = meshFilter.mesh = new Mesh();
        mesh.name = "GroundMesh";

		if ( layout == null )
			layout = new GroundNode[( width + 1 ) * ( height + 1 )];
		FinishLayout();

		if ( mainBuilding == null )
		{
			mainBuilding = Stock.Create();
			mainBuilding.SetupMain( this, GetNode( width / 2, height / 2 ) );
		}
    }

	public void FinishLayout()
	{
		for ( int x = 0; x <= width; x++ )
		{
			for ( int y = 0; y <= height; y++ )
			{
				if ( layout[y * ( width + 1 ) + x] == null )
					layout[y * ( width + 1 ) + x] = new GroundNode();
			}
		}
		for ( int x = 0; x <= width; x++ )
			for ( int y = 0; y <= height; y++ )
				GetNode( x, y ).Initialize( this, x, y );

		var t = Resources.Load<Texture2D>( "heightMap" );
		foreach ( var n in layout )
		{
			Vector3 p = n.Position();
			n.height = t.GetPixel( (int)( p.x / GroundNode.size / width * 400 + 200 ), (int)( p.z / GroundNode.size / height * 400 + 200 ) ).g * GroundNode.size * 2;
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
        return layout[y * (width + 1) + x];
    }

	public void SetNode( int x, int y, GroundNode node )
	{
		if ( layout == null )
			layout = new GroundNode[( width + 1 ) * ( height + 1 )];

		layout[y * ( width + 1 ) + x] = node;
	}
	void CheckMouse()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        var size = GroundNode.size;
        if (collider.Raycast(ray, out hit, size * (width + height)))
        {
            Vector3 localPosition = transform.InverseTransformPoint(hit.point);
            var node = GroundNode.FromPosition( localPosition, this );
            currentColumn = node.x;
            currentRow = node.y;
            currentNode.transform.localPosition = node.Position();
        }
    }

	void CheckUserInput()
	{
		var currentNode = GetNode(currentColumn, currentRow);
		if ( Input.GetKeyDown( KeyCode.F ) )
		{
			Flag flag = Flag.Create();
			if ( !flag.Setup( this, currentNode ) )
				Destroy( flag );
		};
		if ( Input.GetKeyDown( KeyCode.R ) )
			Road.AddNodeToNew( this, currentNode );
		if ( Input.GetKeyDown( KeyCode.B ) )
		{
			var w = Workshop.Create();
			if ( w.Setup( this, currentNode ) )
				w.SetType( Workshop.Type.woodcutter );
			else
				Destroy( w );
		}
		if ( Input.GetKeyDown( KeyCode.V ) )
		{
			var w = Workshop.Create();
			if ( w.Setup( this, currentNode ) )
				w.SetType( Workshop.Type.sawmill );
			else
				Destroy( w );
		}
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
			selectedNode = currentNode;
		if ( Input.GetKeyDown( KeyCode.K ) )
		{
			if ( currentNode.road )
				currentNode.road.Remove();
		}

	}

	void UpdateMesh()
    {
        if ( mesh == null )
            return;

        if ( mesh.vertices == null || mesh.vertices.Length == 0 )
        {
            var vertices = new Vector3[(width+1)*(height+1)];
            for ( int i = 0; i < (width+1)*(height+1); i++ )
            {
                vertices[i] = new Vector3();
                vertices[i] = layout[i].Position();
            }
            mesh.vertices = vertices;

            var triangles = new int[width*height*2*3];
            for ( int x = 0; x < width; x++ )
            {
                for ( int y = 0; y < height; y++ )
                {
                    var i = (y*width+x)*2*3;
                    triangles[i+0] = (y+0)*(width+1)+(x+0);
                    triangles[i+1] = (y+1)*(width+1)+(x+0);
                    triangles[i+2] = (y+0)*(width+1)+(x+1);
                    triangles[i+3] = (y+0)*(width+1)+(x+1);
                    triangles[i+4] = (y+1)*(width+1)+(x+0);
                    triangles[i+5] = (y+1)*(width+1)+(x+1);
                }
            }
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            collider.sharedMesh = mesh;
        }
        else
        {
            var vertices = mesh.vertices;
            for (int i = 0; i < (width + 1) * (height + 1); i++)
                vertices[i] = layout[i].Position();
            mesh.vertices = vertices;
            collider.sharedMesh = mesh;
        }
    }
    public void Validate()
    {
        Assert.IsTrue( width > 0 && height > 0, "Map size is not correct (" + width + ", " + height );
        Assert.AreEqual( ( width + 1 ) * ( height + 1 ), layout.Length, "Map layout size is incorrect" );
        foreach ( var node in layout )
            node.Validate();
    }
}