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
	static Material highlightMaterial;
	static Material smoothMaterial;

	public static Eye Create()
	{
		var eyeObject = new GameObject();
		Eye eye = eyeObject.AddComponent<Eye>();
		return eye;
	}

	public static void Initialize()
	{
		highlightMaterial = new Material( Resources.Load<Shader>( "Highlight" ) );
		smoothMaterial = new Material( Resources.Load<Shader>( "Smooth" ) );
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

	//Texture2D tex;
	//[SerializeField]
	//bool debug = true;
	void OnRenderImage( RenderTexture src, RenderTexture dst )
	{
		if ( Interface.instance.highlightType == Interface.HighlightType.none )
		{
			Graphics.Blit( src, dst );
			return;
		}
		//if ( !debug || Time.time == 0 )
		//{
		//	Graphics.Blit( src, dst );
		//	return;
		//}
		//debug = false;

		//if ( !mat )
		//{
		//	mat = new Material( Resources.Load<Shader>( "Highlight" ) );
		//	//mat = new Material( Shader.Find( "Unlit/Texture" ) );
		//	//tex = Resources.Load<Texture2D>( "coin" );
		//}

		var tempRT = RenderTexture.GetTemporary( src.width, src.height, 24 );

		//SaveRT( src, "src" );
		/*
		SaveRT( null, "null" );
		SaveRT( RenderTexture.active, "active" );*/

		//ManualBlit( tex, src, mat );
		Graphics.Blit( src, tempRT, smoothMaterial );
		Graphics.Blit( tempRT, src, highlightMaterial );
		Graphics.Blit( src, dst );

		//SaveRT( src, "src_after" );
		//SaveRT( dst, "dst" );
		//SaveRT( tempRT, "temp" );

		//Graphics.Blit( tex, tempRT, mat );		
		//Graphics.SetRenderTarget( tempRT.colorBuffer, src.depthBuffer );
		//ManualBlit( src, tempRT, mat );
		//Graphics.Blit( src, tempRT, mat );

		//SaveRT( null, "null_after" );
		//SaveRT( RenderTexture.active, "active_after" );
		//SaveRT( tempRT, "temp" );


		//Graphics.SetRenderTarget( temp.colorBuffer, src.depthBuffer );
		//Graphics.Blit( src, temp, mat );
		//Graphics.Blit( temp, dst );
		//RenderTexture.ReleaseTemporary( temp );

		RenderTexture.ReleaseTemporary( tempRT );
	}

	void ManualBlit( Texture source, RenderTexture target, Material material )
	{
		//var prevRT = RenderTexture.active;
		//RenderTexture.active = target;
		Graphics.SetRenderTarget( target );
		material.mainTexture = source;

		GL.PushMatrix();
		GL.LoadOrtho();

		// activate the first shader pass (in this case we know it is the only pass)
		bool b = material.SetPass( 0 );
		print( "SetPass: " + b );
		// draw a quad over whole screen
		GL.Begin( GL.QUADS );
		GL.TexCoord2( 0f, 0f ); GL.Vertex3( 0.2f, 0.2f, 0f );
		GL.TexCoord2( 0f, 1f ); GL.Vertex3( 0.2f, 0.8f, 0f );
		GL.TexCoord2( 1f, 1f ); GL.Vertex3( 0.8f, 0.8f, 0f );
		GL.TexCoord2( 1f, 0f );	GL.Vertex3( 0.8f, 0.2f, 0f );
		GL.End();
		GL.PopMatrix();
		//RenderTexture.active = prevRT;
	}

	[JsonIgnore]
	public Texture2D saveTexture;
	public void SaveRT( RenderTexture texture, string fileName = "test" )
	{
		var prevRT = RenderTexture.active;
		RenderTexture.active = texture;
		int x = Screen.width, y = Screen.height;
		if ( texture != null )
		{
			x = texture.width;
			y = texture.height;
		}
		saveTexture = new Texture2D( x, y );
		saveTexture.ReadPixels( new Rect( 0, 0, x, y ), 0, 0 );

		//for ( int xx = 0; xx < saveTexture.width; xx++ )
		//{
		//	for ( int yy = 0; yy < saveTexture.height; yy++ )
		//	{
		//		var c = saveTexture.GetPixel( xx, yy );
		//		if ( c.a != 1 )
		//		{
		//			int h = 7;
		//		}
		//		if ( xx < 300 )
		//		{
		//			c.a = 1;
		//			saveTexture.SetPixel( xx, yy, c );
		//		}
		//	}
		//}
		//saveTexture.Apply();

		System.IO.File.WriteAllBytes( fileName + ".png", saveTexture.EncodeToPNG() );
		System.IO.File.WriteAllBytes( fileName + ".jpg", saveTexture.EncodeToJPG() );
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
