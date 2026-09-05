using NiveraAPI.IO.Network.Entities.Attributes;
using NiveraAPI.IO.Serialization;

using PRTS.Levels;

namespace PRTS.ScpSl.Modules.Levels;

/// <summary>
/// Represents a module for managing levels and experience points in the game.
/// </summary>
[ClientType("PRTS.Client.Levels.LevelModule")]
public class LevelModule : ScpSlModule
{
    [IndexField] private static ushort rpc_RpcNotifyChange = 0;

    /// <summary>
    /// Sends a remote procedure call (RPC) to notify clients about a change in a user's level and experience points.
    /// </summary>
    /// <param name="userId">The unique identifier of the user whose level and experience points were changed.</param>
    /// <param name="newLevel">The updated level of the user.</param>
    /// <param name="newExperience">The updated experience points of the user.</param>
    public void CallRpcNotifyChange(string userId, int newLevel, int newExperience)
    {
        SendRemoteCallback(rpc_RpcNotifyChange, writer =>
        {
            writer.WriteString(userId);         
            
            writer.WriteInt32(newLevel);
            writer.WriteInt32(newExperience);           
        });
    }

    /// <summary>
    /// Processes a server command to reset the experience points (XP) of a user and responds with
    /// the result of the operation.
    /// </summary>
    /// <param name="reader">A <see cref="ByteReader"/> instance used to read the input data for the command, including the user's unique identifier.</param>
    /// <param name="writer">A <see cref="ByteWriter"/> instance used to write the command response, which includes the result of the XP reset operation.</param>
    [ServerCmd(true)]
    public void CmdResetXp(ByteReader reader, ByteWriter writer)
    {
        var userId = reader.ReadString();   
        var result = LevelManager.ResetXp(userId);       
        
        writer.WriteBool(result);
    }

    /// <summary>
    /// Processes a server command to modify the experience points (XP) of a user and responds with the result of the operation.
    /// </summary>
    /// <param name="reader">A <see cref="ByteReader"/> instance used to read the input data for the command, including the user's unique identifier and the XP value to be modified.</param>
    /// <param name="writer">A <see cref="ByteWriter"/> instance used to write the command response, which includes the result of the XP modification operation.</param>
    [ServerCmd(true)]
    public void CmdModifyXp(ByteReader reader, ByteWriter writer)
    {
        var userId = reader.ReadString();   
        var xp = reader.ReadInt32();

        var result = LevelManager.ModifyXpSteam(userId, xp);
        
        writer.WriteByte((byte)result);
    }

    /// <summary>
    /// Handles the server command to retrieve the complete list of levels and their associated experience points.
    /// </summary>
    /// <param name="_">A <see cref="ByteReader"/> instance (not used).</param>
    /// <param name="writer">A <see cref="ByteWriter"/> instance used to write the response data.</param>
    [ServerCmd(true)]
    public void CmdGetLevels(ByteReader _, ByteWriter writer)
    {
        var array = LevelManager.Levels;

        writer.WriteInt32(array.Length);

        for (var x = 0; x < array.Length; x++)
        {
            var level = array[x];

            writer.WriteInt32(level.Level);
            writer.WriteInt32(level.Experience);
            writer.WriteBool(level.IsMaxLevel);
            writer.WriteString(level.MilestoneName);
        }
    }

    /// <summary>
    /// Handles the server command to retrieve the level and experience information for a specific user.
    /// </summary>
    /// <param name="reader">A <see cref="ByteReader"/> instance used to read the input data for the command, including the user's unique identifier.</param>
    /// <param name="writer">A <see cref="ByteWriter"/> instance used to write the command response, which includes the result of the operation.</param>
    [ServerCmd(true)]
    public void CmdGetPlayerLevel(ByteReader reader, ByteWriter writer)
    {
        var userId = reader.ReadString();

        if (LevelManager.TryGetLevels(userId, true, out var levels))
        {
            var level = LevelManager.GetLevelForXp(levels.Experience);

            writer.WriteBool(true);

            writer.WriteInt32(level.Level);
            writer.WriteInt32(levels.Experience);
        }
        else
        {
            writer.WriteBool(false);           
        }
    }

    /// <summary>
    /// Handles the server command to retrieve the level and experience information for multiple users,
    /// </summary>
    /// <param name="reader">The <see cref="ByteReader"/> used to read the user identifiers from the incoming request.</param>
    /// <param name="writer">The <see cref="ByteWriter"/> used to write the response data including the success status, level, and experience back to the client.</param>
    [ServerCmd(true)]
    public void CmdGetPlayerLevels(ByteReader reader, ByteWriter writer)
    {
        var ids = reader.ReadArray<string>();

        writer.WriteInt32(ids.Length);
        
        for (var x = 0; x < ids.Length; x++)
        {
            var userId = ids[x];

            writer.WriteString(userId);

            if (LevelManager.TryGetLevels(userId, true, out var levels))
            {
                var level = LevelManager.GetLevelForXp(levels.Experience);

                writer.WriteBool(true);

                writer.WriteInt32(level.Level);
                writer.WriteInt32(levels.Experience);
            }
            else
            {
                writer.WriteBool(false);
            }
        }
    }
}