using NiveraAPI.IO.Serialization;

using NiveraAPI.IO.Network.Entities;
using NiveraAPI.IO.Network.Entities.Attributes;

using NiveraAPI.Logs;

using PRTS.ScpSl.Discord;
using PRTS.ScpSl.Interfaces;

using System.Diagnostics;
using System.Collections.Concurrent;

using PRTS.ScpSl.Modules.Levels;
using PRTS.ScpSl.Modules.Reports;
using PRTS.ScpSl.Modules.Plugins;
using PRTS.ScpSl.Modules.Profiles;
using PRTS.ScpSl.Modules.Punishments;

using PRTS.ScpSl.Objects;

using PRTS.Levels;
using PRTS.Profiles;
using PRTS.Extensions;

namespace PRTS.ScpSl;

/// <summary>
/// Represents a connected SCP:SL server.
/// </summary>
[ClientType("PRTS.Client.PrtsClient")]
public class ScpSlServer : Entity, IScpSlLatencyProvider
{
    /// <summary>
    /// Event triggered when the server has been successfully identified, providing the identified <see cref="ScpSlServer"/> instance.
    /// </summary>
    public static event Action<ScpSlServer>? Identified;

    /// <summary>
    /// Event triggered when the server has been destroyed, providing the destroyed <see cref="ScpSlServer"/> instance.
    /// </summary>
    public static event Action<ScpSlServer>? Destroyed;

    private static volatile ConcurrentDictionary<string, ScpSlLatency> latencyProviders = new();

    [IndexField] private static ushort rpc_RpcInvokeCommand;
    [IndexField] private static ushort rpc_RpcRequestIdentity;

    [IndexField] private static ushort rpc_RpcRestart;
    [IndexField] private static ushort rpc_RpcShutdown;
    [IndexField] private static ushort rpc_RpcKick;

    internal volatile string endpoint;

    private bool latencyNotified = false;

    private Stopwatch latencySampleWatch = new();
    private Stopwatch latencyThresholdWatch = new();

    private volatile LevelModule levelModule;
    private volatile ReportModule reportModule;
    private volatile ProfileModule profileModule;
    private volatile PunishmentModule punishmentModule;
    private volatile PluginManagerModule pluginManagerModule;

    private volatile byte maxPlayers;

    private volatile string serverIp = string.Empty;
    private volatile string serverName = string.Empty;
    private volatile string serverAlias = string.Empty;

    private volatile ushort serverPort = 0;

    private volatile int tps = 0;

    /// <summary>
    /// Event triggered when high latency is detected, providing the current latency value in milliseconds.
    /// </summary>
    public event Action<int>? HighLatencyDetected;

    /// <summary>
    /// Event triggered when high latency is resolved, providing the current latency value in milliseconds.
    /// </summary>
    public event Action<int>? HighLatencyResolved;

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
    /// The maximum number of players allowed on the server.
    /// </summary>
    public byte MaxPlayers
    {
        get => maxPlayers;
        set => maxPlayers = value;
    }

    /// <summary>
    /// The current ticks per second (TPS) of the server, indicating its performance and responsiveness.
    /// </summary>
    public int Tps => tps;
    
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
    /// Represents the latency monitoring client associated with the server instance.
    /// </summary>
    public ScpSlLatency? LatencyMonitor { get; internal set; }

    /// <summary>
    /// Gets the level module associated with the server instance. If the level module is not already initialized, it attempts to retrieve it from the manager's entities.
    /// </summary>
    public LevelModule? LevelModule
    {
        get
        {
            if (levelModule != null)
                return levelModule;

            if (Manager.TryGetFirstEntity(out levelModule))
                return levelModule;

            return null;
        }
    }

    /// <summary>
    /// Gets the report module associated with the server instance. If the report module is not already initialized, it attempts to retrieve it from the manager's entities.
    /// </summary>
    public ReportModule? ReportModule
    {
        get
        {
            if (reportModule != null)
                return reportModule;

            if (Manager.TryGetFirstEntity(out reportModule))
                return reportModule;

            return null;
        }
    }

    /// <summary>
    /// Gets the profile module associated with the server instance. If the profile module is not already initialized, it attempts to retrieve it from the manager's entities.
    /// </summary>
    public ProfileModule? ProfileModule
    {
        get
        {
            if (profileModule != null)
                return profileModule;

            if (Manager.TryGetFirstEntity(out profileModule))
                return profileModule;

            return null;
        }
    }

    /// <summary>
    /// Gets the punishment module associated with the server instance. If the punishment module is not already initialized, it attempts to retrieve it from the manager's entities.
    /// </summary>
    public PunishmentModule? PunishmentModule
    {
        get
        {
            if (punishmentModule != null)
                return punishmentModule;

            if (Manager.TryGetFirstEntity(out punishmentModule))
                return punishmentModule;

            return null;
        }
    }

    /// <summary>
    /// Gets the plugin manager module associated with the server instance.
    /// </summary>
    public PluginManagerModule? PluginManagerModule
    {
        get
        {
            if (pluginManagerModule != null)
                return pluginManagerModule;

            if (Manager.TryGetFirstEntity(out pluginManagerModule))
                return pluginManagerModule;

            return null;
        }
    }

    /// <summary>
    /// Gets the latency provider for the server instance. If a latency monitor is available, it returns that; otherwise, it returns the server instance itself as the latency provider.
    /// </summary>
    public IScpSlLatencyProvider LatencyProvider
    {
        get
        {
            if (LatencyMonitor != null && LatencyMonitor.IsConnected)
                return LatencyMonitor;

            return this;
        }
    }

    /// <summary>
    /// Gets the current latency of the server connection in milliseconds, rounded up to the nearest integer.
    /// </summary>
    public int Latency => (int)Math.Ceiling(Manager?.Connection?.Ping?.Last ?? 0f);

    /// <summary>
    /// Gets the lowest recorded latency of the server connection in milliseconds, rounded up to the nearest integer.
    /// </summary>
    public int LowestLatency => (int)Math.Ceiling(Manager?.Connection?.Ping?.Lowest ?? 0f);

    /// <summary>
    /// Gets the highest recorded latency of the server connection in milliseconds, rounded up to the nearest integer.
    /// </summary>
    public int HighestLatency => (int)Math.Ceiling(Manager?.Connection?.Ping?.Highest ?? 0f);

    /// <summary>
    /// Gets the average latency of the server connection in milliseconds, rounded up to the nearest integer.
    /// </summary>
    public int AverageLatency => (int)Math.Ceiling(Manager?.Connection?.Ping?.Average ?? 0f);

    /// <summary>
    /// Gets a thread-safe collection of latency samples, where each entry consists of a timestamp and the corresponding latency value in milliseconds.
    /// </summary>
    public ConcurrentDictionary<DateTime, int> LatencySamples { get; } = new();

    /// <summary>
    /// Gets a thread-safe collection of players currently connected to the server, where each entry consists of a player's unique identifier and their corresponding player information.
    /// </summary>
    public volatile ConcurrentDictionary<string, PlayerInfo> Players = new();

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

        endpoint = Manager.Connection.EndPoint.ToString();
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
        
        Destroyed?.Invoke(this);

        DiscordBot?.OnServerDisconnected();
        
        Log.Info("Destroyed!");       
    }

    /// <summary>
    /// Called on each update cycle of the SCP:SL server. This method is responsible for updating the server's state, including latency monitoring and handling high latency events.
    /// </summary>
    /// <param name="localDeltaTime">The time elapsed since the last update cycle, in seconds.</param>
    /// <param name="networkDeltaTime">The time elapsed since the last network update cycle, in seconds.</param>
    public override void OnUpdate(float localDeltaTime, float networkDeltaTime)
    {
        base.OnUpdate(localDeltaTime, networkDeltaTime);

        if (LatencyMonitor == null || !LatencyMonitor.IsConnected)
        {
            if (ScpSlLatency.LatencySampleCount > 0)
            {
                if (!latencySampleWatch.IsRunning)
                {
                    latencySampleWatch.Restart();
                }
                else if (latencySampleWatch.ElapsedMilliseconds >= ScpSlLatency.LatencySampleInterval)
                {
                    latencySampleWatch.Restart();
                    LatencySamples[DateTime.UtcNow] = Latency;

                    while (LatencySamples.Count > ScpSlLatency.LatencySampleCount)
                    {
                        var oldestSample = LatencySamples.Keys.Min();

                        LatencySamples.TryRemove(oldestSample, out _);
                    }
                }
            }

            if (Latency > ScpSlLatency.LatencyThreshold)
            {
                if (!latencyThresholdWatch.IsRunning)
                {
                    latencyThresholdWatch.Restart();
                }
                else if (latencyThresholdWatch.ElapsedMilliseconds >= ScpSlLatency.LatencyDuration)
                {
                    if (!latencyNotified)
                    {
                        latencyNotified = true;

                        HighLatencyDetected?.Invoke(Latency);
                    }
                }
            }
            else 
            {
                if (latencyNotified)
                {
                    latencyNotified = false;

                    HighLatencyResolved?.Invoke(Latency);
                }

                if (latencyThresholdWatch.IsRunning)
                {
                    latencyThresholdWatch.Stop();
                    latencyThresholdWatch.Reset();
                }
            }
        }
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
    /// Sends a remote procedure call (RPC) to request the connected SCP:SL server to restart.
    /// </summary>
    public void CallRpcRestart()
    {
        SendRemoteCallback(rpc_RpcRestart, default(byte[]));
    }

    /// <summary>
    /// Sends a remote procedure call (RPC) to request the connected SCP:SL server to shut down.
    /// </summary>
    public void CallRpcShutdown()
    {
        SendRemoteCallback(rpc_RpcShutdown, default(byte[]));
    }

    /// <summary>
    /// Sends a remote procedure call (RPC) to request the connected SCP:SL server to kick specified users.
    /// </summary>
    /// <param name="reason">The reason for kicking the users. Must not be null or empty.</param>
    /// <param name="userIds">The collection of user IDs to be kicked. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown if the <paramref name="reason"/> is null or empty, or if the <paramref name="userIds"/> is null.</exception>
    public void CallRpcKick(string reason, IEnumerable<string> userIds)
    {
        if (string.IsNullOrEmpty(reason))
            throw new ArgumentNullException(nameof(reason));

        if (userIds == null)
            throw new ArgumentNullException(nameof(userIds));

        SendRemoteCallback(rpc_RpcKick,
            writer =>
            {
                writer.WriteString(reason);
                writer.WriteArray(userIds.ToArray());
            });
    }

    /// <summary>
    /// Sends a remote procedure call (RPC) to invoke a command on the connected SCP:SL server.
    /// </summary>
    /// <param name="command">The command string to be sent to the server. Must not be null or empty.</param>
    /// <param name="callback">The callback action to process the server's response. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown if the <paramref name="command"/> is null or empty, or if the <paramref name="callback"/> is null.</exception>
    public void CallRpcInvokeCommand(string command, Action<string> callback)
    {
        if (string.IsNullOrEmpty(command))
            throw new ArgumentNullException(nameof(command));

        if (callback == null)
            throw new ArgumentNullException(nameof(callback));

        SendRemoteCallback(rpc_RpcInvokeCommand, 
            writer => writer.WriteString(command), 
            reader => callback(reader?.ReadString() ?? string.Empty));
    }

    /// <summary>
    /// Processes the synchronization of ticks per second (TPS) sent from the client. This method reads
    /// the incoming data to update the local TPS value.
    /// </summary>
    /// <param name="reader">The reader responsible for deserializing the incoming data stream.</param>
    [ServerCmd]
    public void CmdSyncTps(ByteReader reader)
    {
        tps = reader.ReadInt32();
    }

    /// <summary>
    /// Processes the removal of a player from the server's player list. This method reads
    /// the incoming data to identify the player to be removed.
    /// </summary>
    /// <param name="reader">The reader responsible for deserializing the incoming data stream.</param>
    [ServerCmd]
    public void CmdRemovePlayer(ByteReader reader)
    {
        var userId = reader.ReadString();

        if (Players.TryRemove(userId, out _))
            DiscordBot?.UpdatePlayerCount(Players.Count, maxPlayers);
    }

    /// <summary>
    /// Processes the synchronization of a single player's information sent from the client. This method reads
    /// the incoming data to update the local player information.
    /// </summary>
    /// <param name="reader">The reader responsible for deserializing the incoming data stream.</param>
    [ServerCmd]
    public void CmdSyncPlayer(ByteReader reader)
    {
        var id = reader.ReadString();

        if (Players.TryGetValue(id, out var info))
        {
            info.UserId = id;
            info.Nick = reader.ReadString();
            info.Address = reader.ReadString();
            info.Country = reader.ReadString();

            info.Ping = reader.ReadInt32();
            info.Role = reader.ReadString();

            info.UtcUpdate = DateTime.UtcNow;

            info.CustomData.Clear();

            reader.ReadIntoConcurrentDictionary(info.CustomData);
        }
        else
        {
            info = new()
            {
                UserId = id,

                Nick = reader.ReadString(),
                Address = reader.ReadString(),
                Country = reader.ReadString(),

                Ping = reader.ReadInt32(),
                Role = reader.ReadString(),

                UtcUpdate = DateTime.UtcNow
            };

            info.CustomData.Clear();

            reader.ReadIntoConcurrentDictionary(info.CustomData);

            if (info.Profile == null
                && ProfileManager.TryGetProfileByUserId(id, out var profile))
            {
                info.Profile = profile;

                if (profile.Value.TryGetProperty<LevelProperty>(LevelManager.PropertyName, out var levelProperty))
                    info.Level = levelProperty;
            }

            Players.TryAdd(id, info);

            DiscordBot?.UpdatePlayerCount(Players.Count, maxPlayers);
        }
    }

    /// <summary>
    /// Processes the synchronization of player information sent from the client. This method reads 
    /// the incoming data to update the local player information dictionary.
    /// </summary>
    /// <param name="reader">The reader responsible for deserializing the incoming data stream.</param>
    [ServerCmd]
    public void CmdSyncPlayers(ByteReader reader)
    {
        var count = reader.ReadByte();
        var time = DateTime.UtcNow;

        var anyNew = false;
        var anyRemoved = false;

        for (var x = 0; x < count; x++)
        {
            var userId = reader.ReadString();

            if (!Players.TryGetValue(userId, out var playerInfo))
            {
                Players.TryAdd(userId, playerInfo = new());

                anyNew = true;
            }

            playerInfo.UserId = userId;
            playerInfo.UtcUpdate = time;

            playerInfo.Nick = reader.ReadString();
            playerInfo.Address = reader.ReadString();
            playerInfo.Country = reader.ReadString();
            playerInfo.Ping = reader.ReadInt32();

            playerInfo.CustomData.Clear();

            reader.ReadIntoConcurrentDictionary(playerInfo.CustomData);

            if (playerInfo.Profile == null
                && ProfileManager.TryGetProfileByUserId(userId, out var profile))
            {
                playerInfo.Profile = profile;

                if (profile.Value.TryGetProperty<LevelProperty>(LevelManager.PropertyName, out var levelProperty))
                    playerInfo.Level = levelProperty;
            }
        }

        foreach (var kvp in Players)
        {
            if (kvp.Value.UtcUpdate != time)
            {
                Players.TryRemove(kvp.Key, out _);

                anyRemoved = true;
            }
        }

        if (anyNew || anyRemoved)
            DiscordBot?.UpdatePlayerCount(Players.Count, maxPlayers);
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
        MaxPlayers = reader.ReadByte();
        
        Log.Name = ServerAlias ?? Manager.Connection.EndPoint.ToString();
        Log.Info($"&1{Manager.Connection.EndPoint}&r identified as &1{ServerAlias}&r (&6{ServerIp}:{ServerPort}&r)!");

        if (string.IsNullOrEmpty(ServerAlias))
        {
            Log.Warn("Server alias is empty - Discord bot will NOT be connected!");
        }
        else
        {
            if (ScpSlLatency.MonitorEnabled) // TCP latency monitoring is not as realiable as literally connecting to the server itself via UDP, but it is better than nothing.
            {
                if (latencyProviders.TryGetValue(ServerAlias, out var scpSlLatency))
                {
                    LatencyMonitor = scpSlLatency;

                    Log.Info("CmdReceiveIdentity", $"Latency monitor &1{scpSlLatency.GetType().Name}&r connected to server &1{ServerAlias}&r!");
                }
                else
                {
                    LatencyMonitor = new() { ServerIp = $"{ServerIp}:{ServerPort}" };
                    LatencyMonitor.Initialize();

                    latencyProviders.TryAdd(ServerAlias, LatencyMonitor);

                    Log.Info("CmdReceiveIdentity", $"Latency monitor &1{LatencyMonitor.GetType().Name}&r created and connected to server &1{ServerAlias}&r!");
                }
            }

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

        Identified?.Invoke(this);
    }
}