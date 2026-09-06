using LabExtended.Core;
using LabExtended.Core.Pooling.Pools;

using NiveraAPI.IO.Configs;

using PRTS.Client.Sitrep.Events;

namespace PRTS.Client.Sitrep;

/// <summary>
/// Service responsible for handling Sitrep-related events within the system.
/// Combines and manages different event services, including round events, player events,
/// and warhead events, by registering and initializing them within the application's service collection.
/// </summary>
public static class SitrepService
{
    private static Dictionary<SitrepEvent, string> mappedChannels = new();

    /// <summary>
    /// Gets or sets a dictionary that maps Sitrep events to their respective Discord channel aliases.
    /// </summary>
    [Config("sitrep", "event-specific-channels", "A dictionary mapping Sitrep events to their respective Discord channel aliases.")]
    public static Dictionary<SitrepEvent, string> EventSpecificWebhooks { get; set; } = new()
    {
        { SitrepEvent.WarheadStarted, string.Empty }
    };

    /// <summary>
    /// Gets or sets a dictionary that maps Discord channel aliases to a list of Sitrep events.
    /// </summary>
    [Config("sitrep", "channel-events", "A dictionary mapping Discord channel aliases to a list of Sitrep events that should be triggered in that channel.")]
    public static Dictionary<string, List<SitrepEvent>> WebhookEvents { get; set; } = new()
    {
        { "example", new() { SitrepEvent.WarheadStarted, SitrepEvent.WarheadStopped } }
    };

    /// <summary>
    /// Attempts to retrieve the mapped Discord channel for a given Sitrep event.
    /// </summary>
    /// <param name="sitrepEvent">The Sitrep event for which the mapped Discord channel is being retrieved.</param>
    /// <param name="mappedChannel">The output parameter that will contain the associated Discord channel alias if found.</param>
    /// <returns>True if a Discord channel alias is successfully retrieved; otherwise, false.</returns>
    public static bool TryGetWebhook(SitrepEvent sitrepEvent, out string mappedChannel)
    {
        mappedChannel = null!;

        if (Network.Prts == null)
            return false;
        
        return mappedChannels.TryGetValue(sitrepEvent, out mappedChannel);
    }

    /// <summary>
    /// Attempts to send a Sitrep event message to a specific Discord channel based on the provided event.
    /// </summary>
    /// <param name="sitrepEvent">The Sitrep event to be sent.</param>
    /// <param name="message">The message template containing variables to be replaced.</param>
    /// <param name="variableBuilder">An action to populate a dictionary with variables used for message replacement.</param>
    /// <param name="removeTime">Indicates whether the time stamp should be removed from the message.</param>
    /// <returns>True if the Sitrep event message was successfully sent to the associated channel; otherwise, false.</returns>
    public static bool TrySendEvent(SitrepEvent sitrepEvent, string message,
        Action<Dictionary<string, object>>? variableBuilder, bool removeTime = false)
    {
        if (!TryGetWebhook(sitrepEvent, out var channel))
            return false;       

        if (!removeTime)
            message = string.Concat($"**[{DateTime.Now.ToLocalTime().ToString("HH:mm:ss")}]** ", message);
        
        if (variableBuilder == null)
        {
            Network.Prts!.CallCmdPostMessage(channel, message);
            return true;
        }
        
        var dict = DictionaryPool<string, object>.Shared.Rent();
        
        variableBuilder?.Invoke(dict);
        
        var str = SitrepStrings.ReplaceVariables(message, dict);

        Network.Prts!.CallCmdPostMessage(channel, str);
        
        DictionaryPool<string, object>.Shared.Return(dict);
        return true;
    }

    /// <summary>
    /// Starts the service by initializing its dependencies.
    /// This method adds the SitrepRoundEvents, SitrepPlayerEvents,
    /// and SitrepWarheadEvents services to the service collection.
    /// </summary>
    public static void Start()
    {
        mappedChannels.Clear();

        foreach (var sitrepEvent in EnumUtils<SitrepEvent>.Values)
        {
            if (EventSpecificWebhooks.TryGetValue(sitrepEvent, out var channelAlias))
            {
                if (string.IsNullOrWhiteSpace(channelAlias))
                    continue;
                
                mappedChannels.Add(sitrepEvent, channelAlias);
                
                ApiLog.Info("PRTS", $"Mapped Sitrep event &1{sitrepEvent}&r to channel &1{channelAlias}&r.");
            }
            else
            {
                foreach (var kvp in WebhookEvents)
                {
                    if (kvp.Value.Contains(sitrepEvent))
                    {
                        if (string.IsNullOrWhiteSpace(kvp.Key))
                            continue;
                        
                        mappedChannels.Add(sitrepEvent, kvp.Key);
                        
                        ApiLog.Info("PRTS", $"Mapped Sitrep event &1{sitrepEvent}&r to channel &1{kvp.Key}&r.");
                    }
                }
            }
        }

        if (mappedChannels.Count == 0)
        {
            ApiLog.Warn("PRTS", "No Sitrep event channels mapped. Please check your configuration.");
            return;
        }

        if (mappedChannels.Keys.Any(k => k.ToString().StartsWith("Round")))
            SitrepRoundEvents.Start();
        else
            ApiLog.Warn("PRTS", "No Sitrep round channels mapped. Please check your configuration.");
        
        if (mappedChannels.Keys.Any(k => k.ToString().StartsWith("Player")))
            SitrepPlayerEvents.Start();
        else
            ApiLog.Warn("PRTS", "No Sitrep player channels mapped. Please check your configuration.");
        
        if (mappedChannels.Keys.Any(k => k.ToString().StartsWith("Warhead")))
            SitrepWarheadEvents.Start();
        else
            ApiLog.Warn("PRTS", "No Sitrep warhead channels mapped. Please check your configuration.");
    }
}