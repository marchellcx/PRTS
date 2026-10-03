using System.Reflection;

using NiveraAPI.Logs;
using NiveraAPI.Extensions;

using NiveraAPI.IO.Configs;

using NiveraAPI.IO.Serialization;
using NiveraAPI.IO.Serialization.Interfaces;

using PRTS.Core;
using PRTS.Core.Attributes;

using PRTS.ScpSl;
using PRTS.Addons;
using PRTS.Database;

namespace PRTS;

/// <summary>
/// Provides static methods and properties to manage the loading and initialization
/// of configurations, application settings, and the network manager for the application.
/// </summary>
public static class Loader
{
    private static volatile LogSink log = LogManager.GetSource("Core", "Loader");
    private static volatile MethodInfo registerDefaultSerializer = typeof(ObjectSerializer).FindMethod("RegisterDefaultSerializer");
    
    // Exit Handling
    internal static volatile int code = 0;
    
    internal static volatile bool lib = false;
    internal static volatile bool quit = false;
    
    internal static volatile string message = string.Empty;

    /// <summary>
    /// Represents a configuration handler responsible for managing the application's
    /// configuration settings.
    /// </summary>
    public static volatile ConfigHandler Config;

    /// <summary>
    /// Saves the current configuration settings to the configuration file.
    /// </summary>
    public static void SaveConfig()
    {
        try
        {
            Config?.Save();
        }
        catch (Exception ex)
        {
            log.Error(ex);
        }
    }

    /// <summary>
    /// Asynchronously initializes and starts the loader by configuring application settings,
    /// loading configurations, and starting the network manager.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static void Start()
    {
        Config = new();
        Config.FilePath = Path.Combine(Directory.GetCurrentDirectory(), "config.ini");
        
        var asm = typeof(Program).Assembly;
        var inits = LoadInits(asm);
        
        RegisterConfigs(asm);
        
        ScpSlManager.RegisterModules(asm);
        ObjectSerializer.RegisterSerializers(asm);
        
        PrtsAddonManager.LoadDependencies();
        PrtsAddonManager.LoadAddons();
        
        Config.Load();
        Config.Save();
        
        DbManager.Start();
        
        Network.Start();
        ScpSlManager.Start();
        
        inits.ForEach(m =>
        {
            try
            {
                m.Key.Invoke(null, null);
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        });
        
        PrtsAddonManager.EnableAddons();
    }

    /// <summary>
    /// Registers the configuration types defined in the specified assembly
    /// using the application's configuration handler.
    /// </summary>
    /// <param name="assembly">The assembly containing the types to be registered.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if the provided <paramref name="assembly"/> is null.
    /// </exception>
    public static void RegisterConfigs(Assembly assembly)
    {
        if (assembly == null)
            throw new ArgumentNullException(nameof(assembly));

        foreach (var type in assembly.GetTypes())
            Config.Register(type);
    }
    
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
    /// Loads initialization methods annotated with the <c>InitAttribute</c> from the specified assembly.
    /// Scans all types in the given assembly, identifies static methods marked with the <c>InitAttribute</c>,
    /// and collects them along with their defined execution order.
    /// </summary>
    /// <param name="assembly">The assembly to scan for initialization methods.</param>
    /// <returns>A list of key-value pairs where the key is the initialization method and the value is its execution order.</returns>
    public static List<KeyValuePair<MethodInfo, int>> LoadInits(Assembly assembly)
    {
        var inits = new List<KeyValuePair<MethodInfo, int>>();
        
        try
        {
            var types = assembly.GetTypes();

            try
            {
                foreach (var type in types)
                {
                    try
                    {
                        foreach (var method in type.GetAllMethods())
                        {
                            if (!method.HasAttribute<InitAttribute>(out var initAttribute))
                                continue;

                            if (!method.IsStatic || method.ReturnType != typeof(void)
                                                 || method.GetAllParameters().Length > 0)
                                continue;

                            inits.Add(new(method, initAttribute.Order));

                            log.Debug($"Loaded init method &1{method}&r with order &1{initAttribute.Order}&r.");
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Error(ex);
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }
        catch (Exception ex)
        {
            log.Error(ex);
        }

        inits.Sort((x, y) => x.Value.CompareTo(y.Value));
        return inits;
    }
}