using NiveraAPI.IO.Network.Entities;
using NiveraAPI.Logs;

using PRTS.ScpSl.Discord;

namespace PRTS.ScpSl;

/// <summary>
/// Represents a module for SCP:SL servers.
/// </summary>
public class ScpSlModule : Entity
{
    /// <summary>
    /// Gets the associated server instance.
    /// </summary>
    public ScpSlServer Server { get; private set; }
    
    /// <summary>
    /// Gets the associated Discord bot instance.
    /// </summary>
    public ScpSlBot? Bot => Server?.DiscordBot;
    
    /// <summary>
    /// Gets the module's logger.
    /// </summary>
    public LogSink Log { get; private set; }

    /// <summary>
    /// Called when the module is spawned on the server.
    /// </summary>
    public override void OnServerSpawned()
    {
        base.OnServerSpawned();

        if (!Manager.TryGetFirstEntity<ScpSlServer>(out var server))
        {
            Destroy();
            return;
        }

        Server = server;
        
        Log = LogManager.GetSource(Server.ServerAlias, GetType().Name);
        Log.Info("Spawned!");
    }
}