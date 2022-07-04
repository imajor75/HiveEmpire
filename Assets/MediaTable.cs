using System.Collections.Generic;
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

		public void Load( string prefix, string file = "" )
		{
			bool reportError = false;
			if ( file == "" )
			{
				file = this.file;
				reportError = true;
			}
			data = Resources.Load<MediaType>( prefix + file );
			if ( data == null && reportError )
				Assert.global.Fail( "Resource " + prefix + file + " not found" );
		}
	}

	List<Media> table;
	Media failure;
	public string fileNamePrefix;

	public void Fill( object[] data )
	{
		table = new List<Media>();
		foreach ( var g in data )
		{
			if ( g is string file )
				table.Add( new Media { file = file } );
			if ( g == null )
			{
				table.Add( new Media() );
				continue;
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
			if ( l.file != null )
				l.Load( fileNamePrefix );
		}
	}

	public Media GetMedia( Key key, int randomNumber = -1 )
	{
		if ( table == null )
			table = new List<Media>();
		List<Media> candidates = new List<Media>();
		foreach ( Media media in table )
			if ( media.keys.Contains( key ) )
				candidates.Add( media );
             
		if ( candidates.Count == 0 )
		{
			if ( failure != null )
				return failure;

			Media media = new Media();
			media.Load( fileNamePrefix, key.ToString() );
			media.keys.Add( key );
			table.Add( media );
			return media;
		}
		if ( candidates.Count == 1 )
			return candidates[0];
		
		Assert.global.AreNotEqual( randomNumber, -1 );
		return candidates[randomNumber % candidates.Count];
	}

	public MediaType GetMediaData( Key key, int randomNumber = -1 )
	{
		return GetMedia( key, randomNumber )?.data;
	}
}
