using Newtonsoft.Json;
using System;
using UnityEngine;

[RequireComponent( typeof( Camera ) )]
public class Eye : MonoBehaviour
{
	public float altitude = 4.0f;
	public float targetAltitude = 4.0f;
	static public float minAltitude = 2.0f;
	static public float maxAltitude = 15.0f;
	[JsonIgnore]
	public float viewDistance = 5.0f;
	public World world;
	public float x, y;
	public float direction;
	public new Camera camera;
	[JsonIgnore]
	public IDirector director;
	Transform ear;

	public static Eye Create()
	{
		var eyeObject = new GameObject();
		Eye eye = eyeObject.AddComponent<Eye>();
		return eye;
	}

	public Eye Setup( World world )
	{
		this.world = world;
		return this;
	}

	void Start()
	{
		name = "Eye";
		camera = GetComponent<Camera>();
		camera.cullingMask &= int.MaxValue - ( 1 << World.layerIndexMapOnly );
		transform.SetParent( World.instance.transform );

		ear = new GameObject().transform;
		ear.gameObject.AddComponent<AudioListener>();
		ear.name = "Ear";
		ear.transform.SetParent( World.instance.transform );
	}

	public Material mat;
	public Texture2D tex;
	void OnRenderImage( RenderTexture src, RenderTexture dst )
	{
		if ( !mat )
		{
			//mat = new Material( Resources.Load<Shader>( "Highlight" ) );
			mat = new Material( Shader.Find( "Unlit/Texture" ) );
			tex = Resources.Load<Texture2D>( "coins" );
		}

		//ManualBlit( tex, mat );
		SaveRT( src, "src.png" );
		SaveRT( null, "null.png" );
		SaveRT( RenderTexture.active, "active.png" );

		//Graphics.SetRenderTarget( temp.colorBuffer, src.depthBuffer );
		//Graphics.Blit( src, temp, mat );
		//Graphics.Blit( temp, dst );
		//RenderTexture.ReleaseTemporary( temp );

		Application.Quit();
	}

	void ManualBlit( Texture texture, Material material )
	{
		material.mainTexture = texture;

		GL.PushMatrix();
		GL.LoadOrtho();

		// activate the first shader pass (in this case we know it is the only pass)
		material.SetPass( 0 );
		// draw a quad over whole screen
		GL.Begin( GL.QUADS );
		GL.Vertex3( 0, 0, 0 );
		GL.Vertex3( 1, 0, 0 );
		GL.Vertex3( 1, 1, 0 );
		GL.Vertex3( 0, 1, 0 );
		GL.End();
	}

	static public void SaveRT( RenderTexture texture, string fileName = "test.png" )
	{
		var prevRT = RenderTexture.active;
		RenderTexture.active = texture;
		int x = Screen.width, y = Screen.height;
		if ( texture != null )
		{
			x = texture.width;
			y = texture.height;
		}
		Texture2D texture2D = new Texture2D( x, y );
		texture2D.ReadPixels( new Rect( 0, 0, x, y ), 0, 0 );
		System.IO.File.WriteAllBytes( fileName, texture2D.EncodeToPNG() );
		RenderTexture.active = prevRT;
	}

	private void Update()
	{
		Ray ray = new Ray( new Vector3( x, GroundNode.size * 50, y ), Vector3.down );
		RaycastHit hit;
		if ( world.ground.collider.Raycast( ray, out hit, GroundNode.size * 100 ) )
		{
			Vector3 position = hit.point;
			if ( position.y < World.instance.waterLevel * World.instance.maxHeight )
				position.y = World.instance.waterLevel * World.instance.maxHeight;
			ear.position = position;
			Vector3 viewer = new Vector3( (float)( viewDistance*Math.Sin(direction) ), -altitude, (float)( viewDistance*Math.Cos(direction) ) );
			transform.position = position - viewer;
			transform.LookAt( ear );
		}
		if ( director == null )
		{
			director = null;
			viewDistance = 5;
		}
		else
		{
			viewDistance = 2;
			IDirector director = this.director;
			director.SetCameraTarget( this );
			this.director = director;
		}
	}

	public void GrabFocus( IDirector director )
	{
		this.director = director;
	}

	public void ReleaseFocus( IDirector director )
	{
		if ( this.director == director )
			this.director = null;
	}

	public void FocusOn( GroundNode node )
	{
		var p = node.Position();
		x = p.x;
		y = p.z;
		director = null;
	}

	public void FocusOn( Component component )
	{
		x = component.transform.position.x;
		y = component.transform.position.z;
		director = null;
	}

	void FixedUpdate()
	{
		Vector3 movement = new Vector3();
		if ( Input.GetKey( KeyCode.A ) )
		{
			movement += transform.right * -0.1f * altitude / 6;
			director = null;
		}
		if ( Input.GetKey( KeyCode.D ) )
		{
			movement += transform.right * 0.1f * altitude / 6;
			director = null;
		}
		if ( Input.GetKey( KeyCode.W ) )
		{
			movement += transform.forward * 0.13f * altitude / 6;
			director = null;
		}
		if ( Input.GetKey( KeyCode.S ) )
		{
			movement += transform.forward * -0.13f * altitude / 6;
			director = null;
		}
		x += movement.x;
		y += movement.z;

		if ( Input.GetKey( KeyCode.Q ) )
			direction += 0.03f;
		if ( Input.GetKey( KeyCode.E ) )
			direction -= 0.03f;
		if ( direction >= Math.PI * 2 )
			direction -= (float)Math.PI * 2;
		if ( direction < 0 )
			direction += (float)Math.PI * 2;

		if ( Input.GetKey( KeyCode.Z ) )
			targetAltitude *= 1.01f;
		if ( Input.GetKey( KeyCode.X ) )
			targetAltitude *= 0.99f;
		if ( Input.GetAxis( "Mouse ScrollWheel" ) < 0 )		// TODO Use something else instead of strings here
			targetAltitude += 1;	
		if ( Input.GetAxis( "Mouse ScrollWheel" ) > 0 )		
			targetAltitude -= 1;
		if ( targetAltitude < minAltitude )
			targetAltitude = minAltitude;
		if ( targetAltitude > maxAltitude )
			targetAltitude = maxAltitude;


		altitude += ( targetAltitude - altitude ) * 0.1f;
	}

	public interface IDirector
	{
		void SetCameraTarget( Eye eye );
	}
}
