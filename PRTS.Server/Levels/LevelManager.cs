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
    /// Gets or sets the starting level for players when they first join the game. This property is configurable and can be adjusted to set the initial level for new players.
    /// </summary>
    [Config("level-manager", "start-level", "The starting level for players.")]
    public static int StartLevel { get; set; } = 1;

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
        
        levelProperty.Level = StartLevel;
        levelProperty.Experience = 0;
        
        ScpSlManager.BroadcastEntities<LevelModule>(module =>
        { 
            module.CallRpcNotifyChange(profile.Value.UserId, levelProperty.Level, levelProperty.Experience);
        });
        
        log.Info($"Reset XP and level of &1{userId}&r to their initial values!");
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
    public static LevelModifyResult ModifyXp(string userId, int xp)
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
            levelProperty = profile.GetOrAddProperty<LevelProperty>(PropertyName, prop =>
            {
                prop.Level = 0;
                prop.Experience = 0;
            });

            return true;
        }

        return profile.Value.TryGetProperty(PropertyName, out levelProperty);
    }

    /// <summary>
    /// Calculates the new level of a user based on their current level and total experience points.
    /// Determines whether the user levels up, levels down, or remains at the same level.
    /// </summary>
    /// <param name="currentLevel">
    /// The user's current level before applying the experience points.
    /// </param>
    /// <param name="currentExp">
    /// The user's current total experience points.
    /// </param>
    /// <returns>
    /// The user's new level if a level change occurs; otherwise, <c>null</c> if the current level remains unchanged.
    /// </returns>
    public static LevelInfo? GetLevelChange(int currentLevel, int currentExp)
    {
        for (var x = 0; x < Levels.Length; x++)
        {
            var level = Levels[x];

            if (level.Experience > currentExp)
            {
                var newLevelIndex = Math.Max(0, x - 1);
                var newLevel = Levels[newLevelIndex];

                if (newLevel.Level != currentLevel)
                    return newLevel;
            }
        }

        return null;
    }

    private static LevelModifyResult ModifyProfileXp(StorageValue<ProfileInfo> profile, int xp)
    {
        if (xp != 0)
        {
            var levels = profile.GetOrAddProperty<LevelProperty>(PropertyName, prop =>
            {
                prop.Level = StartLevel;
                prop.Experience = 0;
            });

            levels.Experience += xp;

            var curLevel = levels.Level;
            var newLevel = GetLevelChange(levels.Level, levels.Experience);

            log.Info($"Player &1{profile.Value.UserId}&r has {(xp > 0 ? "&2gained&r" : "&1lost&r")} &1{xp}&r XP!");

            ScpSlManager.BroadcastEntities<LevelModule>(module =>
            {
                module.CallRpcNotifyChange(profile.Value.UserId, newLevel?.Level ?? curLevel, levels.Experience);
            });

            if (newLevel != null)
            {
                log.Info($"Player &1{profile.Value.UserId}&r has leveled up to &1{newLevel}&r!");

                levels.Level = newLevel.Level;

                return levels.Level > curLevel
                    ? LevelModifyResult.LevelUp
                    : LevelModifyResult.LevelDown;
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
            var level = Levels.FirstOrDefault(l => l.Level == levelProperty.Level);

            if (level != null && !string.IsNullOrEmpty(level.MilestoneName))
            {
                builder.AddField(":trophy: Milestone", $"**{level.MilestoneName}**");
            }

            if (levelProperty.Level + 1 < Levels.Length)
            {
                builder.AddField(":bar_chart: Level", $"**{levelProperty.Level}** / {LevelCap}");
                builder.AddField(":books: XP", $"**{levelProperty.Experience}** / {Levels[levelProperty.Level + 1].Experience}");
            }
            else
            {
                builder.AddField(":bar_chart: Level", $"**{levelProperty.Level}** *(MAX)*");
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
                Level = StartLevel,
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
                if (kvp.Key >= x + 1)
                {
                    offset += kvp.Value;
                }
            }

            foreach (var kvp in MilestoneNames)
            {
                if (kvp.Key >= x + 1)
                {
                    milestone = kvp.Value;
                }
            }

            xp += offset;

            var info = new LevelInfo();

            info.Level = x + 1;
            info.Experience = xp;
            info.MilestoneName = milestone;

            info.IsMaxLevel = x + 1 == LevelCap;

            Levels[x] = info;

            log.Debug($"Created level &1{info.Level}&r with &1{info.Experience}&r XP and milestone &1{info.MilestoneName}&r.");
        }

        ProfileManager.ProfileEmbedBuilder += AppendLevel;
        
        log.Info($"Initialized level system with &1{LevelCap}&r levels.");
    }
}