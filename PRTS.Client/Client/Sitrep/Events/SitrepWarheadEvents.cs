using LabApi.Events.Arguments.WarheadEvents;
using LabApi.Events.Handlers;

using LabExtended.API;

using NiveraAPI.IO.Configs;

namespace PRTS.Client.Sitrep.Events;

/// <summary>
/// Represents the service responsible for handling Sitrep events related to warheads.
/// This class facilitates the monitoring and notification of events such as warhead activation,
/// detonation, and deactivation within the system.
/// </summary>
public static class SitrepWarheadEvents
{
    /// <summary>
    /// Represents the message sent when a warhead is detonated.
    /// This property is configurable through the "sitrep" configuration under the key "message-warhead-detonated".
    /// It is used within the Sitrep system to notify about warhead detonation events.
    /// </summary>
    [Config("sitrep", "message-warhead-detonated", "Message sent when the warhead is detonated.")]
    public static string WarheadDetonatedMessage { get; set; } = "Warhead detonated!";

    /// <summary>
    /// Represents the message sent when a warhead starts detonating.
    /// This property is configurable through the "sitrep" configuration under the key "message-warhead-started".
    /// It is used within the Sitrep system to notify about the initiation of a warhead detonation process.
    /// </summary>
    [Config("sitrep", "message-warhead-started", "Message sent when the warhead starts detonating.")]
    public static string WarheadStartedMessage { get; set; } = "Warhead detonated!";

    /// <summary>
    /// Represents the message sent when a warhead is stopped.
    /// This property is configurable through the "sitrep" configuration under the key "message-warhead-stopped".
    /// It is utilized within the Sitrep system to notify about events where an active warhead has been halted.
    /// </summary>
    [Config("sitrep", "message-warhead-stopped", "Message sent when the warhead is stopped.")]
    public static string WarheadStoppedMessage { get; set; } = "Warhead detonated!";

    /// <summary>
    /// Starts the SitrepPlayerEvents service, registering the necessary event listeners
    /// and initializing the associated SitrepService instance.
    /// This method subscribes to player join and leave events, enabling Sitrep notifications to the appropriate channels.
    /// </summary>
    public static void Start()
    {
        WarheadEvents.Started += OnWarheadStarted;
        WarheadEvents.Stopped += OnWarheadStopped;
        WarheadEvents.Detonated += OnWarheadDetonated;
    }

    private static void OnWarheadDetonated(WarheadDetonatedEventArgs args)
    {
        SitrepService.TrySendEvent(SitrepEvent.WarheadDetonated, WarheadDetonatedMessage, null);       
    }

    private static void OnWarheadStarted(WarheadStartedEventArgs args)
    {
        if (args.Player is not ExPlayer player)
            return;
        
        SitrepService.TrySendEvent(SitrepEvent.WarheadStarted, WarheadStartedMessage, dict => dict.AddPlayerVariables("Player", player));      
    }

    private static void OnWarheadStopped(WarheadStoppedEventArgs args)
    {
        if (args.Player is not ExPlayer player)
            return;
        
        SitrepService.TrySendEvent(SitrepEvent.WarheadStopped, WarheadStoppedMessage, dict => dict.AddPlayerVariables("Player", player));       
    }
}