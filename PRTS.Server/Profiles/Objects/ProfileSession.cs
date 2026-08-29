namespace PRTS.Profiles.Objects;

/// <summary>
/// Represents a session for a user's profile.
/// </summary>
public class ProfileSession
{
    private volatile string id = string.Empty;

    /// <summary>
    /// The ID of the session.
    /// </summary>
    public string Id
    {
        get => id;
        set => id = value;
    }
    
    /// <summary>
    /// The date and time the session ended.
    /// </summary>
    public DateTime Ended { get; set; } = DateTime.MinValue;
    
    /// <summary>
    /// The date and time the session started.
    /// </summary>
    public DateTime Started { get; set; } = DateTime.MinValue;
}