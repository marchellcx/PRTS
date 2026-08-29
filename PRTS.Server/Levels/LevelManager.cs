using Fergun.Interactive;

using NiveraAPI.Logs;
using NiveraAPI.IO.Configs;

using PRTS.Profiles;

using PRTS.Levels.Enums;
using PRTS.Core.Attributes;
using PRTS.Profiles.Objects;

using PRTS.ScpSl;
using PRTS.ScpSl.Modules.Levels;

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
    /// Represents the maximum level a player can achieve within the system.
    /// This property defines the upper limit for player progression and is used
    /// to validate and cap level-related operations, ensuring that no level
    /// exceeds the configured threshold.
    /// </summary>
    [Config("level-manager", "level-cap", "The maximum level a player can reach.")]
    public static int LevelCap { get; set; } = 100;

    /// <summary>
    /// Represents the amount of experience points required for a player to level up.
    /// This property is used to determine the progression threshold between levels
    /// and is factored into calculations for experience gains and level advancements.
    /// </summary>
    [Config("level-manager", "level-step", "The amount of experience points required to level up.")]
    public static int LevelStep { get; set; } = 10;

    /// <summary>
    /// Represents a collection of level-specific experience point offsets used to adjust
    /// progression requirements dynamically. The dictionary maps specific level numbers
    /// to their corresponding experience offsets, allowing for non-linear level progression.
    /// This property is particularly useful for customizing progression curves beyond
    /// the standard level step values.
    /// </summary>
    [Config("level-manager", "level-step-offsets", "A dictionary of level step offsets.")]
    public static Dictionary<int, int> LevelStepOffsets { get; set; } = new()
    {
        [20] = 20
    };

    /// <summary>
    /// Represents the maximum number of logs to retain for each user's level changes.
    /// </summary>
    [Config("level-manager", "max-log-count", "The maximum number of logs to keep for each user's level changes.")]
    public static int MaxLogCount { get; set; } = 100;

    /// <summary>
    /// Indicates whether to log only level changes or to include experience point (XP) changes as well.
    /// </summary>
    [Config("level-manager", "log-only-levels", "Whether to log only level changes, or also include XP changes.")]
    public static bool LogOnlyLevels { get; set; } = true;
    
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
    public static volatile int[] Levels = [];

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
    public static bool ResetXp(string userId, string reasonId, string reasonMessage)
    {
        if (!ProfileManager.TryGetProfileByUserId(userId, out var profile))
            return false;

        if (!profile.Value.TryGetProperty<LevelProperty>(PropertyName, out var levelProperty))
            return true;
        
        levelProperty.Level = 0;
        levelProperty.Experience = 0;
        
        ScpSlManager.BroadcastEntities<LevelModule>(module =>
        { 
            module.CallRpcNotifyChange(profile.Value.UserId, levelProperty.Level, levelProperty.Experience, reasonId, reasonMessage);
        });
        
        log.Info($"Reset XP and level of &1{userId}&r to their initial values!");
        return true;
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
    public static LevelModifyResult ModifyXp(string userId, int xp, string reasonId, string reasonMessage)
    {
        if (xp != 0)
        {
            if (!ProfileManager.TryGetProfileByUserId(userId, out var profile))
                return LevelModifyResult.ProfileNotFound;

            var levels = profile.GetOrAddProperty<LevelProperty>(PropertyName, prop =>
            {
                prop.Level = 0;
                prop.Experience = 0;
            });

            levels.Experience += xp;

            var curLevel = levels.Level;
            var newLevel = GetLevelChange(levels.Level, levels.Experience);

            if (newLevel.HasValue || !LogOnlyLevels)
            {
                if (MaxLogCount > 0 && levels.Logs.Count > MaxLogCount)
                {
                    log.Debug($"Clearing logs for &1{userId}&r as they exceeded the maximum count of &1{MaxLogCount}&r!");
                    
                    levels.ClearLogs();
                }

                levels.AddLog(new()
                {
                    Time = DateTime.UtcNow,

                    Change = xp,

                    LevelAfter = newLevel ?? curLevel,
                    LevelBefore = curLevel,

                    ReasonId = reasonId,
                    ReasonMessage = reasonMessage
                });
            }

            log.Debug($"Player &1{userId}&r has {(xp > 0 ? "&2gained&r" : "&1lost&r")} &1{xp}&r XP!");
            
            ScpSlManager.BroadcastEntities<LevelModule>(module =>
            { 
                module.CallRpcNotifyChange(profile.Value.UserId, newLevel ?? curLevel, levels.Experience, reasonId, reasonMessage);
            });

            if (newLevel.HasValue)
            {
                log.Info($"Player &1{userId}&r has leveled up to &1{newLevel}&r!");
                
                levels.Level = newLevel.Value;

                return levels.Level > curLevel
                    ? LevelModifyResult.LevelUp
                    : LevelModifyResult.LevelDown;
            }
        }
        else
        {
            log.Warn($"Received an XP modification of &1{xp}&r for &1{userId}&r, but it was ignored as it was zero.");
        }

        return LevelModifyResult.Ok;
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
    public static int? GetLevelChange(int currentLevel, int currentExp)
    {
        log.Debug($"Calculating level change for current level &1{currentLevel}&r with &1{currentExp}&r XP...");

        for (var x = 0; x < Levels.Length; x++)
        {
            var requiredXp = Levels[x];

            if (requiredXp > currentExp)
            {
                var newLevel = Math.Max(0, x - 1);

                if (newLevel != currentLevel)
                    return newLevel;
            }
        }

        return null;
    }

    private static void AppendLevel(ProfileInfo profile, Func<PageBuilder> factory, List<IPageBuilder> pages)
    {
        if (profile.TryGetProperty<LevelProperty>(PropertyName, out var levelProperty))
        {
            var builder = factory();

            if (levelProperty.Level + 1 < Levels.Length)
            {
                builder.AddField(":bar_chart: Level", $"**{levelProperty.Level}** / {LevelCap}");
                builder.AddField(":books: XP", $"**{levelProperty.Experience}** / {Levels[levelProperty.Level + 1]}");
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
            var property = new LevelProperty();
            
            property.Level = 0;
            property.Experience = 0;
            
            return property;
        });
        
        Levels = new int[LevelCap];
       
        var baseXp = LevelStep;

        for (var x = 0; x < LevelCap; x++)
        {
            baseXp += LevelStep;

            var xp = baseXp;

            foreach (var kvp in LevelStepOffsets)
            {
                if (kvp.Key <= x)
                {
                    xp += kvp.Value;
                }
            }
            
            Levels[x] = xp;
        }

        ProfileManager.ProfileEmbedBuilder += AppendLevel;
        
        log.Info($"Initialized level system with &1{LevelCap}&r levels and &1{LevelStep}&r XP per level.");
    }
}