using LiteNetLib;
using LiteNetLib.Utils;

using NiveraAPI;
using NiveraAPI.Extensions;
using NiveraAPI.IO.Configs;

using System.Net;
using System.Diagnostics;
using System.Collections.Concurrent;

using PRTS.ScpSl.Interfaces;
using NiveraAPI.Logs;

namespace PRTS.ScpSl;

/// <summary>
/// Represents a monitoring client that connects to a specified server and measures latency (ping) to the server. It provides functionality to track the lowest, highest, and average latency, as well as to notify when high latency is detected or resolved.
/// </summary>
public class ScpSlLatency : IScpSlLatencyProvider, INetLogger
{
    #region Configs
    /// <summary>
    /// Gets or sets the duration in milliseconds to wait before notifying about high latency.
    /// </summary>
    [Config("scp-sl", "latency-duration", "The duration in milliseconds to wait before notifying about high latency.")]
    public static int LatencyDuration { get; set; } = 10000;

    /// <summary>
    /// Gets or sets the latency threshold in milliseconds. If the latency exceeds this threshold for the specified duration, a notification will be triggered.
    /// </summary>
    [Config("scp-sl", "latency-threshold", "The latency threshold in milliseconds.")]
    public static int LatencyThreshold { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the number of latency samples to keep for calculating average latency. This determines how many recent latency measurements are stored for analysis.
    /// </summary>
    [Config("scp-sl", "latency-sample-count", "The number of latency samples to keep for calculating average latency.")]
    public static int LatencySampleCount { get; set; } = 10;

    /// <summary>
    /// Gets or sets the interval in milliseconds at which to sample latency. This is used to calculate average latency over time.
    /// </summary>
    [Config("scp-sl", "latency-sample-interval", "The interval in milliseconds at which to sample latency.")]
    public static int LatencySampleInterval { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the ID used to identify the monitoring client.
    /// </summary>
    [Config("scp-sl", "latency-monitor-client-id", "The ID used to identify the monitoring client.")]
    public static byte MonitoringId { get; set; } = 5;

    /// <summary>
    /// Gets or sets the key used to identify the monitoring client.
    /// </summary>
    [Config("scp-sl", "latency-monitor-client-key", "The key used to identify the monitoring client.")]
    public static string MonitoringKey { get; set; } = "PrtsMonitoring";

    /// <summary>
    /// Gets or sets the time in milliseconds to wait for a connection before retrying.
    /// </summary>
    [Config("scp-sl", "latency-monitor-connect-wait", "The time in milliseconds to wait for a connection before retrying.")]
    public static int ConnectWait { get; set; } = 5000;

    /// <summary>
    /// Gets or sets a value indicating whether the latency monitor is enabled. If set to false, the monitoring client will not attempt to connect to the server or measure latency.
    /// </summary>
    [Config("scp-sl", "latency-monitor-enabled", "Whether the latency monitor is enabled.")]
    public static bool MonitorEnabled { get; set; }
    #endregion

    private volatile bool latencyNotified = false;

    private volatile LogSink log;
    private volatile IPEndPoint target;

    private volatile Stopwatch connectWatch = new();

    private volatile Stopwatch latencyWatch = new();
    private volatile Stopwatch latencySampleWatch = new();

    private volatile LiteNetPeer? peer;
    private volatile LiteNetPeer? connectPeer;

    private volatile LiteNetManager? client;

    private volatile EventBasedLiteNetListener listener;

    private volatile int lowestLatency = -1;
    private volatile int highestLatency = -1;

    private volatile string serverIp = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the monitoring client is currently connected to the server.
    /// </summary>
    public bool IsConnected => client != null
        && client.IsRunning
        && peer != null
        && peer.ConnectionState is ConnectionState.Connected;

    /// <summary>
    /// Gets or sets the server IP address to connect to. The format should be "IP:Port".
    /// </summary>
    public string ServerIp
    {
        get => serverIp;
        set => serverIp = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets the latency (ping) in milliseconds to the connected server. Returns -1 if not connected.
    /// </summary>
    public int Latency => peer?.Ping ?? -1;

    /// <summary>
    /// Gets or sets the lowest latency (ping) recorded during the monitoring session. Returns -1 if not connected.
    /// </summary>
    public int LowestLatency => lowestLatency;

    /// <summary>
    /// Gets or sets the highest latency (ping) recorded during the monitoring session. Returns -1 if not connected.
    /// </summary>
    public int HighestLatency => highestLatency;

    /// <summary>
    /// Gets the average latency (ping) in milliseconds to the connected server, calculated as the average of the lowest and highest recorded latencies. Returns -1 if not connected.
    /// </summary>
    public int AverageLatency => (int)Math.Ceiling((LowestLatency + HighestLatency) / 2f);

    /// <summary>
    /// Gets or sets a collection of latency samples, where the key is the timestamp of the sample and the value is the latency in milliseconds. This can be used to analyze latency trends over time.
    /// </summary>
    public ConcurrentDictionary<DateTime, int> LatencySamples { get; } = new();

    /// <summary>
    /// Gets or sets the event that is triggered when high latency is detected. The event provides the current latency value in milliseconds as an argument.
    /// </summary>
    public event Action<int>? HighLatencyDetected;

    /// <summary>
    /// Gets or sets the event that is triggered when high latency is resolved. The event provides the current latency value in milliseconds as an argument.
    /// </summary>
    public event Action<int>? HighLatencyResolved;

    /// <summary>
    /// Initializes the monitoring client and starts the underlying network manager.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the monitoring client is already initialized.</exception>
    public void Initialize()
    {
        if (client != null)
            throw new InvalidOperationException("MonitoringClient is already initialized.");

        NetDebug.Logger ??= this;

        log ??= LogManager.GetSource($"ScpSlLatency", ServerIp);
        log.Info("Initializing ..");

        listener = new();

        listener.PeerConnectedEvent += OnConnected;
        listener.PeerDisconnectedEvent += OnDisconnected;

        log.Info("Starting client ..");

        client = new LiteNetManager(listener);
        client.Start();

        LibraryUpdate.Register(OnUpdate);

        log.Info("Connecting ..");

        if (!serverIp.TrySplit(':', true, 2, out var segments))
            throw new InvalidOperationException("Server IP is not in the correct format. Expected format: 'IP:Port'.");

        if (!IPAddress.TryParse(segments[0], out var address))
            throw new InvalidOperationException("Server IP is not a valid IP address.");

        if (!int.TryParse(segments[1], out var port))
            throw new InvalidOperationException("Server port is not a valid integer.");

        connectPeer = client.Connect(target = new IPEndPoint(address, port), CreateVerificationRequest());
        connectWatch.Restart();
    }

    /// <summary>
    /// Writes a log message with the specified network log level. The message is logged using the appropriate logging method based on the provided level. Additionally, any arguments passed to the method are logged as debug messages.
    /// </summary>
    /// <param name="level">The network log level.</param>
    /// <param name="str">The log message.</param>
    /// <param name="args">Additional arguments to be logged as debug messages.</param>
    public void WriteNet(NetLogLevel level, string str, params object[] args)
    {
        switch (level)
        {
            case NetLogLevel.Trace:
                log.Debug("LiteNetLib", str);
                break;

            case NetLogLevel.Error:
                log.Error("LiteNetLib", str);
                break;

            case NetLogLevel.Warning:
                log.Warn("LiteNetLib", str);
                break;

            case NetLogLevel.Info:
                log.Info("LiteNetLib", str);
                break;
        }

        for (var x = 0; x < args.Length; x++)
            log.Debug("LiteNetLib", $"Arg[{x}]: {args[x]}");
    }

    private void OnConnected(LiteNetPeer peer)
    {
        this.peer = peer;
        this.connectPeer = null;

        lowestLatency = -1;
        highestLatency = -1;

        connectWatch.Stop();
        connectWatch.Reset();

        log.Info("Connected!");
    }

    private void OnDisconnected(LiteNetPeer peer, DisconnectInfo disconnectInfo)
    {
        this.peer = null;
        this.connectPeer = null;

        lowestLatency = -1;
        highestLatency = -1;

        log.Warn("Disconnected!");
    }

    private void OnUpdate()
    {
        if (client != null)
        {
            client.PollEvents();

            if (peer == null)
            {
                if (connectPeer != null)
                {
                    if (connectWatch.ElapsedMilliseconds < ConnectWait)
                        return;

                    connectPeer.Disconnect();
                    connectPeer = null;

                    log.Info("Connection attempt timed out, retrying ..");
                }

                connectPeer = client.Connect(target, CreateVerificationRequest());
                connectWatch.Restart();
            }
            else
            {
                if (lowestLatency == -1 || peer.Ping < lowestLatency)
                    lowestLatency = peer.Ping;

                if (highestLatency == -1 || peer.Ping > highestLatency)
                    highestLatency = peer.Ping;

                if (LatencySampleCount > 0)
                {
                    if (!latencySampleWatch.IsRunning)
                    {
                        latencySampleWatch.Restart();
                    }
                    else
                    {
                        if (latencySampleWatch.ElapsedMilliseconds >= LatencySampleInterval)
                        {
                            latencySampleWatch.Restart();
                            LatencySamples[DateTime.UtcNow] = peer.Ping;

                            while (LatencySamples.Count > LatencySampleCount)
                            {
                                var oldestKey = LatencySamples.Keys.Min();

                                LatencySamples.TryRemove(oldestKey, out _);
                            }
                        }
                    }
                }

                if (peer.Ping > LatencyThreshold)
                {
                    if (!latencyWatch.IsRunning)
                    {
                        latencyWatch.Restart();
                    }
                    else if (latencyWatch.ElapsedMilliseconds > LatencyDuration)
                    {
                        if (!latencyNotified)
                        {
                            latencyNotified = true;

                            HighLatencyDetected?.Invoke(peer.Ping);
                        }
                    }
                }
                else
                {
                    if (latencyNotified)
                    {
                        latencyNotified = false;

                        HighLatencyResolved?.Invoke(peer.Ping);
                    }

                    if (latencyWatch.IsRunning)
                    {
                        latencyWatch.Stop();
                        latencyWatch.Reset();
                    }
                }
            }
        }
    }

    private static NetDataWriter CreateVerificationRequest()
    {
        var writer = new NetDataWriter(true, 1 + MonitoringKey.Length);

        writer.Put(MonitoringId); // Identify monitoring client on the first byte
        writer.Put(MonitoringKey);

        return writer;
    }
}
