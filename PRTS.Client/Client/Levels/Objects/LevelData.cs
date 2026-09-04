namespace PRTS.Client.Levels.Objects;

/// <summary>
/// Represents the level information of a player in the game.
/// </summary>
public class LevelData
{
    internal int curLevelNum;

    /// <summary>
    /// Gets the current level information of the player.
    /// </summary>
    public LevelInfo? CurLevel { get; internal set; }

    /// <summary>
    /// Gets the next level information of the player.
    /// </summary>
    public LevelInfo? NextLevel { get; internal set; }

    /// <summary>
    /// Gets the current experience points of the player.
    /// </summary>
    public int Experience { get; internal set; }

    /// <summary>
    /// Gets the required experience points for the next level of the player.
    /// </summary>
    public int RequiredExperience => NextLevel?.Experience ?? 0;

    /// <summary>
    /// Gets a value indicating whether the player has reached the maximum level.
    /// </summary>
    public bool IsCapped => NextLevel == null;

    /// <summary>
    /// Creates a copy of the current LevelData instance.
    /// </summary>
    /// <returns>A new LevelData instance with the same values as the current instance.</returns>
    public LevelData Copy()
    {
        return new LevelData()
        {
            curLevelNum = curLevelNum,

            CurLevel = CurLevel,
            NextLevel = NextLevel,

            Experience = Experience
        };
    }
}