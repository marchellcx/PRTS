using LabExtended.API;

using PRTS.Client.Levels.Enums;
using PRTS.Client.Levels.Objects;

namespace PRTS.Client.Levels;

/// <summary>
/// A static class providing extension methods to manage and retrieve level and experience
/// data associated with an <see cref="ExPlayer"/>.
/// </summary>
public static class LevelExtensions
{
    /// <summary>
    /// Retrieves the current level of the specified player.
    /// </summary>
    /// <param name="player">The player for whom the level information is being retrieved.</param>
    /// <returns>The current level of the specified player if it exists; otherwise, 0.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the specified player is null or their reference hub is unavailable.</exception>
    public static int GetLevel(this ExPlayer player)
    {
        if (player?.ReferenceHub == null)
            throw new InvalidOperationException("Player is null!");
        
        if (LevelModule.PlayerLevels.TryGetValue(player.UserId, out var levelData))
            return levelData.CurLevel?.Level ?? 0;

        return 0;
    }

    /// <summary>
    /// Retrieves the current experience points (XP) of the specified player.
    /// </summary>
    /// <param name="player">The player for whom the experience information is being retrieved.</param>
    /// <returns>The current experience points (XP) of the specified player if it exists; otherwise, 0.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the specified player is null or their reference hub is unavailable.</exception>
    public static int GetExperience(this ExPlayer player)
    {
        if (player?.ReferenceHub == null)
            throw new InvalidOperationException("Player is null!");
        
        if (LevelModule.PlayerLevels.TryGetValue(player.UserId, out var levelData))
            return levelData.Experience;

        return 0;
    }

    /// <summary>
    /// Retrieves the level data associated with the specified player.
    /// </summary>
    /// <param name="player">The player for whom the level data is being retrieved.</param>
    /// <returns>The level data of the specified player if it exists; otherwise, null.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the specified player is null or their reference hub is unavailable.</exception>
    /// <remarks>This instance can be used as a direct reference to access player's level as this instance will be updated directly until the player leaves or the round restarts.</remarks>
    public static LevelData? GetLevelData(this ExPlayer player)
    {
        if (player?.ReferenceHub == null)
            throw new InvalidOperationException("Player is null!");

        if (LevelModule.PlayerLevels.TryGetValue(player.UserId, out var levelData))
            return levelData;
        
        return null;
    }

    /// <summary>
    /// Modifies the experience points (XP) of the specified player by a given amount.
    /// </summary>
    /// <param name="player">The player whose experience points are being modified.</param>
    /// <param name="xp">The amount of experience points to add or subtract. Positive values increase XP, while negative values decrease XP.</param>
    /// <param name="callback">An optional callback action to be invoked with the result of the XP modification.</param>
    /// <returns>True if the XP modification request was successfully sent to the server; otherwise, false.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the player is null or their reference hub is unavailable.</exception>
    public static bool ModifyXp(this ExPlayer player, int xp, Action<LevelModifyResult?>? callback = null)
    {
        if (player?.ReferenceHub == null)
            throw new InvalidOperationException("Player is null!");

        if (LevelModule.Singleton == null)
            return false;

        xp *= LevelModule.ExperienceMultiplier;

        LevelModule.Singleton.CallCmdModifyXp(player.UserId, xp, callback);
        return true;
    }

    /// <summary>
    /// Resets the experience points (XP) of the specified player to zero.
    /// </summary>
    /// <param name="player">The player whose experience points are to be reset.</param>
    /// <param name="callback">An optional callback action to be invoked with the result of the reset operation.</param>
    /// <returns>True if the reset request was successfully sent to the server; otherwise, false.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the player is null or their reference hub is unavailable.</exception>
    public static bool ResetXp(this ExPlayer player, Action<bool>? callback = null)
    {
        if (player?.ReferenceHub == null)
            throw new InvalidOperationException("Player is null!");

        if (LevelModule.Singleton == null)
            return false;

        LevelModule.Singleton.CallCmdResetXp(player.UserId, callback);
        return true;
    }

    /// <summary>
    /// Attempts to retrieve the current level of the specified player.
    /// </summary>
    /// <param name="player">The player whose level is being requested.</param>
    /// <param name="level">When this method returns, contains the level of the specified player if the operation succeeded, or 0 if the operation failed.</param>
    /// <returns>True if the level was successfully retrieved for the specified player; otherwise, false.</returns>
    public static bool TryGetLevel(this ExPlayer player, out int level)
    {
        if (LevelModule.PlayerLevels.TryGetValue(player.UserId, out var levels)
            && levels.CurLevel != null)
        {
            level = levels.CurLevel.Level;
            return true;
        }

        level = 0;
        return false;
    }

    /// <summary>
    /// Attempts to retrieve the current level and experience points (XP) of the specified player.
    /// </summary>
    /// <param name="player">
    /// The player for whom the level and XP information is being requested.
    /// </param>
    /// <param name="level">
    /// When this method returns, contains the level of the specified player if the operation succeeded,
    /// or 0 if the operation failed. This parameter is passed uninitialized.
    /// </param>
    /// <param name="xp">
    /// When this method returns, contains the experience points (XP) of the specified player if the operation succeeded,
    /// or 0 if the operation failed. This parameter is passed uninitialized.
    /// </param>
    /// <returns>
    /// true if the level and XP were successfully retrieved for the specified player; otherwise, false.
    /// </returns>
    public static bool TryGetLevelAndXp(this ExPlayer player, out int level, out int xp)
    {
        if (LevelModule.PlayerLevels.TryGetValue(player.UserId, out var levels)
            && levels.CurLevel != null)
        {
            xp = levels.Experience;
            level = levels.CurLevel.Level;

            return true;
        }

        xp = 0;
        level = 0;

        return false;
    }
}