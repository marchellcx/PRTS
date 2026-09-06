using LabExtended.API;

using LabExtended.Core;
using LabExtended.Events;

using NiveraAPI.IO.Configs;
using NiveraAPI.IO.Serialization;

using NiveraAPI.IO.Network.Entities.Attributes;

using PRTS.Client.Levels.Enums;
using PRTS.Client.Levels.Objects;

using PRTS.Client.Punishments;
using PRTS.Client.Levels.Rewards;

namespace PRTS.Client.Levels;

/// <summary>
/// Represents the LevelModule, which manages the leveling system for players.
/// </summary>
[ServerType("Prts.ScpSl.Modules.Levels.LevelModule")]
public class LevelModule : PrtsModule
{
    static LevelModule()
    {
        PunishmentModule.PlayerVerified += OnVerified;
        
        ExPlayerEvents.Left += OnLeft;   
        ExRoundEvents.WaitingForPlayers += OnWaiting;

        KillRewards.Initialize();
        EscapeRewards.Initialize();
        WarheadRewards.Initialize();
        RoundSurvivalReward.Initialize();
    }

    /// <summary>
    /// Occurs when the level information is received.
    /// </summary>
    public static event Action? LevelsReceived;

    /// <summary>
    /// Occurs when a player's level information is received.
    /// </summary>
    public static event Action<ExPlayer, LevelData>? PlayerLevelReceived;

    /// <summary>
    /// Occurs when a player's level information changes.
    /// </summary>
    public static event Action<ExPlayer, LevelData, LevelData, string?>? PlayerLevelChanged;

    /// <summary>
    /// Gets or sets the experience multiplier used for calculating experience points in the leveling system.
    /// </summary>
    [Config("level-module", "experience-multiplier", "The multiplier used for calculating experience points in the leveling system.")]
    public static int ExperienceMultiplier = 1;

    /// <summary>
    /// The singleton instance of the LevelModule.
    /// </summary>
    public static LevelModule Singleton { get; internal set; }

    /// <summary>
    /// A dictionary that maps players to their corresponding level and experience information.
    /// </summary>
    public static LevelInfo[] Levels { get; private set; } = [];

    /// <summary>
    /// A dictionary that maps player IDs to their corresponding level and experience information.
    /// </summary>
    public static Dictionary<string, LevelData> PlayerLevels { get; } = new();

    [IndexField] private static ushort cmd_CmdGetLevels = 0;

    [IndexField] private static ushort cmd_CmdGetPlayerLevel = 0;
    [IndexField] private static ushort cmd_CmdGetPlayerLevels = 0;

    [IndexField] private static ushort cmd_CmdResetXp = 0;
    [IndexField] private static ushort cmd_CmdModifyXp = 0;

    /// <summary>
    /// Called when the LevelModule is spawned on the client side. It initializes the module, retrieves level information from the server, and sets up player level data.
    /// </summary>
    public override void OnClientSpawned()
    {
        base.OnClientSpawned();

        Singleton = this;

        PlayerLevels.Clear();

        CallCmdGetLevels(levels =>
        {
            if (levels != null)
            {
                Levels = levels;

                for (var x = 0; x < levels.Length; x++)
                {
                    var level = levels[x];

                    if (x > 0)
                    {
                        level.PreviousLevel = levels[x - 1];
                    }
                    else if (x < levels.Length - 1)
                    {
                        level.NextLevel = levels[x + 1];
                    }

                    for (var y = x; y < levels.Length; y++)
                    {
                        var nextLevel = levels[y];

                        if (!string.IsNullOrEmpty(nextLevel.MilestoneName)
                            && (string.IsNullOrEmpty(level.MilestoneName) || level.MilestoneName != nextLevel.MilestoneName))
                        {
                            level.NextMilestone = nextLevel;
                            break;
                        }
                    }
                }

                LevelsReceived?.Invoke();

                if (ExPlayer.Count > 0)
                    RefreshPlayerLevels();
            }
            else
            {
                ApiLog.Warn($"Failed to retrieve levels from the server");
            }
        });
    }

    /// <summary>
    /// Cleans up resources and resets the Levels dictionary when the LevelModule is destroyed.
    /// </summary>
    public override void OnDestroyed()
    {
        base.OnDestroyed();

        Singleton = null!;

        Levels = [];
        
        PlayerLevels.Clear();       
    }

    /// <summary>
    /// Refreshes the level and experience information for all connected players by sending a command to the server.
    /// </summary>
    public void RefreshPlayerLevels()
    {
        CallCmdGetPlayerLevels(ExPlayer.Players
            .Where(p => p?.ReferenceHub != null && !string.IsNullOrEmpty(p.UserId))
            .Select(p => p.UserId), levels =>
        {
            if (levels != null)
            {
                foreach (var ply in ExPlayer.Players)
                {
                    if (levels.TryGetValue(ply.UserId, out var data))
                    {
                        data.CurLevel = Levels.FirstOrDefault(l => l.Level == data.curLevelNum);
                        data.NextLevel = Levels.FirstOrDefault(l => l.Level == data.curLevelNum + 1);

                        PlayerLevelReceived?.Invoke(ply, data);
                        PlayerLevels[ply.UserId] = data;
                    }
                    else
                    {
                        PlayerLevels.Remove(ply.UserId);
                    }
                }
            }
            else
            {
                ApiLog.Warn($"Failed to refresh player levels from the server");
            }
        });
    }

    /// <summary>
    /// Sends a command to the server to retrieve level information and invokes the provided callback with the result.
    /// </summary>
    /// <param name="callback">A callback function that is invoked with the result of the operation.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="callback"/> is null.</exception>
    public void CallCmdGetLevels(Action<LevelInfo[]?> callback)
    {
        if (callback == null)
            throw new ArgumentNullException(nameof(callback));

        void Response(ByteReader? reader)
        {
            if (reader == null)
            {
                callback(null);

                ApiLog.Warn($"Failed to get levels from the server");
            }
            else
            {
                var count = reader.ReadInt32();
                var array = new LevelInfo[count];

                for (var x = 0; x < count; x++)
                {
                    array[x] = new LevelInfo
                    {
                        Level = reader.ReadInt32(),
                        Experience = reader.ReadInt32(),
                        IsMaxLevel = reader.ReadBool(),
                        MilestoneName = reader.ReadString()
                    };
                }

                callback(array);
            }
        }

        SendRemoteCallback(cmd_CmdGetLevels, default(byte[]?), Response);
    }

    /// <summary>
    /// Sends a command to the server to retrieve the level and experience information for a specific user based on their ID.
    /// </summary>
    /// <param name="userId">The ID of the user whose level and experience information is to be retrieved.</param>
    /// <param name="callback">A callback function that is invoked with the result of the operation. The callback parameter contains the user's level and experience information if found, or null if no data is available.</param>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="userId"/> is null or empty, or when <paramref name="callback"/> is null.</exception>
    public void CallCmdGetPlayerLevel(string userId, Action<LevelData?> callback)
    {
        if (string.IsNullOrEmpty(userId))
            throw new ArgumentNullException(nameof(userId));
        
        if (callback == null)
            throw new ArgumentNullException(nameof(callback));

        void Response(ByteReader? reader)
        {
            if (reader == null)
            {
                callback(null);
                
                ApiLog.Warn($"Failed to get level for ID &1{userId}&r");
            }
            else
            {
                if (reader.ReadBool())
                {
                    var data = new LevelData
                    {
                        curLevelNum = reader.ReadInt32(),
                        Experience = reader.ReadInt32()
                    };

                    data.CurLevel = Levels.FirstOrDefault(l => l.Level == data.curLevelNum);
                    data.NextLevel = Levels.FirstOrDefault(l => l.Level == data.curLevelNum + 1);

                    callback(data);
                }
                else
                {
                    callback(null);
                }
            }
        }

        SendRemoteCallback(cmd_CmdGetPlayerLevel, writer =>
        {
            writer.WriteString(userId);
        }, Response);
    }

    /// <summary>
    /// Retrieves the level and experience information for multiple users based on their IDs.
    /// </summary>
    /// <param name="userIds">A collection of user IDs whose level and experience information is to be retrieved.</param>
    /// <param name="callback">A callback function that is invoked with the result of the operation. The callback parameter contains a dictionary mapping user IDs to their corresponding <see cref="LevelData"/> if found, or null if no data is available.</param>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="userIds"/> or <paramref name="callback"/> is null.</exception>
    public void CallCmdGetPlayerLevels(IEnumerable<string> userIds, Action<Dictionary<string, LevelData?>?> callback)
    {
        if (userIds == null)
            throw new ArgumentNullException(nameof(userIds));

        if (callback == null)
            throw new ArgumentNullException(nameof(callback));

        void Response(ByteReader? reader)
        {
            if (reader == null)
            {
                callback(null);
            }
            else
            {
                var dict = new Dictionary<string, LevelData?>();
                var length = reader.ReadInt32();

                for (var i = 0; i < length; i++)
                {
                    var userId = reader.ReadString();

                    if (reader.ReadBool())
                    {
                        var data = new LevelData
                        {
                            curLevelNum = reader.ReadInt32(),
                            Experience = reader.ReadInt32()
                        };

                        data.CurLevel = Levels.FirstOrDefault(l => l.Level == data.curLevelNum);
                        data.NextLevel = Levels.FirstOrDefault(l => l.Level == data.curLevelNum + 1);

                        dict[userId] = data;
                    }
                    else
                    {
                        dict[userId] = null;
                    }
                }

                callback(dict);
            }
        }

        SendRemoteCallback(cmd_CmdGetPlayerLevels, writer =>
        {
            writer.WriteEnumerable(userIds);
        }, Response);
    }

    /// <summary>
    /// Sends a command to the server to modify the experience points (XP) of a specified user. The result of the operation is returned via the provided callback.
    /// </summary>
    /// <param name="userId">The unique identifier of the user whose XP is to be modified.</param>
    /// <param name="reason">The reason for modifying the user's XP.</param>
    /// <param name="xp">The amount of XP to add or subtract. Positive values add XP, while negative values subtract XP.</param>
    /// <param name="callback">A callback function that is invoked with the result of the modification operation. The callback parameter contains the <see cref="LevelModifyResult"/> indicating the outcome of the modification.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="userId"/> is null or empty.</exception>
    public void CallCmdModifyXp(string userId, string? reason, int xp, Action<LevelModifyResult?>? callback)
    {
        if (string.IsNullOrEmpty(userId))
            throw new ArgumentNullException(nameof(userId));

        void Response(ByteReader? reader)
        {
            if (callback == null)
                return;
            
            if (reader == null)
            {
                callback(null);
            }
            else
            {
                var result = reader.ReadByte();
                
                callback((LevelModifyResult)result);
            }
        }
        
        SendRemoteCallback(cmd_CmdModifyXp, writer =>
        {
            writer.WriteString(userId);
            writer.WriteString(reason);
            writer.WriteInt32(xp);
        }, Response);
    }

    /// <summary>
    /// Sends a command to reset the experience points (XP) of a specified user.
    /// </summary>
    /// <param name="userId">The unique identifier of the user whose XP is to be reset.</param>
    /// <param name="reason">The reason for resetting the user's XP.</param>
    /// <param name="callback">An action to be invoked with a boolean result indicating whether the reset was successful.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="userId"/> is null or empty, or if <paramref name="callback"/> is null.
    /// </exception>
    public void CallCmdResetXp(string userId, string? reason, Action<bool>? callback)
    {
        if (string.IsNullOrEmpty(userId))
            throw new ArgumentNullException(nameof(userId));

        if (callback == null)
            throw new ArgumentNullException(nameof(callback));

        SendRemoteCallback(cmd_CmdResetXp, writer =>
        {
            writer.WriteString(userId);
            writer.WriteString(reason);
        }, reader =>
        {
            var result = reader?.ReadBool() ?? false;
            
            callback?.Invoke(result);      
        });
    }

    /// <summary>
    /// Handles the notification of a player's level and experience change from the server. This method is called on the client side when the server sends a notification about a player's level or experience update. 
    /// It reads the user ID, new level, and new experience from the provided ByteReader, updates the corresponding player's level data, and invokes the appropriate events to notify listeners of the change.
    /// </summary>
    /// <param name="reader">A <see cref="ByteReader"/> instance that contains serialized data about the user ID, the new level, and the new experience values.</param>
    [ClientRpc]
    public void RpcNotifyChange(ByteReader reader)
    {
        var userId = reader.ReadString();

        var newLevel = reader.ReadInt32();
        var newExperience = reader.ReadInt32();

        var reason = reader.ReadString();

        if (ExPlayer.TryGetByUserId(userId, out var player))
        {
            if (PlayerLevels.TryGetValue(userId, out var oldData)
                && (oldData.Experience != newExperience || oldData.curLevelNum != newLevel))
            {
                var oldCopy = oldData.Copy();

                oldData.curLevelNum = newLevel;
                oldData.Experience = newExperience;

                oldData.CurLevel = Levels.FirstOrDefault(l => l.Level == newLevel);
                oldData.NextLevel = Levels.FirstOrDefault(l => l.Level == newLevel + 1);

                PlayerLevelChanged?.Invoke(player, oldCopy, oldData, reason);
            }
            else
            {
                var newData = new LevelData
                {
                    curLevelNum = newLevel,
                    Experience = newExperience,

                    CurLevel = Levels.FirstOrDefault(l => l.Level == newLevel),
                    NextLevel = Levels.FirstOrDefault(l => l.Level == newLevel + 1)
                };

                PlayerLevels[userId] = newData;
                PlayerLevelReceived?.Invoke(player, newData);
            }
        }
        else
        {
            ApiLog.Warn($"Received level change notification for unknown user ID &1{userId}&r");
        }
    }

    private static void OnLeft(ExPlayer player)
    {
        PlayerLevels.Remove(player.UserId);
    }

    private static void OnWaiting()
    {
        PlayerLevels.Clear();
    }
    
    private static void OnVerified(ExPlayer player)
    {
        Singleton?.CallCmdGetPlayerLevel(player.UserId, level =>
        {
            if (level != null)
            {
                PlayerLevels[player.UserId] = level;
                PlayerLevelReceived?.Invoke(player, level);
            }
            else
            {
                ApiLog.Warn($"Failed to retrieve level data for verified player &1{player.UserId}&r");
            }
        });
    }
}