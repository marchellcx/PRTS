using HarmonyLib;

using LabApi.Features.Wrappers;
using LabApi.Features.Permissions.Providers;

using NiveraAPI.Pooling;
using NiveraAPI.Utilities;

namespace PRTS.Client.Profiles.Patches;

/// <summary>
/// Patches the LabApi DefaultPermissionsProvider to include additional permissions based on user roles.
/// </summary>
public static class LabApiPermissionsPatch
{
    [HarmonyPatch(typeof(DefaultPermissionsProvider), nameof(DefaultPermissionsProvider.GetPermissions), typeof(Player))]
    private static bool Prefix(DefaultPermissionsProvider __instance, Player player, ref string[] __result) 
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
}