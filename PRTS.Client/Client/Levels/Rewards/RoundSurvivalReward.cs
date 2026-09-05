using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Handlers;

using LabExtended.API;
using LabExtended.Events;

using NiveraAPI.IO.Configs;

namespace PRTS.Client.Levels.Rewards;

/// <summary>
/// Represents the round survival reward system, which grants experience points to players who survive a round in the game.
/// </summary>
public static class RoundSurvivalReward
{
    /// <summary>
    /// The amount of experience to grant for surviving a round.
    /// </summary>
    [Config("level-rewards", "round-survival", "The amount of experience to grant for surviving a round.")]
    public static int RewardAmount { get; set; } = 2;

    /// <summary>
    /// A set of user IDs representing players who have died in the current round.
    /// </summary>
    public static HashSet<string> DeadPlayers = new();

    /// <summary>
    /// Determines if a player is eligible for the round survival reward.
    /// </summary>
    /// <param name="player">The player to check eligibility for.</param>
    /// <returns>True if the player is eligible; otherwise, false.</returns>
    public static bool IsEligible(ExPlayer player)
         => player?.ReferenceHub != null && !DeadPlayers.Contains(player.UserId);

    private static void OnRoundEnd()
    {
        var eligiblePlayers = ExPlayer.Players.Where(IsEligible);

        foreach (var player in eligiblePlayers)
            player.AddXp(RewardAmount);
    }

    private static void OnRoundRestart()
            => DeadPlayers.Clear();

    private static void OnDied(PlayerDeathEventArgs args)
    {
        if (args.Player is not ExPlayer player)
            return;

        DeadPlayers.Add(player.UserId);
    }

    internal static void Initialize()
    {
        ExRoundEvents.Ended += OnRoundEnd;
        ExRoundEvents.Restarting += OnRoundRestart;

        PlayerEvents.Death += OnDied;
    }
}