using Discord;

using NiveraAPI.Logs;
using NiveraAPI.Utilities;

using NiveraAPI.IO.Storage;
using NiveraAPI.IO.Configs;

using PRTS.Profiles;
using PRTS.Profiles.Objects;

using PRTS.Main;
using PRTS.Core.Attributes;
using PRTS.Levels.Properties;
using PRTS.Discord.MessageCache;

using System.Diagnostics;

namespace PRTS.Levels;

public static class LevelLeaderboard
{
    private static volatile LogSink log = LogManager.GetSource("Levels", "Leaderboard");
    private static volatile Dictionary<ProfileInfo, int>? cachedLeaderboard;

    private static volatile Stopwatch forcedUpdateWatch = new();

    /// <summary>
    /// The number of top profiles to display in the leaderboard.
    /// </summary>
    [Config("level-manager", "leaderboard-size", "The number of top profiles to display in the leaderboard.")]
    public static volatile int LeaderboardSize = 10;

    /// <summary>
    /// The interval in seconds at which the leaderboard is updated.
    /// </summary>
    [Config("level-manager", "leaderboard-update-interval", "The interval in seconds at which the leaderboard is updated.")]
    public static volatile int LeaderboardUpdateInterval = 60;

    /// <summary>
    /// The interval in seconds at which the leaderboard is forcibly updated, regardless of whether there have been changes.
    /// </summary>
    [Config("level-manager", "leaderboard-forced-update-interval", "The interval in seconds at which the leaderboard is forcibly updated.")]
    public static volatile int LeaderboardForcedUpdateInterval = 300;

    /// <summary>
    /// The cached Discord message representing the leaderboard.
    /// </summary>
    public static volatile CachedDiscordMessage? LeaderboardMessage;

    /// <summary>
    /// A mapping of numbers to their corresponding emoji representations.
    /// </summary>
    public static volatile IReadOnlyDictionary<int, string> NumberToEmoji = new Dictionary<int, string>()
    {
         { 0, ":zero:" },
         { 1, ":one:" },
         { 2, ":two:" },
         { 3, ":three:" },
         { 4, ":four:" },
         { 5, ":five:" },
         { 6, ":six:" },
         { 7, ":seven:" },
         { 8, ":eight:" },
         { 9, ":nine:" }
    };

    /// <summary>
    /// Posts the leaderboard to the specified Discord text channel.
    /// </summary>
    /// <param name="channel">The Discord text channel to post the leaderboard in.</param>
    /// <returns>The message that was posted.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the channel is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the embed for the leaderboard cannot be edited.</exception>
    public static async Task<IUserMessage> PostLeaderboardAsync(ITextChannel channel, bool cacheMessage = false)
    {
        if (channel == null)
            throw new ArgumentNullException(nameof(channel), "Channel cannot be null.");

        var embedBuilder = new EmbedBuilder();
        var embedResult = await EditEmbedAsync(embedBuilder, LeaderboardSize);

        if (!embedResult)
            throw new InvalidOperationException("Failed to edit the embed for the leaderboard.");

        var message = await channel.SendMessageAsync(embed: embedBuilder.Build());

        if (cacheMessage)
        {
            CachedDiscordMessageStorage.Messages.RemoveStorageValue("LevelLeaderboard");

            try
            {
                await LeaderboardMessage?.Message?.DeleteAsync()!;
            }
            catch
            {
                // Ignore any exceptions that occur while trying to delete the previous leaderboard message.
            }

            LeaderboardMessage = message.CacheMessage("LevelLeaderboard");
        }

        return message;
    }

    /// <summary>
    /// Translates a given integer number into its corresponding emoji representation.
    /// </summary>
    /// <param name="number">The integer number to translate.</param>
    /// <returns>The emoji representation of the given number.</returns>
    public static string TranslateToEmoji(int number)
    {
        if (NumberToEmoji.TryGetValue(number, out var emoji))
            return emoji;

        var chars = number.ToString();
        var builder = Pools.PoolStringBuilder();

        for (var x = 0; x < chars.Length; x++)
        {
            var c = chars[x];

            if (c == '-')
                builder.Append(":heavy_minus_sign:");
            else if (c == '.')
                builder.Append(".");
            else if (c == ',')
                builder.Append(",");
            else if (int.TryParse(c.ToString(), out var digit) 
                && NumberToEmoji.TryGetValue(digit, out var digitEmoji))
                builder.Append(digitEmoji);
            else
                builder.Append(c);

        }

        return builder.ReturnStringBuilderValue();
    }

    /// <summary>
    /// Generates a leaderboard of profiles based on their experience points.
    /// </summary>
    /// <param name="size">The number of top profiles to include in the leaderboard.</param>
    /// <returns>A dictionary mapping profiles to their experience points.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the size is less than or equal to zero.</exception>
    public static Dictionary<ProfileInfo, int> GenerateLeaderboard(int size)
    {
        if (size <= 0)
            throw new ArgumentOutOfRangeException(nameof(size), "Size must be greater than zero.");

        if (ProfileManager.Profiles == null || ProfileManager.Profiles.ValueCount == 0)
            return [];

        var dict = new Dictionary<ProfileInfo, int>();

        foreach (var profile in ProfileManager.Profiles.Values)
        {
            if (profile.Value is not StorageValue<ProfileInfo> profileInfo)
                continue;

            if (!profileInfo.Value.TryGetProperty<LevelDataProperty>(LevelManager.DataPropertyName, out var levelData))
                continue;

            dict[profileInfo.Value] = levelData.Experience;
        }

        return dict
            .OrderByDescending(kv => kv.Value)
            .Take(size)
            .ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    /// <summary>
    /// Edits the provided EmbedBuilder to include a leaderboard of profiles based on their experience points.
    /// </summary>
    /// <param name="builder">The EmbedBuilder to edit.</param>
    /// <param name="leaderboardSize">The number of top profiles to include in the leaderboard.</param>
    /// <returns>True if the embed was successfully edited; otherwise, false.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the leaderboard size exceeds Discord's limit of 25 fields.</exception>
    public static async Task<bool> EditEmbedAsync(EmbedBuilder builder, int leaderboardSize)
    {
        if (leaderboardSize > 25)
            throw new InvalidOperationException($"Discord only allows up to 25 fields in an embed.");

        var leaderboard = GenerateLeaderboard(leaderboardSize);

        if (leaderboard.Count == 0)
            return false;

        var index = 0;

        builder.WithTitle(":trophy: Leaderboard");
        builder.WithColor(Color.Gold);
        builder.WithCurrentTimestamp();

        foreach (var kvp in leaderboard)
        {
            var level = LevelManager.GetLevelForXp(kvp.Value);
            var emojí = index == 0 ? ":first_place:" : (index == 1 ? ":second_place:" : (index == 2 ? ":third_place:" : TranslateToEmoji(index + 1)));

            if (kvp.Key.DiscordId != 0
                && MainBotInstance.Instance != null
                && MainBotInstance.Instance.Client != null
                && MainBotInstance.Instance.IsConnected)
            {
                var user = await MainBotInstance.Instance.Client.GetUserAsync(kvp.Key.DiscordId);

                builder.AddField($"{emojí} {user.GlobalName} ({kvp.Key.GetNickname()})", $"{(!string.IsNullOrEmpty(level.MilestoneName) ? $"**{level.MilestoneName}** " : "")}\n**Level**: {level.Level}\n**XP**: {kvp.Value}", false);
            }
            else
            {
                builder.AddField($"{emojí} {kvp.Key.GetNickname()}", $"{(!string.IsNullOrEmpty(level.MilestoneName) ? $"**{level.MilestoneName}** " : "")}\n**Level**: {level.Level}\n**XP**: {kvp.Value}", false);
            }

            index++;
        }

        return true;
    }

    private static bool ShouldUpdate()
    {
        if (!forcedUpdateWatch.IsRunning)
            forcedUpdateWatch.Restart();

        if (forcedUpdateWatch.Elapsed.TotalSeconds >= LeaderboardForcedUpdateInterval)
            return true;

        if (cachedLeaderboard == null)
            return true;

        var leaderboard = GenerateLeaderboard(LeaderboardSize);

        if (leaderboard.Count != cachedLeaderboard.Count)
        {
            cachedLeaderboard = leaderboard;
            return true;
        }

        var index = 0;

        foreach (var kvp in leaderboard)
        {
            var cachedKvp = cachedLeaderboard.ElementAt(index++);

            if (cachedKvp.Key.Id != kvp.Key.Id || cachedKvp.Value != kvp.Value)
            {
                cachedLeaderboard = leaderboard;
                return true;
            }
        }

        return false;
    }

    private static void OnReady()
    {
        if (CachedDiscordMessageStorage.TryGetMessage("LevelLeaderboard", out var cachedMessage))
            LeaderboardMessage = cachedMessage;

        Task.Run(UpdateAsync);
    }

    private static async Task UpdateAsync()
    {
        while (true)
        {
            await Task.Delay(LeaderboardUpdateInterval);

            try
            {
                if (LeaderboardMessage == null || !LeaderboardMessage.WasResolved)
                    continue;

                if (!ShouldUpdate())
                    continue;

                forcedUpdateWatch.Restart();

                var embedBuilder = new EmbedBuilder();
                var embedResult = await EditEmbedAsync(embedBuilder, LeaderboardSize);

                if (!embedResult)
                    continue;

                await LeaderboardMessage.Message?.ModifyAsync(msg => msg.Embed = embedBuilder.Build());
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }
    }

    [Init]
    private static void Init()
    {
        MainBotInstance.Ready += OnReady;
    }
}