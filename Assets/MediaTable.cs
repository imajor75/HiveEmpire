using System.Collections;
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
	}

	List<Media> table;

	public void Fill( object[] data )
	{
		table = new List<Media>();
		foreach ( var g in data )
		{
			string file = g as string;
			if ( file != null )
			{
				Media media = new Media();
				media.file = file;
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
			l.data = Resources.Load<MediaType>( l.file );
			if ( l.data == null )
			{
				Debug.Log( "Resource " + l.file + " not found" );
				continue;
			}
		}
	}

	public Media GetMedia( Key key )
	{
		List<Media> candidates = new List<Media>();
		foreach ( Media media in table )
			if ( media.keys.Contains( key ) )
				candidates.Add( media );

		if ( candidates.Count == 0 )
			return null;
		if ( candidates.Count == 1 )
			return candidates[0];
		return candidates[World.rnd.Next( candidates.Count )];
	}

	public MediaType GetMediaData( Key key )
	{
		return GetMedia( key )?.data;
	}
}
