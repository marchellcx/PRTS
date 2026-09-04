namespace PRTS.Levels;

/// <summary>
/// Represents the data for a specific level.
/// </summary>
public class LevelInfo
{
    /// <summary>
    /// The number of the level.
    /// </summary>
    public volatile int Level = 0;

    /// <summary>
    /// The experience points required to reach this level.
    /// </summary>
    public volatile int Experience = 0;

    /// <summary>
    /// Whether this level is the highest achievable level.
    /// </summary>
    public volatile bool IsMaxLevel = false;

    /// <summary>
    /// The name of the milestone associated with the level.
    /// </summary>
    public volatile string MilestoneName = string.Empty;
}
