using PRTS.Punishments.Enums;

namespace PRTS;

/// <summary>
/// Contains constants and methods for managing server permissions.
/// </summary>
public static class Permissions
{
    /// <summary>
    /// Permission to manage reports.
    /// </summary>
    public const string ManageReports = "ManageReports";

    /// <summary>
    /// Permission to manage staff.
    /// </summary>
    public const string ManageStaff = "ManageStaff";

    /// <summary>
    /// Permission to manage synchronized roles.
    /// </summary>
    public const string ManageSyncRoles = "ManageSyncRoles";

    /// <summary>
    /// Permission to manage the leaderboard.
    /// </summary>
    public const string ManageLeaderboard = "ManageLeaderboard";

    /// <summary>
    /// Permission to edit levels.
    /// </summary>
    public const string EditLevels = "EditLevels";

    /// <summary>
    /// Permission to reset levels.
    /// </summary>
    public const string ResetLevel = "ResetLevel";

    /// <summary>
    /// Permission to send remote commands.
    /// </summary>
    public const string SendRemoteCommands = "SendRemoteCommands";

    /// <summary>
    /// Permission to restart the server.
    /// </summary>
    public const string RestartServer = "RestartServer";

    /// <summary>
    /// Permission to shut down the server.
    /// </summary>
    public const string ShutdownServer = "ShutdownServer";

    /// <summary>
    /// Permission to restart the round.
    /// </summary>
    public const string RestartRound = "RestartRound";

    /// <summary>
    /// Permission to update the round or lobby lock.
    /// </summary>
    public const string UpdateRoundOrLobbyLock = "UpdateRoundOrLobbyLock";

    /// <summary>
    /// Permission to ban players.
    /// </summary>
    public const string BanPlayers = "BanPlayers";

    /// <summary>
    /// Permission to kick players.
    /// </summary>
    public const string KickPlayers = "KickPlayers";

    /// <summary>
    /// Permission to mute players.
    /// </summary>
    public const string MutePlayers = "MutePlayers";

    /// <summary>
    /// Permission to warn players.
    /// </summary>
    public const string WarnPlayers = "WarnPlayers";

    /// <summary>
    /// Permission to revoke punishments.
    /// </summary>
    public const string RevokePunishments = "RevokePunishments";

    /// <summary>
    /// Permission to search punishments.
    /// </summary>
    public const string SearchPunishments = "SearchPunishments";

    /// <summary>
    /// Gets the permission string for a specific punishment type and duration.
    /// </summary>
    /// <param name="type">The type of punishment.</param>
    /// <param name="isPermanent">Whether the punishment is permanent.</param>
    /// <returns>The permission string.</returns>
    public static string GetPunishmentPermission(this PunishmentType type, bool isPermanent)
    {
        if (isPermanent)
            return $"ManagePermanent{type}s";

        return $"ManageTemporary{type}s";
    }
}
