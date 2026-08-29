namespace PRTS.Client.Punishments.Enums;

/// <summary>
/// Represents the type of punishment that can be applied within the system.
/// </summary>
public enum PunishmentType
{
    /// <summary>
    /// Represents a warning issued as a form of punishment.
    /// This is a non-restrictive action, typically used to notify a user of inappropriate behavior
    /// without imposing any limitations or restrictions on their actions.
    /// </summary>
    Warn,

    /// <summary>
    /// Represents a punishment where a user's ability to communicate is restricted.
    /// This action is typically enforced to prevent further disruptive or inappropriate behavior
    /// while still allowing the user to participate in other activities.
    /// </summary>
    Mute,

    /// <summary>
    /// Represents a ban imposed as a form of punishment.
    /// This action restricts a user's access entirely, typically used for severe violations
    /// or repeated misconduct to enforce compliance with the system's rules.
    /// </summary>
    Ban
}