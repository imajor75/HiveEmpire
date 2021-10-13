using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.Types;
#pragma warning disable 0618

public class Network : HiveCommon
{
	public enum State
	{
		receivingGameState,
		client,
		server
	}

	public class Task
	{
		public enum Result
		{
			done,
			needModeTime,
			unknown
		}

		public enum Type
		{
			sendFile,
			sendPacket
		}

		public Task( string fileName, int connection )
		{
			type = Type.sendFile;
			name = fileName;
			sent = 0;
			this.connection = connection;
			reader = new BinaryReader( File.Open( fileName, FileMode.Open ) );
		}

		public Task( List<byte> packet, int connection )
		{
			type = Type.sendPacket;
			this.packet = packet;
			this.connection = connection;
		}

		public Result Progress()
		{
			byte error;
			switch ( type )
			{
				case Type.sendFile:
				reader.BaseStream.Seek( sent, SeekOrigin.Begin );
				var bytes = reader.ReadBytes( Constants.Network.bufferSize );
				while ( bytes.Length > 0 )
				{
					NetworkTransport.Send( network.host, connection, reliableChannel, bytes, bytes.Length, out error );
					if ( error != 0 )
						return Result.needModeTime;
					sent += bytes.Length;
					bytes = reader.ReadBytes( Constants.Network.bufferSize );
				}
				Log( $"File {name} sent" );
				return Result.done;

				case Type.sendPacket:
				NetworkTransport.Send( network.host, connection, reliableChannel, packet.ToArray(), packet.Count, out error );
				return error == 0 ? Result.done : Result.needModeTime;

				default:
				return Result.unknown;
			}
		}

		public int sent;
		public BinaryReader reader;
		int connection;
		string name;
		public Type type;
		public List<byte> packet;
	}

	public List<Task> tasks = new List<Task>();
	public State state;

	public long gameStateSize, gameStateWritten;
	public string gameStateFile;
	public BinaryWriter gameState;
	public static int reliableChannel;
	public static HostTopology hostTopology;

	public int host;
	public int clientConnection;
	public List<int> serverConnections = new List<int>();
	byte[] buffer = new byte[Constants.Network.bufferSize];

    public static Network Create()
    {
        return new GameObject( "Network" ).AddComponent<Network>();
    }

    public static void Initialize()
    {
		GlobalConfig g = new GlobalConfig();
		g.MaxPacketSize = 50000;
		NetworkTransport.Init( g );
		ConnectionConfig config = new ConnectionConfig();
		reliableChannel = config.AddChannel( QosType.ReliableSequenced );
		hostTopology = new HostTopology( config, 10 );
    }

    void Start()
    {
		var port = GetAvailablePort();
		host = NetworkTransport.AddHost( hostTopology, port );
		Assert.global.IsTrue( host >= 0 );
		Log( $"Ready for connections at port {port}", true );
        
    }

    void Update()
    {
        while ( Receive() != NetworkEventType.Nothing );

		List<Task> finishedTasks = new List<Task>();
		foreach ( var task in tasks )
		{
			if ( task.Progress() == Task.Result.done )
			finishedTasks.Add( task );
		}
		foreach ( var task in finishedTasks )
			tasks.Remove( task );
    }

    NetworkEventType Receive()
    {
    	int host, connection, channel, receivedSize;
		byte error;
		NetworkEventType recData = NetworkTransport.Receive( out host, out connection, out channel, buffer, buffer.Length, out receivedSize, out error );
		switch( recData )
		{
			case NetworkEventType.Nothing:
			break;
			case NetworkEventType.ConnectEvent:
			{
				if ( clientConnection == connection )
				{
					Log( $"Connected to server" );
					break;
				}
				else
				{
					int port;
                    NetworkID network;
					NodeID dstNode;
					string clientAddress;
					NetworkTransport.GetConnectionInfo( host, connection, out clientAddress, out port, out network, out dstNode, out error );
					Log( $"Incoming connection from {clientAddress}" );
					serverConnections.Add( connection );
					string fileName = System.IO.Path.GetTempFileName();
					world.Save( fileName, false, true );
					FileInfo fi = new FileInfo( fileName );
					byte[] size = BitConverter.GetBytes( fi.Length );
					NetworkTransport.Send( host, connection, reliableChannel, size, size.Length, out error );
					tasks.Add( new Task( fileName, connection ) );
					Log( $"Sending game state to {clientAddress} ({fi.Length} bytes)" );
					break;
				}
			}
			case NetworkEventType.DisconnectEvent:
			{
				Log( $"Client disconnected with ID {connection}" );
				serverConnections.Remove( connection );
				break;
			}
			case NetworkEventType.DataEvent:
			{
				if ( state == State.receivingGameState )
				{
					int start = 0;
					if ( gameStateSize == -1 )
					{
						int longSize = BitConverter.GetBytes( gameStateSize ).Length;	// TODO there must be a better way to do this
						byte[] fileSize = new byte[longSize];
						for ( int i = 0; i < longSize; i++ )
							fileSize[i] = buffer[i];
						gameStateSize = BitConverter.ToInt64( fileSize, 0 );
						Log( $"Size of game state: {gameStateSize}" );
						start += longSize;
					}
					int gameStateBytes = receivedSize - start;
					if ( gameStateWritten + gameStateBytes > gameStateSize )
						gameStateBytes = (int)( gameStateSize - gameStateWritten );
					if ( gameStateBytes > 0 )
					{
						gameState.Write( buffer, start, gameStateBytes );
						gameStateWritten += gameStateBytes;
						Assert.global.IsFalse( gameStateWritten > gameStateSize );
						if ( gameStateWritten == gameStateSize )
						{
							gameState.Close();
							Log( $"Game state received to {gameStateFile}" );
							world.Load( gameStateFile );
							state = State.client;
						}
					}
				}

				break;
			}
			default:
			Log( $"Network event occured: {recData}", true );
			break;
		}
        return recData;
    }

    public void OnGameFrameEnd()
    {
        if ( state == State.server )
        {
            List<byte> endOfFramePacket = new List<byte>();
            endOfFramePacket.Add( oh.currentCRCCode ).Add( time );
            foreach ( var connection in serverConnections )
                tasks.Add( new Task( endOfFramePacket, connection ) );
        }
    }

    public void Join( string address, int port )
    {
		byte error;	
		clientConnection = NetworkTransport.Connect( host, address, port, 0, out error );
		state = State.receivingGameState;
		gameStateSize = -1;
		gameStateWritten = 0;
		gameStateFile = System.IO.Path.GetTempFileName();
		gameState = new BinaryWriter( File.Create( gameStateFile ) );
    }
    
	public static int GetAvailablePort( int startingPort = Constants.Network.defaultPort )
	{
		IPEndPoint[] endPoints;
		List<int> portArray = new List<int>();

		IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();

		//getting active connections
		TcpConnectionInformation[] connections = properties.GetActiveTcpConnections();
		portArray.AddRange(from n in connections
							where n.LocalEndPoint.Port >= startingPort
							select n.LocalEndPoint.Port);

		//getting active tcp listners - WCF service listening in tcp
		endPoints = properties.GetActiveTcpListeners();
		portArray.AddRange(from n in endPoints
							where n.Port >= startingPort
							select n.Port);

		//getting active udp listeners
		endPoints = properties.GetActiveUdpListeners();
		portArray.AddRange(from n in endPoints
							where n.Port >= startingPort
							select n.Port);

		portArray.Sort();

		for (int i = startingPort; i < UInt16.MaxValue; i++)
			if (!portArray.Contains(i))
				return i;

		return 0;
	}
}
