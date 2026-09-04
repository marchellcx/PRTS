using System.Collections.Concurrent;

namespace PRTS.ScpSl.Interfaces;

/// <summary>
/// Defines an interface for monitoring and providing latency (ping) information to a connected server in a networked application. This interface includes properties for current, lowest, highest, and average latency, as well as a collection of latency samples over time. It also defines events for detecting and resolving high latency conditions.
/// </summary>
public interface IScpSlLatencyProvider
{
    /// <summary>
    /// Gets the latency (ping) in milliseconds to the connected server. Returns -1 if not connected.
    /// </summary>
    int Latency { get; }

    /// <summary>
    /// Gets or sets the lowest latency (ping) recorded during the monitoring session. Returns -1 if not connected.
    /// </summary>
    int LowestLatency { get; }

    /// <summary>
    /// Gets or sets the highest latency (ping) recorded during the monitoring session. Returns -1 if not connected.
    /// </summary>
    int HighestLatency { get; }

    /// <summary>
    /// Gets the average latency (ping) in milliseconds to the connected server, calculated as the average of the lowest and highest recorded latencies. Returns -1 if not connected.
    /// </summary>
    int AverageLatency { get; }

    /// <summary>
    /// Gets or sets a collection of latency samples, where the key is the timestamp of the sample and the value is the latency in milliseconds. This can be used to analyze latency trends over time.
    /// </summary>
    ConcurrentDictionary<DateTime, int> LatencySamples { get; }

    /// <summary>
    /// Gets or sets the event that is triggered when high latency is detected. The event provides the current latency value in milliseconds as an argument.
    /// </summary>
    event Action<int>? HighLatencyDetected;

    /// <summary>
    /// Gets or sets the event that is triggered when high latency is resolved. The event provides the current latency value in milliseconds as an argument.
    /// </summary>
    event Action<int>? HighLatencyResolved;
}
