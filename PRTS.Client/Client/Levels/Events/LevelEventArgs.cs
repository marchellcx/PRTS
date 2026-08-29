using LabExtended.API;

namespace PRTS.Client.Levels.Events;

/// <summary>
/// Represents the event data for a level change event.
/// </summary>
public class LevelEventArgs : EventArgs
{
    /// <summary>
    /// Gets the player associated with the event.
    /// </summary>
    public ExPlayer Player { get; }

    /// <summary>
    /// Gets the previous level of the player before the level change event.
    /// </summary>
    public int PreviousLevel { get; }

    /// <summary>
    /// Gets the amount of experience the player had prior to the level change event.
    /// </summary>
    public int PreviousExperience { get; }
    
    /// <summary>
    /// Gets the new level of the player after the level change event.
    /// </summary>
    public int NewLevel { get; }
    
    /// <summary>
    /// Gets the amount of experience the player has after the level change event.
    /// </summary>
    public int NewExperience { get; }
    
    /// <summary>
    /// Gets the reason for the level change.
    /// </summary>
    public string ReasonId { get; }
    
    /// <summary>
    /// Gets the message associated with the level change reason.
    /// </summary>
    public string ReasonMessage { get; }

    /// <summary>
    /// Gets a value indicating whether the player's level has changed as a result of the event.
    /// </summary>
    public bool HasLevelChanged => PreviousLevel != NewLevel;

    /// <summary>
    /// Determines whether the experience value has changed between the previous and new states.
    /// </summary>
    public bool HasExperienceChanged => PreviousExperience != NewExperience;
    
    /// <summary>
    /// Creates a new instance of the LevelEventArgs class.
    /// </summary>
    public LevelEventArgs(ExPlayer player, int previousLevel, int previousExperience, int newLevel, int newExperience, string reasonId, string reasonMessage)
    {
        Player = player;
        
        PreviousLevel = previousLevel;
        PreviousExperience = previousExperience;
        
        NewLevel = newLevel;
        NewExperience = newExperience;
        
        ReasonId = reasonId ?? throw new ArgumentNullException(nameof(reasonId));
        ReasonMessage = reasonMessage ?? throw new ArgumentNullException(nameof(reasonMessage));       
    }
}