using System.Collections.Concurrent;
using System.Reflection;

using LabApi.Loader;

using NiveraAPI.Logs;
using NiveraAPI.Extensions;

using NiveraAPI.IO.Serialization;
using NiveraAPI.IO.Serialization.Interfaces;

namespace PRTS.Addons;

/// <summary>
/// Manages the loading and activation of addons within the PRTS system.
/// </summary>
public static class PrtsAddonManager
{
    private static volatile LogSink log = LogManager.GetSource("Core", "PrtsAddonManager");
    private static volatile MethodInfo registerDefaultSerializer = typeof(ObjectSerializer).FindMethod("RegisterDefaultSerializer");
    
    /// <summary>
    /// A collection of loaded addons, organized by their assembly.
    /// </summary>
    public static volatile ConcurrentDictionary<Assembly, PrtsAddon[]> Addons = new();
    
    /// <summary>
    /// Registers all valid serializable message types from the specified assembly.
    /// Scans the provided assembly for types that implement the <see cref="ISerializableObject"/> interface
    /// and are value types, then invokes the default serializer registration method for each valid type.
    /// </summary>
    /// <param name="assembly">The assembly containing the message types to register.</param>
    /// <exception cref="ArgumentNullException">Thrown if the provided <paramref name="assembly"/> is null.</exception>
    public static void RegisterMessages(Assembly assembly)
    {
        if (assembly == null)
            throw new ArgumentNullException(nameof(assembly));

        foreach (var type in assembly.GetTypes())
        {
            try
            {
                if (!typeof(ISerializableObject).IsAssignableFrom(type))
                    continue;

                if (!type.IsValueType)
                {
                    log.Warn($"Message &1{type}&r is not a value type!");
                    continue;
                }
                
                registerDefaultSerializer
                    .MakeGenericMethod(type)
                    .Invoke(null, null);
                
                log.Info($"Registered message &1{type}&r!");
            }
            catch (Exception ex)
            {
                log.Error($"Could not register message &1{type}&r:\n{ex}");
            }
        }
    }

    /// <summary>
    /// Enables all loaded addons by invoking their respective activation logic.
    /// </summary>
    public static void EnableAddons()
    {
        foreach (var kvp in Addons)
        {
            foreach (var addon in kvp.Value)
            {
                try
                {
                    addon.OnEnabled();
                    
                    log.Info($"Enabled addon &1{addon.Name}&r");
                }
                catch (Exception ex)
                {
                    log.Error($"Error while enabling addon &1{addon.Name}&r:\n{ex}");
                }
            }
        }
    }

    /// <summary>
    /// Loads and initializes all addons found in the designated addons directory.
    /// </summary>
    public static void LoadAddons()
    {
        var assemblies = PluginLoader.Plugins
            .Select(x => x.Value)
            .Where(x => x != null)
            .Distinct();
        
        foreach (var assembly in assemblies)
        {
            try
            {
                var types = assembly.GetTypes();

                foreach (var type in types)
                {
                    try
                    {
                        if (type.IsSubclassOf(typeof(PrtsAddon)))
                        {
                            if (Activator.CreateInstance(type) is PrtsAddon addon)
                            {
                                if (Addons.TryRemove(assembly, out var addonList))
                                {
                                    Addons.TryAdd(assembly, addonList.Append(addon).ToArray());
                                }
                                else
                                {
                                    Addons.TryAdd(assembly, [addon]);
                                }

                                addon.OnLoaded();

                                log.Info($"Loaded addon &1{addon.Name}&r");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Error($"Error while loading addon &1{type}&r:\n{ex}");
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error($"Error while loading addon file &1{assembly}&r:\n{ex}");
            }
        }
    }
}