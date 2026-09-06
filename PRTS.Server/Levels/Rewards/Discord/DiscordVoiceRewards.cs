using Discord.WebSocket;

using PRTS.Main;
using PRTS.Core.Attributes;

using System.Collections.Concurrent;

using NiveraAPI.Logs;
using NiveraAPI.IO.Configs;

namespace PRTS.Levels.Rewards.Discord;

/// <summary>
/// This class handles the voice rewards system for Discord users. It tracks users' voice activity and rewards them with level rewards based on their participation in voice channels. The reward amount and interval can be configured through the provided properties.
/// </summary>
public static class DiscordVoiceRewards
{
    private static volatile bool initialized;

    private static volatile int voiceInterval = 30;
    private static volatile int voiceAmount = 1;

    private static volatile ulong[] channelWhitelist = [];
    private static volatile ulong[] channelBlacklist = [];

    private static volatile LogSink log = LogManager.GetSource("Discord", "VoiceRewards");
    private static volatile ConcurrentDictionary<ulong, DateTime> utcUserVoiceDuration = new();

    /// <summary>
    /// The amount of level rewards to give for voice activity. This determines how many rewards users will receive for their voice activity in Discord.
    /// </summary>
    [Config("level-rewards", "voice-reward-amount", "The amount of level rewards to give for voice activity.")]
    public static int VoiceRewardAmount
    {
        get => voiceAmount;
        set => voiceAmount = value;
    }

    /// <summary>
    /// The interval in minutes for giving voice rewards. This determines how often users will receive rewards for their voice activity in Discord.
    /// </summary>
    [Config("level-rewards", "voice-reward-interval", "The interval in minutes for giving voice rewards.")]
    public static int VoiceRewardInterval
    {
        get => voiceInterval;
        set => voiceInterval = value;
    }

    /// <summary>
    /// The list of voice channel IDs that are whitelisted for voice rewards. Users in these channels will receive rewards for their voice activity.
    /// </summary>
    [Config("level-rewards", "voice-channel-whitelist", "The list of voice channel IDs that are whitelisted for voice rewards.")]
    public static ulong[] ChannelWhitelist
    {
        get => channelWhitelist;
        set => channelWhitelist = value;
    }

    /// <summary>
    /// The list of voice channel IDs that are blacklisted for voice rewards. Users in these channels will not receive rewards for their voice activity.
    /// </summary>
    [Config("level-rewards", "voice-channel-blacklist", "The list of voice channel IDs that are blacklisted for voice rewards.")]
    public static ulong[] ChannelBlacklist
    {
        get => channelBlacklist;
        set => channelBlacklist = value;
    }

    private static async Task OnUpdateAsync()
    {
        while (true)
        {
            await Task.Delay(100);

            try
            {
                foreach (var kvp in utcUserVoiceDuration)
                {
                    var userId = kvp.Key;
                    var lastVoiceActivity = kvp.Value;

                    if ((DateTime.UtcNow - lastVoiceActivity).TotalMinutes >= VoiceRewardInterval)
                    {
                        LevelManager.ModifyXpDiscord(userId, $"{VoiceRewardInterval} minut ve voice channelu", VoiceRewardAmount);

                        utcUserVoiceDuration[userId] = DateTime.UtcNow;

                        log.Info($"Rewarded {VoiceRewardAmount} XP to user {userId} for voice activity.");
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }
    }

    private static Task OnUserVoiceChanged(SocketUser user, SocketVoiceState oldState, SocketVoiceState newState)
    {
        if (user.IsBot)
            return Task.CompletedTask;

        if (oldState.VoiceChannel == null && newState.VoiceChannel == null)
            return Task.CompletedTask;

        if (oldState.VoiceChannel == null && newState.VoiceChannel != null)
        {
            utcUserVoiceDuration.TryRemove(user.Id, out _);

            log.Info($"User {user.Username} joined voice channel {newState.VoiceChannel.Name}.");

            if (channelWhitelist.Length > 0 && !channelWhitelist.Contains(newState.VoiceChannel.Id))
                return Task.CompletedTask;

            if (channelBlacklist.Length > 0 && channelBlacklist.Contains(newState.VoiceChannel.Id))
                return Task.CompletedTask;

            utcUserVoiceDuration.TryAdd(user.Id, DateTime.UtcNow);

            log.Debug($"Started tracking voice activity for user {user.Username}.");
        }
        else if (oldState.VoiceChannel != null && newState.VoiceChannel == null)
        {
            utcUserVoiceDuration.TryRemove(user.Id, out _);

            log.Info($"User {user.Username} left voice channel {oldState.VoiceChannel.Name}.");
        }
        else if (oldState.VoiceChannel != null && newState.VoiceChannel != null && oldState.VoiceChannel.Id != newState.VoiceChannel.Id)
        {
            utcUserVoiceDuration.TryRemove(user.Id, out _);

            log.Info($"User {user.Username} switched from voice channel {oldState.VoiceChannel.Name} to {newState.VoiceChannel.Name}.");

            if (channelWhitelist.Length > 0 && !channelWhitelist.Contains(newState.VoiceChannel.Id))
                return Task.CompletedTask;

            if (channelBlacklist.Length > 0 && channelBlacklist.Contains(newState.VoiceChannel.Id))
                return Task.CompletedTask;

            utcUserVoiceDuration.TryAdd(user.Id, DateTime.UtcNow);

            log.Debug($"Started tracking voice activity for user {user.Username} in new channel.");
        }

        return Task.CompletedTask;
    }

    private static void OnReady()
    {
        if (initialized)
            return;

        initialized = true;

        MainBotInstance.Instance.Client.UserVoiceStateUpdated += OnUserVoiceChanged;

        Task.Run(OnUpdateAsync);
        
        log.Info("DiscordVoiceRewards is now active and tracking voice activity.");
    }

    [Init]
    private static void Initialize()
    {
        MainBotInstance.Ready += OnReady;

        log.Info($"Initialized DiscordVoiceRewards.");
    }
}
