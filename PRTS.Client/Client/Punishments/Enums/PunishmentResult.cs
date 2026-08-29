namespace PRTS.Client.Punishments.Enums;

/// <summary>
/// Represents the result of a punishment operation.
/// </summary>
public enum PunishmentResult : byte
{
    /// <summary>
    /// The staff profile could not be found.
    /// </summary>
    StaffProfileNotFound,
    
    /// <summary>
    /// The target profile could not be found.
    /// </summary>
    TargetProfileNotFound,
    
    /// <summary>
    /// The punishment failed.
    /// </summary>
    Failed,
    
    /// <summary>
    /// The punishment was successful.
    /// </summary>
    Ok
}