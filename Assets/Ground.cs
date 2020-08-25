using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.EventSystems;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class Ground : MonoBehaviour
{
    public int width = 10, height = 10;
    GroundNode[] layout;
    int layoutVersion = 1;
    int currentRow, currentColumn;
    GameObject currentNode;

    int meshVersion = 0;
    Mesh mesh;
    new Transform transform;
    new MeshCollider collider;

    // Start is called before the first frame update
    void Start()
    {
        MeshFilter meshFilter = (MeshFilter)gameObject.GetComponent(typeof(MeshFilter));
        transform = (Transform)gameObject.GetComponent( typeof( Transform ) );
        Assert.IsNotNull( transform );
        collider = (MeshCollider)gameObject.GetComponent( typeof( MeshCollider ) );
        Assert.IsNotNull( collider );
        currentNode = GameObject.Find( "CurrentNode" );
        Assert.IsNotNull( currentNode );
        Road.material = Resources.Load<Material>( "Road" );
        Assert.IsNotNull( Road.material );
        Building.prefab = (GameObject)Resources.Load( "house" );
        Assert.IsNotNull( Building.prefab );

        mesh = /*collider.sharedMesh = */meshFilter.mesh = new Mesh();
        mesh.name = "GroundMesh";

        layout = new GroundNode[( width + 1 ) * ( height + 1 )];
        for ( int x = 0; x <= width; x++ )
            for ( int y = 0; y <= height; y++ )
                layout[y * ( width + 1 ) + x] = new GroundNode();
        for ( int x = 0; x <= width; x++ )
            for ( int y = 0; y <= height; y++ )
                GetNode( x, y ).Initialize( this, x, y );

        Building.SetupMain( this, GetNode( width / 2, height / 2 ) );
    }

    // Update is called once per frame
        void Update()
    {
        if (layoutVersion != meshVersion)
        {
            UpdateMesh();
            meshVersion = layoutVersion;
        }
        CheckMouse();
        CheckUserInput();
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

    void CheckMouse()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        var size = GroundNode.size;
        if (collider.Raycast(ray, out hit, size * (width + height)))
        {
            Vector3 localPosition = transform.InverseTransformPoint(hit.point);
            currentRow = (int)((localPosition.z + (size / 2)) / size);
            currentColumn = (int)((localPosition.x - currentRow * size / 2 + (size / 2)) / size);
            currentNode.transform.localPosition = GetNode(currentColumn, currentRow).Position();
        }
    }

    void CheckUserInput()
    {
        var currentNode = GetNode(currentColumn, currentRow);
        if ( Input.GetKeyDown( KeyCode.F ) )
            Flag.CreateNew( this, currentNode );
        if ( Input.GetKeyDown( KeyCode.R ) )
            Road.AddNodeToNew( this, currentNode );
        if ( Input.GetKeyDown( KeyCode.V ) )
        {
            Validate();
            Debug.Log( "Validated" );
        }
        if ( Input.GetKeyDown( KeyCode.B ) )
            Building.CreateNew( this, currentNode );
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
