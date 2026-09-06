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
    /// Adds experience points (XP) to the specified player.
    /// </summary>
    /// <param name="player">The player to whom the experience points will be added.</param>
    /// <param name="xp">The amount of experience points to add.</param>
    /// <param name="reason">An optional reason for adding the experience points.</param>
    /// <returns>True if the experience points were successfully added; otherwise, false.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the specified player is null or their reference hub is unavailable.</exception>
    /// <exception cref="ArgumentException">Thrown when the amount of experience points to add is less than one.</exception>
    public static bool AddXp(this ExPlayer player, int xp, string? reason = null) 
    {
        if (player?.ReferenceHub == null)
            throw new InvalidOperationException("Player is null!");

        if (xp < 1)
            throw new ArgumentException("XP to add must be greater than zero.", nameof(xp));

        if (LevelModule.Singleton == null)
            return false;

        LevelModule.Singleton.CallCmdModifyXp(player.UserId, reason, xp, null);
        return true;
    }

    /// <summary>
    /// Removes experience points (XP) from the specified player.
    /// </summary>
    /// <param name="player">The player from whom the experience points will be removed.</param>
    /// <param name="xp">The amount of experience points to remove.</param>
    /// <param name="reason">An optional reason for removing the experience points.</param>
    /// <returns>True if the experience points were successfully removed; otherwise, false.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the specified player is null or their reference hub is unavailable.</exception>
    /// <exception cref="ArgumentException">Thrown when the amount of experience points to remove is less than one.</exception>
    public static bool RemoveXp(this ExPlayer player, int xp, string? reason = null)
    {
        if (player?.ReferenceHub == null)
            throw new InvalidOperationException("Player is null!");

        if (xp < 1)
            throw new ArgumentException("XP to remove must be greater than zero.", nameof(xp));

        if (LevelModule.Singleton == null)
            return false;

        LevelModule.Singleton.CallCmdModifyXp(player.UserId, reason, -xp, null);
        return true;
    }

    /// <summary>
    /// Checks if the specified player has reached a specific level milestone.
    /// </summary>
    /// <param name="player">The player to check for the level milestone.</param>
    /// <param name="milestoneName">The name of the milestone to check.</param>
    /// <returns>True if the player has reached the specified milestone; otherwise, false.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the specified player is null or their reference hub is unavailable.</exception>
    public static bool HasLevelMilestone(this ExPlayer player, string milestoneName)
    {
        if (player?.ReferenceHub == null)
            throw new InvalidOperationException("Player is null!");

        if (LevelModule.PlayerLevels.TryGetValue(player.UserId, out var levelData)
            && levelData.CurLevel != null
            && levelData.CurLevel.MilestoneName == milestoneName)
            return true;

        return false;
    }

    /// <summary>
    /// Checks if the specified player has reached an exact level.
    /// </summary>
    /// <param name="player">The player to check for the exact level.</param>
    /// <param name="requiredLevel">The level to check against.</param>
    /// <returns>True if the player has reached the exact level; otherwise, false.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the specified player is null or their reference hub is unavailable.</exception>
    public static bool HasExactLevel(this ExPlayer player, int requiredLevel)
    {
        if (player?.ReferenceHub == null)
            throw new InvalidOperationException("Player is null!");

        if (LevelModule.PlayerLevels.TryGetValue(player.UserId, out var levelData))
            return levelData.CurLevel?.Level == requiredLevel;

        return false;
    }

    /// <summary>
    /// Checks if the specified player has reached at least a certain level.
    /// </summary>
    /// <param name="player">The player to check for the minimum level.</param>
    /// <param name="requiredLevel">The level to check against.</param>
    /// <returns>True if the player has reached at least the specified level; otherwise, false.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the specified player is null or their reference hub is unavailable.</exception>
    public static bool HasAtLeastLevel(this ExPlayer player, int requiredLevel)
    {
        if (player?.ReferenceHub == null)
            throw new InvalidOperationException("Player is null!");

        if (LevelModule.PlayerLevels.TryGetValue(player.UserId, out var levelData))
            return levelData.CurLevel?.Level >= requiredLevel;

        return false;
    }

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
    /// Retrieves the milestone name associated with the current level of the specified player.
    /// </summary>
    /// <param name="player">The player for whom the milestone name is being retrieved.</param>
    /// <returns>The milestone name of the specified player's current level if it exists; otherwise, null.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the specified player is null or their reference hub is unavailable.</exception>
    public static string? GetMilestoneName(this ExPlayer player)
    {
        if (player?.ReferenceHub == null)
            throw new InvalidOperationException("Player is null!");

        if (LevelModule.PlayerLevels.TryGetValue(player.UserId, out var levelData))
            return levelData.CurLevel?.MilestoneName;

        return null;
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
    /// <param name="reason">An optional reason for the XP modification.</param>
    /// <param name="callback">An optional callback action to be invoked with the result of the XP modification.</param>
    /// <returns>True if the XP modification request was successfully sent to the server; otherwise, false.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the player is null or their reference hub is unavailable.</exception>
    public static bool ModifyXp(this ExPlayer player, int xp, string? reason = null, Action<LevelModifyResult?>? callback = null)
    {
        if (player?.ReferenceHub == null)
            throw new InvalidOperationException("Player is null!");

        if (LevelModule.Singleton == null)
            return false;

        xp *= LevelModule.ExperienceMultiplier;

        LevelModule.Singleton.CallCmdModifyXp(player.UserId, reason, xp, callback);
        return true;
    }

    /// <summary>
    /// Resets the experience points (XP) of the specified player to zero.
    /// </summary>
    /// <param name="player">The player whose experience points are to be reset.</param>
    /// <param name="reason">An optional reason for resetting the experience points.</param>
    /// <param name="callback">An optional callback action to be invoked with the result of the reset operation.</param>
    /// <returns>True if the reset request was successfully sent to the server; otherwise, false.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the player is null or their reference hub is unavailable.</exception>
    public static bool ResetXp(this ExPlayer player, string? reason = null, Action<bool>? callback = null)
    {
        if (player?.ReferenceHub == null)
            throw new InvalidOperationException("Player is null!");

        if (LevelModule.Singleton == null)
            return false;

        LevelModule.Singleton.CallCmdResetXp(player.UserId, reason, callback);
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

    /// <summary>
    /// Attempts to retrieve the milestone name associated with the current level of the specified player.
    /// </summary>
    /// <param name="player">The player whose milestone name is being requested.</param>
    /// <param name="milestoneName">When this method returns, contains the milestone name of the specified player's current level if the operation succeeded, or null if the operation failed.</param>
    /// <returns>True if the milestone name was successfully retrieved for the specified player; otherwise, false.</returns>
    public static bool TryGetMilestoneName(this ExPlayer player, out string? milestoneName)
    {
        if (LevelModule.PlayerLevels.TryGetValue(player.UserId, out var levels)
            && levels.CurLevel != null)
        {
            milestoneName = levels.CurLevel.MilestoneName;
            return !string.IsNullOrEmpty(milestoneName);
        }

        milestoneName = null;
        return false;
    }

    /// <summary>
    /// Attempts to retrieve the level data associated with the specified player.
    /// </summary>
    /// <param name="player">The player whose level data is being requested.</param>
    /// <param name="levelData">When this method returns, contains the level data of the specified player if the operation succeeded, or null if the operation failed.</param>
    /// <returns>True if the level data was successfully retrieved for the specified player; otherwise, false.</returns>
    public static bool TryGetLevelData(this ExPlayer player, out LevelData? levelData)
    {
        if (LevelModule.PlayerLevels.TryGetValue(player.UserId, out var levels))
        {
            levelData = levels;
            return true;
        }

        levelData = null;
        return false;
    }
}