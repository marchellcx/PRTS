namespace PRTS.Levels;

/// <summary>
/// Represents a log entry for a level change.
/// </summary>
public class LevelLog
{
    /// <summary>
    /// The time of the experience gain.
    /// </summary>
    public DateTime Time;

    /// <summary>
    /// The identifier for the reason associated with the experience gain.
    /// </summary>
    public volatile string ReasonId = string.Empty;

    /// <summary>
    /// The descriptive message explaining the reason associated with the experience gain.
    /// </summary>
    public volatile string ReasonMessage = string.Empty;

    /// <summary>
    /// The amount of experience gained or lost during the change.
    /// </summary>
    public volatile int Change;

    /// <summary>
    /// The level of the entity after the experience change.
    /// </summary>
    public volatile int LevelAfter;

    /// <summary>
    /// The level of the entity before the experience change.
    /// </summary>
    public volatile int LevelBefore;
}