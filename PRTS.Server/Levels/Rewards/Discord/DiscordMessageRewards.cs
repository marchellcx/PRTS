using Discord;
using Discord.WebSocket;

using PRTS.Main;

using NiveraAPI.Logs;
using NiveraAPI.Utilities;

using NiveraAPI.IO.Configs;
using NiveraAPI.IO.Storage;

using PRTS.Profiles;
using PRTS.Profiles.Objects;

using PRTS.Core.Attributes;
using PRTS.Discord.Utilities;

namespace PRTS.Levels.Rewards.Discord;

/// <summary>
/// This class handles the logic for rewarding users with XP based on their message activity in Discord channels. It includes rate limiting, whitelisting, blacklisting, and total message count rewards.
/// </summary>
public static class DiscordMessageRewards
{
    private static bool initialized;

    private static LogSink log = LogManager.GetSource("Discord", "MessageRewards");

    private static DiscordMessageRateLimit messageRateLimit = new();

    private static Dictionary<ulong, int> counters = new();
    private static Dictionary<ulong, StorageValue<ProfileInfo>> profiles = new();

    /// <summary>
    /// The maximum number of messages a user can send in the specified interval to receive rewards.
    /// </summary>
    [Config("level-rewards", "discord-message-reward-rate-limit-amount", "The maximum number of messages a user can send in the specified interval to receive rewards.")]
    public static int MessageRewardRateLimitAmount { get; set; } = 10;

    /// <summary>
    /// The interval in seconds for the message reward rate limit.
    /// </summary>
    [Config("level-rewards", "discord-message-reward-rate-limit-interval", "The interval in seconds for the message reward rate limit.")]
    public static int MessageRewardRateLimitInterval { get; set; } = 60;

    /// <summary>
    /// The number of messages a user must send to receive a reward.
    /// </summary>
    [Config("level-rewards", "discord-message-reward-count", "The number of messages a user must send to receive a reward.")]
    public static int MessageRewardCount { get; set; } = 100;

    /// <summary>
    /// The amount of XP to reward for sending the specified number of messages.
    /// </summary>
    [Config("level-rewards", "discord-message-reward-amount", "The amount of XP to reward for sending the specified number of messages.")]
    public static int MessageRewardAmount { get; set; } = 1;

    /// <summary>
    /// A list of channel IDs where message rewards are enabled.
    /// </summary>
    [Config("level-rewards", "discord-message-reward-whitelist", "A list of channel IDs where message rewards are enabled.")]
    public static List<ulong> WhitelistedChannels { get; set; } = new();

    /// <summary>
    /// A list of channel IDs where message rewards are disabled.
    /// </summary>
    [Config("level-rewards", "discord-message-reward-blacklist", "A list of channel IDs where message rewards are disabled.")]
    public static List<ulong> BlacklistedChannels { get; set; } = new();

    /// <summary>
    /// A dictionary of total message counts and their corresponding XP rewards.
    /// </summary>
    [Config("level-rewards", "discord-message-reward-total-messages", "A dictionary of total message counts and their corresponding XP rewards.")]
    public static Dictionary<int, int> TotalMessagesRewards { get; set; } = new()
    {
        { 5000, 5 },
        { 10000, 10 },
        { 25000, 25 }
    };

    private static void RewardTotalMessages(SocketMessage message, out StorageValue<ProfileInfo> profile) 
    {
        if (!profiles.TryGetValue(message.Author.Id, out profile)) 
        {
            profile = ProfileManager.GetOrAddProfileWithDiscordId(message.Author.Id);
            profiles.Add(message.Author.Id, profile);
        }

        if (!profile.Value.CustomData.TryGetValue("DiscordTotalMessages", out var totalMessagesStr) 
            || !int.TryParse(totalMessagesStr, out var totalMessages))
        {
            profile.Value.CustomData["DiscordTotalMessages"] = "1";
            profile.IsDirty = true;
        }
        else
        {
            foreach (var kvp in TotalMessagesRewards)
            {
                if (totalMessages >= kvp.Key)
                {
                    if (profile.Value.CustomData.ContainsKey($"DiscordTotalMessagesRewarded_{kvp.Key}"))
                        continue;

                    LevelManager.ModifyProfileXp(profile, $"{kvp.Key} Discord zpráv (jednou)", kvp.Value);

                    log.Info($"Rewarded {kvp.Value} XP to user {message.Author.Username} ({message.Author.Id}) for reaching {totalMessages} total messages.");

                    var newTotalMessages = (totalMessages + 1).ToString();

                    profile.Value.CustomData.TryAdd("DiscordTotalMessagesRewarded_" + kvp.Key, "true");
                    profile.Value.CustomData.TryUpdate("DiscordTotalMessages", newTotalMessages, totalMessagesStr);

                    profile.IsDirty = true;
                }
            }
        }
    }

    private static void OnMessage(SocketMessage message)
    {
        try
        {
            if (message.Author.IsBot || message.Author.IsWebhook)
                return;

            if (message.Channel.ChannelType is not ChannelType.Text)
                return;

            if (WhitelistedChannels.Count > 0 && !WhitelistedChannels.Contains(message.Channel.Id))
                return;

            if (BlacklistedChannels.Contains(message.Channel.Id))
                return;

            if (!messageRateLimit.TryAllow(message.Author.Id))
                return;

            RewardTotalMessages(message, out var profile);

            if (!counters.TryGetValue(message.Author.Id, out int count))
                counters[message.Author.Id] = count = 1;

            if (count < MessageRewardCount)
            {
                counters[message.Author.Id] = count + 1;
                return;
            }

            LevelManager.ModifyProfileXp(profile, $"{MessageRewardCount} Discord zpráv", MessageRewardAmount);

            counters.Remove(message.Author.Id);
        }
        catch (Exception ex)
        {
            log.Error($"Error processing message from user {message.Author.Username} ({message.Author.Id}):\n{ex}");
        }
    }

    private static Task _OnMessage(SocketMessage message)
    {
        ThreadHelper.RunOnMainThread(() => OnMessage(message));
        return Task.CompletedTask;
    }

    private static void OnReady()
    {
        if (initialized)
            return;

        initialized = true;

        MainBotInstance.Instance.Client.MessageReceived += _OnMessage;

        log.Info($"DiscordMessageRewards is now listening for messages.");
    }

    [Init]
    private static void Initialize()
    {
        messageRateLimit.Interval = TimeSpan.FromSeconds(MessageRewardRateLimitInterval);
        messageRateLimit.MaxMessages = MessageRewardRateLimitAmount;

        MainBotInstance.Ready += OnReady;

        log.Info($"Initialized DiscordMessageRewards.");
    }
}