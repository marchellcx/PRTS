using System.Collections.Concurrent;

using PRTS.Staff;

namespace PRTS.Profiles.Objects;

/// <summary>
/// Represents a profile containing information about a user's identity and associated data.
/// </summary>
public class ProfileInfo
{
    private volatile string id = string.Empty;
    private volatile string userId = string.Empty;
    
    private volatile ConcurrentDictionary<string, ProfileSession> sessions = new();
    private volatile ConcurrentDictionary<string, ProfileProperty> properties = new();

    private volatile ConcurrentDictionary<string, DateTime> nicknames = new();
    private volatile ConcurrentDictionary<string, DateTime> addresses = new();
    
    private volatile ConcurrentDictionary<string, string> customData = new();
    private volatile ConcurrentDictionary<string, byte[]> propertyValues = new();
    
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
    /// All profile properties and their values.
    /// </summary>
    public ConcurrentDictionary<string, byte[]> PropertyValues => propertyValues;

    /// <summary>
    /// A list of custom profile properties.
    /// </summary>
    public ConcurrentDictionary<string, ProfileProperty> Properties => properties;

    /// <summary>
    /// Attempts to retrieve a property from the profile by its name.
    /// </summary>
    /// <param name="name">
    /// The name of the property to retrieve. This parameter cannot be null or empty.
    /// </param>
    /// <param name="property">
    /// When this method returns, contains the property if found; otherwise, it contains null.
    /// </param>
    /// <returns>
    /// A <see cref="bool"/> indicating whether the property exists in the profile.
    /// Returns <c>true</c> if the property was found; otherwise, <c>false</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the <paramref name="name"/> is null or empty.
    /// </exception>
    public bool TryGetProperty(string name, out ProfileProperty property)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentNullException(nameof(name));

        return Properties.TryGetValue(name, out property);   
    }

    /// <summary>
    /// Attempts to retrieve a property from the profile by its name and cast it to the specified type.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the property to retrieve. Must be derived from <see cref="ProfileProperty"/>.
    /// </typeparam>
    /// <param name="name">
    /// The name of the property to retrieve.
    /// </param>
    /// <param name="value">
    /// When this method returns, contains the property cast to the specified type if found and the cast is successful;
    /// otherwise, contains the default value for the type <typeparamref name="T"/>.
    /// </param>
    /// <returns>
    /// A <see cref="bool"/> indicating whether the property exists and could be cast to the specified type.
    /// Returns <c>true</c> if the property was found and cast successfully; otherwise, <c>false</c>.
    /// </returns>
    public bool TryGetProperty<T>(string name, out T value) where T : ProfileProperty
    {
        value = default!;

        if (!TryGetProperty(name, out var prop))
            return false;

        if (prop is T castProp)
        {
            value = castProp;
            return true;
        }
        
        return false;  
    }

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
    /// Determines whether the profile is associated with a user who has administrative privileges.
    /// </summary>
    /// <returns>
    /// A <see cref="bool"/> indicating whether the user is an administrator. Returns <c>false</c> if the Discord ID is not set or if administrative privileges are not found.
    /// </returns>
    public bool IsAdministrator()
    {
        if (DiscordId == 0)
            return false;

        _ = StaffRole.GetAllPermissions(0, DiscordId, out var isAdmin);
        return isAdmin;
    }

    /// <summary>
    /// Checks if the profile has the specified permission based on the associated Discord ID.
    /// </summary>
    /// <param name="perm">The specific permission to check for the profile.</param>
    /// <returns>
    /// A <see cref="bool"/> indicating whether the profile has the specified permission.
    /// Returns false if the profile's Discord ID is not set (equal to 0).
    /// </returns>
    public bool HasPermission(string perm)
    {
        if (DiscordId == 0)
            return false;
        
        return StaffRole.HasPermissionAll(0, DiscordId, perm);
    }

    /// <summary>
    /// Retrieves all roles associated with the specified Discord user for the given guild.
    /// If no guild ID is provided, roles are retrieved across all available guilds.
    /// </summary>
    /// <param name="guildId">
    /// The identifier of the guild for which to retrieve the roles.
    /// If set to 0, roles are retrieved globally instead of being limited to a specific guild.
    /// </param>
    /// <returns>
    /// An array of <see cref="StaffRole"/> objects representing the roles associated with the user.
    /// Returns an empty array if the Discord ID is not set or no roles are found.
    /// </returns>
    public StaffRole[] GetAllRoles(ulong guildId = 0)
    {
        if (DiscordId == 0)
            return [];

        return StaffRole.GetAllRoles(guildId, DiscordId);
    }

    /// <summary>
    /// Retrieves all permissions associated with the profile's Discord user within a specified guild or across all guilds.
    /// </summary>
    /// <param name="guildId">
    /// The unique identifier of the guild. If set to 0, permissions are retrieved for all guilds.
    /// </param>
    /// <param name="isAdmin">
    /// An output parameter that indicates whether the user has administrative privileges.
    /// </param>
    /// <returns>
    /// An array of strings representing the permissions associated with the profile's Discord user.
    /// </returns>
    public string[] GetAllPermissions(ulong guildId, out bool isAdmin)
    {
        isAdmin = false;

        if (DiscordId == 0)
            return [];

        return StaffRole.GetAllPermissions(guildId, DiscordId, out isAdmin);
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