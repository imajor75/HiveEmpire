using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
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
    //Connection connection;
    //Connection startingHere[neighbourCount];
    public GroundNode[] neighbours = new GroundNode[neighbourCount];
    public float height = 0;
    public Vector3 Position()
    {
        Vector3 position = new Vector3( x*size+y*size/2, height, y*size );
        return position;
    }
}

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class Ground : MonoBehaviour
{
    int width = 10, height = 10;
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
        UnityEngine.Debug.Log( "Helllo world!");
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
        for (int x = 0; x <= width; x++)
        {
            for (int y = 0; y <= height; y++)
            {
                GetNode(x, y).neighbours[0] = GetNode(x + 0, y - 1);
                GetNode(x, y).neighbours[1] = GetNode(x + 1, y - 1);
                GetNode(x, y).neighbours[2] = GetNode(x - 1, y + 0);
                GetNode(x, y).neighbours[3] = GetNode(x + 1, y + 0);
                GetNode(x, y).neighbours[4] = GetNode(x - 1, y + 1);
                GetNode(x, y).neighbours[5] = GetNode(x + 0, y + 1);
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
        if (x < 0) x += width;
        if (y < 0) y += height;
        if (x >= width) x -= width;
        if (y >= height) y -= height;
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
        if ( Input.GetKeyDown(KeyCode.F))
        {
            Flag.CreateNew(this, GetNode(currentColumn, currentRow));
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
    void OnDrawGizmos()
    {
        var n = GetNode(currentColumn, currentRow);
        var localPosition = n.Position();
        var worldPosition = transform.TransformPoint(localPosition);

        Gizmos.DrawSphere(GetNode(currentColumn, currentRow).Position(), 1);
    }
}
