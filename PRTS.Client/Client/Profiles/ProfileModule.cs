using LabExtended.API;

using LabExtended.Core;
using LabExtended.Events;

using NiveraAPI.IO.Serialization;
using NiveraAPI.IO.Network.Entities.Attributes;

using PRTS.Client.Profiles.Objects;
using PRTS.Client.Profiles.Sessions;

using PRTS.Client.Punishments;

namespace PRTS.Client.Profiles;

/// <summary>
/// The ProfileModule class is responsible for managing the lifecycle of player profile sessions within a client context.
/// It handles initialization, cleanup, and session orchestration for connected players by subscribing to necessary events,
/// starting sessions, providing updates, and applying role-based configurations.
/// </summary>
[ServerType("PRTS.ScpSl.Modules.Profiles.ProfileModule")]
public class ProfileModule : PrtsModule
{
    /// <summary>
    /// The singleton instance of the ProfileModule.
    /// </summary>
    public static ProfileModule Singleton;

    static ProfileModule()
    {
        ExPlayerEvents.Left += OnPlayerLeft;
        PunishmentModule.PlayerVerified += OnPlayerVerified;
    }
    
    [IndexField] private static ushort cmd_CmdGetProfile;
    [IndexField] private static ushort cmd_CmdGetSessions;
    
    [IndexField] private static ushort cmd_CmdStartSession;
    [IndexField] private static ushort cmd_CmdUpdateSession;

    public static Dictionary<ExPlayer, ProfileUpdater> Sessions { get; } = new();

    public static Dictionary<string, string[]> Roles { get; } = new();

    /// <summary>
    /// Initializes and prepares the client session when a client is spawned.
    /// Subscribes to player-related events, ensures any existing profile sessions are stopped and cleared,
    /// and initiates new profile sessions for all connected players.
    /// This method is invoked during the spawning phase of the client lifecycle within the module context.
    /// </summary>
    public override void OnClientSpawned()
    {
        base.OnClientSpawned();

        Singleton = this;
        
        foreach (var kvp in Sessions)
            kvp.Value.Stop();

        Sessions.Clear();
        
        foreach (var player in ExPlayer.Players)
            CallCmdStartSession(player.UserId, player.Nickname, player.IpAddress);
    }

    /// <summary>
    /// Handles cleanup and disposal of resources during the destruction of the module.
    /// This method ensures that all event subscriptions are unsubscribed,
    /// active session activities are stopped, and relevant data structures are cleared.
    /// </summary>
    public override void OnDestroyed()
    {
        base.OnDestroyed();
        
        foreach (var kvp in Sessions)
            kvp.Value.Stop();

        Sessions.Clear();

        Singleton = null!;
    }

    /// <summary>
    /// Retrieves the profile sessions associated with the specified user.
    /// This method sends a remote request to fetch session data
    /// for the user and invokes the provided callback with the resulting data.
    /// </summary>
    /// <param name="userId">
    /// The unique identifier of the user whose profile sessions are being queried.
    /// Must not be null or empty.
    /// </param>
    /// <param name="callback">
    /// A callback function that will be invoked with the retrieved profile sessions.
    /// The dictionary key corresponds to the session ID, and the value is the respective session instance.
    /// If no sessions are found, the callback will be triggered with a null value.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the <paramref name="userId"/> is null or empty, or when the <paramref name="callback"/> is null.
    /// </exception>
    public void CallCmdGetSessions(string userId, Action<Dictionary<string, ProfileSession>?> callback)
    {
        if (string.IsNullOrEmpty(userId))
            throw new ArgumentNullException(nameof(userId));
        
        if (callback == null)
            throw new ArgumentNullException(nameof(callback));
        
        SendRemoteResponse(cmd_CmdGetSessions, userId, callback);
    }

    /// <summary>
    /// Requests and retrieves the profile information associated with the specified user ID.
    /// This method sends a remote call to fetch the user's profile from the server and invokes the provided callback
    /// with the retrieved profile data.
    /// </summary>
    /// <param name="userId">The unique identifier of the user whose profile information is being requested.</param>
    /// <param name="callback">The callback action to be invoked with the retrieved <see cref="ProfileInfo"/> instance.
    /// This parameter must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="userId"/> is null or empty,
    /// or when the <paramref name="callback"/> is null.</exception>
    public void CallCmdGetProfile(string userId, Action<ProfileInfo?> callback)
    {
        if (string.IsNullOrEmpty(userId))
            throw new ArgumentNullException(nameof(userId));
        
        if (callback == null)
            throw new ArgumentNullException(nameof(callback));
        
        void Response(ByteReader? reader)
        {
            if (reader != null && reader.ReadBool())
            {
                callback(ProfileSerialization.DeserializeProfile(reader));
            }
            else
            {
                callback(null);
            }
        }

        SendRemoteCallback(cmd_CmdGetProfile, writer => writer.WriteString(userId), Response);
    }

    /// <summary>
    /// Initiates a user session and synchronizes session-related information across the network.
    /// This method sends a command to begin a session for the specified user, ensuring the user's session is registered and tracked.
    /// </summary>
    /// <param name="userId">The unique identifier of the user starting the session.</param>
    /// <param name="userNick">The nickname or display name of the user starting the session.</param>
    /// <param name="userIp">The IP address of the user starting the session.</param>
    public void CallCmdStartSession(string userId, string userNick, string userIp)
    {
        SendRemoteCallback(cmd_CmdStartSession, writer =>
        {
            writer.WriteString(userId);
            writer.WriteString(userNick);
            writer.WriteString(userIp);
        });
    }
    
    /// <summary>
    /// Sends a command to update a user's session and synchronize session-related data across the network.
    /// This method triggers the remote session update process for the specified user, ensuring consistency in session tracking and related operations.
    /// </summary>
    /// <param name="userId">The unique identifier of the user whose session is being updated.</param>
    /// <param name="sessionId">The unique identifier associated with the session to be updated.</param>
    public void CallCmdUpdateSession(string userId, string sessionId)
    {
        SendRemoteCallback(cmd_CmdUpdateSession, writer =>
        {
            writer.WriteString(userId);
            writer.WriteString(sessionId);
        });
    }
    
    /// <summary>
    /// Sends a directive to assign roles to a user and synchronize their permissions and group properties across the network.
    /// This method triggers the role update remotely for the specified user, ensuring consistency in permissions and group structure.
    /// </summary>
    [ClientRpc]
    public void RpcSetRoles(ByteReader reader)
    {
        SetRoleSync(reader.ReadString(), reader.ReadArray<string>());
    }

    /// <summary>
    /// Initiates a new session for a user and synchronizes session-related data across the network.
    /// This method triggers the remote session start process for the specified user, ensuring that all relevant systems are updated.
    /// </summary>
    [ClientRpc]
    public void RpcStartSession(ByteReader reader)
    {
        var userId = reader.ReadString();
        var sessionId = reader.ReadString();
        
        if (!ExPlayer.TryGet(userId, out var player))
        {
            ApiLog.Warn($"Could not start session for user &1{userId}&r: player not found!");
            return;
        }
        
        if (Sessions.TryGetValue(player, out var session))
            session.Stop();

        session = new(sessionId, userId, player, this);
        session.Start();

        Sessions[player] = session;
    }

    private static void OnPlayerLeft(ExPlayer player)
    {
        if (Sessions.TryGetValue(player, out var session))
        {
            session.Stop();

            Sessions.Remove(player);
        }
    }

    private static void OnPlayerVerified(ExPlayer player)
    {
        Singleton?.CallCmdStartSession(player.UserId, player.Nickname, player.IpAddress);
    }

    /// <summary>
    /// Assigns roles to a user and synchronizes the user's permissions and group properties.
    /// Updates the user's group based on the specified roles and refreshes their server permissions.
    /// </summary>
    /// <param name="userId">The unique identifier of the user to whom the roles will be assigned.</param>
    /// <param name="roles">An array of role names that will be used to update the user's group and permissions.</param>
    public static void SetRoleSync(string userId, string[] roles)
    {
        if (roles.Length == 0)
        {
            ApiLog.Warn($"No roles received for user &1{userId}&r!");
            return;
        }

        if (!ExPlayer.TryGetByUserId(userId, out var player))
        {
            ApiLog.Warn($"User &1{userId}&r not found!");
            return;       
        }
        
        ApiLog.Debug($"Setting roles for user &1{userId}&r: &3{string.Join(", ", roles)}&r");

        Roles[player.UserId] = roles;

        if (roles.Length > 0)
        {
            player.UserGroup = GenerateCombinedGroup(roles);
            player.ReferenceHub.serverRoles.RefreshPermissions();
        }
        else
        {
            var group = ServerStatic.PermissionsHandler.GetGroup(roles[0]);

            if (group == null)
            {
                ApiLog.Warn($"Could not find role &1{roles[0]}&r!");
                return;
            }
            
            player.UserGroup = group;
            player.ReferenceHub.serverRoles.RefreshPermissions();
        }
    }
    
    /// <summary>
    /// Generates a combined user group based on the given roles.
    /// The resulting group aggregates properties such as permissions, badge details,
    /// visibility, and kick power from the provided roles.
    /// </summary>
    /// <param name="roles">An array of role names used to generate the combined group.</param>
    /// <returns>A <see cref="UserGroup"/> object representing the aggregated group with combined properties from the specified roles.</returns>
    public static UserGroup GenerateCombinedGroup(string[] roles)
    {
        var group = new UserGroup();
            
        foreach (var role in roles)
        {
            var found = ServerStatic.PermissionsHandler.GetGroup(role);

            if (found == null)
            {
                ApiLog.Warn($"Could not find role &1{role}&r!");
                continue;
            }
                
            if (string.IsNullOrEmpty(group.BadgeColor))
                group.BadgeColor = found.BadgeColor;
                
            if (string.IsNullOrEmpty(group.BadgeText))
                group.BadgeText = found.BadgeText;
            else
                group.BadgeText += $" | {found.BadgeText}";

            if (string.IsNullOrEmpty(group.Name))
                group.Name = found.Name;
            else
                group.Name += $",{found.Name}";
                
            if (found.KickPower > group.KickPower)
                group.KickPower = found.KickPower;
                
            if (found.RequiredKickPower > group.RequiredKickPower)
                group.RequiredKickPower = found.RequiredKickPower;
                
            group.Cover |= found.Cover;
            group.Permissions |= found.Permissions;
            group.HiddenByDefault |= found.HiddenByDefault;
        }

        return group;
    }
}