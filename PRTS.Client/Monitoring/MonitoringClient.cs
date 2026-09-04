using LabExtended.Core;

using LiteNetLib;

using NiveraAPI.IO.Configs;

namespace PRTS.Monitoring;

/// <summary>
/// Represents the monitoring client that connects to the monitoring server and sends monitoring data.
/// </summary>
public static class MonitoringClient
{
    /// <summary>
    /// The ID used to identify the monitoring client.
    /// </summary>
    [Config("monitoring-client", "client-id", "The ID used to identify the monitoring client.")]
    public static byte ClientId { get; set; } = 5;

    /// <summary>
    /// The key used to identify the monitoring client.
    /// </summary>
    [Config("monitoring-client", "client-key", "The key used to identify the monitoring client.")]
    public static string ClientKey { get; set; } = "PrtsMonitoring";

    internal static void AcceptClient(ConnectionRequest request)
    {
        request.Accept();

        ApiLog.Info("Monitoring client connected!");
    }
}