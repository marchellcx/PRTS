using LabExtended.API;

using LabExtended.Core;
using LabExtended.Events;
using LabExtended.Extensions;

using NiveraAPI.IO.Serialization;
using NiveraAPI.IO.Network.Entities.Attributes;

using ObscurisCore.Features.Elements.Alerts;

using PRTS.Client.Punishments.Enums;
using PRTS.Client.Punishments.Objects;

using PRTS.Extensions;

namespace PRTS.Client.Punishments;

/// <summary>
/// Represents a module for handling punishments.
/// </summary>
[ServerType("PRTS.ScpSl.Modules.Punishments.PunishmentModule")]
public class PunishmentModule : PrtsModule
{
    /// <summary>
    /// The singleton instance of the PunishmentModule.
    /// </summary>
    public static PunishmentModule Singleton;

    /// <summary>
    /// The event that is raised when a player is verified.
    /// </summary>
    public static event Action<ExPlayer>? PlayerVerified; 
    
    static PunishmentModule()
    {
        ExPlayerEvents.Verified += OnPlayerVerified;
    }

    [IndexField] private static ushort cmd_CmdListPunishments = 0;
    [IndexField] private static ushort cmd_CmdIssuePunishment = 0;
    [IndexField] private static ushort cmd_CmdRevokePunishment = 0;
    [IndexField] private static ushort cmd_CmdGetActivePunishments = 0;

    /// <summary>
    /// Initializes the module when the client is spawned.
    /// </summary>
    public override void OnClientSpawned()
    {
        base.OnClientSpawned();

        Singleton = this;
    }

    /// <summary>
    /// Destroys the module when the client is destroyed.
    /// </summary>
    public override void OnDestroyed()
    {
        base.OnDestroyed();

        Singleton = null!;
    }

    /// <summary>
    /// Sends a request to retrieve a list of punishments based on the specified criteria.
    /// </summary>
    /// <param name="id">The identifier of the punishment. Can be null.</param>
    /// <param name="staffId">The identifier of the staff member who issued the punishments. Can be null.</param>
    /// <param name="targetId">The identifier of the target who received the punishments. Can be null.</param>
    /// <param name="type">The type of punishment to filter by. Can be null.</param>
    /// <param name="status">The status of the punishment to filter by. Can be null.</param>
    /// <param name="from">The starting date and time of the punishment period to filter by. Can be null.</param>
    /// <param name="to">The ending date and time of the punishment period to filter by. Can be null.</param>
    /// <param name="callback">The callback action that processes the received list of punishment information.</param>
    /// <exception cref="ArgumentNullException">Thrown when the callback parameter is null.</exception>
    public void CallCmdListPunishments(string? id, string? staffId, string? targetId, PunishmentType? type,
        PunishmentStatus? status, DateTime? from, DateTime? to, Action<List<PunishmentInfo>?> callback)
    {
        if (callback == null)
            throw new ArgumentNullException(nameof(callback));
        
        ApiLog.Info($"Requesting punishments for &1{staffId ?? "null staff ID"}&r and &1{targetId ?? "null target ID"}&r " +
                    $"of type &3{type?.ToString() ?? "null type"}&r and status &3{status?.ToString() ?? "null status"}&r");

        SendRemoteCallback(cmd_CmdListPunishments, writer =>
        {
            writer.WriteString(id!);
            writer.WriteString(staffId!);
            writer.WriteString(targetId!);

            if (type.HasValue)
            {
                writer.WriteBool(true);
                writer.WriteByte((byte)type.Value);
            }
            else
            {
                writer.WriteBool(false);
            }

            if (status.HasValue)
            {
                writer.WriteBool(true);
                writer.WriteByte((byte)status.Value);
            }
            else
            {
                writer.WriteBool(false);
            }
            
            if (from.HasValue)
            {
                writer.WriteBool(true);
                writer.WriteDate(from.Value);
            }
            else
            {
                writer.WriteBool(false);
            }

            if (to.HasValue)
            {
                writer.WriteBool(true);
                writer.WriteDate(to.Value);
            }
        }, reader =>
        {
            var list = reader?.ReadList<PunishmentInfo>();
            
            ApiLog.Info($"Received &3{list?.Count ?? -1}&r punishment(s) from server");
            
            callback(list);
        });
    }

    /// <summary>
    /// Revokes a punishment given its ID, along with the staff member's ID who issued the revocation
    /// and the reason for the action. Executes a callback with the updated punishment information.
    /// </summary>
    /// <param name="id">The unique identifier of the punishment to be revoked.</param>
    /// <param name="staffId">The unique identifier of the staff member who revoked the punishment.</param>
    /// <param name="reason">The reason for revoking the punishment.</param>
    /// <param name="callback">A callback function that receives the updated punishment information.</param>
    public void CallCmdRevokePunishment(string id, string staffId, string reason, Action<PunishmentInfo?> callback)
    {
        ApiLog.Info($"Revoking punishment &1{id}&r issued by &1{staffId}&r for &1{reason}&r");
        
        SendRemoteCallback(cmd_CmdRevokePunishment, writer =>
        {
            writer.WriteString(id);
            writer.WriteString(staffId);
            writer.WriteString(reason);
        }, reader =>
        {
            var info = reader?.Read<PunishmentInfo>();

            if (info != null)
            {
                if (info.IsRevoked)
                {
                    ApiLog.Info($"Punishment &3{id}&r has been revoked by &3{staffId}&r: &3{reason}&r");

                    callback(info);
                }
                else
                {
                    ApiLog.Warn($"Punishment &3{id}&r has not been revoked!");
                    
                    callback(null);
                }
            }
            else
            {
                ApiLog.Warn("Received null punishment info from server!");
                    
                callback(null);
            }
        });
    }

    /// <summary>
    /// Issues a punishment to a specified target with the given parameters.
    /// </summary>
    /// <param name="staffId">The identifier of the staff member issuing the punishment.</param>
    /// <param name="targetId">The identifier of the target receiving the punishment.</param>
    /// <param name="type">The type of punishment being issued (e.g., warn, mute, ban).</param>
    /// <param name="reason">The reason for issuing the punishment.</param>
    /// <param name="expiresAt">The optional expiration date of the punishment, or null for no expiration.</param>
    /// <param name="appliedServers">The list of servers where the punishment applies, or null to apply to all servers.</param>
    /// <param name="callback">The callback to execute after the punishment is issued, providing a nullable <see cref="PunishmentInfo"/> object with details.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="staffId"/>, <paramref name="targetId"/>, or <paramref name="reason"/> is null or empty.
    /// </exception>
    public void CallCmdIssuePunishment(string staffId, string targetId, PunishmentType type, string reason,
        DateTime? expiresAt, string[]? appliedServers, Action<PunishmentInfo?>? callback = null)
    {
        if (string.IsNullOrEmpty(staffId))
            throw new ArgumentNullException(nameof(staffId));

        if (string.IsNullOrEmpty(targetId))
            throw new ArgumentNullException(nameof(targetId));
        
        if (string.IsNullOrEmpty(reason))
            throw new ArgumentNullException(nameof(reason));
     
        ApiLog.Info($"Issuing punishment to &1{targetId}&r for &1{reason}&r");
        
        SendRemoteCallback(cmd_CmdIssuePunishment, writer =>
        {
            writer.WriteString(staffId);
            writer.WriteString(targetId);
            writer.WriteString(reason);
            writer.WriteByte((byte)type);
            writer.WriteDate(expiresAt ?? DateTime.MinValue);
            writer.WriteArray(appliedServers ?? Array.Empty<string>());
        }, reader =>
        {
            try
            {
                var result = (PunishmentResult)reader.ReadByte();

                if (result is PunishmentResult.Ok)
                {
                    var info = reader.Read<PunishmentInfo>();

                    if (info != null)
                    {
                        ApiLog.Info($"Issued punishment &3{info.Id}&r for &3{info.TargetId}&r (by &3{info.StaffId}&r): &3{info.Reason}&r");

                        callback?.Invoke(info);
                    }
                    else
                    {
                        ApiLog.Warn("Received &2OK&r result but no punishment info was received!");

                        callback?.Invoke(null);
                    }
                }
                else
                {
                    ApiLog.Warn($"Received &2FAILED&r result for punishment issue: &3{result}&r");

                    callback?.Invoke(null);
                }
            }
            catch (Exception ex)
            {
                ApiLog.Error($"Error while handling punishment result: &1{ex}&r");
            }
        });
    }

    /// <summary>
    /// Sends a remote command to retrieve the list of active punishments for a specified user and executes a callback with the results.
    /// </summary>
    /// <param name="userId">The unique identifier of the user for whom punishments are being retrieved. Cannot be null or empty.</param>
    /// <param name="userIp">The IP address of the user for whom punishments are being retrieved. Cannot be null or empty.</param>
    /// <param name="callback">The callback function that processes the retrieved punishments. Receives the user ID and a list of <see cref="PunishmentInfo"/> objects as parameters. Cannot be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="userId"/> is null or empty, or <paramref name="callback"/> is null.</exception>
    public void CallCmdGetActivePunishments(string userId, string userIp, Action<string, List<PunishmentInfo>> callback)
    {
        if (string.IsNullOrEmpty(userId))
            throw new ArgumentNullException(nameof(userId));
        
        if (callback == null)
            throw new ArgumentNullException(nameof(callback));
        
        SendRemoteCallback(cmd_CmdGetActivePunishments,
            writer =>
            {
                writer.WriteString(userId);
                writer.WriteString(userIp);
            },
            reader => callback(userId, reader.ReadList<PunishmentInfo>()));
    }

    /// <summary>
    /// Handles the issuance of a punishment for a user.
    /// </summary>
    /// <param name="reader">The byte reader containing data about the punishment, including the user ID and punishment details.</param>
    [ClientRpc]
    public void RpcPunishmentIssued(ByteReader reader)
    {
        var userId = reader.ReadString();
        var userPunishment = reader.Read<PunishmentInfo>();
        
        ApiLog.Debug($"Handling punishment issuance for user &1{userId}&r: &3{userPunishment?.Id ?? "null"}&r");
        
        if (!ExPlayer.TryGet(userId, out var player))
        {
            ApiLog.Warn($"Could not issue punishment for user &1{userId}&r: player not found!");
            return;
        }
        
        if (userPunishment != null)
        {
            if (userPunishment.IsMute)
            {
                if (!player.IsMuted || !player.IsIntercomMuted)
                {
                    player.Mute(false);
                    player.IntercomMute(false);
                    
                    player.SendAlert(AlertType.Warn, 10f, "Mute", 
                        $"Máš aktivní <color=yellow>mute</color>\n" +
                        $"<color=red>{userPunishment.Reason}</color>\n" +
                        $"{(userPunishment.IsPermanent ? "<color=red>PERMANENTNÍ</color>" : $"Expiruje <b><color=red>{userPunishment.ExpiresAt.ToLocalTime().ToVeCzechString()}</color></b>!)")}");
                    
                    ApiLog.Info($"Muted player {player.ToLogString()}");
                }
                else
                {
                    ApiLog.Debug($"Player {player.ToLogString()} is already muted");
                }
            }
            else if (userPunishment.IsBan)
            {
                ApiLog.Info($"Kicking banned player {player.ToLogString()}: &3{userPunishment.Id}&r");

                if (userPunishment.IsPermanent)
                {
                    player.Kick($"\n<b><color=red>[PRTS]</color></b>\n" +
                                $"<b>Máš aktivní <color=red>PERMANENTNÍ BAN</color> na tomto serveru</b>!\n" +
                                $"<color=yellow><b>{userPunishment.Reason}</b></color>");
                            
                    ApiLog.Info($"Kicked &1permanently&r &3banned&r player {player.ToLogString()}: &3{userPunishment.Id}&r");
                }
                else
                {
                    player.Kick($"\n<b><color=red>[PRTS]</color></b>\n" +
                                $"<b>Máš aktivní <color=red>BAN</color> na tomto serveru</b>!\n" +
                                $"<color=yellow><b>{userPunishment.Reason}</b></color>\n" +
                                $"Ban expiruje <b><color=red>{userPunishment.ExpiresAt.ToLocalTime().ToVeCzechString()}</color></b>!");         
                    
                    ApiLog.Info($"Kicked &3banned&r player {player.ToLogString()}: &3{userPunishment.Id}&r");
                }
            }
            else if (userPunishment.IsWarn)
            {
                player.SendAlert(AlertType.Warn, 10f, "PRTS",
                    $"Obdržel jsi <color=yellow>varování</color>\n" +
                    $"<color=red>{userPunishment.Reason}</color>");
            }
        }
        else
        {
            ApiLog.Warn($"Could not issue punishment for user &1{userId}&r: received null punishment info!");
        }
    }

    /// <summary>
    /// Handles the removal of punishments for a specific user by synchronizing their
    /// punishment state and updating mute settings as necessary.
    /// </summary>
    /// <param name="reader">The ByteReader instance containing user and punishment data.</param>
    [ClientRpc]
    public void RpcPunishmentRemoved(ByteReader reader)
    {
        var userId = reader.ReadString();
        var userPunishments = reader.ReadList<PunishmentInfo>();
        
        ApiLog.Debug($"Handling punishment removal for user &1{userId}&r with &3{userPunishments.Count}&r active punishment(s)");

        if (ExPlayer.TryGet(userId, out var player))
        {
            if (userPunishments.TryGetFirst(p => p.IsMute, out var mute))
            {
                if (!player.IsMuted || !player.IsIntercomMuted)
                {
                    player.Mute(false);
                    player.IntercomMute(false);
                    
                    player.SendAlert(AlertType.Warn, 10f, "Mute", 
                        $"Máš aktivní <color=yellow>mute</color>\n" +
                        $"<color=red>{mute.Reason}</color>\n" +
                        $"{(mute.IsPermanent ? "<color=red>PERMANENTNÍ</color>" : $"Expiruje <b><color=red>{mute.ExpiresAt.ToLocalTime().ToVeCzechString()}</color></b>!)")}");
                    
                    ApiLog.Info($"Muted player {player.ToLogString()}");
                }
                else
                {
                    ApiLog.Debug($"Player {player.ToLogString()} is already muted");
                }
            }
            else
            {
                if (player.IsMuted || player.IsIntercomMuted)
                {
                    player.Unmute(true);
                    player.IntercomUnmute(true);
                    
                    player.SendAlert(AlertType.Warn, 10f, "Mute", "Mute vypršel.");

                    ApiLog.Info($"Unmuted player {player.ToLogString()}");
                }
                else
                {
                    ApiLog.Debug($"Player {player.ToLogString()} is not muted");
                }
            }
        }
        else
        {
            ApiLog.Warn($"Cannot synchronize punishment expiration for user &1{userId}&r: user not found.");
        }
    }

    private static void OnPlayerVerified(ExPlayer player)
    {
        ApiLog.Debug($"Handling player verification for player {player.ToLogString()}");
        
        if (player.IsNorthwoodStaff)
        {
            ApiLog.Info($"Verified &3northwood staff&r player {player.ToLogString()}");
            
            PlayerVerified?.Invoke(player);
        }
        else
        {
            if (Singleton != null)
            {
                Singleton.CallCmdGetActivePunishments(player.UserId, player.IpAddress, (_, list) =>
                {
                    if (list.Count(p => p.IsBan) > 0)
                    {
                        if (list.TryGetFirst(x => x.IsBan && x.IsPermanent, out var permanentBan))
                        {
                            player.Kick($"\n<b><color=red>[PRTS]</color></b>\n" +
                                        $"<b>Máš aktivní <color=red>PERMANENTNÍ BAN</color> na tomto serveru</b>!\n" +
                                        $"<color=yellow><b>{permanentBan.Reason}</b></color>");
                            
                            ApiLog.Info($"Kicked &1permanently&r &3banned&r player {player.ToLogString()}: &3{permanentBan.Id}&r");
                        }
                        else
                        {
                            var mostRecent = list
                                .OrderByDescending(p => p.IssuedAt)
                                .FirstOrDefault(x => x.IsBan);

                            player.Kick($"\n<b><color=red>[PRTS]</color></b>\n" +
                                        $"<b>Máš aktivní <color=red>BAN</color> na tomto serveru</b>!\n" +
                                        $"<color=yellow><b>{mostRecent.Reason}</b></color>\n" +
                                        $"Ban expiruje <b><color=red>{mostRecent.ExpiresAt.ToLocalTime().ToVeCzechString()}</color></b>!");
                            
                            ApiLog.Info($"Kicked &3banned&r player {player.ToLogString()}: &3{mostRecent.Id}&r");
                        }
                    }
                    else
                    {
                        ApiLog.Debug($"Player {player.ToLogString()} is not banned");

                        if (list.TryGetFirst(p => p.IsMute, out var mute))
                        {
                            player.Mute(false);
                            player.IntercomMute(false);
                            
                            player.SendAlert(AlertType.Warn, 10f, "Mute", 
                                $"Máš aktivní <color=yellow>mute</color>\n" +
                                $"<color=red>{mute.Reason}</color>\n" +
                                $"{(mute.IsPermanent ? "<color=red>PERMANENTNÍ</color>" : $"Expiruje <b><color=red>{mute.ExpiresAt.ToLocalTime().ToVeCzechString()}</color></b>!)")}");
                            
                            ApiLog.Info($"Muted player {player.ToLogString()}: &3{string.Join("&r, &3", list.Select(x => x.Id))}&r");
                        }
                        else
                        {
                            ApiLog.Debug($"Player {player.ToLogString()} is not muted");
                        }
                        
                        PlayerVerified?.Invoke(player);
                    }
                });
            }
            else
            {
                ApiLog.Warn("Punishment module not initialized!");
            }
        }
    }
}