using LabApi.Features;

using LabExtended.Core;

using MEC;

using PRTS.Addons;

using PRTS.Client.Sitrep;
using PRTS.Client.Sitrep.Logs;

using Version = System.Version;

namespace PRTS.Core;

/// <summary>
/// The main plugin class.
/// </summary>
public class Plugin : LabApi.Loader.Features.Plugins.Plugin
{
    /// <summary>
    /// Gets the name of the plugin.
    /// </summary>
    public override string Name { get; } = "PRTS";

    /// <summary>
    /// Gets the author of the plugin.
    /// </summary>
    public override string Author { get; } = "marchell";

    /// <summary>
    /// Gets the description of the plugin.
    /// </summary>
    public override string Description { get; } = "Bridges Discord and SCP:SL.";

    /// <summary>
    /// Gets the version of the plugin.
    /// </summary>
    public override Version Version { get; } = new(0, 0, 1);

    /// <summary>
    /// Gets the required API version of the plugin.
    /// </summary>
    public override Version RequiredApiVersion => LabApiProperties.CurrentVersion;
    
    /// <summary>
    /// Enables the plugin.
    /// </summary>
    public override void Enable()
    {
        ConsoleLogService.Start();
        
        SitrepService.Start();
        
        PrtsAddonManager.LoadAddons();
        PrtsAddonManager.EnableAddons();

        Timing.CallDelayed(5f, () =>
        {
            ApiLog.Info("PRTS", "Starting network ...");

            try
            {
                Network.Connect();
            }
            catch (Exception ex)
            {
                ApiLog.Error(ex);
            }
        });
    }

    /// <summary>
    /// Disables the plugin.
    /// </summary>
    public override void Disable()
    {
        ConsoleLogService.Stop();
    }
}