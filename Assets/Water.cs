using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent( typeof( MeshFilter ), typeof( MeshRenderer ) )]
public class Water : HiveObject
{
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
        base.Setup();
        return this;
    }

    override public void Register()
    {
    }

    new void Start()
    {
        name = "Water";
        transform.SetParent( ground.transform.parent );
		transform.localPosition = Vector3.up * game.waterLevel;
        mesh = GetComponent<MeshFilter>().mesh = new ();
        material = GetComponent<MeshRenderer>().material = Resources.Load<Material>( "Water" );
        gameObject.layer = World.layerIndexWater;
        base.Start();
    }

    new void Update()
    {
        Assert.global.AreEqual( this, game.water );
        if ( mesh.triangles.Length != ground.dimension * ground.dimension * 3 * 2 )
        {
            var vertices = new List<Vector3>();
            var heights = new List<Vector2>();
            for ( int y = 0; y <= ground.dimension; y++ )
            {
                for ( int x = 0; x <= ground.dimension; x++ )
                {
                    Vector3 position = new ();
                    position.x = ground.n00x + ( ground.n10x - ground.n00x ) * x + ( ground.n01x - ground.n00x ) * y;
                    position.y = 0;
                    position.z = ground.n00y + ( ground.n10y - ground.n00y ) * x + ( ground.n01y - ground.n00y ) * y;
                    vertices.Add( position );
                    heights.Add( new Vector2( game.waterLevel - ground.GetNode( x, y ).height, 0 ) );
                }
            }
            Assert.global.AreEqual( vertices.Count, heights.Count );
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
        var t = ( time % 800 ) * 0.005f;
        material.SetFloat( offset0ID, (float)Math.Sin( t * Math.PI / 2 )    );
        material.SetFloat( offset1ID, (float)Math.Cos( t * Math.PI / 2 ) );
        material.SetFloat( iterID, (float)( t - Math.Floor( t ) ) );
        
        base.Update();
    }
}
