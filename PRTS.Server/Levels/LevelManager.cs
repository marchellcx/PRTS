using Fergun.Interactive;

using NiveraAPI.Logs;
using NiveraAPI.IO.Configs;

using PRTS.Profiles;

using PRTS.Levels.Enums;
using PRTS.Core.Attributes;
using PRTS.Profiles.Objects;

using PRTS.ScpSl;
using PRTS.ScpSl.Modules.Levels;

using NiveraAPI.IO.Storage;

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
    
    private static volatile LogSink log = LogManager.GetSource("Core", "LevelManager");
    
    /// <summary>
    /// The name of the property used to represent the player's level within a user's profile.
    /// This constant is utilized for accessing and modifying player level-related data,
    /// such as retrieving the current level, updating experience points, and determining
    /// level progression or regression.
    /// </summary>
    public const string PropertyName = "PlayerLevel";
    
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
    /// Resets the experience points (XP) and level of the specified user to their initial values.
    /// </summary>
    /// <param name="userId">
    /// The unique identifier of the user whose XP and level are to be reset.
    /// </param>
    /// <param name="reasonId">
    /// The identifier specifying the reason or source of the reset operation.
    /// </param>
    /// <param name="reasonMessage">
    /// A descriptive message explaining the reason for resetting the XP and level.
    /// </param>
    /// <returns>
    /// A boolean value indicating whether the reset operation was successfully performed.
    /// Returns <c>false</c> if the user's profile is not found, and <c>true</c> otherwise.
    /// </returns>
    public static bool ResetXp(string userId)
    {
        if (!ProfileManager.TryGetProfileByUserId(userId, out var profile))
            return false;

        if (!profile.Value.TryGetProperty<LevelProperty>(PropertyName, out var levelProperty))
            return true;
        
        levelProperty.Experience = 0;

        var level = GetLevelForXp(0);
        
        ScpSlManager.BroadcastEntities<LevelModule>(module =>
        { 
            module.CallRpcNotifyChange(profile.Value.UserId, level.Level, levelProperty.Experience);
        });
        
        log.Info($"Reset XP and level of &1{userId}&r to their initial values!");
        return true;
    }

    /// <summary>
    /// Resets the experience points (XP) and level of the user associated with the specified Discord ID to their initial values.
    /// </summary>
    /// <param name="discordId">The Discord ID of the user whose XP and level are to be reset.</param>
    /// <returns>A boolean value indicating whether the reset operation was successfully performed. Returns <c>false</c> if the user's profile is not found, and <c>true</c> otherwise.</returns>
    public static bool ResetXp(ulong discordId)
    {
        if (!ProfileManager.TryGetProfile(x => x.DiscordId == discordId, out var profile))
            return false;

        if (!profile.Value.TryGetProperty<LevelProperty>(PropertyName, out var levelProperty))
            return true;

        levelProperty.Experience = 0;

        if (!string.IsNullOrEmpty(profile.Value.UserId))
        {
            var level = GetLevelForXp(0);

            ScpSlManager.BroadcastEntities<LevelModule>(module =>
            {
                module.CallRpcNotifyChange(profile.Value.UserId, level.Level, levelProperty.Experience);
            });
        }

        log.Info($"Reset XP and level of &1{profile.Value.UserId}&r (Discord ID: &1{discordId}&r) to their initial values!");
        return true;
    }

    /// <summary>
    /// Modifies the experience points (XP) of a user identified by their Discord ID and handles associated level changes.
    /// </summary>
    /// <param name="discordId">The Discord ID of the user whose experience points are to be modified.</param>
    /// <param name="xp">The amount of XP to add or remove. Positive values add XP, while negative values subtract XP.</param>
    /// <returns>An instance of <c>LevelModifyResult</c> indicating the result of the operation.</returns>
    public static LevelModifyResult ModifyXpDiscord(ulong discordId, int xp)
    {
        if (!ProfileManager.TryGetProfile(x => x.DiscordId == discordId, out var profile))
            return LevelModifyResult.ProfileNotFound;

        return ModifyProfileXp(profile, xp);
    }

    /// <summary>
    /// Modifies the experience points (XP) of the specified user and handles associated level changes.
    /// </summary>
    /// <param name="userId">
    /// The unique identifier of the user whose experience points are to be modified.
    /// </param>
    /// <param name="xp">
    /// The amount of XP to add or remove. Positive values add XP, while negative values subtract XP.
    /// </param>
    /// <param name="reasonId">
    /// The identifier for the reason or source of the XP modification.
    /// </param>
    /// <param name="reasonMessage">
    /// A descriptive message explaining the reason for the XP modification.
    /// </param>
    /// <returns>
    /// An instance of <c>LevelModifyResult</c> indicating the result of the operation, such as whether the profile was found,
    /// the operation was successful, or if a level change occurred.
    /// </returns>
    public static LevelModifyResult ModifyXpSteam(string userId, int xp)
    {
        if (!ProfileManager.TryGetProfileByUserId(userId, out var profile))
            return LevelModifyResult.ProfileNotFound;

        return ModifyProfileXp(profile, xp);
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
    public static bool TryGetLevels(string userId, bool addProperty, out LevelProperty levelProperty)
    {
        levelProperty = null!;

        if (!ProfileManager.TryGetProfileByUserId(userId, out var profile))
            return false;

        if (addProperty)
        {
            levelProperty = profile.GetOrAddProperty<LevelProperty>(PropertyName);
            return true;
        }

        return profile.Value.TryGetProperty(PropertyName, out levelProperty);
    }

    /// <summary>
    /// Attempts to get the level property associated with the specified Discord ID.
    /// </summary>
    /// <param name="discordId">The Discord ID of the user whose level property is to be retrieved.</param>
    /// <param name="addProperty">If <c>true</c>, the method will create a new level property if one does not exist.</param>
    /// <param name="levelProperty">When this method returns, contains the level property associated with the specified Discord ID, if found; otherwise, contains null. This parameter is passed uninitialized.</param>
    /// <returns><c>true</c> if the level property was successfully retrieved; otherwise, <c>false</c>.</returns>
    public static bool TryGetLevels(ulong discordId, bool addProperty, out LevelProperty levelProperty)
    {
        levelProperty = null!;

        if (!ProfileManager.TryGetProfile(x => x.DiscordId == discordId, out var profile))
            return false;

        if (addProperty)
        {
            levelProperty = profile.GetOrAddProperty<LevelProperty>(PropertyName);
            return true;
        }

        return profile.Value.TryGetProperty(PropertyName, out levelProperty);
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
    public static LevelModifyResult ModifyProfileXp(StorageValue<ProfileInfo> profile, int xp)
    {
        if (xp != 0)
        {
            var levels = profile.GetOrAddProperty<LevelProperty>(PropertyName);
            var experience = Math.Max(0, levels.Experience + xp);

            var curLevel = GetLevelForXp(levels.Experience);
            var newLevel = GetLevelForXp(experience);

            levels.Experience = experience;

            ScpSlManager.BroadcastEntities<LevelModule>(module =>
            {
                module.CallRpcNotifyChange(profile.Value.UserId, newLevel.Level, levels.Experience);
            });

            if (curLevel.Level != newLevel.Level)
            {
                log.Info($"Player &1{profile.Value.UserId}&r has {(xp > 0 ? "&2gained&r" : "&1lost&r")} &1{xp}&r XP and changed level from &1{curLevel.Level}&r to &1{newLevel?.Level ?? curLevel.Level}&r!");

                return newLevel.Level > curLevel.Level
                    ? LevelModifyResult.LevelUp
                    : LevelModifyResult.LevelDown;
            }
            else
            {
                log.Info($"Player &1{profile.Value.UserId}&r has {(xp > 0 ? "&2gained&r" : "&1lost&r")} &1{xp}&r XP but did not change level (still at &1{curLevel.Level}&r).");
                return LevelModifyResult.Ok;
            }
        }
        else
        {
            log.Warn($"Received an XP modification of &1{xp}&r for &1{profile.Value.UserId}&r, but it was ignored as it was zero.");
        }

        return LevelModifyResult.Ok;
    }

    private static void AppendLevel(ProfileInfo profile, Func<PageBuilder> factory, List<IPageBuilder> pages)
    {
        if (profile.TryGetProperty<LevelProperty>(PropertyName, out var levelProperty))
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
            
            pages.Add(builder);
        }
    }

    [Init]
    private static void Initialize()
    {
        ProfileManager.Properties.Add(PropertyName, typeof(LevelProperty));
        
        ProfileManager.EnsureProperty(PropertyName, () =>
        {
            var property = new LevelProperty
            {
                Experience = 0
            };

            return property;
        });

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
        
        log.Info($"Initialized level system with &1{LevelCap}&r levels.");
    }
}