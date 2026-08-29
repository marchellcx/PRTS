using LabExtended.API;

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
    /// <param name="player">
    /// The player for whom the level information is being retrieved.
    /// </param>
    /// <returns>
    /// The current level of the specified player if it exists; otherwise, 0.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the specified player is null or their reference hub is unavailable.
    /// </exception>
    public static int GetLevel(this ExPlayer player)
    {
        if (player?.ReferenceHub == null)
            throw new InvalidOperationException("Player is null!");
        
        if (LevelModule.Levels.TryGetValue(player, out var levels))
            return levels.Level;

        return 0;
    }

    /// <summary>
    /// Retrieves the current experience points of the specified player.
    /// </summary>
    /// <param name="player">
    /// The player for whom the experience information is being retrieved.
    /// </param>
    /// <returns>
    /// The current experience points of the specified player if it exists; otherwise, 0.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the specified player is null or their reference hub is unavailable.
    /// </exception>
    public static int GetExperience(this ExPlayer player)
    {
        if (player?.ReferenceHub == null)
            throw new InvalidOperationException("Player is null!");
        
        if (LevelModule.Levels.TryGetValue(player, out var levels))
            return levels.Experience;

        return 0;
    }

    /// <summary>
    /// Retrieves both the current level and experience of the specified player.
    /// </summary>
    /// <param name="player">
    /// The player for whom the level and experience information is being retrieved.
    /// </param>
    /// <returns>
    /// A tuple containing the current level and experience of the specified player.
    /// If the level data does not exist, it returns (0, 0).
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the specified player is null or their reference hub is unavailable.
    /// </exception>
    public static (int Level, int Experience) GetLevelInfo(this ExPlayer player)
    {
        if (player?.ReferenceHub == null)
            throw new InvalidOperationException("Player is null!");

        if (LevelModule.Levels.TryGetValue(player, out var levels))
            return levels;
        
        return (0, 0);
    }

    /// <summary>
    /// Modifies the experience points (XP) of the specified player.
    /// </summary>
    /// <param name="player">
    /// The player whose experience points are being modified.
    /// </param>
    /// <param name="xp">
    /// The amount of experience points to add or subtract. Positive values increase XP, while negative values decrease XP.
    /// </param>
    /// <param name="reasonId">
    /// A unique identifier indicating the reason for the XP modification.
    /// </param>
    /// <param name="reasonMessage">
    /// A descriptive message providing additional context about the XP modification.
    /// </param>
    /// <returns>
    /// <c>true</c> if the XP modification request was successfully sent to the server; otherwise, <c>false</c>.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the player is null or their reference hub is unavailable.
    /// </exception>
    public static bool ModifyXp(this ExPlayer player, int xp, string reasonId, string reasonMessage)
    {
        if (player?.ReferenceHub == null)
            throw new InvalidOperationException("Player is null!");

        if (LevelModule.Singleton == null)
            return false;
        
        LevelModule.Singleton.CallCmdModifyXp(player.UserId, reasonId, reasonMessage, xp, null);
        return true;
    }

    /// <summary>
    /// Resets the experience points of the specified player.
    /// </summary>
    /// <param name="player">
    /// The player whose experience points are to be reset.
    /// </param>
    /// <param name="reasonId">
    /// The identifier representing the reason for resetting the experience.
    /// </param>
    /// <param name="reasonMessage">
    /// A descriptive message providing additional context for the reset action.
    /// </param>
    /// <returns>
    /// True if the reset action was successfully initiated; otherwise, false.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the specified player is null or their reference hub is unavailable.
    /// </exception>
    public static bool ResetXp(this ExPlayer player, string reasonId, string reasonMessage)
    {
        if (player?.ReferenceHub == null)
            throw new InvalidOperationException("Player is null!");

        if (LevelModule.Singleton == null)
            return false;

        LevelModule.Singleton.CallCmdResetXp(player.UserId, reasonId, reasonMessage, null);
        return true;
    }
    
    /// <summary>
    /// Attempts to retrieve the current level of the specified player.
    /// </summary>
    /// <param name="player">
    /// The player for whom the level information is being requested.
    /// </param>
    /// <param name="level">
    /// When this method returns, contains the level of the specified player if the operation succeeded,
    /// or 0 if the operation failed. This parameter is passed uninitialized.
    /// </param>
    /// <returns>
    /// true if the level was successfully retrieved for the specified player; otherwise, false.
    /// </returns>
    public static bool TryGetLevel(this ExPlayer player, out int level)
    {
        if (LevelModule.Levels.TryGetValue(player, out var levels))
        {
            level = levels.Level;
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
        if (LevelModule.Levels.TryGetValue(player, out var levels))
        {
            xp = levels.Experience;
            level = levels.Level;

            return true;
        }

        xp = 0;
        level = 0;

        return false;
    }
}