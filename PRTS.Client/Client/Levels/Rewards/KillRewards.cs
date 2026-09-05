using CustomPlayerEffects;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Arguments.Scp173Events;

using LabApi.Events.Handlers;

using LabExtended.API;
using LabExtended.Events;
using LabExtended.Utilities.Update;

using NiveraAPI.IO.Configs;

using PlayerRoles;
using PlayerRoles.PlayableScps.Scp049;
using PlayerRoles.PlayableScps.Scp939;

using PlayerStatsSystem;

using UnityEngine;

namespace PRTS.Client.Levels.Rewards;

/// <summary>
/// Handles kill rewards for players based on various conditions and abilities.
/// </summary>
public static class KillRewards
{
    private static Dictionary<ExPlayer, float> breakneckSpeedsStopTimes = new();
    private static Dictionary<ExPlayer, Dictionary<ExPlayer, float>> teamKills = new();

    private static HashSet<ExPlayer> traumatizedPlayers = new();

    /// <summary>
    /// The reward for Scp173's Breakneck Speeds ability.
    /// </summary>
    [Config("level-rewards", "scp-173-breakneck-speeds-reward", "The reward for Scp173's Breakneck Speeds ability")]
    public static int Scp173BreakneckSpeedsReward { get; set; } = 1;

    /// <summary>
    /// The maximum time in seconds for Scp173's Breakneck Speeds ability.
    /// </summary>
    [Config("level-rewards", "scp-173-breakneck-speeds-max-time", "The maximum time in seconds for Scp173's Breakneck Speeds ability")]
    public static int Scp173BreakneckSpeedsMaxTime { get; set; } = 2;

    /// <summary>
    /// The reward for Scp049's Good Sense ability.
    /// </summary>
    [Config("level-rewards", "scp-049-good-sense-reward", "The reward for Scp049's Good Sense ability")]
    public static int Scp049GoodSenseReward { get; set; } = 2;

    /// <summary>
    /// The reward for killing a human player with Scp096's charge attack.
    /// </summary>
    [Config("level-rewards", "scp-096-charge-killed-human-reward", "The reward for killing a human player with Scp096's charge attack")]
    public static int Scp096ChargeKilledHumanReward { get; set; } = 2;

    /// <summary>
    /// The reward for killing a human player with Scp939's lunge attack.
    /// </summary>
    [Config("level-rewards", "scp939-lunge-killed-human-reward", "The reward for killing a human player with a lunge attack")]
    public static int Scp939LungeKilledHumanReward { get; set; } = 2;

    /// <summary>
    /// The reward for killing a traumatized human player with Scp106.
    /// </summary>
    [Config("level-rewards", "scp-106-killed-traumatized-human-reward", "The reward for killing a traumatized human player with Scp106")]
    public static int Scp106KilledTraumatizedHumanReward { get; set; } = 1;

    /// <summary>
    /// The reward for killing a human player.
    /// </summary>
    [Config("level-rewards", "scp-killed-human-reward", "The reward for killing a human player")]
    public static int ScpKilledHumanReward { get; set; } = 1;

    /// <summary>
    /// The reward for killing a human player.
    /// </summary>
    [Config("level-rewards", "human-killed-human-reward", "The reward for killing a human player")]
    public static int HumanKilledHumanReward { get; set; } = 1;

    /// <summary>
    /// The reward for killing a zombie player.
    /// </summary>
    [Config("level-rewards", "humna-killed-zombie-reward", "The reward for killing a zombie player")]
    public static int HumanKilledZombieReward { get; set; } = 1;

    /// <summary>
    /// The reward for team kills.
    /// </summary>
    [Config("level-rewards", "team-kill-reward", "The reward for team kills")]
    public static int TeamKillReward { get; set; } = 1;

    /// <summary>
    /// The interval in seconds for multi-team kills.
    /// </summary>
    [Config("level-rewards", "multi-team-kill-interval", "The interval in seconds for multi-team kills")]
    public static int MultiTeamKillInterval { get; set; } = 60;

    private static void OnDied(PlayerDeathEventArgs args)
    {
        if (args.Attacker is not ExPlayer attacker
            || args.Player is not ExPlayer victim)
            return;

        if (attacker == victim)
            return;

        if (HitboxIdentity.IsEnemy(attacker.ReferenceHub, victim.ReferenceHub))
        {
            if (victim.Role.Type.IsHuman() && attacker.Role.Type.IsHuman())
            {
                attacker.AddXp(HumanKilledHumanReward);
            }
            else if (victim.Role.Is(RoleTypeId.Scp0492) && attacker.Role.Type.IsHuman())
            {
                attacker.AddXp(HumanKilledZombieReward);
            }
            else if (victim.Role.Type.IsHuman() && attacker.Role.IsScp)
            {
                if (args.DamageHandler is Scp939DamageHandler scp939DamageHandler
                    && scp939DamageHandler.Scp939DamageType is Scp939DamageType.LungeSecondary 
                                                            or Scp939DamageType.LungeTarget)
                {
                    attacker.AddXp(Scp939LungeKilledHumanReward);
                }
                else if (attacker.Role.Is(RoleTypeId.Scp173)
                    && breakneckSpeedsStopTimes.TryGetValue(attacker, out var breakneckSpeedStopTime)
                    && (Time.realtimeSinceStartup - breakneckSpeedStopTime) <= Scp173BreakneckSpeedsMaxTime)
                {
                    attacker.AddXp(Scp173BreakneckSpeedsReward);
                }
                else if (attacker.Role.Is(RoleTypeId.Scp049)
                    && attacker.Subroutines.Scp049SenseAbility is Scp049SenseAbility scp049SenseAbility
                    && scp049SenseAbility.HasTarget
                    && scp049SenseAbility.Target != null
                    && scp049SenseAbility.Target == victim.ReferenceHub)
                {
                    attacker.AddXp(Scp049GoodSenseReward);
                }
                else if (args.DamageHandler is Scp096DamageHandler scp096DamageHandler
                    && scp096DamageHandler._attackType is Scp096DamageHandler.AttackType.Charge)
                {
                    attacker.AddXp(Scp096ChargeKilledHumanReward);
                }
                else if (attacker.Role.Is(RoleTypeId.Scp106)
                    && traumatizedPlayers.Contains(victim))
                {
                    attacker.AddXp(Scp106KilledTraumatizedHumanReward);
                }

                attacker.AddXp(ScpKilledHumanReward);
            }
        }
        else
        {
            if (!teamKills.TryGetValue(attacker, out var dict))
                teamKills.Add(attacker, dict = new());

            dict[victim] = Time.realtimeSinceStartup;
        }
    }

    private static void OnRoleChanged(PlayerChangedRoleEventArgs args)
    {
        if (args.Player is not ExPlayer player)
            return;

        traumatizedPlayers.Remove(player);
        breakneckSpeedsStopTimes.Remove(player);
    }

    private static void OnBreakneckSpeedsChanged(Scp173BreakneckSpeedChangedEventArgs args)
    {
        if (args.Player is not ExPlayer player)
            return;

        if (!args.Active)
            breakneckSpeedsStopTimes[player] = Time.realtimeSinceStartup;
        else
            breakneckSpeedsStopTimes.Remove(player);
    }

    private static void OnUpdatedEffect(PlayerEffectUpdatedEventArgs args)
    {
        if (args.Player is not ExPlayer player)
            return;

        if (args.Effect is not Traumatized traumatizedEffect)
            return;

        if (traumatizedEffect.IsEnabled)
            traumatizedPlayers.Add(player);
        else
            traumatizedPlayers.Remove(player);
    }

    private static void OnLeft(ExPlayer player)
    {
        teamKills.Remove(player);
        traumatizedPlayers.Remove(player);
        breakneckSpeedsStopTimes.Remove(player);

        foreach (var kvp in teamKills)
            kvp.Value.Remove(player);
    }

    private static void OnWaiting()
    {
        teamKills.Clear();
        traumatizedPlayers.Clear();
        breakneckSpeedsStopTimes.Clear();
    }

    private static void OnUpdate()
    {
        if (!ExRound.IsRunning)
            return;

        if (ExPlayer.Count < 1)
            return;

        if (teamKills.Count < 1)
            return;

        foreach (var kvp in teamKills)
        {
            if (kvp.Value.Count < 1)
                return;

            var first = kvp.Value.First();

            if (Time.realtimeSinceStartup - first.Value < MultiTeamKillInterval)
                continue;

            var xp = TeamKillReward;

            for (var x = 0; x < kvp.Value.Count; x++)
                xp *= 2;

            kvp.Key.RemoveXp(xp);
            kvp.Value.Clear();
        }
    }

    internal static void Initialize()
    {
        ExPlayerEvents.Left += OnLeft;
       
        ExRoundEvents.WaitingForPlayers += OnWaiting;

        PlayerUpdateHelper.OnLateUpdate += OnUpdate;

        PlayerEvents.Death += OnDied;
        PlayerEvents.ChangedRole += OnRoleChanged;
        PlayerEvents.UpdatedEffect += OnUpdatedEffect;

        Scp173Events.BreakneckSpeedChanged += OnBreakneckSpeedsChanged;
    }
}
