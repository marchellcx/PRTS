using LabExtended.Commands;
using LabExtended.Commands.Attributes;
using LabExtended.Commands.Interfaces;

namespace PRTS.Commands;

/// <summary>
/// Represents a set of commands related to the PRTS client.
/// </summary>
[Command("prts", "Commands for the PRTS client.")]
public class PrtsCommand : CommandBase, IServerSideCommand
{
    [CommandOverload("reloadcfgs", "Reloads all configurations.", null)]
    private void ReloadConfigs()
    {
        NiveraAPI.ScpSl.Loader.ReloadConfig();
        
        Ok("Configuration reloaded.");
    }
    
    [CommandOverload("reconnect", "Reconnects to the server.", null)]
    private void Reconnect()
    {
        Network.Reconnect();
        
        Ok($"Started reconnection.");
    }
}