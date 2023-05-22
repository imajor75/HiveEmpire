using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

public struct MediaTable<MediaType, Key> where MediaType : UnityEngine.Object
{
	public class Media
	{
		public string fileName;
		public MediaType data;
		public float floatData;
		public int intData;
		public bool boolData;
		public List<Key> keys = new ();
		public MediaTable<MediaType, Key> boss;

		public void Load( string prefix, string file = "" )
		{
			bool reportError = false;
			if ( file == "" )
			{
				file = this.fileName;
				reportError = true;
			}
			data = Resources.Load<MediaType>( prefix + file );
			if ( data == null && boss.missingMediaHandler != null )
				data = boss.missingMediaHandler( keys.First() );
			if ( data == null )
			{
				if ( reportError )
					Assert.global.Fail( "Resource " + prefix + file + " not found" );
				else
					HiveCommon.Log( $"Failed to load resource {prefix+file} of type {typeof(MediaType)}" );
			}
		}
	}

	List<Media> table;
	Media failure;
	bool autoExpand;
	public Func<Key, string> fileNameGenerator;
	public string fileNamePrefix;
	public Func<Key, MediaType> missingMediaHandler;

	public void Fill( object[] data = null, bool autoExpand = true )
	{
		if ( fileNameGenerator == null )
			fileNameGenerator = ( key ) => key.ToString();

		this.autoExpand = autoExpand;
		if ( data == null )
			return;
		table = new ();
		foreach ( var g in data )
		{
			if ( g is string file )
				table.Add( new Media { boss = this, fileName = file } );
			if ( g == null )
			{
				table.Add( new Media { boss = this } );	// why is this needed?
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
			if ( l.fileName != null )
				l.Load( fileNamePrefix );
		}
	}

	public Media GetMedia( Key key, int randomNumber = -1 )
	{
		if ( table == null )
			table = new ();
		List<Media> candidates = new ();
		foreach ( Media media in table )
			if ( media.keys.Contains( key ) )
				candidates.Add( media );
             
		if ( candidates.Count == 0 )
		{
			if ( failure != null )
				return failure;

			if ( !autoExpand )
				return null;

			var media = new Media { boss = this };
			media.keys.Add( key );
			media.Load( fileNamePrefix, fileNameGenerator( key ) );
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
