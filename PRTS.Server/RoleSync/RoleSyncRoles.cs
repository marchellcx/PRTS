using NiveraAPI;
using NiveraAPI.Extensions;

using NiveraAPI.IO.Storage;
using NiveraAPI.Logs;

using PRTS.Database.Attributes;
using PRTS.Database.Serializers;
using PRTS.Discord;

namespace PRTS.RoleSync;

/// <summary>
/// Provides a set of static methods to manage role synchronization for a specific Discord server.
/// This includes adding, removing, clearing, and retrieving roles associated with Discord users.
/// </summary>
public static class RoleSyncRoles
{
    private static volatile LogSink log;
    
    /// <summary>
    /// The directory containing role storage information.
    /// </summary>
    [DbStorage("role-sync", typeof(ByteReaderWriterSerializer<string[]>))]
    public static volatile StorageDirectory Roles;

    /// <summary>
    /// Attempts to clear all roles from the local role storage.
    /// </summary>
    /// <returns>A boolean value indicating whether the roles were successfully cleared. Returns false if the role storage is uninitialized.</returns>
    public static bool TryClearRoles()
    {
        if (Roles == null)
            return false;
        
        Roles.ClearValues(true);
        return true;
    }

    /// <summary>
    /// Attempts to remove a specified role from a Discord user's local role configuration, if it exists.
    /// </summary>
    /// <param name="discordId">The unique identifier of the Discord user whose role is being removed.</param>
    /// <param name="roleName">The name of the role to be removed from the user.</param>
    /// <returns>A boolean value indicating whether the role was successfully removed. Returns false if the role does not exist, the role storage is uninitialized, or if an error occurred.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="roleName"/> is null or empty.</exception>
    public static bool TryRemoveRole(ulong discordId, string roleName)
    {
        if (string.IsNullOrEmpty(roleName))
            throw new ArgumentNullException(nameof(roleName));

        log.Info($"Removing role &1{roleName}&r for ID &1{discordId}&r");
        
        if (Roles == null)
        {
            log.Warn("Role storage is not initialized!");
            return false;
        }
        
        if (Roles.TryGetStorageValue<string[]>(discordId.ToString(), out var localRoles))
        {
            if (localRoles.Value == null)
                return false;
            
            localRoles.Value = localRoles.Value.Where(r => r != roleName).ToArray();
            
            if (localRoles.Value.Length == 0)
                Roles.RemoveStorageValue(discordId.ToString());
            
            log.Info($"Removed role &1{roleName}&r for ID &1{discordId}&r");
            return true;
        }
        
        log.Warn($"No role found for ID &1{discordId}&r");
        return false; 
    }

    /// <summary>
    /// Attempts to add a specified role to a Discord user if the role does not already exist in their local role configuration.
    /// </summary>
    /// <param name="discordId">The unique identifier of the Discord user to whom the role is being added.</param>
    /// <param name="roleName">The name of the role to be added to the user.</param>
    /// <returns>A boolean value indicating whether the role was successfully added. Returns false if the role already exists or if an error occurred.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="roleName"/> is null or empty.</exception>
    public static bool TryAddRole(ulong discordId, string roleName)
    {
        if (string.IsNullOrEmpty(roleName))
            throw new ArgumentNullException(nameof(roleName));

        log.Info($"Adding role &1{roleName}&r for ID &1{discordId}&r");

        if (Roles == null)
        {
            log.Warn("Role storage is not initialized!");
            return false;
        }

        if (Roles.TryGetStorageValue<string[]>(discordId.ToString(), out var localRoles))
        {
            if (localRoles.Value == null)
                localRoles.Value = Array.Empty<string>();

            if (localRoles.Value.Contains(roleName))
            {
                log.Warn($"Role &1{roleName}&r already exists for ID &1{discordId}&r");
                return false;
            }

            localRoles.Value = localRoles.Value.Append(roleName).ToArray();
            return true;
        }
        
        localRoles = Roles.AddStorageValue(discordId.ToString(), () => Array.Empty<string>());
        localRoles.Value = localRoles.Value.Append(roleName).ToArray();
        
        log.Info($"Added role &1{roleName}&r for ID &1{discordId}&r");
        return true;  
    }

    /// <summary>
    /// Retrieves a list of roles associated with the specified role IDs and user ID.
    /// </summary>
    /// <param name="roleIds">A list of role IDs to retrieve roles for. Cannot be null.</param>
    /// <param name="userId">The user ID whose roles should also be included in the result.</param>
    /// <returns>An array of role names associated with the specified role IDs and user ID.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="roleIds"/> parameter is null.</exception>
    public static string[] GetRoles(List<ulong> roleIds, ulong userId)
    {
        if (roleIds == null)
            throw new ArgumentNullException(nameof(roleIds));

        var userRoles = new List<string>();

        foreach (var roleId in roleIds)
        {
            if (Roles.TryGetStorageValue<string[]>(roleId.ToString(), out var syncedRoles)
                && syncedRoles.Value != null)
            {
                userRoles.AddRangeWhere(syncedRoles.Value, str => !userRoles.Contains(str));
            }
        }

        if (Roles.TryGetStorageValue<string[]>(userId.ToString(), out var localRoles))
        {
            userRoles.AddRangeWhere(localRoles.Value, str => !userRoles.Contains(str));
        }
        
        return userRoles.ToArray();  
    }
    
    /// <summary>
    /// Retrieves a list of roles associated with a specific Discord user by synchronizing
    /// their roles with the locally stored role configurations.
    /// </summary>
    /// <param name="discordId">The unique identifier of the Discord user whose roles are being retrieved.</param>
    /// <returns>An array of role names associated with the user, or an empty array if no roles are found.</returns>
    public static async Task<string[]> GetRoles(ulong discordId)
    {
        if (Roles == null)
            return Array.Empty<string>();
        
        var discordRoles = await DiscordBot.TryGetRoleIds(0, discordId);

        if (discordRoles == null)
            return Array.Empty<string>();

        var userRoles = new List<string>();

        foreach (var roleId in discordRoles)
        {
            if (Roles.TryGetStorageValue<string[]>(roleId.ToString(), out var syncedRoles)
                && syncedRoles.Value != null)
            {
                userRoles.AddRangeWhere(syncedRoles.Value, str => !userRoles.Contains(str));
            }
        }

        if (Roles.TryGetStorageValue<string[]>(discordId.ToString(), out var localRoles))
        {
            userRoles.AddRangeWhere(localRoles.Value, str => !userRoles.Contains(str));
        }
        
        return userRoles.ToArray();   
    }
    
    private static void StorageInit_Roles()
    {
        log = LogManager.GetSource("RoleSync", "Module");
        log.Info($"Storage loaded: {Roles.ValueCount} role(s)!");

        if (LibraryLoader.HasArgument("LogSyncedRoles"))
        {
            foreach (var kvp in Roles.Values)
            {
                if (kvp.Value is not StorageValue<string[]> castValue)
                {
                    log.Error($"Invalid connection value: &1{kvp.Value?.GetType().Name ?? "null"}&r");
                    continue;
                }
                
                log.Info($"RoleSync for ID &1{kvp.Key}&r: &1{string.Join(", ", castValue.Value)}&r");
            }
        }
    }
}