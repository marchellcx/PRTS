using HarmonyLib;

using LabApi.Features.Wrappers;
using LabApi.Features.Permissions.Providers;

using NiveraAPI.Pooling;
using NiveraAPI.Utilities;
using NiveraAPI.Extensions;

namespace PRTS.Client.Profiles.Patches;

/// <summary>
/// Patches the LabApi DefaultPermissionsProvider to include additional permissions based on user roles.
/// </summary>
public static class LabApiPermissionsPatch
{
    private static List<PermissionGroup> GetPooledPermissionGroups(string userId, string? permsGroup, DefaultPermissionsProvider permissionsProvider)
    {
        var permissionGroups = Pools.PoolList<PermissionGroup>();

        if (!string.IsNullOrEmpty(permsGroup)
            && permissionsProvider._permissionsDictionary.TryGetValue(permsGroup!, out var permissionGroup))
            permissionGroups.Add(permissionGroup);

        if (ProfileModule.Roles.TryGetValue(userId, out var roles) && roles.Length > 0)
        {
            foreach (var role in roles)
            {
                if (permissionsProvider._permissionsDictionary.TryGetValue(role, out permissionGroup))
                {
                    permissionGroups.AddUnique(permissionGroup);
                }
            }
        }

        return permissionGroups;
    }

    [HarmonyPatch(typeof(DefaultPermissionsProvider), nameof(DefaultPermissionsProvider.GetPermissions), typeof(Player))]
    private static bool GetPermissionsPrefix(DefaultPermissionsProvider __instance, Player player, ref string[] __result) 
    {
        if (!ProfileModule.Roles.TryGetValue(player.UserId, out var roles) || roles.Length < 1)
            return true;

        var perms = Pools.PoolList<string>();

        if (!string.IsNullOrEmpty(player.PermissionsGroupName)
            && __instance._permissionsDictionary.TryGetValue(player.PermissionsGroupName!, out var permissionGroup))
            perms.AddRange(__instance.GetPermissions(permissionGroup));

        foreach (var role in roles)
        {
            if (!__instance._permissionsDictionary.TryGetValue(role, out permissionGroup))
                continue;

            perms.AddRange(__instance.GetPermissions(permissionGroup));
        }

        __result = ListObjectPool<string>.ReturnToArray(perms);
        return false;
    }

    [HarmonyPatch(typeof(DefaultPermissionsProvider), nameof(DefaultPermissionsProvider.HasAnyPermission), typeof(Player), typeof(string[]))]
    private static bool HasAnyPermissionPrefix(DefaultPermissionsProvider __instance, Player player, string[] permissions, ref bool __result)
    {
        var groups = GetPooledPermissionGroups(player.UserId, player.PermissionsGroupName, __instance);

        __result = groups.Any(group => permissions.Any(permission => __instance.HasPermission(group, permission)));

        groups.ReturnToPool();
        return false;
    }

    [HarmonyPatch(typeof(DefaultPermissionsProvider), nameof(DefaultPermissionsProvider.HasPermission), typeof(Player), typeof(string))]
    private static bool HasPermissionPrefix(DefaultPermissionsProvider __instance, Player player, string specificPermission, ref bool __result)
    {
        var groups = GetPooledPermissionGroups(player.UserId, player.PermissionsGroupName, __instance);

        __result = groups.Any(group => __instance.HasPermission(group, specificPermission));

        groups.ReturnToPool();
        return false;
    }

    [HarmonyPatch(typeof(DefaultPermissionsProvider), nameof(DefaultPermissionsProvider.HasPermissions), typeof(Player), typeof(string[]))]
    private static bool HasPermissionsPrefix(DefaultPermissionsProvider __instance, Player player, string[] permissions, ref bool __result)
    {
        var groups = GetPooledPermissionGroups(player.UserId, player.PermissionsGroupName, __instance);

        __result = groups.Any(group => permissions.All(permission => __instance.HasPermission(group, permission)));

        groups.ReturnToPool();
        return false;
    }
}