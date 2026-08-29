using System.Collections.Concurrent;

using PRTS.Client.Profiles.Sessions;

namespace PRTS.Client.Profiles.Objects;

/// <summary>
/// Represents a profile containing information about a user's identity and associated data.
/// </summary>
public class ProfileInfo
{
    private volatile string id = string.Empty;
    private volatile string userId = string.Empty;
    
    private volatile ConcurrentDictionary<string, ProfileSession> sessions = new();

    private volatile ConcurrentDictionary<string, DateTime> nicknames = new();
    private volatile ConcurrentDictionary<string, DateTime> addresses = new();
    
    private volatile ConcurrentDictionary<string, string> customData = new();
    
    /// <summary>
    /// The ID of the profile.
    /// </summary>
    public string Id
    {
        get => id;
        set => id = value;
    }

    /// <summary>
    /// The user ID tied to this profile.
    /// </summary>
    public string UserId
    {
        get => userId;
        set => userId = value;
    }

    /// <summary>
    /// The Discord ID of the user associated with this profile.
    /// </summary>
    public ulong DiscordId { get; set; } = 0;
    
    /// <summary>
    /// The date and time the profile was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// The date and time the profile was last modified.
    /// </summary>
    public DateTime ModifiedAt { get; set; }
    
    /// <summary>
    /// The date and time the player last logged in.
    /// </summary>
    public DateTime LastLogin { get; set; }

    /// <summary>
    /// All sessions the player has logged in.
    /// </summary>
    public ConcurrentDictionary<string, ProfileSession> Sessions => sessions;

    /// <summary>
    /// All nicknames the player has used.
    /// </summary>
    public ConcurrentDictionary<string, DateTime> Nicknames => nicknames;

    /// <summary>
    /// All IP addresses the player joined from.
    /// </summary>
    public ConcurrentDictionary<string, DateTime> Addresses => addresses;

    /// <summary>
    /// Additional data associated with the profile.
    /// </summary>
    public ConcurrentDictionary<string, string> CustomData => customData;

    /// <summary>
    /// Retrieves the nickname associated with the profile, returning the first nickname
    /// if multiple are available, or a default value if no nicknames are set.
    /// </summary>
    /// <returns>
    /// A <see cref="string"/> containing the nickname associated with the profile.
    /// Returns "Unknown" if no nickname is available.
    /// </returns>
    public string GetNickname()
    {
        if (Nicknames.Count < 1)
            return "Unknown";
        
        return Nicknames.First().Key;
    }

    /// <summary>
    /// Retrieves the address associated with the profile, returning the first address
    /// if multiple are available, or a default value if no addresses are set.
    /// </summary>
    /// <returns>
    /// A <see cref="string"/> containing the address associated with the profile.
    /// Returns "0.0.0.0" if no address is available.
    /// </returns>
    public string GetAddress()
    {
        if (Addresses.Count < 1)
            return "0.0.0.0";
        
        return Addresses.First().Key;   
    }

    /// <summary>
    /// Calculates the total playtime for all sessions in the profile,
    /// considering only sessions with valid start and end times.
    /// </summary>
    /// <returns>
    /// A <see cref="TimeSpan"/> representing the total duration of all valid sessions
    /// in the profile. Returns a zero timespan if there are no valid sessions.
    /// </returns>
    public TimeSpan GetTotalPlaytime()
    {
        var total = TimeSpan.Zero;

        foreach (var kvp in Sessions)
        {
            if (kvp.Value.Started == DateTime.MinValue || kvp.Value.Ended == DateTime.MinValue)
                continue;

            var duration = kvp.Value.Ended - kvp.Value.Started;
            
            if (duration > TimeSpan.Zero)
                total += duration;
        }

        return total;
    }

    /// <summary>
    /// Calculates the total playtime for all sessions within a specified time range,
    /// considering only sessions with valid start and end times that fall within the range.
    /// </summary>
    /// <param name="start">
    /// The starting date and time of the range to filter sessions.
    /// </param>
    /// <param name="end">
    /// The ending date and time of the range to filter sessions.
    /// </param>
    /// <returns>
    /// A <see cref="TimeSpan"/> representing the total duration of all valid sessions
    /// within the specified time range. Returns a zero timespan if there are no valid sessions
    /// in the range or if the inputs are invalid.
    /// </returns>
    public TimeSpan GetTotalPlaytime(DateTime start, DateTime end)
    {
        var total = TimeSpan.Zero;

        foreach (var kvp in Sessions)
        {
            if (kvp.Value.Started == DateTime.MinValue || kvp.Value.Ended == DateTime.MinValue)
                continue;
            
            if (kvp.Value.Started >= start && kvp.Value.Ended <= end)
            {
                var duration = kvp.Value.Ended - kvp.Value.Started;
                
                if (duration > TimeSpan.Zero)
                    total += duration;
            }
        }

        return total;
    }
    
    /// <summary>
    /// Calculates the total playtime for a specific session, identified by its session ID,
    /// considering only sessions with valid start and end times.
    /// </summary>
    /// <param name="sessionId">
    /// The unique identifier of the session for which the playtime is to be calculated.
    /// </param>
    /// <returns>
    /// A <see cref="TimeSpan"/> representing the duration of the specified session.
    /// Returns a zero timespan if the session ID does not exist or the session has invalid start or end times.
    /// </returns>
    public TimeSpan GetTotalPlaytime(string sessionId)
    {
        if (Sessions.TryGetValue(sessionId, out var session))
            return session.Ended - session.Started;
        
        return TimeSpan.Zero;   
    }
}