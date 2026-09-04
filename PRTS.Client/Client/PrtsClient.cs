using LabApi.Features.Wrappers;

using LabExtended.API;
using LabExtended.Core;
using LabExtended.Events;

using MEC;

using NiveraAPI.IO.Configs;

using NiveraAPI.IO.Network;
using NiveraAPI.IO.Network.Entities;
using NiveraAPI.IO.Network.Entities.Attributes;

using NiveraAPI.IO.Serialization;

using PRTS.Client.Objects;

using UnityEngine;

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
    static PrtsClient()
    {
        ExPlayerEvents.Left += OnLeft;
        ExPlayerEvents.Verified += OnVerified;

        ExRoundEvents.Restarting += OnRestarting;
        ExRoundEvents.WaitingForPlayers += OnWaiting;
    }

    /// <summary>
    /// Gets or sets the alias of the server to connect to.
    /// </summary>
    [Config("prts", "server-alias", "The alias of the server to connect to.")]
    public static string ServerAlias { get; set; } = "test";

    /// <summary>
    /// Gets the currently active instance of the <see cref="PrtsClient"/>.
    /// </summary>
    public static PrtsClient? Active { get; private set; }

    [IndexField] private static ushort cmd_CmdSyncTps;

    [IndexField] private static ushort cmd_CmdSyncPlayer;
    [IndexField] private static ushort cmd_CmdSyncPlayers;

    [IndexField] private static ushort cmd_CmdRemovePlayer;

    [IndexField] private static ushort cmd_CmdReceiveIdentity;

    private bool playerListUpdatePaused;
    private CoroutineHandle infoUpdateCoroutine;

    /// <summary>
    /// Gets a dictionary mapping <see cref="ExPlayer"/> instances to their corresponding <see cref="PlayerInfo"/>.
    /// </summary>
    public Dictionary<ExPlayer, PlayerInfo> PlayerToInfo { get; } = new();
    
    /// <summary>
    /// Initializes the client when it has been successfully spawned.
    /// This method sets the active client instance, subscribes to relevant player-related
    /// and round-related events, and begins managing server communication.
    /// </summary>
    public override void OnClientSpawned()
    {
        base.OnClientSpawned();
        
        ApiLog.Info("PRTS", "Client spawned!");

        infoUpdateCoroutine = Timing.RunCoroutine(InfoUpdateCoroutine());     
        
        Active = this;
    }

    /// <summary>
    /// Handles cleanup and resource deallocation when the client is destroyed.
    /// This method unsubscribes from all relevant events and resets the active client instance.
    /// </summary>
    public override void OnDestroyed()
    {
        base.OnDestroyed();
        
        Timing.KillCoroutines(infoUpdateCoroutine);      
        
        ApiLog.Info("PRTS", "Client destroyed!");      
        
        Active = null;
    }

    /// <summary>
    /// Sends a command to the server to synchronize the current ticks per second (TPS) value.
    /// </summary>
    /// <param name="tps">The current ticks per second (TPS) value to synchronize with the server.</param>
    public void CallCmdSyncTps(int tps)
    {
        SendRemoteCallback(cmd_CmdSyncTps, writer =>
        {
            writer.WriteInt32(tps);
        });
    }

    /// <summary>
    /// Sends a command to the server to remove a player with the specified user ID.
    /// </summary>
    /// <param name="userId">The user ID of the player to remove.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userId"/> is null or empty.</exception>
    public void CallCmdRemovePlayer(string userId)
    {
        if (string.IsNullOrEmpty(userId))
            throw new ArgumentNullException(nameof(userId));

        SendRemoteCallback(cmd_CmdRemovePlayer, writer =>
        {
            writer.WriteString(userId);
        });
    }

    /// <summary>
    /// Sends a command to the server to synchronize a specific player's information.
    /// </summary>
    /// <param name="info">The player's information to synchronize.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="info"/> is null.</exception>
    public void CallCmdSyncPlayer(PlayerInfo info)
    {
        if (info == null)
            throw new ArgumentNullException(nameof(info));

        SendRemoteCallback(cmd_CmdSyncPlayer, writer =>
        {
            writer.WriteString(info.UserId);
            writer.WriteString(info.Nick);
            writer.WriteString(info.Address);
            writer.WriteString(info.Country);
            writer.WriteInt32(info.Latency);
            writer.WriteString(info.Role);
            writer.WriteDictionary(info.CustomData);
        });
    }

    /// <summary>
    /// Sends a command to the server to synchronize the list of players.
    /// </summary>
    public void CallCmdSyncPlayers()
    {
        ApiLog.Info("PRTS", "Sending player list sync request ..");

        SendRemoteCallback(cmd_CmdSyncPlayers, writer =>
        {
            writer.WriteByte((byte)PlayerToInfo.Count);

            foreach (var kvp in PlayerToInfo)
            {
                var info = kvp.Value;

                writer.WriteString(info.UserId);
                writer.WriteString(info.Nick);
                writer.WriteString(info.Address);
                writer.WriteString(info.Country);
                writer.WriteInt32(info.Latency);
                writer.WriteString(info.Role);
                writer.WriteDictionary(info.CustomData);
            }
        });
    }

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
            writer.WriteByte((byte)Server.MaxPlayers);
        });

        PlayerToInfo.Clear();
        playerListUpdatePaused = false;

        foreach (var player in ExPlayer.Players)
        {
            if (!player.IsVerified)
                continue;

            var info = new PlayerInfo
            {
                UserId = player.UserId,
                Nick = player.Nickname,
                Address = player.IpAddress,
                Country = player.CountryCode,
                Latency = player.Peer?.RoundTripTime ?? 0,
                Role = player.Role.Name,
            };

            PlayerToInfo[player] = info;
        }

        CallCmdSyncPlayers();
    }

    /// <summary>
    /// Handles a kick request received from the server, kicking specified players with a given reason.
    /// </summary>
    /// <param name="reader">The reader instance used to deserialize the incoming kick request data.</param>
    [ClientRpc]
    public void RpcKick(ByteReader reader)
    {
        var reason = reader.ReadString();
        var users = reader.ReadArray<string>();

        ApiLog.Info("PRTS", $"Received kick request for &1{users.Length}&r users with reason: &1{reason}&r");

        foreach (var userId in users)
        {
            if (!ExPlayer.TryGetByUserId(userId, out var player))
            {
                ApiLog.Warn("PRTS", $"Player with user ID &1{userId}&r not found!");
                continue;
            }

            ApiLog.Info("PRTS", $"Kicking player &1{player.Nickname}&r with reason: &1{reason}&r");

            player.Kick(reason);
        }
    }

    /// <summary>
    /// Handles a restart request received from the server, initiating a server restart.
    /// </summary>
    [ClientRpc]
    public void RpcRestart(ByteReader _)
    {
        ApiLog.Info("PRTS", "Received restart request!");

        Server.Restart();
    }

    /// <summary>
    /// Handles a shutdown request received from the server, initiating a server shutdown.
    /// </summary>
    [ClientRpc]
    public void RpcShutdown(ByteReader _)
    {
        ApiLog.Info("PRTS", "Received shutdown request!");

        Server.Shutdown();
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

    private static void OnRestarting()
    {
        if (Network.Prts == null)
            return;

        Network.Prts.playerListUpdatePaused = true;
        Network.Prts.PlayerToInfo.Clear();

        Network.Prts.CallCmdSyncPlayers();
    }

    private static void OnWaiting()
    {
        Timing.CallDelayed(1.5f, () =>
        {
            if (Network.Prts == null)
                return;

            Network.Prts.playerListUpdatePaused = false;

            foreach (var player in ExPlayer.Players)
            {
                if (!player.IsVerified)
                    continue;

                var info = new PlayerInfo
                {
                    UserId = player.UserId,
                    Nick = player.Nickname,
                    Address = player.IpAddress,
                    Country = player.CountryCode,
                    Latency = player.Peer?.RoundTripTime ?? 0,
                    Role = player.Role.Name,
                };

                Network.Prts.PlayerToInfo[player] = info;
            }

            Network.Prts.CallCmdSyncPlayers();
        });
    }

    private static void OnLeft(ExPlayer player)
    {
        if (Network.Prts == null)
            return;

        if (Network.Prts.playerListUpdatePaused)
            return;

        Network.Prts.PlayerToInfo.Remove(player);
        Network.Prts.CallCmdRemovePlayer(player.UserId);
    }

    private static void OnVerified(ExPlayer player)
    {
        if (Network.Prts == null)
            return;

        if (Network.Prts.playerListUpdatePaused)
            return;

        var info = new PlayerInfo
        {
            UserId = player.UserId,
            Nick = player.Nickname,
            Address = player.IpAddress,
            Country = player.CountryCode,
            Latency = player.Peer?.RoundTripTime ?? 0,
            Role = player.Role.Name,
        };

        Network.Prts.PlayerToInfo[player] = info;
        Network.Prts.CallCmdSyncPlayer(info);
    }

    private IEnumerator<float> InfoUpdateCoroutine()
    {
        var lastTps = -1;

        while (true)
        {
            yield return Timing.WaitForOneFrame;

            var tps = Mathf.CeilToInt(ExServer.Tps);

            if (tps != lastTps)
            {
                lastTps = tps;

                CallCmdSyncTps(tps);
            }

            foreach (var kvp in PlayerToInfo)
            {
                if (kvp.Key?.ReferenceHub != null)
                {
                    var info = kvp.Value;

                    var role = kvp.Key.Role.Name;
                    var latency = kvp.Key.Peer?.RoundTripTime ?? 0;

                    var sync = false;

                    if (info.Latency != latency)
                    {
                        info.Latency = latency;

                        sync = true;
                    }

                    if (info.Role != role)
                    {
                        info.Role = role;

                        sync = true;
                    }

                    if (sync)
                    {
                        CallCmdSyncPlayer(info);
                    }
                }
            }
        }
    }
}