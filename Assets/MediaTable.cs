using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public struct MediaTable<MediaType, Key> where MediaType : UnityEngine.Object
{
	public class Media
	{
		public string file;
		public MediaType data;
		public float floatData;
		public int intData;
		public bool boolData;
		public List<Key> keys = new List<Key>();

		public void Load( string file = "" )
		{
			bool reportError = false;
			if ( file == "" )
			{
				file = this.file;
				reportError = true;
			}
			if ( typeof( MediaType ) == typeof( Sprite ) )
			{
				var texture = Resources.Load<Texture2D>( file );
				data = Sprite.Create( texture, new Rect( 0.0f, 0.0f, texture.width, texture.height ), Vector2.zero ) as MediaType;
			}
			else
				data = Resources.Load<MediaType>( file );
			if ( data == null && reportError )
				Assert.global.Fail( "Resource " + file + " not found" );
		}
	}

	List<Media> table;
	Media failure;

	public void Fill( object[] data )
	{
		table = new List<Media>();
		foreach ( var g in data )
		{
			if ( g is string file )
			{
				Media media = new Media { file = file };
				table.Add( media );
			}
			if ( g.GetType() == typeof( Key ) )
				table[table.Count - 1].keys.Add( (Key)g );
			if ( g.GetType() == typeof( int ) )
				table[table.Count - 1].intData = (int)g;
			if ( g.GetType() == typeof( float ) )
				table[table.Count - 1].floatData = (float)g;
			if ( g.GetType() == typeof( bool ) )
				table[table.Count - 1].boolData = (bool)g;
		}
		foreach ( var l in table )
		{
			if ( l.keys.Count == 0 )
				failure = l;
			l.Load();
		}
	}

	public Media GetMedia( Key key )
	{
		List<Media> candidates = new List<Media>();
		foreach ( Media media in table )
			if ( media.keys.Contains( key ) )
				candidates.Add( media );

		if ( candidates.Count == 0 )
		{
			if ( failure != null )
				return failure;

			Media media = new Media();
			media.Load( key.ToString() );
			media.keys.Add( key );
			table.Add( media );
			return media;
		}
		if ( candidates.Count == 1 )
			return candidates[0];
		
		return candidates[World.rnd.Next( candidates.Count )];
	}

	public MediaType GetMediaData( Key key )
	{
		return GetMedia( key )?.data;
	}
}
