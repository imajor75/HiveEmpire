using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using UnityEngine;
using Unity.Networking.Transport;
using Unity.Collections;

using UnityEngine.Events;

public class Network : NetworkDiscovery<DiscoveryBroadcastData, DiscoveryResponseData>
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
		public Client( NetworkConnection connection )
		{
			this.connection = connection;
		}
		public NetworkConnection connection;
		public List<Task> tasks = new ();
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

		public Task( Network boss, string fileName, NetworkConnection connection )
		{
			this.boss = boss;
			type = Type.sendFile;
			name = fileName;
			sent = 0;
			this.connection = connection;
			reader = new BinaryReader( File.Open( fileName, FileMode.Open ) );
		}

		public Task( Network boss, NativeArray<byte> packet, NetworkConnection connection )
		{
			this.boss = boss;
			type = Type.sendPacket;
			this.packet = packet;
			this.connection = connection;
		}

		public Result Progress()
		{
			switch ( type )
			{
				case Type.sendFile:
				reader.BaseStream.Seek( sent, SeekOrigin.Begin );
				var bytes = reader.ReadBytes( Constants.Network.bufferSize );
				while ( bytes.Length > 0 )
				{
					if ( boss.driver.BeginSend( boss.reliablePipeline, connection, out var writer ) != 0 )
						return Result.needModeTime;
					NativeArray<byte> nativeArray = new NativeArray<byte>( bytes, Allocator.Temp );
					writer.WriteBytes( nativeArray );
					nativeArray.Dispose();
					int sentNow = boss.driver.EndSend( writer );
					if ( sentNow == 0 )
						return Result.needModeTime;
					sent += sentNow;
					bytes = reader.ReadBytes( Constants.Network.bufferSize );
				}
				HiveCommon.Log( $"File {name} sent" );
				return Result.done;

				case Type.sendPacket:
				{
					boss.driver.BeginSend( boss.reliablePipeline, connection, out var writer );
					writer.WriteBytes( packet );
					int sentPacket = boss.driver.EndSend( writer );
					Assert.global.IsTrue( sentPacket == 0 || sentPacket == packet.Length );
					return sentPacket == packet.Length ? Result.done : Result.needModeTime;
				}

				default:
				return Result.unknown;
			}
		}

		public int sent;
		public BinaryReader reader;
		NetworkConnection connection;
		string name;
		public Type type;
		public NativeArray<byte> packet;
		public Network boss;
	}

	public State state;

	public void Awake()
	{
		driver = NetworkDriver.Create();
		var endPoint = NetworkEndPoint.AnyIpv4;
		endPoint.Port = Constants.Network.defaultPort;
		var bindResult = driver.Bind( endPoint );
		if ( bindResult != 0 )
			HiveCommon.Log( $"Failed to bind network interface to {endPoint} due to error {(Unity.Networking.Transport.Error.StatusCode)bindResult}" );
		reliablePipeline = driver.CreatePipeline( typeof( ReliableSequencedPipelineStage ) );		

		if ( active )
		{
			Assert.global.AreNotEqual( active, this );
			active.Remove();
		}
		active = this;
 	}

	public void Remove()
	{
		driver.Dispose();
		Destroy( gameObject );
		if ( active == this )
			active = null;
	}

	public void SetState( State state )
	{
		if ( state == this.state )
			return;

		this.state = state;
	}

    protected override bool ProcessBroadcast( IPEndPoint sender, DiscoveryBroadcastData broadCast, out DiscoveryResponseData response )
    {
        response = new DiscoveryResponseData()
        {
            ServerName = HiveCommon.game.name,
			Port = 5555,
        };
        return true;
    }

    protected override void ResponseReceived( IPEndPoint sender, DiscoveryResponseData response )
    {
		localDestinations.Add( new AvailableHost { address = sender.Address.ToString(), name = name, port = sender.Port } );
    }

	public long gameStateSize, gameStateWritten;
	public string gameStateFile;
	public string gameStateFileReady;
	public int gameStateFileReadyDelayer;
	public BinaryWriter gameState;
	public NetworkDriver driver;
	public NetworkPipeline reliablePipeline;
	public static Network active;

	public int id = 0, nextClientId = 1;
	public NetworkConnection clientConnection;
	public List<Client> serverConnections = new ();
	public List<AvailableHost> localDestinations = new ();
	public string serverName;
	public string password;

	public float lag;

	[Serializable]
	public class AvailableHost
	{
		public string address;
		public string name;
		public int port;
	}
	
    public static Network Create()
    {
        return new GameObject( "Network" ).AddComponent<Network>();
    }

    void Update()
    {
		driver.ScheduleUpdate().Complete();

		if ( state == State.server )
		{
			var newConnection = driver.Accept();
			if ( newConnection != default(NetworkConnection) )
			{
				var otherEnd = driver.RemoteEndPoint( newConnection );
				HiveCommon.Log( $"Incoming connection from {otherEnd.Address}" );
				var client = new Network.Client( newConnection );
				string fileName = System.IO.Path.GetTempFileName();
				HiveCommon.game.Save( fileName, false, true );
				FileInfo fi = new FileInfo( fileName );
				driver.BeginSend( reliablePipeline, newConnection, out var writer );
				writer.WriteInt( nextClientId++ );
				writer.WriteLong( fi.Length );
				driver.EndSend( writer );
				client.tasks.Add( new Task( this, fileName, newConnection ) );
				HiveCommon.Log( $"Sending game state to {driver.RemoteEndPoint( newConnection ).Address} ({fi.Length} bytes)" );
				serverConnections.Add( client );
			}
		}

        while ( Handle() && !HiveCommon.root.requestUpdate );

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
			HiveCommon.root.Load( gameStateFile );
			HiveCommon.root.mainPlayer = null;
			Interface.PlayerSelectorPanel.Create( true );
			SetState( State.client );
			gameStateFileReady = null;
		}
    }

	public bool StartServer( string name )
	{
		if ( state != State.server )
			return false;

		serverName = name;
		var listening = driver.Listen();
		if ( listening != 0 )
			HiveCommon.Log( $"Failed to start network listening on port {driver.LocalEndPoint().Port}:, error: {(Unity.Networking.Transport.Error.StatusCode)listening}", HiveCommon.Severity.error );
		else
			HiveCommon.Log( $"Listening on port {driver.LocalEndPoint().Port}" );
		return true;
	}

    bool Handle()
    {
		if ( state == State.prepare )
			return false;

		var nextEvent = driver.PopEvent( out var connection, out var receiver );
		switch( nextEvent )
		{
			case NetworkEvent.Type.Empty:
				return false;
			case NetworkEvent.Type.Connect:
			{
				if ( clientConnection == connection )
					HiveCommon.Log( $"Connected to server" );
				else
					HiveCommon.Log( $"Connected to {connection}" );
				break;
			}
			case NetworkEvent.Type.Disconnect:
			{
				switch ( state )
				{
					case State.server:
					HiveCommon.Log( $"Client {connection} disconnected" );
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
					HiveCommon.root.OpenMainMenu();
					Interface.MessagePanel.Create( "Failed to connect to server", autoclose:3 );
					break;
				
					case State.client:
					HiveCommon.Log( $"Server disconnected, switching to server mode and waiting for incoming connections", HiveCommon.Severity.important );
					SetState( State.server );
					break;
				}
				break;
			}
			case NetworkEvent.Type.Data:
			{
				switch ( state )
				{
					case State.receivingGameState:
					{
						if ( id == -1 )
						{
							id = receiver.ReadInt();
							HiveCommon.Log( $"Network ID: {id}" );
						}
						if ( gameStateSize == -1 )
						{
							gameStateSize = receiver.ReadLong();
							HiveCommon.Log( $"Size of game state: {gameStateSize}" );
						}
						var nativeArray = new NativeArray<byte>( receiver.Length - receiver.GetBytesRead(), Allocator.Temp );
						receiver.ReadBytes( nativeArray );
						gameState.Write( nativeArray.ToArray() );
						HiveCommon.Log( $"{nativeArray.Length} bytes written to {gameStateFile}" );
						gameStateWritten += nativeArray.Length;
						Assert.global.IsFalse( gameStateWritten > gameStateSize );
						Interface.MessagePanel.Create( $"Receiving game state from server {100*gameStateWritten/gameStateSize}%" );
						if ( gameStateWritten == gameStateSize )
						{
							gameState.Close();
							HiveCommon.Log( $"Game state received to {gameStateFile}" );
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
						if ( receiver.Length == 2 * sizeof( int ) )
						{
							var frameOrder = new OperationHandler.GameStepOrder();
							frameOrder.time = receiver.ReadInt();
							frameOrder.CRC = receiver.ReadInt();
							HiveCommon.oh.orders.AddLast( frameOrder );
							lag = (float)HiveCommon.oh.orders.Count / Constants.World.normalSpeedPerSecond;
						}
						else
						{
							var binForm = new BinaryFormatter();
							var memStream = new MemoryStream();
							var nativeArray = new NativeArray<byte>();
							receiver.ReadBytes( nativeArray );
							memStream.Write( nativeArray.ToArray() );
							memStream.Seek( 0, SeekOrigin.Begin );
							var o = binForm.Deserialize( memStream ) as Operation;
							o.source = Operation.Source.networkServer;
							Assert.global.AreEqual( memStream.Position, memStream.Length );
							HiveCommon.oh.executeBuffer.Add( o );
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
							var nativeArray = new NativeArray<byte>();
							receiver.ReadBytes( nativeArray );
							memStream.Write( nativeArray.ToArray() );
							memStream.Seek( 0, SeekOrigin.Begin );
							var binForm = new BinaryFormatter();
							o = binForm.Deserialize( memStream ) as Operation;
						}
						o.source = Operation.Source.networkClient;
						HiveCommon.oh.ScheduleOperation( o );
						break;
					}
				}
				break;
			}
			default:
			HiveCommon.Log( $"Network event occured: {state}", HiveCommon.Severity.warning );
			break;
		}
        return true;
    }

    public void OnBeginGameStep()
    {
        if ( state == State.server && serverConnections.Count != 0 )
        {
            var frameBeginPacket = new DataStreamWriter( 8, Allocator.Persistent );
            frameBeginPacket.WriteInt( HiveCommon.time );
			frameBeginPacket.WriteInt( HiveCommon.oh.currentCRCCode );

            foreach ( var client in serverConnections )
                client.tasks.Add( new Task( this, frameBeginPacket.AsNativeArray(), client.connection ) );
        }
    }

	public bool OnScheduleOperation( Operation operation )
	{
        Assert.global.AreNotEqual( state, State.receivingGameState );
	    if ( state != Network.State.client )
			return true;

		BinaryFormatter bf = new ();
		using ( var ms = new MemoryStream() )
		{
			bf.Serialize( ms, operation );
			driver.BeginSend( reliablePipeline, clientConnection, out var writer );
			var nativeArray = new NativeArray<byte>( ms.ToArray(), Allocator.Persistent );
			writer.WriteBytes( nativeArray );
			nativeArray.Dispose();
			driver.EndSend( writer );
		}
		return false;
    }

	public void OnExecuteOperation( Operation operation )
	{
		BinaryFormatter bf = new ();
		using ( var ms = new MemoryStream() )
		{
			bf.Serialize( ms, operation );
			using ( var na = new NativeArray<byte>( ms.ToArray(), Allocator.Persistent ) )
			{
				foreach ( var client in serverConnections )
					client.tasks.Add( new Task( this, na, client.connection ) );
			}
		}
	}			

    public bool Join( string address, int port )
    {
		clientConnection = driver.Connect( NetworkEndPoint.Parse( address, (ushort)port ) );
		if ( !clientConnection.IsCreated )
		{
			HiveCommon.Log( $"Failed to connect to {address}:{port}" );
			return false;
		}
		SetState( State.receivingGameState );
		id = -1;
		gameStateSize = -1;
		gameStateWritten = 0;
		gameStateFile = System.IO.Path.GetTempFileName();
		gameState = new BinaryWriter( File.Create( gameStateFile ) );
		return true;
    }
}