using LabExtended.API;
using LabExtended.Events;

using NiveraAPI.IO.Configs;

namespace PRTS.Client.Sitrep.Events;

/// <summary>
/// Represents a service responsible for handling round-based events and broadcasting corresponding messages
/// through the Sitrep system. This class interacts with external round events such as start, end, restart,
/// and waiting for players, allowing configurable messages to be sent for each event type.
/// </summary>
public static class SitrepRoundEvents
{
    /// <summary>
    /// Represents the configurable message that is sent when a round ends.
    /// This property is defined within the Sitrep configuration as "message-round-ended"
    /// and is utilized to notify relevant services or systems upon the end of a round.
    /// </summary>
    [Config("sitrep", "message-round-ended", "Message to send when the round ends.")]
    public static string RoundEndedMessage { get; set; } = "Round ended!";

    /// <summary>
    /// Represents the configurable message that is sent when a round starts.
    /// This property is defined within the Sitrep configuration as "message-round-started"
    /// and is utilized to notify relevant services or systems upon the start of a round.
    /// </summary>
    [Config("sitrep", "message-round-started", "Message to send when the round starts.")]
    public static string RoundStartedMessage { get; set; } = "Round number $Number started!";

    /// <summary>
    /// Represents the configurable message that is sent when a round is waiting for players.
    /// This property is defined within the Sitrep configuration as "message-round-waiting"
    /// and is utilized to notify relevant services or systems when the round is in a waiting state.
    /// </summary>
    [Config("sitrep", "message-round-waiting", "Message to send when the round is waiting for players.")]
    public static string RoundWaitingMessage { get; set; } = "Waiting for players ..";

    /// <summary>
    /// Represents the configurable message that is sent when a round is restarting.
    /// This property is defined within the Sitrep configuration as "message-round-restarting"
    /// and is utilized to notify relevant services or systems when a round enters the restarting phase.
    /// </summary>
    [Config("sitrep", "message-round-restarting", "Message to send when the round is restarting.")]
    public static string RoundRestartingMessage { get; set; } = "Round restarting ..";

    /// <summary>
    /// Starts the SitrepPlayerEvents service, registering the necessary event listeners
    /// and initializing the associated SitrepService instance.
    /// This method subscribes to player join and leave events, enabling Sitrep notifications to the appropriate channels.
    /// </summary>
    public static void Start()
    {
        ExRoundEvents.Ended += OnEnded;
        ExRoundEvents.Started += OnStarted;
        ExRoundEvents.Restarting += OnRestarting;
        ExRoundEvents.WaitingForPlayers += OnWaiting;
    }

    private static void OnStarted()
        => SitrepService.TrySendEvent(SitrepEvent.RoundStarted, RoundStartedMessage, dict => dict.Add("Number", ExRound.RoundNumber.ToString("00")));
    
    private static void OnEnded()
        => SitrepService.TrySendEvent(SitrepEvent.RoundEnded, RoundEndedMessage, null);
    
    private static void OnWaiting()
        => SitrepService.TrySendEvent(SitrepEvent.RoundWaiting, RoundWaitingMessage, null);

    private static void OnRestarting()
        => SitrepService.TrySendEvent(SitrepEvent.RoundRestarting, RoundRestartingMessage, null);
}