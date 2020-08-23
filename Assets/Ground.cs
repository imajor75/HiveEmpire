using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.EventSystems;

public class GroundNode
{
    public static float size = 1;
    public static int neighbourCount = 6;
    public int x, y;
    //Building building;
    public Flag flag;
    public Road road;
    public Road[] startingHere = new Road[neighbourCount];
    public GroundNode[] neighbours = new GroundNode[neighbourCount];
    public float height = 0;
    public Vector3 Position()
    {
        Vector3 position = new Vector3( x*size+y*size/2, height, y*size );
        return position;
    }
    public int IsAdjacentTo( GroundNode another )
    {
        int direction = -1;
        for ( int i = 0; i < 6; i++ )
            if ( neighbours[i] == another )
                direction = i;
        return direction;
    }
    public void Validate()
    {
        Assert.IsTrue( flag == null || road == null, "Both flag and road at the same node (" + x + ", " + y );
        for ( int i = 0; i < 6; i++ )
            Assert.AreEqual( this, neighbours[i].neighbours[( i + 3 ) % 6] );
        for ( int j = 0; j < 6; j++ )
            if ( startingHere[j] )
                startingHere[j].Validate();
        if ( flag )
            flag.Validate();
    }
}

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
        transform = (Transform)gameObject.GetComponent(typeof(Transform));
        Assert.IsNotNull(transform);
        collider = (MeshCollider)gameObject.GetComponent(typeof(MeshCollider));
        Assert.IsNotNull(collider);
        currentNode = GameObject.Find("CurrentNode");
        Assert.IsNotNull(currentNode);

        mesh = /*collider.sharedMesh = */meshFilter.mesh = new Mesh();
        mesh.name = "GroundMesh";

        layout = new GroundNode[(width+1)*(height+1)];
        for ( int x = 0; x <= width; x++ )
        {
            for ( int y = 0; y <= height; y++ )
            {
                var node = layout[y*(width+1)+x] = new GroundNode();
                node.x = x;
                node.y = y;
            }
        }
        for ( int x = 0; x <= width; x++ )
        {
            for (int y = 0; y <= height; y++)
            {
                var node = GetNode( x, y );
                node.neighbours[0] = GetNode( x + 0, y - 1 );
                node.neighbours[1] = GetNode( x + 1, y - 1 );
                node.neighbours[2] = GetNode( x + 1, y + 0 );
                node.neighbours[3] = GetNode( x + 0, y + 1 );
                node.neighbours[4] = GetNode( x - 1, y + 1 );
                node.neighbours[5] = GetNode( x - 1, y + 0 );
                //for ( int i = 0; i < 6; i++ )
                  //  Assert.IsNotNull( node.neighbours[i] );
            }
        }
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
        if (Input.GetMouseButtonDown(0))
        {
            UnityEngine.Debug.Log("Down!");
            layout[60].height = GroundNode.size * 10;
            layoutVersion++;
        }
    }

    GroundNode GetNode( int x, int y )
    {
        if ( x < 0 )
            x += width+1;
        if ( y < 0 )
            y += height+1;
        if ( x > width )
            x -= width + 1;
        if ( y > height )
            y -= height + 1;
        Assert.IsNotNull( layout[y * ( width + 1 ) + x] );
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
            //UnityEngine.Debug.Log( "mouse: " + row + " - " + column );
        }
        //else
            //UnityEngine.Debug.Log("nohit");
    }

    void CheckUserInput()
    {
        var currentNode = GetNode(currentColumn, currentRow);
        if (Input.GetKeyDown(KeyCode.F))
            Flag.CreateNew(this, currentNode);
        if ( Input.GetKeyDown( KeyCode.R ) )
            Road.AddNodeToNew( this, currentNode );
        if ( Input.GetKeyDown( KeyCode.V ) )
        {
            Validate();
            UnityEngine.Debug.Log( "Validated" );
        }
    }

    void UpdateMesh()
    {
        UnityEngine.Debug.Log("UpdateMesh");
        if ( mesh == null )
            return;

        if ( mesh.vertices == null || mesh.vertices.Length == 0 )
        {
            UnityEngine.Debug.Log("Generating mesh");

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
