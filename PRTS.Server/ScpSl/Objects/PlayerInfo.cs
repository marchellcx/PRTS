using NiveraAPI.IO.Storage;

using PRTS.Levels.Properties;

using PRTS.Profiles.Objects;

using System.Collections.Concurrent;

namespace PRTS.ScpSl.Objects;

/// <summary>
/// Represents information about a player in the game.
/// </summary>
public class PlayerInfo
{
    /// <summary>
    /// Gets or sets the ping of the player.
    /// </summary>
    public volatile int Ping = 0;

    /// <summary>
    /// Gets or sets the role of the player.
    /// </summary>
    public volatile string Role = string.Empty;

    /// <summary>
    /// Gets or sets the nickname of the player.
    /// </summary>
    public volatile string Nick = string.Empty;

    /// <summary>
    /// Gets or sets the user ID of the player.
    /// </summary>
    public volatile string UserId = string.Empty;

    /// <summary>
    /// Gets or sets the address of the player.
    /// </summary>
    public volatile string Address = string.Empty;

    /// <summary>
    /// Gets or sets the country of the player.
    /// </summary>
    public volatile string Country = string.Empty;

    /// <summary>
    /// Gets or sets the UTC time when the player joined the game.
    /// </summary>
    public DateTime UtcJoin = DateTime.MinValue;

    /// <summary>
    /// Gets or sets the UTC time when the player left the game.
    /// </summary>
    public DateTime UtcLeave = DateTime.MinValue;

    /// <summary>
    /// Gets or sets the UTC time when the player information was last updated.
    /// </summary>
    public DateTime UtcUpdate = DateTime.MinValue;

    /// <summary>
    /// Gets or sets the level information of the player.
    /// </summary>
    public volatile LevelDataProperty? Level;

    /// <summary>
    /// Gets or sets the profile information of the player.
    /// </summary>
    public volatile StorageValue<ProfileInfo>? Profile;

    /// <summary>
    /// Gets or sets a concurrent dictionary containing custom data associated with the player.
    /// </summary>
    public volatile ConcurrentDictionary<string, string> CustomData = new();
}