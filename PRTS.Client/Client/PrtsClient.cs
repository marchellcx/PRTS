using LabApi.Features.Wrappers;

using LabExtended.API;
using LabExtended.Core;

using MEC;

using NiveraAPI.IO.Configs;

using NiveraAPI.IO.Network;
using NiveraAPI.IO.Network.Entities;
using NiveraAPI.IO.Network.Entities.Attributes;

using NiveraAPI.IO.Serialization;

namespace PRTS.Client;

/// <summary>
/// Represents a client service for connecting to a PRTS server.
/// Inherits from the <see cref="NetService"/> base class and manages
/// client-server communication, including sending server identification
/// and player count updates.
/// </summary>
[ServerType("PRTS.ScpSl.ScpSlServer")]
public class PrtsClient : Entity
{
    /// <summary>
    /// Gets or sets the alias of the server to connect to.
    /// </summary>
    [Config("prts", "server-alias", "The alias of the server to connect to.")]
    public static string ServerAlias { get; set; } = "test";

    /// <summary>
    /// Gets the currently active instance of the <see cref="PrtsClient"/>.
    /// </summary>
    public static PrtsClient? Active { get; private set; }
    
    [IndexField] private static ushort cmd_CmdReceiveIdentity;
    [IndexField] private static ushort cmd_CmdUpdatePlayerCount;

    private CoroutineHandle playerCountUpdateCoroutine;
    
    /// <summary>
    /// Initializes the client when it has been successfully spawned.
    /// This method sets the active client instance, subscribes to relevant player-related
    /// and round-related events, and begins managing server communication.
    /// </summary>
    public override void OnClientSpawned()
    {
        base.OnClientSpawned();
        
        ApiLog.Info("PRTS", "Client spawned!");   
        
        playerCountUpdateCoroutine = Timing.RunCoroutine(PlayerCountUpdateCoroutine());     
        
        Active = this;
    }

    /// <summary>
    /// Handles cleanup and resource deallocation when the client is destroyed.
    /// This method unsubscribes from all relevant events and resets the active client instance.
    /// </summary>
    public override void OnDestroyed()
    {
        base.OnDestroyed();
        
        Timing.KillCoroutines(playerCountUpdateCoroutine);      
        
        ApiLog.Info("PRTS", "Client destroyed!");      
        
        Active = null;
    }

    /// <summary>
    /// Sends an updated player count to the connected PRTS server.
    /// This method relays the current number of active players to ensure
    /// synchronization between the client and server-side systems.
    /// </summary>
    /// <param name="playerCount">The total number of active players to report to the server.</param>
    public void CallCmdUpdatePlayerCount(byte playerCount)
        => SendRemoteCallback(cmd_CmdUpdatePlayerCount, playerCount);

    /// <summary>
    /// Sends a server identification message to the connected server.
    /// </summary>
    public void CallCmdReceiveIdentity()
    {
        ApiLog.Info("PRTS", "Sending identification response ..");
        
        SendRemoteCallback(cmd_CmdReceiveIdentity, writer =>
        {
            writer.WriteString(Server.ServerListName);
            writer.WriteString(ServerAlias);
            writer.WriteString(Server.IpAddress);
            writer.WriteUInt16(Server.Port);
            writer.WriteByte((byte)Server.PlayerCount);
            writer.WriteByte((byte)Server.MaxPlayers);
        });
    }

    /// <summary>
    /// Requests the server identity information by invoking the command to send
    /// the server's identification message. This method ensures the client
    /// receives the necessary identity data to establish or maintain proper
    /// communication with the server.
    /// </summary>
    [ClientRpc]
    public void RpcRequestIdentity(ByteReader _)
    {
        ApiLog.Info("PRTS", "Received identification request!");
        
        CallCmdReceiveIdentity();
    }

    /// <summary>
    /// Executes a command received from the client and writes the result back to the client.
    /// The command is processed through the server's command execution system,
    /// and any generated response or error is returned to the client.
    /// </summary>
    /// <param name="reader">The reader instance used to deserialize the incoming command data.</param>
    /// <param name="writer">The writer instance used to serialize and send the command execution result back to the client.</param>
    [ClientRpc(true)]
    public void RpcInvokeCommand(ByteReader reader, ByteWriter writer)
    {
        try
        {
            var command = reader.ReadString();
            var response = Server.RunCommand(command, ServerConsole.Scs);
            
            writer.WriteString(response);
        }
        catch (Exception ex)
        {
            writer.WriteString(ex.ToString());
            
            ApiLog.Error("PRTS", $"Error while executing command: &1{ex}&r");
        }
    }

    private IEnumerator<float> PlayerCountUpdateCoroutine()
    {
        var lastPlayerCount = ExPlayer.Count;
        
        while (true)
        {
            yield return Timing.WaitForOneFrame;

            if (lastPlayerCount != ExPlayer.Count)
            {
                lastPlayerCount = ExPlayer.Count;
                
                CallCmdUpdatePlayerCount((byte)ExPlayer.Count);
                
                ApiLog.Debug("PRTS", $"Sent player count update: &1{ExPlayer.Count}&r");
            }
        }
    }
}