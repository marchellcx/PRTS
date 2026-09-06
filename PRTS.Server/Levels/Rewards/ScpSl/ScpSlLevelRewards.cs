using NiveraAPI.IO.Configs;
using NiveraAPI.Utilities;

using PRTS.Extensions;
using PRTS.Profiles.Objects;

using System.Collections.Concurrent;

using NiveraAPI.IO.Storage;

namespace PRTS.Levels.Rewards.ScpSl;

/// <summary>
/// This class is responsible for managing the playtime rewards for players in the SCP: Secret Laboratory game. 
/// It defines the rewards based on the amount of time a player has spent in the game, both for individual sessions and total playtime.
/// The class provides methods to update the player's rewards based on their playtime and ensures that players receive the appropriate experience points for their achievements.
/// </summary>
public static class ScpSlLevelRewards
{
    /// <summary>
    /// This dictionary is used to store the playtime rewards in minutes and their corresponding reward keys.
    /// </summary>
    [Config("level-manager", "playtime-rewards", "Rewards for playtime in minutes")]
    public static Dictionary<int, string> PlaytimeRewards { get; set; } = new()
    {
        { 30, "Playtime30" },
        { 60, "Playtime60"  },
        { 120, "Playtime120" },
        { 240, "Playtime240" },
    };

    /// <summary>
    /// This dictionary is used to store the total playtime rewards in minutes and their corresponding reward keys.
    /// </summary>
    [Config("level-manager", "total-playtime-rewards", "Rewards for total playtime in minutes")]
    public static Dictionary<int, string> TotalPlaytimeRewards { get; set; } = new()
    {
        { 1440, "Playtime24" },
        { 2880, "Playtime48" },
        { 6000, "Playtime100" }
    };

    /// <summary>
    /// This dictionary is used to store the experience rewards for playtime in minutes.
    /// </summary>
    [Config("level-manager", "rewards-experience", "Experience rewards for playtime")]
    public static Dictionary<string, int> RewardsExperience { get; set; } = new()
    {
        { "Playtime30", 2 },
        { "Playtime60", 3 },
        { "Playtime120", 4 },
        { "Playtime240", 5 },
        { "Playtime24", 24 },
        { "Playtime48", 48 },
        { "Playtime100", 100 }
    };

    /// <summary>
    /// This variable is used to store the last reward key in the PlaytimeRewards dictionary.
    /// </summary>
    public static volatile string? LastReward;

    /// <summary>
    /// This dictionary is used to store the playtime rewards in descending order of playtime.
    /// </summary>
    public static volatile ConcurrentDictionary<int, string> OrderedPlaytimeRewards = new();

    /// <summary>
    /// This method updates the playtime rewards for a given player based on their total playtime for the day. 
    /// It checks if the player has reached any of the defined playtime thresholds and awards them the corresponding experience points if they haven't already received that reward for the current day.
    /// </summary>
    /// <param name="player">The player for whom to update playtime rewards.</param>
    public static void UpdatePlayTimeReward(StorageValue<ProfileInfo> profile)
    {
        if (PlaytimeRewards.Count < 1 || RewardsExperience.Count < PlaytimeRewards.Count)
            return;

        if (OrderedPlaytimeRewards.Count < 1)
        {
            var ordered = PlaytimeRewards.OrderByDescending(kvp => kvp.Key);

            foreach (var kvp in ordered)
                OrderedPlaytimeRewards.TryAdd(kvp.Key, kvp.Value);
        }

        if (profile?.Value != null)
        {
            LastReward ??= PlaytimeRewards.Last().Value;

            var dayEnd = DateTimeExtensions.DayEnd;
            var dayStart = DateTimeExtensions.DayStart;

            var totalUserPlaytime = TimeSpan.Zero;
            var totalPlaytime = TimeSpan.Zero;
            var totalSessions = Pools.PoolList<ProfileSession>();

            var stop = false;

            foreach (var session in profile.Value.Sessions) 
            {
                if (session.Value.Started == DateTime.MinValue || session.Value.Ended == DateTime.MinValue)
                    continue;

                if (session.Value.Started < dayStart || session.Value.Ended > dayEnd)
                {
                    totalUserPlaytime += (session.Value.Ended - session.Value.Started);
                    continue;
                }

                var sessionDuration = session.Value.Ended - session.Value.Started;

                totalUserPlaytime += sessionDuration;

                if (!stop)
                {
                    totalPlaytime += sessionDuration;
                    totalSessions.Add(session.Value);

                    foreach (var kvp in OrderedPlaytimeRewards)
                    {
                        if (totalPlaytime.TotalMinutes >= kvp.Key)
                        {
                            var rewardKey = kvp.Value;

                            if (totalSessions.Any(s => s.Rewards.Contains(kvp.Value)))
                                continue;

                            if (RewardsExperience.TryGetValue(rewardKey, out var experience))
                            {
                                totalSessions.ForEach(s => s.Rewards.Add(rewardKey));

                                LevelManager.ModifyProfileXp(profile, rewardKey, experience);

                                profile.IsDirty = true;

                                stop = true;
                                break;
                            }
                        }
                    }

                    if (stop)
                        totalSessions.ReturnList();
                }
            }

            if (TotalPlaytimeRewards.Count > 0)
            {
                foreach (var kvp in TotalPlaytimeRewards)
                {
                    if (totalUserPlaytime.TotalMinutes >= kvp.Key)
                    {
                        if (profile.Value.CustomData.ContainsKey($"SlRewards_{kvp.Key}"))
                            continue;

                        if (RewardsExperience.TryGetValue(kvp.Value, out var experience))
                        {
                            LevelManager.ModifyProfileXp(profile, kvp.Value, experience);

                            profile.Value.CustomData[$"SlRewards_{kvp.Key}"] = "true";
                            profile.IsDirty = true;
                        }
                    }
                }
            }
        }
    }
}