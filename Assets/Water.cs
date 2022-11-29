using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent( typeof( MeshFilter ), typeof( MeshRenderer ) )]
public class Water : HiveObject
{
    public Mesh mesh;
    public new Ground ground;
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
        base.Setup( ground.world );
        PrepareRendering();
        return this;
    }

    override public void Register()
    {
    }

    new void Start()
    {
        PrepareRendering();
        base.Start();
    }

    void PrepareRendering()
    {
        if ( transform.parent )
            return;

        name = "Water";
        transform.SetParent( ground.transform.parent, false );
		transform.localPosition = Vector3.up * game.waterLevel;
        mesh = GetComponent<MeshFilter>().mesh = new ();
        material = GetComponent<MeshRenderer>().material = Instantiate( Resources.Load<Material>( "Water" ) );
        material.SetInt( "Waves", 1 );
        gameObject.layer = World.layerIndexWater;
        UpdateMesh();

        if ( world.generatorSettings.reliefSettings.island )
        {
            var cloneMaterial = Instantiate( Resources.Load<Material>( "Water" ) );
            float worldSize = world.ground.dimension * Constants.Node.size;
            for ( int i = 0; i < 25; i++ )
            {
                if ( i == 12 )
                    continue;

                var mf = new GameObject( "Water clone" ).AddComponent<MeshFilter>();
                mf.gameObject.layer = World.layerIndexWater;
                mf.mesh = mesh;
                mf.gameObject.AddComponent<MeshRenderer>().material = cloneMaterial;
                mf.transform.SetParent( transform );
                int x = ( i % 5 ) - 2;
                int y = ( i / 5 ) - 2;
                mf.transform.localPosition = new Vector3( x * worldSize + ( y * worldSize / 2 ), 0, y * worldSize );
            }
        }
    }

    new void Update()
    {
        UpdateMesh();
        var t = ( time % 800 ) * 0.005f;
        Shader.SetGlobalFloat( offset0ID, (float)Math.Sin( t * Math.PI / 2 ) );
        Shader.SetGlobalFloat( offset1ID, (float)Math.Cos( t * Math.PI / 2 ) );
        Shader.SetGlobalFloat( iterID, (float)( t - Math.Floor( t ) ) );
        base.Update();
    }

    void UpdateMesh()
    {
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
    }
}
