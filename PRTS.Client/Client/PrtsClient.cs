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
    /// Defines a constant representing the full player synchronization flag.
    /// </summary>
    public const ushort FullPlayerSyncFlag = 2 | 4 | 8 | 16 | 32 | 64 | 128;

    /// <summary>
    /// Gets or sets the alias of the server to connect to.
    /// </summary>
    [Config("prts", "server-alias", "The alias of the server to connect to.")]
    public static string ServerAlias { get; set; } = "test";

    /// <summary>
    /// Gets the currently active instance of the <see cref="PrtsClient"/>.
    /// </summary>
    public static PrtsClient? Active { get; private set; }

    private int syncTps;
    private bool syncLobbyLock;
    private bool syncRoundLock;

    [IndexField] private static ushort cmd_CmdSyncTps;

    [IndexField] private static ushort cmd_CmdSyncPlayer;
    [IndexField] private static ushort cmd_CmdSyncPlayers;

    [IndexField] private static ushort cmd_CmdSyncLobbyLock;
    [IndexField] private static ushort cmd_CmdSyncRoundLock;

    [IndexField] private static ushort cmd_CmdRemovePlayer;

    [IndexField] private static ushort cmd_CmdPostMessage;
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
        SendRemoteCallback(cmd_CmdSyncTps, writer => writer.WriteInt32(tps));
    }

    /// <summary>
    /// Sends a command to the server to synchronize the lobby lock state.
    /// </summary>
    /// <param name="isLocked">A boolean value indicating whether the lobby is locked.</param>
    public void CallCmdSyncLobbyLock(bool isLocked)
    {
        SendRemoteCallback(cmd_CmdSyncLobbyLock, writer => writer.WriteBool(isLocked));
    }

    /// <summary>
    /// Sends a command to the server to synchronize the round lock state.
    /// </summary>
    /// <param name="isLocked">A boolean value indicating whether the round is locked.</param>
    public void CallCmdSyncRoundLock(bool isLocked)
    {
        SendRemoteCallback(cmd_CmdSyncRoundLock, writer => writer.WriteBool(isLocked));
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

        SendRemoteCallback(cmd_CmdRemovePlayer, writer => writer.WriteString(userId));
    }

    /// <summary>
    /// Sends a command to the server to synchronize a specific player's information.
    /// </summary>
    /// <param name="info">The player's information to synchronize.</param>
    /// <param name="hash">A bitmask indicating which properties of the player to synchronize.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="info"/> is null.</exception>
    public void CallCmdSyncPlayer(PlayerInfo info, ushort hash)
    {
        if (info == null)
            throw new ArgumentNullException(nameof(info));

        SendRemoteCallback(cmd_CmdSyncPlayer, writer => WritePlayer(writer, info, hash));
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
                WritePlayer(writer, kvp.Value, FullPlayerSyncFlag);
        });
    }

    /// <summary>
    /// Sends a command to the server to post a message to a specific channel.
    /// </summary>
    /// <param name="channelAlias">The alias of the channel to post the message to.</param>
    /// <param name="message">The message content to post.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="channelAlias"/> or <paramref name="message"/> is null or empty.</exception>
    public void CallCmdPostMessage(string channelAlias, string message)
    {
        if (string.IsNullOrEmpty(channelAlias))
            throw new ArgumentNullException(nameof(channelAlias));

        if (string.IsNullOrEmpty(message))
            throw new ArgumentNullException(nameof(message));

        SendRemoteCallback(cmd_CmdPostMessage, writer =>
        {
            writer.WriteString(channelAlias);
            writer.WriteString(message);
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

            PlayerToInfo[player] = CreatePlayer(player);
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
    /// Handles a lobby lock request received from the server, updating the lobby lock state accordingly.
    /// </summary>
    /// <param name="reader">The reader instance used to deserialize the incoming lobby lock request data.</param>
    [ClientRpc]
    public void RpcSetLobbyLock(ByteReader reader)
    {
        var isLocked = reader.ReadBool();

        ApiLog.Info("PRTS", $"Received lobby lock request: &1{isLocked}&r");

        ExRound.IsLobbyLocked = isLocked;
    }

    /// <summary>
    /// Handles a round lock request received from the server, updating the round lock state accordingly.
    /// </summary>
    /// <param name="reader">The reader instance used to deserialize the incoming round lock request data.</param>
    [ClientRpc]
    public void RpcSetRoundLock(ByteReader reader)
    {
        var isLocked = reader.ReadBool();

        ApiLog.Info("PRTS", $"Received round lock request: &1{isLocked}&r");

        ExRound.IsRoundLocked = isLocked;
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
    /// Handles a restart round request received from the server, initiating a round restart.
    /// </summary>
    /// <param name="_">The reader instance used to deserialize the incoming restart round request data.</param>
    [ClientRpc]
    public void RpcRestartRound(ByteReader _)
    {
        ApiLog.Info("PRTS", "Received restart round request!");

        Round.Restart();
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

                Network.Prts.PlayerToInfo[player] = CreatePlayer(player);
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

        var info = CreatePlayer(player);

        Network.Prts.PlayerToInfo[player] = info;
        Network.Prts.CallCmdSyncPlayer(info, FullPlayerSyncFlag);
    }

    private IEnumerator<float> InfoUpdateCoroutine()
    {
        while (true)
        {
            yield return Timing.WaitForOneFrame;

            var tps = Mathf.CeilToInt(ExServer.Tps);

            if (tps != syncTps)
            {
                syncTps = tps;
                CallCmdSyncTps(tps);
            }

            if (syncLobbyLock != ExRound.IsLobbyLocked)
            {
                syncLobbyLock = ExRound.IsLobbyLocked;
                CallCmdSyncLobbyLock(syncLobbyLock);
            }

            if (syncRoundLock != ExRound.IsRoundLocked)
            {
                syncRoundLock = ExRound.IsRoundLocked;
                CallCmdSyncRoundLock(syncRoundLock);
            }

            foreach (var kvp in PlayerToInfo)
            {
                if (kvp.Key?.ReferenceHub != null)
                {
                    var hash = (ushort)0;
                    var info = kvp.Value;

                    var role = kvp.Key.Role.Name;
                    var latency = kvp.Key.Peer?.RoundTripTime ?? 0;

                    if (info.Latency != latency)
                    {
                        hash |= 16;
                        info.Latency = latency;
                    }

                    if (info.Role != role)
                    {
                        hash |= 32;
                        info.Role = role;
                    }

                    if (info.SyncCustomData)
                    {
                        hash |= 64;
                        info.SyncCustomData = false;
                    }

                    if (hash != 0)
                        CallCmdSyncPlayer(info, hash);
                }
            }
        }
    }

    private static PlayerInfo CreatePlayer(ExPlayer player)
    {
        return new PlayerInfo
        {
            UserId = player.UserId,
            Nick = player.Nickname,
            Address = player.IpAddress,
            Country = player.CountryCode,
            Latency = player.Peer?.RoundTripTime ?? 0,
            Role = player.Role.Name,
        };
    }

    private static void WritePlayer(ByteWriter writer, PlayerInfo playerInfo, ushort hash)
    {
        var writeAll = hash == FullPlayerSyncFlag;

        writer.WriteString(playerInfo.UserId);
        writer.WriteUInt16(hash);

        if (writeAll || (hash & 2) != 0)
            writer.WriteString(playerInfo.Nick);

        if (writeAll || (hash & 4) != 0)
            writer.WriteString(playerInfo.Address);

        if (writeAll || (hash & 8) != 0)
            writer.WriteString(playerInfo.Country);

        if (writeAll || (hash & 16) != 0)
            writer.WriteInt32(playerInfo.Latency);

        if (writeAll || (hash & 32) != 0)
            writer.WriteString(playerInfo.Role);

        if (writeAll || (hash & 64) != 0)
            writer.WriteDictionary(playerInfo.CustomData);
    }
}