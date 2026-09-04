using NiveraAPI.IO.Configs;
using NiveraAPI.Utilities;

using PRTS.Extensions;
using PRTS.Profiles.Objects;

using System.Collections.Concurrent;

using NiveraAPI.IO.Storage;

namespace PRTS.Levels.Rewards.ScpSl;

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
    /// This dictionary is used to store the experience rewards for playtime in minutes.
    /// </summary>
    [Config("level-manager", "rewards-experience", "Experience rewards for playtime")]
    public static Dictionary<string, int> RewardsExperience { get; set; } = new()
    {
        { "Playtime30", 2 },
        { "Playtime60", 3 },
        { "Playtime120", 4 },
        { "Playtime240", 5 }  
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

        LastReward ??= PlaytimeRewards.Last().Value;

        var dayEnd = DateTimeExtensions.DayEnd;
        var dayStart = DateTimeExtensions.DayStart;

        if (profile?.Value != null)
        {
            var totalPlaytime = TimeSpan.Zero;
            var totalSessions = Pools.PoolList<ProfileSession>();

            var stop = false;

            foreach (var session in profile.Value.Sessions) 
            {
                if (session.Value.Started < dayStart || session.Value.Ended > dayEnd)
                    continue;

                totalPlaytime += session.Value.Ended - session.Value.Started;
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

                            LevelManager.ModifyProfileXp(profile, experience);

                            profile.IsDirty = true;

                            stop = true;
                            break;
                        }
                    }
                }

                if (stop)
                {
                    totalSessions.ReturnList();
                    break;
                }
            }
        }
    }
}