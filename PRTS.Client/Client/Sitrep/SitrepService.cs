using LabExtended.Core;
using LabExtended.Core.Pooling.Pools;

using NiveraAPI.Discord;
using NiveraAPI.Services;

using NiveraAPI.IO.Configs;

using PRTS.Discord;
using PRTS.Client.Sitrep.Events;

namespace PRTS.Client.Sitrep;

/// <summary>
/// Service responsible for handling Sitrep-related events within the system.
/// Combines and manages different event services, including round events, player events,
/// and warhead events, by registering and initializing them within the application's service collection.
/// </summary>
public static class SitrepService
{
    private static Dictionary<SitrepEvent, WebhookClient> mappedWebhooks = new();
    
    /// <summary>
    /// Gets or sets a dictionary that maps each Sitrep event to a corresponding Discord webhook URL.
    /// This property is used to associate specific Sitrep events with distinct webhooks for targeted notifications.
    /// </summary>
    [Config("sitrep", "event-specific-channels", "A dictionary mapping Sitrep events to their respective Discord webhook URLs.")]
    public static Dictionary<SitrepEvent, string> EventSpecificWebhooks { get; set; } = new()
    {
        { SitrepEvent.WarheadStarted, string.Empty }
    };

    /// <summary>
    /// Gets or sets a mapping between Discord webhook URLs and the list of Sitrep events
    /// that should be triggered in the respective channel. This dictionary is utilized to
    /// associate multiple Sitrep events with specific Discord channels for event notifications.
    /// </summary>
    [Config("sitrep", "channel-events", "A dictionary mapping Discord webhook URLs to a list of Sitrep events that should be triggered in that channel.")]
    public static Dictionary<string, List<SitrepEvent>> WebhookEvents { get; set; } = new()
    {
        { "example", new() { SitrepEvent.WarheadStarted, SitrepEvent.WarheadStopped } }
    };

    /// <summary>
    /// Attempts to retrieve the webhook client associated with a specified Sitrep event.
    /// </summary>
    /// <param name="sitrepEvent">The Sitrep event for which the webhook client is being retrieved.</param>
    /// <param name="client">The output parameter that will contain the associated webhook client if found.</param>
    /// <returns>True if a webhook client is successfully retrieved; otherwise, false.</returns>
    public static bool TryGetWebhook(SitrepEvent sitrepEvent, out WebhookClient client)
    {
        client = null!;

        if (Network.Prts == null)
            return false;
        
        return mappedWebhooks.TryGetValue(sitrepEvent, out client);
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
        if (!TryGetWebhook(sitrepEvent, out var webhook))
            return false;       

        if (!removeTime)
            message = string.Concat($"**[{DateTime.Now.ToLocalTime().ToString("HH:mm:ss")}]** ", message);
        
        if (variableBuilder == null)
        {
            webhook.Post(new() { Content = message }, false);
            return true;
        }
        
        var dict = DictionaryPool<string, object>.Shared.Rent();
        
        variableBuilder?.Invoke(dict);
        
        var str = SitrepStrings.ReplaceVariables(message, dict);
        
        webhook.Post(new() { Content = str }, false);
        
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
        mappedWebhooks.Clear();
        
        var webhooks = DictionaryPool<string, WebhookClient>.Shared.Rent();

        foreach (var sitrepEvent in EnumUtils<SitrepEvent>.Values)
        {
            if (EventSpecificWebhooks.TryGetValue(sitrepEvent, out var webhookUrl))
            {
                if (string.IsNullOrWhiteSpace(webhookUrl))
                    continue;
                
                if (!webhooks.TryGetValue(webhookUrl, out var webhookClient))
                    webhooks.Add(webhookUrl, webhookClient = new UnityWebhookClient(webhookUrl));
                
                mappedWebhooks.Add(sitrepEvent, webhookClient);
                
                ApiLog.Info("PRTS", $"Mapped Sitrep event &1{sitrepEvent}&r to webhook &1{webhookUrl}&r.");
            }
            else
            {
                foreach (var kvp in WebhookEvents)
                {
                    if (kvp.Value.Contains(sitrepEvent))
                    {
                        if (string.IsNullOrWhiteSpace(kvp.Key))
                            continue;
                        
                        if (!webhooks.TryGetValue(kvp.Key, out var webhookClient))
                            webhooks.Add(kvp.Key, webhookClient = new UnityWebhookClient(kvp.Key));
                        
                        mappedWebhooks.Add(sitrepEvent, webhookClient);
                        
                        ApiLog.Info("PRTS", $"Mapped Sitrep event &1{sitrepEvent}&r to webhook &1{kvp.Key}&r.");
                    }
                }
            }
        }
        
        DictionaryPool<string, WebhookClient>.Shared.Return(webhooks);

        if (mappedWebhooks.Count == 0)
        {
            ApiLog.Warn("PRTS", "No Sitrep event channels mapped. Please check your configuration.");
            return;
        }

        if (mappedWebhooks.Keys.Any(k => k.ToString().StartsWith("Round")))
            SitrepRoundEvents.Start();
        else
            ApiLog.Warn("PRTS", "No Sitrep round channels mapped. Please check your configuration.");
        
        if (mappedWebhooks.Keys.Any(k => k.ToString().StartsWith("Player")))
            SitrepPlayerEvents.Start();
        else
            ApiLog.Warn("PRTS", "No Sitrep player channels mapped. Please check your configuration.");
        
        if (mappedWebhooks.Keys.Any(k => k.ToString().StartsWith("Warhead")))
            SitrepWarheadEvents.Start();
        else
            ApiLog.Warn("PRTS", "No Sitrep warhead channels mapped. Please check your configuration.");
    }
}