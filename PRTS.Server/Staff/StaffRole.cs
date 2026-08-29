using NiveraAPI.Pooling;
using NiveraAPI.Utilities;
using NiveraAPI.Extensions;

using NiveraAPI.IO.Configs;
using NiveraAPI.IO.Storage;
using NiveraAPI.IO.Serialization;

using PRTS.Database.Attributes;
using PRTS.Database.Serializers;

using PRTS.Discord;

namespace PRTS.Staff;

/// <summary>
/// Represents a staff member's role.
/// </summary>
public class StaffRole
{
    static StaffRole()
    {
        ByteSerializer<StaffRole>.Serialize = (writer, role) =>
        {
            writer.WriteString(role.Id);
            writer.WriteBool(role.IsAdministrator);
            writer.WriteArray(role.RoleIds);
            writer.WriteArray(role.Permissions);
        };

        ByteSerializer<StaffRole>.Deserialize = reader =>
        {
            var role = new StaffRole
            {
                Id = reader.ReadString(),
                IsAdministrator = reader.ReadBool(),

                RoleIds = reader.ReadArray<ulong>(),
                Permissions = reader.ReadArray<string>()
            };

            return role;
        };
    }
    
    /// <summary>
    /// The Discord IDs of the administrators.
    /// </summary>
    [Config("prts", "admin-ids", "The Discord IDs of the administrators.")]
    public static ulong[] AdminIds { get; set; } = [];
    
    /// <summary>
    /// The directory containing the staff roles.
    /// </summary>
    [DbStorage("staff-roles", typeof(ByteReaderWriterSerializer<StaffRole>))]
    public static volatile StorageDirectory Roles;

    /// <summary>
    /// Determines whether a user has a specific permission in a guild. If the user is an administrator,
    /// the method returns true regardless of the specified permission.
    /// </summary>
    /// <param name="guildId">The unique identifier of the guild. If 0, the permission is checked across all guilds.</param>
    /// <param name="discordUserId">The unique identifier of the Discord user whose permission is being checked.</param>
    /// <param name="perm">The specific permission to check.</param>
    /// <returns>A boolean value indicating whether the user has the specified permission.</returns>
    public static bool HasPermissionAll(ulong guildId, ulong discordUserId, string perm)
    {
        if (string.IsNullOrEmpty(perm))
            return false;
        
        if (AdminIds.Contains(discordUserId))
            return true;

        var perms = GetAllPermissions(guildId, discordUserId, out var isAdmin);
        
        if (isAdmin)
            return true;
        
        return perms.Contains(perm);
    }
    
    /// <summary>
    /// Retrieves all permissions associated with a specified user in a specific guild or all guilds.
    /// </summary>
    /// <param name="guildId">The unique identifier of the guild. If 0, permissions are retrieved for the user across all guilds.</param>
    /// <param name="discordUserId">The unique identifier of the Discord user whose permissions are to be retrieved.</param>
    /// <param name="isAdmin">A boolean output parameter that indicates whether the user has administrative privileges.</param>
    /// <returns>An array of strings representing the unique permissions associated with the specified user.</returns>
    public static string[] GetAllPermissions(ulong guildId, ulong discordUserId, out bool isAdmin)
    {
        isAdmin = AdminIds.Contains(discordUserId);

        var list = Pools.PoolList<string>();

        foreach (var kvp in DiscordBot.Bots)
        {
            if (kvp.Value.Client == null)
                continue;
            
            foreach (var guild in kvp.Value.Client.Guilds)
            {
                if (guildId != 0 && guildId != guild.Id)
                    continue;
                
                var user = guild.GetUser(discordUserId);
                
                if (user == null)
                    continue;

                foreach (var role in user.Roles)
                {
                    if (!TryGetBoundRole(role.Id, out var roleData))
                        continue;
                    
                    if (roleData.Value.IsAdministrator)
                        isAdmin = true;

                    for (var x = 0; x < roleData.Value.Permissions.Length; x++)
                        list.AddUnique(roleData.Value.Permissions[x]);
                }
            }
        }
        
        return ListObjectPool<string>.ReturnToArray(list);
    }
    
    /// <summary>
    /// Retrieves all staff roles associated with a specified user in a specific or all guilds.
    /// </summary>
    /// <param name="guildId">The unique identifier of the guild. If 0, the method retrieves roles for the user across all guilds.</param>
    /// <param name="discordUserId">The unique identifier of the Discord user whose roles are to be retrieved.</param>
    /// <returns>An array of <see cref="StaffRole"/> objects representing the roles associated with the specified user.</returns>
    public static StaffRole[] GetAllRoles(ulong guildId, ulong discordUserId)
    {
        var list = Pools.PoolList<StaffRole>();

        foreach (var kvp in DiscordBot.Bots)
        {
            if (kvp.Value.Client == null)
                continue;
            
            foreach (var guild in kvp.Value.Client.Guilds)
            {
                if (guildId != 0 && guildId != guild.Id)
                    continue;
                
                var user = guild.GetUser(discordUserId);
                
                if (user == null)
                    continue;

                foreach (var role in user.Roles)
                {
                    if (!TryGetBoundRole(role.Id, out var roleData))
                        continue;
                    
                    list.Add(roleData.Value);
                }
            }
        }
        
        return ListObjectPool<StaffRole>.ReturnToArray(list);
    }

    /// <summary>
    /// Attempts to retrieve a staff role by its identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the staff role.</param>
    /// <param name="role">When this method returns, contains the staff role associated with the specified identifier,
    /// if the identifier is found; otherwise, null. This parameter is passed uninitialized.</param>
    /// <returns>True if the staff role is found; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the provided identifier is null or empty.</exception>
    public static bool TryGetRole(string id, out StaffRole role)
    {
        if (string.IsNullOrEmpty(id))
            throw new ArgumentNullException(nameof(id));

        role = null!;

        if (Roles == null)
            return false;
        
        return Roles.TryGetValue(id, out role);
    }

    /// <summary>
    /// Attempts to retrieve a staff role associated with a specific Discord ID.
    /// </summary>
    /// <param name="discordId">The Discord ID to search for in the staff roles.</param>
    /// <param name="role">When this method returns, contains the staff role associated with the specified Discord ID,
    /// if a match is found; otherwise, null. This parameter is passed uninitialized.</param>
    /// <returns>True if a staff role associated with the given Discord ID is found; otherwise, false.</returns>
    public static bool TryGetBoundRole(ulong discordId, out StorageValue<StaffRole> role)
    {
        role = null!;

        if (Roles == null)
            return false;

        foreach (var kvp in Roles.Values)
        {
            if (kvp.Value is not StorageValue<StaffRole> castValue)
                continue;
            
            if (castValue.Value.RoleIds.Contains(discordId))
            {
                role = castValue;
                return true;
            }
        }
        
        return false;
    }
    
    private volatile string id = string.Empty;
    private volatile string[] perms = [];
    
    private volatile ulong[] roles = [];

    private volatile bool isAdmin;

    /// <summary>
    /// The ID of the role.
    /// </summary>
    public string Id
    {
        get => id;
        set => id = value;
    }

    /// <summary>
    /// The permissions of the role.
    /// </summary>
    public string[] Permissions
    {
        get => perms;
        set => perms = value;
    }
    
    /// <summary>
    /// The Discord IDs of the roles that the staff member has.
    /// </summary>
    public ulong[] RoleIds
    {
        get => roles;
        set => roles = value;
    }

    /// <summary>
    /// Whether the staff member is an administrator.
    /// </summary>
    public bool IsAdministrator
    {
        get => isAdmin;
        set => isAdmin = value;
    }

    /// <summary>
    /// Determines whether the staff role has the specified permission.
    /// </summary>
    /// <param name="perm">The permission to check for.</param>
    /// <returns>True if the staff role has the specified permission or if the staff member is an administrator; otherwise, false.</returns>
    public bool HasPermission(string perm)
    {
        if (string.IsNullOrEmpty(perm))
            return false;

        if (IsAdministrator)
            return true;
        
        return Permissions.Contains(perm);
    }
}