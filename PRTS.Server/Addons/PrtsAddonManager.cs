using System.Collections.Concurrent;
using System.Reflection;
using NiveraAPI.IO.Serialization;
using NiveraAPI.Logs;

namespace PRTS.Addons;

/// <summary>
/// Manages the loading and activation of addons within the PRTS system.
/// </summary>
public static class PrtsAddonManager
{
    private static volatile LogSink log = LogManager.GetSource("Core", "PrtsAddonManager");
    
    /// <summary>
    /// The path to the directory containing addons.
    /// </summary>
    public static volatile string AddonsPath = Path.Combine(Directory.GetCurrentDirectory(), "addons");
    
    /// <summary>
    /// The path to the directory containing dependency assemblies.
    /// </summary>
    public static volatile string DependenciesPath = Path.Combine(Directory.GetCurrentDirectory(), "dependencies");

    /// <summary>
    /// A collection of loaded dependency assemblies.
    /// </summary>
    public static volatile ConcurrentBag<Assembly> Dependencies = new();
    
    /// <summary>
    /// A collection of loaded addons, organized by their assembly.
    /// </summary>
    public static volatile ConcurrentDictionary<Assembly, PrtsAddon[]> Addons = new();

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
        if (!Directory.Exists(AddonsPath))
            Directory.CreateDirectory(AddonsPath);

        foreach (var dllFile in Directory.GetFiles(AddonsPath, "*.dll"))
        {
            try
            {
                var assemblyRaw = File.ReadAllBytes(dllFile);
                var assembly = Assembly.Load(assemblyRaw);

                if (assembly != null)
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
                else
                {
                    log.Error($"Failed to load addon &1{Path.GetFileName(dllFile)}&r");
                }
            }
            catch (Exception ex)
            {
                log.Error($"Error while loading addon file &1{Path.GetFileName(dllFile)}&r:\n{ex}");
            }
        }
    }

    /// <summary>
    /// Loads all dependency assemblies from the designated dependencies directory.
    /// This includes locating `.dll` files, reading their content, and attempting
    /// to load them as assemblies. Successfully loaded assemblies are added to the
    /// dependency collection, which can then be utilized by other components.
    /// </summary>
    public static void LoadDependencies()
    {
        if (!Directory.Exists(DependenciesPath))
            Directory.CreateDirectory(DependenciesPath);

        foreach (var dllFile in Directory.GetFiles(DependenciesPath, "*.dll"))
        {
            try
            {
                var assemblyRaw = File.ReadAllBytes(dllFile);
                var assembly = Assembly.Load(assemblyRaw);

                if (assembly != null)
                {
                    Dependencies.Add(assembly);
                    
                    Loader.RegisterConfigs(assembly);
                    Loader.RegisterMessages(assembly);
                    
                    ObjectSerializer.RegisterSerializers(assembly);
                    
                    log.Info($"Loaded dependency &1{Path.GetFileName(dllFile)}&r");
                }
                else
                {
                    log.Error($"Failed to load dependency &1{Path.GetFileName(dllFile)}&r");
                }
            }
            catch (Exception ex)
            {
                log.Error($"Error while loading dependency &1{Path.GetFileName(dllFile)}&r:\n{ex}");
            }
        }
    }
}