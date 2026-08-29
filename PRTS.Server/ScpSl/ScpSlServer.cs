using NiveraAPI.IO.Network.Entities;
using NiveraAPI.IO.Network.Entities.Attributes;

using NiveraAPI.IO.Serialization;

using NiveraAPI.Logs;

using PRTS.ScpSl.Discord;

namespace PRTS.ScpSl;

/// <summary>
/// Represents a connected SCP:SL server.
/// </summary>
[ClientType("PRTS.Client.PrtsClient")]
public class ScpSlServer : Entity
{
    [IndexField] private static ushort rpc_RpcInvokeCommand;
    [IndexField] private static ushort rpc_RpcRequestIdentity;

    private volatile byte players;
    private volatile byte maxPlayers;

    private volatile string serverIp = string.Empty;
    private volatile string serverName = string.Empty;
    private volatile string serverAlias = string.Empty;

    private volatile ushort serverPort = 0;

    /// <summary>
    /// The server's name.
    /// </summary>
    public string ServerName
    {
        get => serverName;
        set => serverName = value;
    }

    /// <summary>
    /// The server's alias. 
    /// </summary>
    public string ServerAlias
    {
        get => serverAlias;
        set => serverAlias = value;
    }

    /// <summary>
    /// The server's IP address.
    /// </summary>
    public string ServerIp
    {
        get => serverIp;
        set => serverIp = value;
    }

    /// <summary>
    /// The server's port number.
    /// </summary>
    public ushort ServerPort
    {
        get => serverPort;
        set => serverPort = value;
    }

    /// <summary>
    /// The current number of players on the server.
    /// </summary>
    public byte Players
    {
        get => players;
        set => players = value;
    }

    /// <summary>
    /// The maximum number of players allowed on the server.
    /// </summary>
    public byte MaxPlayers
    {
        get => maxPlayers;
        set => maxPlayers = value;
    }
    
    /// <summary>
    /// The Discord bot associated with the server.
    /// </summary>
    public ScpSlBot? DiscordBot { get; internal set; }

    /// <summary>
    /// Represents the logging utility associated with the server instance.
    /// Provides mechanisms for recording and monitoring server events,
    /// warnings, and debugging information.
    /// </summary>
    public LogSink Log { get; private set; }

    /// <summary>
    /// Called when the SCP:SL server is spawned in the system. This method initializes
    /// necessary resources for the server, including the server's logging setup.
    /// Override this method to implement additional initialization behavior for the server.
    /// </summary>
    public override void OnServerSpawned()
    {
        base.OnServerSpawned();
        
        Log = LogManager.GetSource("ScpSlServer", Manager.Connection.EndPoint.ToString());
        Log.Info("Spawned!");
    }

    /// <summary>
    /// Called when a client connection has been confirmed with the SCP:SL server.
    /// This method is typically used to trigger initial server-client synchronization or other
    /// setup operations that require confirmation of the client's connection status.
    /// Override this method to implement additional behavior upon client confirmation.
    /// </summary>
    public override void OnClientConfirmed()
    {
        base.OnClientConfirmed();
        
        Log.Info("Requesting server identity ...");
        
        CallRpcRequestIdentity();
    }

    /// <summary>
    /// Called when the SCP:SL server is destroyed. This method is used to clean up resources,
    /// perform final logging, and execute any necessary teardown logic for the server.
    /// Override this method to implement additional cleanup behavior for the server.
    /// </summary>
    public override void OnDestroyed()
    {
        base.OnDestroyed();
        
        DiscordBot?.OnServerDisconnected();
        
        Log.Info("Destroyed!");       
    }

    /// <summary>
    /// Sends a remote procedure call (RPC) to request the identity of the connected SCP:SL server.
    /// This method triggers a remote callback using the predefined RPC identifier
    /// to initiate the identity retrieval process.
    /// </summary>
    public void CallRpcRequestIdentity()
    {
        SendRemoteCallback(rpc_RpcRequestIdentity, default(byte[]));
    }

    /// <summary>
    /// Sends a remote procedure call (RPC) to the connected SCP:SL server with the specified command,
    /// and processes the server's response asynchronously through the provided callback action.
    /// </summary>
    /// <param name="command">The command string to be sent to the server. Must not be null or empty.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if the <paramref name="command"/> is null or empty, or if the <paramref name="callback"/> is null.
    /// </exception>
    public async Task<string> CallRpcInvokeCommandAsync(string command)
    {
        if (string.IsNullOrEmpty(command))
            throw new ArgumentNullException(nameof(command));
        
        var reader = await SendRemoteAsync(rpc_RpcInvokeCommand, writer => writer.WriteString(command));
        return reader.ReadString();   
    }

    /// <summary>
    /// Updates the current player count on the server and notifies the associated Discord bot
    /// to update its player count status.
    /// This method is called from the server to synchronize the player count between the server
    /// and the Discord bot.
    /// </summary>
    /// <param name="count">The updated number of players currently on the server.</param>
    [ServerCmd]
    public void CmdUpdatePlayerCount(byte count)
    {
        Players = count;
        
        Log.Info("CmdUpdatePlayerCount", $"Player count updated to &1{Players}&r!");
        
        DiscordBot?.UpdatePlayerCount(Players, MaxPlayers);
    }

    /// <summary>
    /// Processes server identification data sent from the client. This method reads
    /// the incoming data to assign server name, alias, address, and player count details.
    /// </summary>
    /// <param name="reader">The reader responsible for deserializing the incoming data stream.</param>
    /// <param name="_">The writer used for sending optional responses to the client, unused in this method.</param>
    [ServerCmd]
    public void CmdReceiveIdentity(ByteReader reader)
    {
        ServerName = reader.ReadString();
        ServerAlias = reader.ReadString();
        ServerIp = reader.ReadString();
        ServerPort = reader.ReadUInt16();
        Players = reader.ReadByte();
        MaxPlayers = reader.ReadByte();
        
        Log.Name = ServerAlias ?? Manager.Connection.EndPoint.ToString();
        Log.Info($"&1{Manager.Connection.EndPoint}&r identified as &1{ServerAlias}&r (&6{ServerIp}:{ServerPort}&r)!");

        if (string.IsNullOrEmpty(ServerAlias))
        {
            Log.Warn("Server alias is empty - Discord bot will NOT be connected!");
        }
        else
        {
            if (!ScpSlManager.Bots.TryGetValue(ServerAlias, out var bot)
                && (!PRTS.Discord.DiscordBot.TryGetBot(ServerAlias, out var discordBot)
                        || (bot = discordBot as ScpSlBot) == null))   
            {
                Log.Warn($"Server alias &1{ServerAlias}&r not found in Discord bot list!");
            }
            else
            {
                DiscordBot = bot;
                DiscordBot.Server = this;
                
                bot.OnServerConnected();
                bot.UpdatePlayerCount(Players, MaxPlayers);
                
                Log.Info("CmdReceiveIdentity", $"Bot &1{bot.BotAlias}&r connected to server &1{ServerAlias}&r!");
            }
        }
        
        Log.Info("CmdReceiveIdentity", "Spawning modules ...");

        foreach (var module in ScpSlManager.Modules)
        {
            try
            {
                Manager.SpawnEntity(module);
            }
            catch (Exception ex)
            {
                Log.Error("CmdReceiveIdentity", $"Failed to spawn module &1{module}&r:\n{ex}");
            }
        }
    }
}