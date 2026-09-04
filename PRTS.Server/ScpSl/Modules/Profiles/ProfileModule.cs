using NiveraAPI.IO.Network.Entities.Attributes;
using NiveraAPI.IO.Serialization;

using PRTS.Discord;
using PRTS.Database;
using PRTS.Profiles;
using PRTS.RoleSync;
using PRTS.Extensions;
using PRTS.Profiles.Objects;

using NiveraAPI.Extensions;

namespace PRTS.ScpSl.Modules.Profiles;

/// <summary>
/// Represents the ProfileModule that facilitates managing user profiles, roles, and sessions
/// within the server system. This module provides methods for sending remote procedure calls
/// to initiate or manage specific user-related operations.
/// </summary>
[ClientType("PRTS.Client.Profiles.ProfileModule")]
public class ProfileModule : ScpSlModule
{
    [IndexField] private static ushort rpc_RpcSetRoles;
    [IndexField] private static ushort rpc_RpcStartSession;
    
    /// <summary>
    /// Sends a remote procedure call to set the roles for a user on the server.
    /// </summary>
    /// <param name="userId">The unique identifier of the user whose roles are being set.</param>
    /// <param name="roles">An array of roles to be assigned to the user.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="userId"/> is null or empty,
    /// or when <paramref name="roles"/> is null.
    /// </exception>
    public void CallRpcSetRoles(string userId, string[] roles)
    {
        if (string.IsNullOrEmpty(userId))
            throw new ArgumentNullException(nameof(userId));

        if (roles == null)
            throw new ArgumentNullException(nameof(roles));

        SendRemoteCallback(rpc_RpcSetRoles, writer =>
        {
            writer.WriteString(userId);
            writer.WriteArray(roles);
        });
    }

    /// <summary>
    /// Sends a remote procedure call to start a session for a user on the server.
    /// </summary>
    /// <param name="userId">The unique identifier of the user for whom the session is being started.</param>
    /// <param name="sessionId">The unique identifier of the session being started.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="userId"/> is null or empty,
    /// or when <paramref name="sessionId"/> is null or empty.
    /// </exception>
    public void CallRpcStartSession(string userId, string sessionId)
    {
        if (string.IsNullOrEmpty(userId))
            throw new ArgumentNullException(nameof(userId));
        
        if (string.IsNullOrEmpty(sessionId))
            throw new ArgumentNullException(nameof(sessionId));
        
        SendRemoteCallback(rpc_RpcStartSession, writer =>
        {
            writer.WriteString(userId);
            writer.WriteString(sessionId);
        });
    }

    /// <summary>
    /// Ends a user session by marking it as completed in the server's profile management system.
    /// </summary>
    [ServerCmd]
    public void CmdUpdateSession(ByteReader reader)
    {
        var userId = reader.ReadString();
        var sessionId = reader.ReadString();
        
        if (!ProfileManager.TryGetProfileByUserId(userId, out var profile))
        {
            Log.Warn($"Received session update for unknown user &1{userId}&r");
            return;
        }

        if (!profile.Value.Sessions.TryGetValue(sessionId, out var session))
        {
            Log.Warn($"Received session update for unknown session &1{sessionId}&r");
            return;
        }
        
        session.Ended = DateTime.UtcNow;

        profile.IsDirty = true;
    }

    /// <summary>
    /// Retrieves all profile sessions associated with the specified user identifier.
    /// </summary>
    /// <returns>
    /// A dictionary where the keys are session IDs and the values are <see cref="ProfileSession"/> objects
    /// representing the user's active or historical sessions. Returns <c>null</c> if no profile is found for the user.
    /// </returns>
    [ServerCmd(true)]
    public Dictionary<string, ProfileSession>? CmdGetSessions(ByteReader reader)
    {
        if (!ProfileManager.TryGetProfileByUserId(reader.ReadString(), out var profile))
            return null;

        return Enumerable.ToDictionary(profile.Value.Sessions);
    }

    /// <summary>
    /// Retrieves the profile information for a user based on their unique identifier.
    /// </summary>
    /// <returns>
    /// The <see cref="ProfileInfo"/> object containing the profile details if the user is found;
    /// otherwise, null if the user does not exist in the system.
    /// </returns>
    [ServerCmd(true)]
    public void CmdGetProfile(ByteReader reader, ByteWriter writer)
    {
        if (!ProfileManager.TryGetProfileByUserId(reader.ReadString(), out var profile))
        {
            writer.WriteBool(false);
            return;
        }

        writer.WriteBool(true);

        ProfileSerialization.SerializeProfile(writer, profile.Value, false);
    }

    /// <summary>
    /// Confirms that a user has joined the server and updates their profile,
    /// while also synchronizing roles from an external service, such as Discord.
    /// </summary>
    [ServerCmd]
    public void CmdStartSession(ByteReader reader)
    {
        var userId = reader.ReadString();
        var userNick = reader.ReadString();
        var userIp = reader.ReadString();
        
        void SendRoles(List<ulong>? roles, ulong discordId)
        {
            if (roles == null || roles.Count < 1)
                return;
            
            var syncRoles = RoleSyncRoles.GetRoles(roles, discordId);
            
            if (syncRoles == null || syncRoles.Length < 1)
                return;
            
            CallRpcSetRoles(userId, syncRoles);
        }
        
        var profile = ProfileManager.AddOrUpdateProfile(userId, userNick, userIp);

        if (profile != null)
        {
            var sessionId = DbManager.NewId;
            var session = new ProfileSession
            {
                Id = sessionId,
                Started = DateTime.UtcNow
            };

            profile.Value.Sessions.TryAdd(sessionId, session);       
            profile.IsDirty = true;
            
            CallRpcStartSession(userId, sessionId);

            if (profile.Value.DiscordId == 0)
            {
                if (userId.TrySplit('@', true, 2, out var idSegments)
                    && idSegments[1] == "discord"
                    && ulong.TryParse(idSegments[0], out var discordId))
                {
                    profile.Value.DiscordId = discordId;
                    profile.IsDirty = true;

                    Log.Debug($"Synchronized Discord ID via game: &3{discordId}&r (profile &6{profile.Value.Id}&r");
                }
            }
            
            if (profile.Value.DiscordId != 0)
            {
                Task.Run(async () => await DiscordBot.TryGetRoleIds(0, profile.Value.DiscordId)).ContinueOnMainThread(profile.Value.DiscordId, SendRoles);
            }
        }
        else
        {
            Log.Error($"Failed to update profile for user ID &1{userId}&r");
        }
    }
}