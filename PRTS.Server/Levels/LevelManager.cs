using Fergun.Interactive;

using NiveraAPI.Logs;
using NiveraAPI.Extensions;

using NiveraAPI.IO.Configs;
using NiveraAPI.IO.Storage;

using PRTS.Main;
using PRTS.Profiles;

using PRTS.Levels.Enums;
using PRTS.Core.Attributes;
using PRTS.Profiles.Objects;

using PRTS.ScpSl;
using PRTS.ScpSl.Modules.Levels;

using Discord.WebSocket;

using PRTS.Levels.Properties;

using System.Text;

namespace PRTS.Levels;

/// <summary>
/// Represents a static manager class responsible for handling all logic
/// related to player level progression, experience points (XP), and level caps.
/// This class provides methods for retrieving, modifying, and resetting level data,
/// as well as maintaining configurable properties for the level progression system.
/// </summary>
public static class LevelManager
{
    /// <summary>
    /// Gets or sets the highest level a player can achieve in the game. This property is configurable and can be adjusted to set the maximum level limit for players.
    /// </summary>
    [Config("level-manager", "level-cap", "The maximum level a player can achieve.")]
    public static int LevelCap { get; set; } = 100;

    /// <summary>
    /// Gets or sets the base amount of experience points (XP) required to level up. This property is configurable and can be adjusted to set the base XP needed for each level progression.
    /// </summary>
    [Config("level-manager", "experience-offsets", "Level experience offsets.")]
    public static Dictionary<int, int> ExperienceOffsets { get; set; } = new()
    {
        { 1, 100 },
        { 10, 200 }
    };

    /// <summary>
    /// Gets or sets the milestone names for specific levels. This property is configurable and can be adjusted to set custom names for milestone levels.
    /// </summary>
    [Config("level-manager", "milestone-names", "Milestone names for specific levels.")]
    public static Dictionary<int, string> MilestoneNames { get; set; } = new()
    {
        { 1, "Beginner" },
        { 10, "Intermediate" },
        { 20, "Advanced" }
    };

    /// <summary>
    /// Gets or sets the roles associated with specific levels. This property is configurable and can be adjusted to assign roles to players based on their achieved levels.
    /// </summary>
    [Config("level-manager", "roles", "Roles for specific levels.")]
    public static Dictionary<int, ulong> Roles { get; set; } = new();
    
    private static volatile LogSink log = LogManager.GetSource("Core", "LevelManager");

    /// <summary>
    /// Gets the name of the property used to represent the player's level data within a user's profile.
    /// </summary>
    public const string LogsPropertyName = "PlayerLevelLogs";

    /// <summary>
    /// Gets the name of the property used to represent the player's level data within a user's profile.
    /// </summary>
    public const string DataPropertyName = "PlayerLevel";
    
    /// <summary>
    /// Defines an array representing the cumulative experience points (XP) required
    /// to achieve each level up to the maximum level (LevelCap).
    /// Each index corresponds to a level in the progression system,
    /// with its value indicating the total XP required to reach that level.
    /// This array is populated and initialized based on the configured LevelCap,
    /// LevelStep, and specific level step offsets.
    /// </summary>
    public static volatile LevelInfo[] Levels = [];

    /// <summary>
    /// Resets the experience points (XP) and level of the user associated with the specified Steam user ID to their initial values.
    /// </summary>
    /// <param name="userId">The Steam user ID of the user whose XP and level are to be reset.</param>
    /// <param name="reason">The reason for resetting the XP and level.</param>
    /// <returns>True if the reset operation was successful; otherwise, false.</returns>
    public static bool ResetXp(string userId, string? reason)
    {
        if (!ProfileManager.TryGetProfileByUserId(userId, out var profile))
            return false;

        if (!profile.Value.TryGetProperty<LevelDataProperty>(DataPropertyName, out var levelProperty))
            return true;

        var experience = levelProperty.Experience;

        if (profile.Value.TryGetProperty<LevelLogsProperty>(LogsPropertyName, out var logsProperty))
            logsProperty.AddLog(experience, -experience, reason);

        levelProperty.Experience = 0;

        var level = GetLevelForXp(0);

        if (profile.Value.DiscordId != 0)
            UpdateDiscordRoles(profile.Value, level.Level);

        ScpSlManager.BroadcastEntities<LevelModule>(module => { module.CallRpcNotifyChange(profile.Value.UserId, reason, level.Level, levelProperty.Experience); });
        
        log.Info($"Reset XP and level of &1{userId}&r to their initial values!");
        return true;
    }

    /// <summary>
    /// Resets the experience points (XP) and level of the user associated with the specified Discord ID to their initial values.
    /// </summary>
    /// <param name="discordId">The Discord ID of the user whose XP and level are to be reset.</param>
    /// <returns>A boolean value indicating whether the reset operation was successfully performed. Returns <c>false</c> if the user's profile is not found, and <c>true</c> otherwise.</returns>
    public static bool ResetXp(ulong discordId, string? reason)
    {
        if (!ProfileManager.TryGetProfile(x => x.DiscordId == discordId, out var profile))
            return false;

        if (!profile.Value.TryGetProperty<LevelDataProperty>(DataPropertyName, out var levelProperty))
            return true;

        var experience = levelProperty.Experience;

        if (profile.Value.TryGetProperty<LevelLogsProperty>(LogsPropertyName, out var logsProperty))
            logsProperty.AddLog(experience, -experience, reason);

        levelProperty.Experience = 0;

        var level = GetLevelForXp(0);

        if (profile.Value.DiscordId != 0)
            UpdateDiscordRoles(profile.Value, level.Level);

        if (!string.IsNullOrEmpty(profile.Value.UserId))
        {
            ScpSlManager.BroadcastEntities<LevelModule>(module =>
            {
                module.CallRpcNotifyChange(profile.Value.UserId, reason, level.Level, levelProperty.Experience);
            });
        }

        log.Info($"Reset XP and level of &1{profile.Value.UserId}&r (Discord ID: &1{discordId}&r) to their initial values!");
        return true;
    }

    /// <summary>
    /// Modifies the experience points (XP) of a user identified by their Discord ID and handles associated level changes.
    /// </summary>
    /// <param name="discordId">The Discord ID of the user whose experience points are to be modified.</param>
    /// <param name="reason">The reason for modifying the experience points. This can be null if no specific reason is provided.</param>
    /// <param name="xp">The amount of XP to add or remove. Positive values add XP, while negative values subtract XP.</param>
    /// <returns>An instance of <c>LevelModifyResult</c> indicating the result of the operation.</returns>
    public static LevelModifyResult ModifyXpDiscord(ulong discordId, string? reason, int xp)
    {
        if (!ProfileManager.TryGetProfile(x => x.DiscordId == discordId, out var profile))
            return LevelModifyResult.ProfileNotFound;

        return ModifyProfileXp(profile, reason, xp);
    }

    /// <summary>
    /// Modifies the experience points (XP) of a user identified by their Steam user ID and handles associated level changes.
    /// </summary>
    /// <param name="userId">The Steam user ID of the user whose experience points are to be modified.</param>
    /// <param name="reason">The reason for modifying the experience points. This can be null if no specific reason is provided.</param>
    /// <param name="xp">The amount of XP to add or remove. Positive values add XP, while negative values subtract XP.</param>
    /// <returns>An instance of <c>LevelModifyResult</c> indicating the result of the operation.</returns>
    public static LevelModifyResult ModifyXpSteam(string userId, string? reason, int xp)
    {
        if (!ProfileManager.TryGetProfileByUserId(userId, out var profile))
            return LevelModifyResult.ProfileNotFound;

        return ModifyProfileXp(profile, reason, xp);
    }
    
    /// <summary>
    /// Attempts to get the level property associated with the specified user identifier.
    /// </summary>
    /// <param name="userId">
    /// The unique user identifier used to locate the level property.
    /// </param>
    /// <param name="addProperty">
    /// If <c>true</c>, the method will create a new level property if one does not exist.
    /// </param>
    /// <param name="levelProperty">
    /// When this method returns, contains the level property associated with the specified
    /// user identifier, if found; otherwise, contains null. This parameter is passed uninitialized.
    /// </param>
    /// <returns>
    /// <c>true</c> if the level property was successfully retrieved; otherwise, <c>false</c>.
    /// </returns>
    public static bool TryGetLevels(string userId, bool addProperty, out LevelDataProperty levelProperty)
    {
        levelProperty = null!;

        if (!ProfileManager.TryGetProfileByUserId(userId, out var profile))
            return false;

        if (addProperty)
        {
            levelProperty = profile.GetOrAddProperty<LevelDataProperty>(DataPropertyName);

            if (profile.Value.DiscordId != 0)
                UpdateDiscordRoles(profile.Value, GetLevelForXp(levelProperty.Experience).Level);

            return true;
        }

        return profile.Value.TryGetProperty(DataPropertyName, out levelProperty);
    }

    /// <summary>
    /// Attempts to get the level property associated with the specified Discord ID.
    /// </summary>
    /// <param name="discordId">The Discord ID of the user whose level property is to be retrieved.</param>
    /// <param name="addProperty">If <c>true</c>, the method will create a new level property if one does not exist.</param>
    /// <param name="levelProperty">When this method returns, contains the level property associated with the specified Discord ID, if found; otherwise, contains null. This parameter is passed uninitialized.</param>
    /// <returns><c>true</c> if the level property was successfully retrieved; otherwise, <c>false</c>.</returns>
    public static bool TryGetLevels(ulong discordId, bool addProperty, out LevelDataProperty levelProperty)
    {
        levelProperty = null!;

        if (!ProfileManager.TryGetProfile(x => x.DiscordId == discordId, out var profile))
            return false;

        if (addProperty)
        {
            levelProperty = profile.GetOrAddProperty<LevelDataProperty>(DataPropertyName);

            if (profile.Value.DiscordId != 0)
                UpdateDiscordRoles(profile.Value, GetLevelForXp(levelProperty.Experience).Level);

            return true;
        }

        return profile.Value.TryGetProperty(DataPropertyName, out levelProperty);
    }

    /// <summary>
    /// Retrieves the level information corresponding to the specified experience points (XP).
    /// </summary>
    /// <param name="xp">The experience points (XP) for which to retrieve the level information.</param>
    /// <returns>The <see cref="LevelInfo"/> corresponding to the specified XP.</returns>
    public static LevelInfo GetLevelForXp(int xp)
    {
        var firstLevel = Levels[0];
        var lastLevel = Levels[Levels.Length - 1];

        if (xp == firstLevel.Experience)
            return firstLevel;

        if (xp >= lastLevel.Experience)
            return lastLevel;

        for (var x = 0; x < Levels.Length; x++)
        {
            var level = Levels[x];

            if (level.Experience > xp)
            {
                var newLevelIndex = Math.Max(0, x - 1);
                return Levels[newLevelIndex];
            }
        }

        return lastLevel;
    }

    /// <summary>
    /// Modifies the experience points (XP) of a user's profile and handles associated level changes.
    /// </summary>
    /// <param name="profile">The user's profile to modify.</param>
    /// <param name="xp">The amount of experience points to add or subtract.</param>
    /// <returns>The result of the level modification.</returns>
    public static LevelModifyResult ModifyProfileXp(StorageValue<ProfileInfo> profile, string? reason, int xp)
    {
        if (xp != 0)
        {
            var logs = profile.GetOrAddProperty<LevelLogsProperty>(LogsPropertyName);
            var levels = profile.GetOrAddProperty<LevelDataProperty>(DataPropertyName);

            var curExperience = levels.Experience;
            var curLevel = GetLevelForXp(levels.Experience)!;

            var newExperience = Math.Max(0, levels.Experience + xp);
            var newLevel = GetLevelForXp(newExperience)!;

            logs.AddLog(curExperience, xp, reason);

            levels.Experience = newExperience;

            ScpSlManager.BroadcastEntities<LevelModule>(module => { module.CallRpcNotifyChange(profile.Value.UserId, reason, newLevel.Level, levels.Experience); });

            if (curLevel.Level != newLevel.Level)
            {
                UpdateDiscordRoles(profile.Value, newLevel.Level);

                return newLevel.Level > curLevel.Level
                    ? LevelModifyResult.LevelUp
                    : LevelModifyResult.LevelDown;
            }
            else
            {
                return LevelModifyResult.Ok;
            }
        }
        else
        {
            log.Warn($"Received an XP modification of &1{xp}&r for &1{profile.Value.UserId}&r, but it was ignored as it was zero.");
        }

        return LevelModifyResult.Ok;
    }

    /// <summary>
    /// Updates the Discord roles of a user based on their current level.
    /// This method checks the user's level and assigns or removes roles accordingly, ensuring that the user's roles reflect their current level in the game.
    /// </summary>
    /// <param name="profile">The user's profile containing their level and Discord ID.</param>
    public static void UpdateDiscordRoles(ProfileInfo profile, int level)
    {
        if (profile.DiscordId == 0)
            return;

        if (Roles.Count < 1)
            return;

        if (MainBotInstance.Instance?.Client == null || MainBotInstance.Instance.PrimaryGuild == null)
        {
            log.Warn($"Discord bot instance is not properly initialized or connected, so role updates cannot be performed for user &1{profile.DiscordId}&r.");
            return;
        }

        if (!MainBotInstance.Instance.IsConnected)
        {
            log.Warn($"Discord bot instance is not connected, so role updates cannot be performed for user &1{profile.DiscordId}&r.");
            return;
        }

        var user = MainBotInstance.Instance.PrimaryGuild.GetUser(profile.DiscordId);

        if (user == null)
            return;

        SocketRole? roleToAdd = null;

        foreach (var kvp in Roles)
        {
            if (kvp.Key <= level)
            {
                roleToAdd = MainBotInstance.Instance.PrimaryGuild.GetRole(kvp.Value);
            }
            else if (user.Roles.TryGetFirst(r => r.Id == kvp.Value, out var role))
            {
                log.Info($"Removing role &1{role.Name}&r from user &1{profile.UserId}&r for dropping below level &1{level}&r.");
                Task.Run(async () => await user.RemoveRoleAsync(role));
            }
        }

        if (roleToAdd != null && !user.Roles.Any(r => r.Id == roleToAdd.Id))
        {
            log.Info($"Assigning role &1{roleToAdd.Name}&r to user &1{profile.DiscordId}&r for reaching level &1{level}&r.");
            Task.Run(async () => await user.AddRoleAsync(roleToAdd));
        }
    }

    private static void AppendLevel(ProfileInfo profile, Func<PageBuilder> factory, List<IPageBuilder> pages)
    {
        if (profile.TryGetProperty<LevelDataProperty>(DataPropertyName, out var levelProperty))
        {
            var builder = factory();

            var level = GetLevelForXp(levelProperty.Experience);
            var nextLevel = level.IsMaxLevel ? null : Levels.FirstOrDefault(l => l.Level == level.Level + 1);

            if (level != null && !string.IsNullOrEmpty(level.MilestoneName))
            {
                builder.AddField(":trophy: Milestone", $"**{level.MilestoneName}**");
            }

            if (nextLevel != null)
            {
                if (!string.IsNullOrEmpty(nextLevel.MilestoneName))
                    builder.AddField(":bar_chart: Level", $"**{level.Level}** / {nextLevel.Level} ({nextLevel.MilestoneName})");
                else
                    builder.AddField(":bar_chart: Level", $"**{level.Level}** / {nextLevel.Level}");

                builder.AddField(":books: XP", $"**{levelProperty.Experience}** / {nextLevel.Experience}");
            }
            else
            {
                builder.AddField(":bar_chart: Level", $"**{level.Level}** *(MAX)*");
                builder.AddField(":books: XP", $"**{levelProperty.Experience}** XP *(MAX)*");
            }

            if (profile.TryGetProperty<LevelLogsProperty>(LogsPropertyName, out var logsProperty) && logsProperty.Logs.Count > 0)
            {
                var logBuilder = new StringBuilder();
                var fieldBuilder = new StringBuilder();

                foreach (var log in logsProperty.Logs.OrderByDescending(l => l.UtcTime))
                {
                    logBuilder.Clear();

                    var change = log.Change > 0 ? $"+{log.Change}" : $"-{log.Change}";
                    var reason = string.IsNullOrEmpty(log.Reason) ? "No reason provided" : log.Reason;

                    logBuilder.AppendLine($"- {change} XP - {reason}");

                    if (fieldBuilder.Length + logBuilder.Length <= 4096)
                    {
                        fieldBuilder.Append(logBuilder);
                    }
                    else
                    {
                        break;
                    }
                }

                if (fieldBuilder.Length > 0)
                    builder.WithDescription($":scroll: Historie\n{fieldBuilder.ToString()}");

                logBuilder.Clear();
                fieldBuilder.Clear();
            }
            
            pages.Add(builder);
        }
    }

    private static void OnReady()
    {
        foreach (var profile in ProfileManager.Profiles.Values)
        {
            if (profile.Value is not StorageValue<ProfileInfo> storageProfile)
                continue;

            if (storageProfile.Value.DiscordId == 0)
                continue;

            if (!storageProfile.Value.TryGetProperty<LevelDataProperty>(DataPropertyName, out var levelProperty))
                continue;

            var level = GetLevelForXp(levelProperty.Experience);

            UpdateDiscordRoles(storageProfile.Value, level.Level);
        }
    }

    [Init]
    private static void Initialize()
    {
        ProfileManager.Properties.Add(DataPropertyName, typeof(LevelDataProperty));
        ProfileManager.Properties.Add(LogsPropertyName, typeof(LevelLogsProperty));

        ProfileManager.EnsureProperty(DataPropertyName, () => new LevelDataProperty());
        ProfileManager.EnsureProperty(LogsPropertyName, () => new LevelLogsProperty());

        Levels = new LevelInfo[LevelCap];

        var xp = 0;

        for (var x = 0; x < LevelCap; x++)
        {
            var offset = 0;
            var milestone = string.Empty;

            foreach (var kvp in ExperienceOffsets)
            {
                if (kvp.Key <= x + 1)
                {
                    offset += kvp.Value;
                }
            }

            foreach (var kvp in MilestoneNames)
            {
                if (kvp.Key <= x + 1)
                {
                    milestone = kvp.Value;
                }
            }

            xp += offset;

            var info = new LevelInfo
            {
                Level = x + 1,

                Experience = xp,
                MilestoneName = milestone,

                IsMaxLevel = x + 1 == LevelCap
            };

            Levels[x] = info;
        }

        ProfileManager.ProfileEmbedBuilder += AppendLevel;
        MainBotInstance.Ready += OnReady;

        log.Info($"Initialized level system with &1{LevelCap}&r levels.");
    }
}