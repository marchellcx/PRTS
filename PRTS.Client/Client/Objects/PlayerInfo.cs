namespace PRTS.Client.Objects;

/// <summary>
/// Represents information about a player in the game.
/// </summary>
public class PlayerInfo
{
    /// <summary>
    /// Gets or sets the nickname of the player.
    /// </summary>
    public string Nick = string.Empty;

    /// <summary>
    /// Gets or sets the role of the player.
    /// </summary>
    public string Role = string.Empty;

    /// <summary>
    /// Gets or sets the user ID of the player.
    /// </summary>
    public string UserId = string.Empty;

    /// <summary>
    /// Gets or sets the address of the player.
    /// </summary>
    public string Address = string.Empty;

    /// <summary>
    /// Gets or sets the country of the player.
    /// </summary>
    public string Country = string.Empty;

    /// <summary>
    /// Gets or sets the latency of the player in milliseconds.
    /// </summary>
    public int Latency = 0;

    /// <summary>
    /// Gets or sets a concurrent dictionary containing custom data associated with the player.
    /// </summary>
    public Dictionary<string, string> CustomData = new();

    /// <summary>
    /// Gets or sets a value indicating whether to synchronize custom data for the player.
    /// </summary>
    public bool SyncCustomData;
}