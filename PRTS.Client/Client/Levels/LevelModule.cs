using LabExtended.API;

using LabExtended.Core;
using LabExtended.Events;

using NiveraAPI.IO.Serialization;
using NiveraAPI.IO.Network.Entities.Attributes;

using PRTS.Client.Levels.Enums;
using PRTS.Client.Levels.Events;

using PRTS.Client.Punishments;
using PRTS.Client.Levels.Objects;

namespace PRTS.Client.Levels;

/// <summary>
/// Represents the LevelModule, which manages the leveling system for players.
/// </summary>
[ServerType("Prts.ScpSl.Modules.Levels.LevelModule")]
public class LevelModule : PrtsModule
{
    /// <summary>
    /// The singleton instance of the LevelModule.
    /// </summary>
    public static LevelModule Singleton;
    
    static LevelModule()
    {
        PunishmentModule.PlayerVerified += OnVerified;
        
        ExPlayerEvents.Left += OnLeft;
        
        ExRoundEvents.WaitingForPlayers += OnWaiting;
    }
    
    /// <summary>
    /// Represents a collection that tracks the levels and experience points of players.
    /// </summary>
    public static Dictionary<ExPlayer, (int Level, int Experience)> Levels { get; } = new();

    /// <summary>
    /// Event triggered when the level of a player changes.
    /// </summary>
    public static event Action<LevelEventArgs>? LevelChanged;

    /// <summary>
    /// Event triggered when the experience of a player changes.
    /// </summary>
    public static event Action<LevelEventArgs>? ExperienceChanged;

    /// <summary>
    /// Event triggered when a player receives a new level and experience.
    /// </summary>
    public static event Action<ExPlayer, (int Level, int Experience)>? LevelReceived; 
    
    [IndexField] private static ushort cmd_CmdGetLogs = 0;
    [IndexField] private static ushort cmd_CmdGetLevel = 0;
    [IndexField] private static ushort cmd_CmdResetXp = 0;
    [IndexField] private static ushort cmd_CmdModifyXp = 0;

    /// <summary>
    /// Initializes the LevelModule instance when a client is spawned and retrieves the level and experience
    /// data for all connected players. Populates the Levels dictionary with this information and triggers
    /// the LevelReceived event for each player whose data is successfully retrieved.
    /// </summary>
    public override void OnClientSpawned()
    {
        base.OnClientSpawned();

        Singleton = this;

        Levels.Clear();
        
        foreach (var player in ExPlayer.Players)
        {
            CallCmdGetLevel(player.UserId, levels =>
            {
                if (levels != null)
                {
                    Levels.Add(player, levels.Value);
                    LevelReceived?.Invoke(player, levels.Value);
                }
            });
        }
    }

    /// <summary>
    /// Cleans up resources and resets the Levels dictionary when the LevelModule is destroyed.
    /// </summary>
    public override void OnDestroyed()
    {
        base.OnDestroyed();

        Singleton = null!;
        
        Levels.Clear();       
    }

    /// <summary>
    /// Retrieves the current level and experience of a specified user.
    /// </summary>
    /// <param name="userId">
    /// The ID of the user whose level and experience information is to be retrieved.
    /// </param>
    /// <param name="callback">
    /// A callback function that is invoked with the result of the operation.
    /// The callback parameter contains a tuple with the user's level and experience if found, or null if the user does not exist.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the <paramref name="userId"/> is null or empty, or when <paramref name="callback"/> is null.
    /// </exception>
    public void CallCmdGetLevel(string userId, Action<(int Level, int Experience)?> callback)
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
                
                ApiLog.Debug($"Failed to get level for ID &1{userId}&r");
            }
            else
            {
                if (reader.ReadBool())
                {
                    var level = reader.ReadInt32();
                    var xp = reader.ReadInt32();

                    callback((level, xp));
                }
                else
                {
                    callback(null);
                    
                    ApiLog.Debug($"No level and experience found for ID &1{userId}&r");
                }
            }
        }

        SendRemoteCallback(cmd_CmdGetLevel, writer =>
        {
            writer.WriteString(userId);
        }, Response);
    }

    /// <summary>
    /// Modifies the experience points for a specific user based on a given reason.
    /// </summary>
    /// <param name="userId">
    /// The ID of the user whose experience points are to be modified.
    /// </param>
    /// <param name="reasonId">
    /// A string identifier representing the reason for modifying the experience points.
    /// </param>
    /// <param name="reasonMessage">
    /// A descriptive message providing additional context about the modification reason.
    /// </param>
    /// <param name="xp">
    /// The amount of experience points to add or subtract from the user's current total.
    /// Positive values add experience, while negative values subtract experience.
    /// </param>
    /// <param name="callback">
    /// A callback function that is invoked with the result of the modification operation.
    /// The callback parameter contains the <see cref="LevelModifyResult"/> indicating
    /// the outcome of the modification.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the <paramref name="userId"/>, <paramref name="reasonId"/>,
    /// <paramref name="reasonMessage"/>, or <paramref name="callback"/> is null or empty.
    /// </exception>
    public void CallCmdModifyXp(string userId, string reasonId, string reasonMessage, int xp,
        Action<LevelModifyResult?>? callback)
    {
        if (string.IsNullOrEmpty(userId))
            throw new ArgumentNullException(nameof(userId));

        if (string.IsNullOrEmpty(reasonId))
            throw new ArgumentNullException(nameof(reasonId));
        
        if (string.IsNullOrEmpty(reasonMessage))
            throw new ArgumentNullException(nameof(reasonMessage));

        void Response(ByteReader? reader)
        {
            if (callback == null)
                return;
            
            if (reader == null)
            {
                callback(null);
                
                ApiLog.Debug($"Failed to modify XP for ID &1{userId}&r");
            }
            else
            {
                var result = reader.ReadByte();
                
                ApiLog.Debug($"XP modification result for ID &1{userId}&r: &1{result}&r");              
                
                callback((LevelModifyResult)result);
            }
        }
        
        SendRemoteCallback(cmd_CmdModifyXp, writer =>
        { 
            writer.WriteString(userId);
            
            writer.WriteString(reasonId);
            writer.WriteString(reasonMessage);
            
            writer.WriteInt32(xp);
        }, Response);
    }

    /// <summary>
    /// Sends a command to reset the experience points (XP) of a specified user.
    /// </summary>
    /// <param name="userId">The unique identifier of the user whose XP is to be reset.</param>
    /// <param name="reasonId">An identifier representing the reason for the reset.</param>
    /// <param name="reasonMessage">A detailed message or description of the reason for the reset.</param>
    /// <param name="callback">An action to be invoked with a boolean result indicating whether the reset was successful.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="userId"/> is null or empty, or if <paramref name="callback"/> is null.
    /// </exception>
    public void CallCmdResetXp(string userId, string reasonId, string reasonMessage, Action<bool>? callback)
    {
        if (string.IsNullOrEmpty(userId))
            throw new ArgumentNullException(nameof(userId));

        if (callback == null)
            throw new ArgumentNullException(nameof(callback));
        
        ApiLog.Debug($"Sending XP reset for ID &1{userId}&r");

        SendRemoteCallback(cmd_CmdResetXp, writer =>
        {
            writer.WriteString(userId);
            
            writer.WriteString(reasonId);
            writer.WriteString(reasonMessage);           
        }, reader =>
        {
            var result = reader?.ReadBool() ?? false;
            
            callback?.Invoke(result);

            if (!result)
                ApiLog.Debug($"Failed to reset XP for ID &1{userId}&r");
            else
                ApiLog.Debug($"XP reset for ID &1{userId}&r");           
        });
    }

    /// <summary>
    /// Retrieves the level and experience logs for a specific user by their ID.
    /// </summary>
    /// <param name="userId">
    /// The ID of the user whose level and experience logs are to be retrieved.
    /// </param>
    /// <param name="callback">
    /// A callback function that is invoked with the retrieval result. The callback parameters include:
    /// - A boolean indicating success or failure of the operation.
    /// - The current level of the user.
    /// - The current experience of the user.
    /// - An array of <see cref="LevelLog"/> entries containing the retrieved logs.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the <paramref name="userId"/> or <paramref name="callback"/> is null or empty.
    /// </exception>
    public void CallCmdGetLogs(string userId, Action<bool, int, int, LevelLog[]> callback)
    {
        if (string.IsNullOrEmpty(userId))
            throw new ArgumentNullException(nameof(userId));
        
        if (callback == null)
            throw new ArgumentNullException(nameof(callback));

        void Response(ByteReader? reader)
        {
            if (reader != null)
            {
                var result = reader.ReadBool();

                if (!result)
                {
                    callback(false, 0, 0, []);
                }
                else
                {
                    var level = reader.ReadInt32();
                    var xp = reader.ReadInt32();
                    var count = reader.ReadInt32();
                    
                    var logs = new LevelLog[count];

                    for (var x = 0; x < count; x++)
                    {
                        var log = new LevelLog
                        {
                            Time = reader.ReadDate(),

                            ReasonId = reader.ReadString(),
                            ReasonMessage = reader.ReadString(),

                            Change = reader.ReadInt32()
                        };

                        logs[x] = log;
                    }
                    
                    callback(true, level, xp, logs);
                }
            }
            else
            {
                callback(false, 0, 0, []);
            }
        }
        
        SendRemoteCallback(cmd_CmdGetLogs, writer =>
        {
            writer.WriteString(userId);
        }, Response);
    }

    /// <summary>
    /// Notifies the client about changes in the level or experience of a player.
    /// </summary>
    /// <param name="reader">
    /// A <see cref="ByteReader"/> instance that contains serialized data about the user ID,
    /// the reason for the change, and the updated level and experience values.
    /// </param>
    [ClientRpc]
    public void RpcNotifyChange(ByteReader reader)
    {
        var userId = reader.ReadString();

        var reasonId = reader.ReadString();
        var reasonMessage = reader.ReadString();       
        
        var newLevel = reader.ReadInt32();
        var newXp = reader.ReadInt32();       
        
        if (!ExPlayer.TryGet(userId, out var player))
            return;

        try
        {
            if (Levels.TryGetValue(player, out var levels))
            {
                ApiLog.Debug($"Received updated level &1{newLevel}&r and experience &1{newXp}&r for player {player.ToLogString()}");
                
                Levels[player] = (newLevel, newXp);
                
                if (newLevel != levels.Level)
                {
                    LevelChanged?.Invoke(new(player, levels.Level, levels.Experience, newLevel, newXp, reasonId, reasonMessage));
                }

                if (newXp != levels.Experience)
                {
                    ExperienceChanged?.Invoke(new(player, levels.Level, levels.Experience, newLevel, newXp, reasonId, reasonMessage));
                }
            }
            else
            {
                ApiLog.Debug($"Received new level &1{newLevel}&r and experience &1{newXp}&r for player {player.ToLogString()}");
                
                Levels.Add(player, (newLevel, newXp));
                LevelReceived?.Invoke(player, (newLevel, newXp));
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Error while handling level change for {player.ToLogString()}:\n{ex}");
        }
    }

    private static void OnLeft(ExPlayer player)
    {
        Levels.Remove(player);
    }

    private static void OnWaiting()
    {
        Levels.Clear();
    }
    
    private static void OnVerified(ExPlayer player)
    {
        Singleton?.CallCmdGetLevel(player.UserId, levels =>
        {
            if (levels != null)
            {
                Levels.Add(player, levels.Value);
                LevelReceived?.Invoke(player, levels.Value);
                
                ApiLog.Debug($"Received level &1{levels.Value.Level}&r and experience &1{levels.Value.Experience}&r for player {player.ToLogString()}");
            }
            else
            {
                ApiLog.Debug($"Received no level and experience for player {player.ToLogString()}");
            }
        });
    }
}