using LabApi.Events.Arguments.WarheadEvents;
using LabApi.Events.Handlers;

using LabExtended.API;
using LabExtended.Events;

using NiveraAPI.IO.Configs;

namespace PRTS.Client.Levels.Rewards;

/// <summary>
/// Handles the rewards for activating the warhead in the game.
/// </summary>
public static class WarheadRewards
{
    private static bool wasWarheadActive;

    /// <summary>
    /// The amount of experience to give when the warhead is activated.
    /// </summary>
    [Config("level-rewards", "warhead-reward", "The amount of experience to give when the warhead is activated.")]
    public static int WarheadRewardCount { get; set; } = 3;

    private static void OnWaiting()
    {
        wasWarheadActive = false;
    }

    private static void OnWarheadActivated(WarheadStartedEventArgs args)
    {
        if (args.Player is not ExPlayer player || player.IsServer)
            return;

        if (wasWarheadActive)
            return;

        wasWarheadActive = true;

        player.AddXp(WarheadRewardCount, $"Prvotní aktivace Alpha Warhead");
    }

    internal static void Initialize()
    {
        WarheadEvents.Started += OnWarheadActivated;

        ExRoundEvents.WaitingForPlayers += OnWaiting;
    }
}
