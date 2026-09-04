namespace PRTS.Client.Levels.Objects;

/// <summary>
/// Represents the data for a specific level in the game, including its number, experience requirements, and milestone information.
/// </summary>
public class LevelInfo
{
    /// <summary>
    /// The number of the level.
    /// </summary>
    public int Level { get; internal set; }

    /// <summary>
    /// The experience points required to reach this level.
    /// </summary>
    public int Experience { get; internal set; }

    /// <summary>
    /// Whether this level is the highest achievable level.
    /// </summary>
    public bool IsMaxLevel { get; internal set; }

    /// <summary>
    /// The name of the milestone associated with the level.
    /// </summary>
    public string MilestoneName { get; internal set; } = string.Empty;

    /// <summary>
    /// The next level data, if it exists.
    /// </summary>
    public LevelInfo? NextLevel { get; internal set; }

    /// <summary>
    /// The previous level data, if it exists.
    /// </summary>
    public LevelInfo? PreviousLevel { get; internal set; }

    /// <summary>
    /// The next milestone level data, if it exists.
    /// </summary>
    public LevelInfo? NextMilestone { get; internal set; }
}