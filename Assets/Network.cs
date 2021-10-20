using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.Types;
#pragma warning disable 0618

public class Network : HiveCommon
{
	public enum State
	{
		idle,
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
	public State state { get; private set; }

	public void SetState( State state )
	{
		if ( state == this.state )
			return;

		byte error;
		this.state = state;
		if ( host < 0 )
			return;
		if ( state == State.server )
		{
			string message = System.Diagnostics.Process.GetCurrentProcess().Id.ToString();
			var buffer = Encoding.ASCII.GetBytes( message );
			NetworkTransport.StartBroadcastDiscovery( host, broadcastPort, 33, 44, 55, buffer, buffer.Length, 1000, out error );
			Assert.global.AreEqual( error, 0 );
			Log( $"Started network discovery on port {broadcastPort}" );
		}
		else
		{
			NetworkTransport.StopBroadcastDiscovery();
			Log( "Stopped network discovery" );
		}
	}

	public long gameStateSize, gameStateWritten;
	public string gameStateFile;
	public BinaryWriter gameState;
	public static int reliableChannel;
	public static HostTopology hostTopology;

	public int port;
	public int host = -1;
	public int broadcastPort;
	public int broadcastHost;
	public int clientConnection;
	public List<int> serverConnections = new List<int>();
	byte[] buffer = new byte[Constants.Network.bufferSize];
	public List<String> localDestinations = new List<string>();

	public float lag;
	
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

    void Awake()
    {
		port = GetAvailablePort( Constants.Network.defaultPort );
		host = NetworkTransport.AddHost( hostTopology, port );
		Assert.global.IsTrue( host >= 0 );
		Log( $"Ready for connections at port {port}", true );

		broadcastPort = Constants.Network.broadcastPort;
		if ( broadcastPort != GetAvailablePort( broadcastPort ) )
		{
			Log( $"Network broadcast port {broadcastPort} is not free, cannot do LAN discovery", true );
			return;
		}

		broadcastHost = NetworkTransport.AddHost( hostTopology, broadcastPort );
		Assert.global.IsTrue( broadcastHost >= 0 );
		byte error;
		NetworkTransport.SetBroadcastCredentials( broadcastHost, 33, 44, 55, out error );
		Assert.global.AreEqual( error, 0 );
    }

    void Update()
    {
        while ( Receive() != NetworkEventType.Nothing );

		List<Task> finishedTasks = new List<Task>();
		foreach ( var task in tasks )
		{
			if ( task.Progress() == Task.Result.done )
				finishedTasks.Add( task );
			else
				break;
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
			case NetworkEventType.BroadcastEvent:
			{
				int port;
				string address;
				byte[] buffer = new byte[100];
				NetworkTransport.GetBroadcastConnectionInfo( broadcastHost, out address, out port, out error );
				NetworkTransport.GetBroadcastConnectionMessage( broadcastHost, buffer, buffer.Length, out receivedSize, out error );
				string message = Encoding.ASCII.GetString( buffer, 0, receivedSize );
				if ( port == this.port && System.Diagnostics.Process.GetCurrentProcess().Id.ToString() == message )
					break;
				string ipV4Address = address.Split( ':' ).Last();
				string destination = ipV4Address + ":" + port.ToString();
				if ( !localDestinations.Contains( destination ) )
				{
					Log( $"Discovered {destination}" );
					localDestinations.Add( destination );
				}
			}
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
				switch ( state )
				{
					case State.server:
					Log( $"Client disconnected with ID {connection}" );
					serverConnections.Remove( connection );
					break;

					case State.receivingGameState:
					root.OpenMainPanel();
					break;
				
					case State.client:
					Log( $"Server disconnected, switching to server mode and waiting for incoming connectios", true );
					SetState( State.server );
					if ( oh.frameFinishPending )
					{
						oh.FinishFrame();
						world.SetSpeed( World.Speed.normal );
					}
					break;
				}
				break;
			}
			case NetworkEventType.DataEvent:
			{
				switch ( state )
				{
					case State.receivingGameState:
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
							Interface.status.SetText( this, "Receiving game state from server", pinX:0.5f, pinY:0.5f );
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
								Interface.status.SetText( this, "Loading game state", pinX:0.5f, pinY:0.5f );
								root.Load( gameStateFile );
								SetState( State.client );
							}
						}
						break;
					}
					case State.client:
					{
						Assert.global.AreEqual( clientConnection, connection );
						var frameOrder = new OperationHandler.FrameOrder();
						var bl = buffer.ToList();
						bl.Extract( ref frameOrder.time ).Extract( ref frameOrder.CRC );
						oh.orders.AddLast( frameOrder );
						lag = (float)oh.orders.Count / Constants.World.normalSpeedPerSecond;
						if ( oh.frameFinishPending )
						{
							if ( oh.FinishFrame() )
							{
								world.SetSpeed( World.Speed.normal );
								Log( $"Resuming execution at {time}" );
							}
							else
								Log( $"Resume failed at {time}" );
						}
						int operationCount = 0;
						bl.Extract( ref operationCount );
						for ( int i = 0; i < operationCount; i++ )
						{
							var o = Operation.Create();
							o.Fill( bl );
							oh.executeBuffer.Add( o );
						}
						Assert.global.AreEqual( buffer.Length - bl.Count, receivedSize );

						break;
					}
					case State.server:
					{
						Assert.global.IsTrue( serverConnections.Contains( connection ) );
						var o = Operation.Create();
						o.Fill( buffer.ToList() );
						oh.ScheduleOperation( o );
						break;
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
        if ( state == State.server && serverConnections.Count != 0 )
        {
            List<byte> endOfFramePacket = new List<byte>();
            endOfFramePacket.Add( time ).Add( oh.currentCRCCode );

			int operationCount = 0;
			while ( oh.executeBuffer.Count > oh.executeIndex + operationCount && oh.executeBuffer[oh.executeIndex + operationCount].scheduleAt == time )
				operationCount++;
			endOfFramePacket.Add( operationCount );

			for ( int i = 0; i < operationCount; i++ )
				oh.executeBuffer[oh.executeIndex+i].Pack( endOfFramePacket );
			
            foreach ( var connection in serverConnections )

                tasks.Add( new Task( endOfFramePacket, connection ) );
        }
    }

    public void Join( string address, int port )
    {
		byte error;	
		clientConnection = NetworkTransport.Connect( host, address, port, 0, out error );
		SetState( State.receivingGameState );
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
