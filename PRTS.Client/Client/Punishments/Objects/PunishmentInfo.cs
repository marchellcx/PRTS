using PRTS.Client.Punishments.Enums;

namespace PRTS.Client.Punishments.Objects;

/// <summary>
/// Represents information related to a punishment, including its associated metadata,
/// the involved users, its status, and various related utility methods.
/// </summary>
public class PunishmentInfo
{
    internal volatile bool expieryFlagged;

    /// <summary>
    /// The ID of the punishment.
    /// </summary>
    public volatile string Id = string.Empty;
    
    /// <summary>
    /// Identifies the staff member responsible for issuing the punishment.
    /// </summary>
    public volatile string StaffId = string.Empty;

    /// <summary>
    /// Identifies the target of the punishment.
    /// </summary>
    public volatile string TargetId = string.Empty;

    /// <summary>
    /// Represents the identifier associated with the report linked to the punishment.
    /// </summary>
    public volatile string ReportId = string.Empty;
    
    /// <summary>
    /// The ID of the server this punishment was issued on.
    /// </summary>
    public volatile string ServerId = string.Empty;

    /// <summary>
    /// Identifies the staff member who revoked the punishment.
    /// </summary>
    public volatile string RevokedId = string.Empty;

    /// <summary>
    /// The reason for the punishment being revoked.
    /// </summary>
    public volatile string RevokedReason = string.Empty;

    /// <summary>
    /// The reason for the punishment.
    /// </summary>
    public volatile string Reason = string.Empty;

    /// <summary>
    /// The message ID of the punishment.
    /// </summary>
    public volatile string CachedMessageId = string.Empty;

    /// <summary>
    /// Server IDs this punishment was applied to.
    /// </summary>
    public volatile string[] AppliedServers = [];

    /// <summary>
    /// The date and time when the punishment was issued.
    /// </summary>
    public DateTime IssuedAt = DateTime.MinValue;
    
    /// <summary>
    /// The date and time when the punishment expires.
    /// </summary>
    public DateTime ExpiresAt = DateTime.MinValue;
    
    /// <summary>
    /// The date and time when the punishment was revoked.
    /// </summary>
    public DateTime RevokedAt = DateTime.MinValue;

    /// <summary>
    /// The type of punishment.
    /// </summary>
    public volatile PunishmentType Type = PunishmentType.Warn;
    
    /// <summary>
    /// The status of the punishment.
    /// </summary>
    public volatile PunishmentStatus Status = PunishmentStatus.Expired;

    /// <summary>
    /// Whether the punishment is a mute.
    /// </summary>
    public bool IsMute => Type == PunishmentType.Mute;
    
    /// <summary>
    /// Whether the punishment is a warning.
    /// </summary>
    public bool IsWarn => Type == PunishmentType.Warn;

    /// <summary>
    /// Whether the punishment is a ban.
    /// </summary>
    public bool IsBan => Type == PunishmentType.Ban;
    
    /// <summary>
    /// Indicates whether the punishment is currently active based on its status.
    /// </summary>
    public bool IsActive => Status == PunishmentStatus.Active;
    
    /// <summary>
    /// Whether the punishment has expired.
    /// </summary>
    public bool IsExpired => Status == PunishmentStatus.Expired;
    
    /// <summary>
    /// Whether the punishment has been revoked.
    /// </summary>
    public bool IsRevoked => Status == PunishmentStatus.Revoked;

    /// <summary>
    /// Indicates whether the punishment is permanent, determined by the absence of an expiration date.
    /// </summary>
    public bool IsPermanent => ExpiresAt == DateTime.MinValue;

    /// <summary>
    /// Whether the punishment was issued by a report.
    /// </summary>
    public bool WasByReport => !string.IsNullOrEmpty(ReportId);
}