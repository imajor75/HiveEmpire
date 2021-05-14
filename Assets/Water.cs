using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent( typeof( MeshFilter ), typeof( MeshRenderer ) )]
public class Water : MonoBehaviour
{
    public Ground ground;
    public Mesh mesh;
    public Material material;
    static int offset0ID, offset1ID, iterID;

    public static void Initialize()
    {
        offset0ID = Shader.PropertyToID( "Offset0" );
        offset1ID = Shader.PropertyToID( "Offset1" );
        iterID = Shader.PropertyToID( "Iter" );
    }

    public static Water Create()
    {
        return new GameObject().AddComponent<Water>();
    }

    public Water Setup( Ground ground )
    {
        this.ground = ground;
        return this;
    }

    void Start()
    {
        name = "Water";
        transform.SetParent( ground.transform.parent );
        mesh = GetComponent<MeshFilter>().mesh = new Mesh();
        material = GetComponent<MeshRenderer>().material = Resources.Load<Material>( "Water" );
    }

    void Update()
    {
        if ( mesh.triangles.Length != ground.dimension * ground.dimension * 3 * 2 )
        {
            var vertices = new List<Vector3>();
            var heights = new List<Vector2>();
            foreach ( var node in ground.nodes )
            {
                var position = node.position;
                position.y = 0;
                vertices.Add( position );
                heights.Add( new Vector2( ground.world.waterLevel - node.height, 0 ) );
            }
            mesh.vertices = vertices.ToArray();
            mesh.uv = heights.ToArray();

            var indices = new List<int>();
            for ( int x = 0; x < ground.dimension; x++ )
            {
                for ( int y = 0; y < ground.dimension; y++ )
                {
                    indices.Add( ( x + 0 ) + ( y + 0 ) * ( ground.dimension + 1 ) );
                    indices.Add( ( x + 0 ) + ( y + 1 ) * ( ground.dimension + 1 ) );
                    indices.Add( ( x + 1 ) + ( y + 0 ) * ( ground.dimension + 1 ) );
                    indices.Add( ( x + 1 ) + ( y + 0 ) * ( ground.dimension + 1 ) );
                    indices.Add( ( x + 0 ) + ( y + 1 ) * ( ground.dimension + 1 ) );
                    indices.Add( ( x + 1 ) + ( y + 1 ) * ( ground.dimension + 1 ) );
                }
            }
            mesh.triangles = indices.ToArray();
        }
        var t = ( ground.world.time % 400 ) * 0.005f;
        material.SetFloat( offset0ID, (float)Math.Sin( t ) );
        material.SetFloat( offset1ID, (float)Math.Cos( t ) );
        material.SetFloat( iterID, (float)( t - Math.Floor( t ) ) );
        
		var overseas = World.instance.overseas + 1;
		for ( int x = -overseas; x <= overseas; x++ )
		{
			for ( int y = -overseas; y <= overseas; y++ )
			{
				if ( x == 0 && y == 0 )
					continue;

				Graphics.DrawMesh( mesh, new Vector3( ( x + (float)y / 2 )* ground.dimension * GroundNode.size, 0, y * ground.dimension * GroundNode.size ) + transform.position, Quaternion.identity, material, 0 );
            }
        }
    }
}
