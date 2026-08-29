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
    /// Notifies the client about a change in a user's level and experience points, along with the associated reason.
    /// </summary>
    /// <param name="userId">
    /// The unique identifier of the user whose level and experience points were changed.
    /// </param>
    /// <param name="newLevel">
    /// The updated level of the user.
    /// </param>
    /// <param name="newExperience">
    /// The updated experience points of the user.
    /// </param>
    /// <param name="reasonId">
    /// The identifier of the reason for the change.
    /// </param>
    /// <param name="reasonMessage">
    /// A detailed message explaining the reason for the change.
    /// </param>
    public void CallRpcNotifyChange(string userId, int newLevel, int newExperience, string reasonId, string reasonMessage)
    {
        SendRemoteCallback(rpc_RpcNotifyChange, writer =>
        {
            writer.WriteString(userId);

            writer.WriteString(reasonId);
            writer.WriteString(reasonMessage);           
            
            writer.WriteInt32(newLevel);
            writer.WriteInt32(newExperience);           
        });
    }

    /// <summary>
    /// Retrieves the level logs for a specific user and sends the data back to the caller.
    /// </summary>
    /// <param name="reader">
    /// The binary reader object used to read the request data, including the user's unique identifier.
    /// </param>
    /// <param name="writer">
    /// The binary writer object used to write the response data, including the level information and logs.
    /// </param>
    [ServerCmd(true)]
    public void CmdGetLogs(ByteReader reader, ByteWriter writer)
    {
        var userId = reader.ReadString();

        if (!LevelManager.TryGetLevels(userId, false, out var levels))
        {
            writer.WriteBool(false);
        }
        else
        {
            writer.WriteBool(true);
            
            writer.WriteInt32(levels.Level);
            writer.WriteInt32(levels.Experience);
            
            writer.WriteInt32(levels.Logs.Count);

            foreach (var log in levels.Logs)
            {
                writer.WriteDate(log.Time);
                
                writer.WriteString(log.ReasonId);
                writer.WriteString(log.ReasonMessage);
                
                writer.WriteInt32(log.Change);
                
                writer.WriteInt32(log.LevelAfter);
                writer.WriteInt32(log.LevelBefore);              
            }
        }
    }

    /// <summary>
    /// Resets the experience points and level of a specified user to default values.
    /// </summary>
    /// <param name="reader">
    /// A <see cref="ByteReader"/> instance used to read the unique identifier of the user.
    /// </param>
    /// <param name="writer">
    /// A <see cref="ByteWriter"/> instance used to write the result of the operation.
    /// </param>
    [ServerCmd(true)]
    public void CmdResetXp(ByteReader reader, ByteWriter writer)
    {
        var userId = reader.ReadString();
        
        var reasonId = reader.ReadString();
        var reasonMessage = reader.ReadString();
        
        var result = LevelManager.ResetXp(userId, reasonId, reasonMessage);       
        
        writer.WriteBool(result);
    }

    /// <summary>
    /// Processes a server command to modify the experience points (XP) of a user and responds with
    /// the result of the modification operation.
    /// </summary>
    /// <param name="reader">
    /// A <see cref="ByteReader"/> instance used to read the input data for the command, including
    /// the user's unique identifier and the XP value to be modified.
    /// </param>
    /// <param name="writer">
    /// A <see cref="ByteWriter"/> instance used to write the command response,
    /// which includes the result of the XP modification operation.
    /// </param>
    [ServerCmd(true)]
    public void CmdModifyXp(ByteReader reader, ByteWriter writer)
    {
        var userId = reader.ReadString();
        
        var reasonId = reader.ReadString();
        var reasonMessage = reader.ReadString();       
        
        var xp = reader.ReadInt32();
        var result = LevelManager.ModifyXp(userId, xp, reasonId, reasonMessage);
        
        writer.WriteByte((byte)result);
    }
    
    /// <summary>
    /// Handles the server command to retrieve the level and experience information
    /// for a specified user, based on their unique user identifier.
    /// </summary>
    /// <param name="reader">
    /// The <see cref="ByteReader"/> used to read the user identifier from the incoming request.
    /// </param>
    /// <param name="writer">
    /// The <see cref="ByteWriter"/> used to write the response data including
    /// the success status, level, and experience back to the client.
    /// </param>
    [ServerCmd(true)]
    public void CmdGetLevel(ByteReader reader, ByteWriter writer)
    {
        var userId = reader.ReadString();

        if (LevelManager.TryGetLevels(userId, true, out var levels))
        {
            writer.WriteBool(true);
            
            writer.WriteInt32(levels.Level);
            writer.WriteInt32(levels.Experience);
        }
        else
        {
            writer.WriteBool(false);           
        }
    }
}