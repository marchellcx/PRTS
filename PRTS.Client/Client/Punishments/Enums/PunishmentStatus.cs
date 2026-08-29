namespace PRTS.Client.Punishments.Enums;

/// <summary>
/// Represents the status of a punishment.
/// </summary>
public enum PunishmentStatus
{
    /// <summary>
    /// The punishment has expired.
    /// </summary>
    Expired,
    
    /// <summary>
    /// The punishment has been revoked.
    /// </summary>
    Revoked,
    
    /// <summary>
    /// The punishment is active.
    /// </summary>
    Active
}