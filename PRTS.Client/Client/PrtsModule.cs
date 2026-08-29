using NiveraAPI.IO.Network.Entities;
using NiveraAPI.Logs;

namespace PRTS.Client;

/// <summary>
/// Represents a module within the PRTS client system.
/// </summary>
public class PrtsModule : Entity
{
    /// <summary>
    /// Gets the log sink for the module.
    /// </summary>
    public LogSink Log { get; private set; }

    /// <summary>
    /// Called when the module is spawned in the client.
    /// </summary>
    public override void OnClientSpawned()
    {
        base.OnClientSpawned();
        
        Log = LogManager.GetSource("PRTS", GetType().Name);
        Log.Info("Spawned!");
    }
}