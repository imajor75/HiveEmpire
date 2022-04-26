using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.Serialization.Formatters.Binary;
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
		server,
		prepare
	}

	public class Client
	{
		public Client( int connection )
		{
			this.connection = connection;
		}
		public int connection;
		public List<Task> tasks = new List<Task>();
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
	public string gameStateFileReady;
	public int gameStateFileReadyDelayer;
	public BinaryWriter gameState;
	public static int reliableChannel;
	public static HostTopology hostTopology;

	public int port;
	public int host = -1;
	public int id = 0, nextClientId = 1;
	public int broadcastPort;
	public int broadcastHost;
	public int clientConnection;
	public List<Client> serverConnections = new List<Client>();
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
        while ( Receive() != NetworkEventType.Nothing && !root.requestUpdate );

		foreach ( var client in serverConnections )
		{
			while ( client.tasks.Count != 0 )
			{
				if ( client.tasks.First().Progress() == Task.Result.done )
					client.tasks.Remove( client.tasks.First() );
				else
					break;
			}
		}

		if ( gameStateFileReady != null && gameStateFileReadyDelayer-- < 0 )
		{
			root.Load( gameStateFile );
			root.mainPlayer = null;
			Interface.PlayerSelectorPanel.Create( true );
			SetState( State.client );
			gameStateFileReady = null;
		}
    }

    NetworkEventType Receive()
    {
		if ( state == State.prepare )
			return NetworkEventType.Nothing;

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
					var client = new Network.Client( connection );
					string fileName = System.IO.Path.GetTempFileName();
					world.Save( fileName, false, true );
					FileInfo fi = new FileInfo( fileName );
					byte[] id = BitConverter.GetBytes( nextClientId++ );
					NetworkTransport.Send( host, connection, reliableChannel, id, id.Length, out error );
					byte[] size = BitConverter.GetBytes( fi.Length );
					NetworkTransport.Send( host, connection, reliableChannel, size, size.Length, out error );
					client.tasks.Add( new Task( fileName, connection ) );
					Log( $"Sending game state to {clientAddress} ({fi.Length} bytes)" );
					serverConnections.Add( client );
					break;
				}
			}
			case NetworkEventType.DisconnectEvent:
			{
				switch ( state )
				{
					case State.server:
					Log( $"Client disconnected with ID {connection}" );
					foreach ( var client in serverConnections )
					{
						if ( client.connection == connection )
						{
							serverConnections.Remove( client );
							break;
						}
					}
					break;

					case State.receivingGameState:
					root.OpenMainPanel();
					break;
				
					case State.client:
					Log( $"Server disconnected, switching to server mode and waiting for incoming connections", true );
					SetState( State.server );
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
						if ( id == -1 )
						{
							Assert.global.AreEqual( receivedSize, BitConverter.GetBytes( id ).Length );
							id = BitConverter.ToInt32( buffer, 0 );
							Log( $"Network ID: {id}" );
							break;
						}
						if ( gameStateSize == -1 )
						{
							Assert.global.AreEqual( receivedSize, BitConverter.GetBytes( gameStateSize ).Length );
							gameStateSize = BitConverter.ToInt64( buffer, 0 );
							Log( $"Size of game state: {gameStateSize}" );
							break;
						}
						gameState.Write( buffer, 0, receivedSize );
						gameStateWritten += receivedSize;
						Assert.global.IsFalse( gameStateWritten > gameStateSize );
						Interface.MessagePanel.Create( $"Receiving game state from server {100*gameStateWritten/gameStateSize}%" );
						if ( gameStateWritten == gameStateSize )
						{
							gameState.Close();
							Log( $"Game state received to {gameStateFile}" );
							Interface.MessagePanel.Create( "Loading game state" );
							SetState( State.prepare );
							gameStateFileReady = gameStateFile;
							gameStateFileReadyDelayer = 2;	// TODO Not a nice thing here. We delay the loading of the file in order to be able to render a message box.
						}
						break;
					}
					case State.client:
					{
						Assert.global.AreEqual( clientConnection, connection );
						if ( receivedSize == 2 * sizeof( int ) )
						{
							var frameOrder = new OperationHandler.GameStepOrder();
							var bl = new List<byte>();
							for ( int i = 0; i < receivedSize; i++ )
								bl.Add( buffer[i] );
							bl.Extract( ref frameOrder.time ).Extract( ref frameOrder.CRC );
							Assert.global.AreEqual( bl.Count, 0 );
							oh.orders.AddLast( frameOrder );
							lag = (float)oh.orders.Count / Constants.World.normalSpeedPerSecond;
						}
						else
						{
							var binForm = new BinaryFormatter();
							var memStream = new MemoryStream();
							memStream.Write( buffer, 0, receivedSize );
							memStream.Seek( 0, SeekOrigin.Begin );
							var o = binForm.Deserialize( memStream ) as Operation;
							o.source = Operation.Source.networkServer;
							Assert.global.AreEqual( memStream.Position, memStream.Length );
							oh.executeBuffer.Add( o );
						}
						break;
					}
					case State.server:
					{
						bool clientRegistered = false;
						foreach( var client in serverConnections )
						{
							if ( client.connection == connection )
								clientRegistered = true;
						}
						Assert.global.IsTrue( clientRegistered );
						Operation o;
						using ( var memStream = new MemoryStream() )
						{
							memStream.Write( buffer, 0, receivedSize );
							memStream.Seek( 0, SeekOrigin.Begin );
							var binForm = new BinaryFormatter();
							o = binForm.Deserialize( memStream ) as Operation;
						}
						o.source = Operation.Source.networkClient;
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

    public void OnBeginGameStep()
    {
        if ( state == State.server && serverConnections.Count != 0 )
        {
            List<byte> frameBeginPacket = new List<byte>();
            frameBeginPacket.Add( time ).Add( oh.currentCRCCode );

            foreach ( var client in serverConnections )
                client.tasks.Add( new Task( frameBeginPacket, client.connection ) );
        }
    }

	public bool OnScheduleOperation( Operation operation )
	{
        Assert.global.AreNotEqual( state, State.receivingGameState );
	    if ( state != Network.State.client )
			return true;

		BinaryFormatter bf = new BinaryFormatter();
		var ms = new MemoryStream();
		bf.Serialize( ms, operation );
		byte error;
		NetworkTransport.Send( host, clientConnection, reliableChannel, ms.ToArray(), (int)ms.Length, out error );
		return false;
    }

	public void OnExecuteOperation( Operation operation )
	{
		BinaryFormatter bf = new BinaryFormatter();
		using (var ms = new MemoryStream())
		{
			bf.Serialize( ms, operation );
			foreach ( var client in serverConnections )
				client.tasks.Add( new Task( ms.ToArray().ToList(), client.connection ) );
		}
	}			

    public void Join( string address, int port )
    {
		byte error;	
		clientConnection = NetworkTransport.Connect( host, address, port, 0, out error );
		SetState( State.receivingGameState );
		id = -1;
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
