namespace PRTS.Levels.Enums;

/// <summary>
/// Represents the result of a level modification operation.
/// </summary>
public enum LevelModifyResult
{
    /// <summary>
    /// Player profile not found.
    /// </summary>
    ProfileNotFound,
    
    /// <summary>
    /// Player did not gain enough XP to level up.
    /// </summary>
    Ok,
    
    /// <summary>
    /// Player gained enough XP and leveled up.
    /// </summary>
    LevelUp,
    
    /// <summary>
    /// Player lost enough XP and leveled down.
    /// </summary>
    LevelDown,
}