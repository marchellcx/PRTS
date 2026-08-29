namespace PRTS.ScpSl.Modules.Plugins;

/// <summary>
/// Represents information about a plugin.
/// </summary>
public class PluginInfo
{
    /// <summary>
    /// The name of the plugin.
    /// </summary>
    public volatile string Name = string.Empty;

    /// <summary>
    /// The file path of the plugin.
    /// </summary>
    public volatile string File = string.Empty;

    /// <summary>
    /// The author of the plugin.
    /// </summary>
    public volatile string Author = string.Empty;

    /// <summary>
    /// The version of the plugin.
    /// </summary>
    public volatile string Version = string.Empty;

    /// <summary>
    /// The description of the plugin.
    /// </summary>
    public volatile string Description = string.Empty;
}