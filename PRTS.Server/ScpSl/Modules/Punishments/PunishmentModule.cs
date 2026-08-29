using NiveraAPI.IO.Network.Entities.Attributes;
using NiveraAPI.IO.Serialization;

using PRTS.Profiles;

using PRTS.Punishments;
using PRTS.Punishments.Enums;
using PRTS.Punishments.Objects;

namespace PRTS.ScpSl.Modules.Punishments;

/// <summary>
/// Represents a module for handling punishments.
/// </summary>
[ClientType("PRTS.Client.Punishments.PunishmentModule")]
public class PunishmentModule : ScpSlModule
{
    [IndexField] private static ushort rpc_RpcPunishmentIssued;
    [IndexField] private static ushort rpc_RpcPunishmentRemoved;

    /// <summary>
    /// Sends a remote procedure call to notify that a punishment has been removed.
    /// </summary>
    /// <param name="userId">
    /// The unique identifier of the user whose punishment has been removed. Must not be null or empty.
    /// </param>
    /// <param name="activePunishments">
    /// A list of <see cref="PunishmentInfo"/> objects representing the active punishments remaining for the user. Must not be null.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the <paramref name="userId"/> parameter is null or empty,
    /// or when the <paramref name="activePunishments"/> parameter is null.
    /// </exception>
    public void CallRpcPunishmentRemoved(string userId, List<PunishmentInfo> activePunishments)
    {
        if (string.IsNullOrEmpty(userId))
            throw new ArgumentNullException(nameof(userId));
        
        if (activePunishments == null)
            throw new ArgumentNullException(nameof(activePunishments));
        
        SendRemoteCallback(rpc_RpcPunishmentRemoved, writer =>
        {
            writer.WriteString(userId);
            writer.WriteList(activePunishments);
        });
    }

    /// <summary>
    /// Sends a remote procedure call to notify that a punishment has been issued.
    /// </summary>
    /// <param name="userId">
    /// The unique identifier of the user for whom the punishment is issued. Must not be null or empty.
    /// </param>
    /// <param name="punishment">
    /// An instance of <see cref="PunishmentInfo"/> containing the details of the issued punishment. Must not be null.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the <paramref name="userId"/> parameter is null or empty,
    /// or when the <paramref name="punishment"/> parameter is null.
    /// </exception>
    public void CallRpcPunishmentIssued(string userId, PunishmentInfo punishment)
    {
        if (string.IsNullOrEmpty(userId))
            throw new ArgumentNullException(nameof(userId));
        
        if (punishment == null)
            throw new ArgumentNullException(nameof(punishment));
        
        SendRemoteCallback(rpc_RpcPunishmentIssued, writer =>
        {
            writer.WriteString(userId);
            writer.Write(punishment);
        });   
    }

    /// <summary>
    /// Handles a server command to retrieve a list of punishments based on specified filter criteria.
    /// </summary>
    /// <param name="reader">
    /// An instance of <see cref="ByteReader"/> used to read the input data for filtering.
    /// This includes optional staff ID, target ID, punishment type, status, and time range.
    /// </param>
    /// <param name="writer">
    /// An instance of <see cref="ByteWriter"/> used to write the filtered list of punishments back to the client.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when either <paramref name="reader"/> or <paramref name="writer"/> is null.
    /// </exception>
    [ServerCmd(true)]
    public void CmdListPunishments(ByteReader reader, ByteWriter writer)
    {
        var id = reader.ReadString();
        var staffId = reader.ReadString();
        var targetId = reader.ReadString();

        PunishmentType? type = null;
        PunishmentStatus? status = null;

        DateTime? from = null;
        DateTime? to = null;
        
        if (reader.ReadBool())
            type = (PunishmentType)reader.ReadByte();
        
        if (reader.ReadBool())
            status = (PunishmentStatus)reader.ReadByte();
        
        if (reader.ReadBool())
            from = reader.ReadDate();
        
        if (reader.ReadBool())
            to = reader.ReadDate();
        
        Log.Info($"Searching for punishments: " +
                 $"{(string.IsNullOrEmpty(staffId) ? "no staff" : $"staff &1{staffId}&r")}" +
                 $"{(string.IsNullOrEmpty(targetId) ? "no target" : $"target &1{targetId}&r")}" +
                 $"{type?.ToString() ?? "no type"} " +
                 $"{status?.ToString() ?? "no status"} " +
                 $"{from?.ToString() ?? "no from"} " +
                 $"{to?.ToString() ?? "no to"}");

        var punishments = PunishmentManager.GetPunishments(x =>
        {
            if (!string.IsNullOrEmpty(id) && x.Id != id)
                return false;

            if (!string.IsNullOrEmpty(staffId) && x.StaffId != staffId)
                return false;
            
            if (!string.IsNullOrEmpty(targetId) && x.TargetId != targetId)
                return false;
            
            if (type.HasValue && x.Type != type.Value)
                return false;
            
            if (status.HasValue && x.Status != status.Value)
                return false;
            
            if (from.HasValue && x.IssuedAt < from.Value)
                return false;
            
            if (to.HasValue && x.ExpiresAt > to.Value)
                return false;
            
            return true;
        });
        
        writer.WriteList(punishments);
    }

    /// <summary>
    /// Handles the revocation of a punishment based on the provided data.
    /// </summary>
    /// <param name="reader">
    /// An instance of <see cref="ByteReader"/> used to read the necessary information for revoking the punishment.
    /// Must not be null.
    /// </param>
    /// <param name="writer">
    /// An instance of <see cref="ByteWriter"/> used to write the response back to the client.
    /// Must not be null.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the <paramref name="reader"/> or <paramref name="writer"/> parameter is null.
    /// </exception>
    /// <remarks>
    /// This method reads the punishment ID, staff ID, and reason for revocation from the <paramref name="reader"/>.
    /// It ensures the punishment and staff exist before proceeding to revoke the punishment.
    /// The outcome of the operation is written back using the <paramref name="writer"/>.
    /// </remarks>
    [ServerCmd(true)]
    public void CmdRevokePunishment(ByteReader reader, ByteWriter writer)
    {
        var id = reader.ReadString();
        var staffId = reader.ReadString();
        var reason = reader.ReadString();

        if (!PunishmentManager.TryGetPunishment(x => x.Id == id, out var punishment))
        {
            Log.Warn($"Received revoke punishment request for unknown punishment &1{id}&r");

            writer.WriteBool(false);
            return;
        }

        if (!ProfileManager.TryGetProfileByUserId(staffId, out var staffProfile))
        {
            Log.Warn($"Received revoke punishment request for unknown staff &1{staffId}&r");

            writer.WriteBool(true);
            writer.Write(punishment);

            return;
        }

        PunishmentManager.RevokePunishment(punishment, staffProfile.Value, reason);

        writer.WriteBool(true);
        
        PunishmentSerialization.WritePunishmentInfo(writer, punishment.Value);
    }

    /// <summary>
    /// Issues a punishment to a specified target based on the provided parameters.
    /// This command is executed on the server side and modifies the punishment-related state.
    /// </summary>
    /// <param name="reader">
    /// An instance of <see cref="ByteReader"/> containing the serialized input data for the punishment operation,
    /// including staff and target user IDs, reason, punishment type, expiration time, and applicable servers.
    /// Must not be null.
    /// </param>
    /// <param name="writer">
    /// An instance of <see cref="ByteWriter"/> used to write the result of the punishment operation back to the client.
    /// Must not be null.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when either the <paramref name="reader"/> or <paramref name="writer"/> parameter is null.
    /// </exception>
    /// <remarks>
    /// The method processes input data from the provided reader, identifies the staff and target profiles using their user IDs,
    /// and applies the specified punishment type. If the punishment is successfully created, it is returned through the <paramref name="writer"/>
    /// along with a success result. Failure conditions, such as missing profiles, are also written back as appropriate.
    /// </remarks>
    [ServerCmd(true)]
    public void CmdIssuePunishment(ByteReader reader, ByteWriter writer)
    {
        var staffId = reader.ReadString();
        var targetId = reader.ReadString();
        var reason = reader.ReadString();
        var type = (PunishmentType)reader.ReadByte();
        var expires = reader.ReadDate();
        var applied = reader.ReadArray<string>();
        
        Log.Info($"Issuing punishment to &1{targetId}&r by &1{staffId}&r: &1{reason}&r");
        
        if (!ProfileManager.TryGetProfileByUserId(staffId, out var staffProfile))
        {
            writer.WriteByte((byte)PunishmentResult.StaffProfileNotFound);
            return;
        }

        var wasByIp = false;

        if (!ProfileManager.TryGetProfileByUserId(targetId, out var targetProfile)
            && !ProfileManager.TryGetProfileByIp(targetId, out targetProfile)
            && !ProfileManager.TryGetProfileByNick(targetId, out targetProfile))
        {

            writer.WriteByte((byte)PunishmentResult.TargetProfileNotFound);
            return;
        }

        var info = PunishmentManager.IssuePunishment(
            staffProfile.Value,
            targetProfile.Value, 
            type,
            expires == DateTime.MinValue ? null : expires,
            Server.ServerAlias,
            null,
            reason,
            applied);

        if (info != null)
        {
            writer.WriteByte((byte)PunishmentResult.Ok);
            writer.Write(info);
        }
        else
        {
            writer.WriteByte((byte)PunishmentResult.Failed);
        }
    }
    
    /// <summary>
    /// Handles a server command to retrieve the active punishments for a specific user.
    /// </summary>
    /// <param name="reader">
    /// A <see cref="ByteReader"/> object used to read the input necessary for processing the command.
    /// Must not be null and should contain the user ID string.
    /// </param>
    /// <param name="writer">
    /// A <see cref="ByteWriter"/> object used to write the output data containing the list of active punishments.
    /// Must not be null.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the user ID read from the <paramref name="reader"/> is null or empty.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the user ID read from the <paramref name="reader"/> does not follow the required format.
    /// </exception>
    /// <remarks>
    /// If the requested user ID does not correspond to any existing profile, a warning will be logged,
    /// and no punishment data will be written to the <paramref name="writer"/>.
    /// </remarks>
    [ServerCmd(true)]
    public void CmdGetActivePunishments(ByteReader reader, ByteWriter writer)
    {
        var userId = reader.ReadString();
        var userIp = reader.ReadString();

        if (!ProfileManager.TryGetProfileByUserId(userId, out var profile))
        {
            if (ProfileManager.TryGetProfileByIp(userIp, out profile))
            {
                if (profile.Value.CustomData.ContainsKey("PunishmentsIgnoreIp"))
                {
                    writer.WriteInt32(0); // empty list
                    return;
                }
            }
            else
            {
                writer.WriteInt32(0);
                
                Log.Warn($"Received active punishments request for unknown user &1{userId}&r");
                return;
            }
        }

        var active = PunishmentManager.GetPunishments(x =>
        {
            if (!x.IsActive)
                return false;

            if (x.TargetId != profile.Value.Id)
                return false;

            if (x.AppliedServers.Length > 0 && !x.AppliedServers.Contains(Server.ServerAlias))
                return false;

            return true;
        });
        
        writer.WriteList(active);
    }
}