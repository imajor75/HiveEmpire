using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.EventSystems;

public class GroundNode
{
    public static float size = 1;
    public int x, y;
    //Building building;
    //Flag flag;
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
        if (Input.GetMouseButtonDown(0))
        {
            UnityEngine.Debug.Log("Down!");
            layout[60].height = GroundNode.size * 10;
            layoutVersion++;
        }
    }

    void CheckMouse()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        var size = GroundNode.size;
        if (collider.Raycast(ray, out hit, size * (width + height)))
        {
            Vector3 localPosition = transform.InverseTransformPoint(hit.point);
            int row = (int)((localPosition.z - (size / 2)) / size);
            int column = (int)((localPosition.x - row * size / 2 - (size / 2)) / size);
            UnityEngine.Debug.Log( "mouse: " + row + " - " + column );
        }
        else
            UnityEngine.Debug.Log("nohit");
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
}
