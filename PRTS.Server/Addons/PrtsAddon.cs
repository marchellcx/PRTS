using System.Reflection;

using NiveraAPI.Logs;
using NiveraAPI.IO.Serialization;

using PRTS.ScpSl;

namespace PRTS.Addons;

/// <summary>
/// Represents a base class for defining addons within the PRTS system.
/// Provides functionality for managing the state and metadata of an addon.
/// </summary>
public class PrtsAddon
{
    internal volatile bool isLoaded;
    internal volatile bool isEnabled;
    
    private volatile Type type;
    private volatile LogSink log;
    private volatile Assembly assembly;

    public PrtsAddon()
    {
        type = GetType();
        
        assembly = type.Assembly;
        
        log = LogManager.GetSource("Addon", type.Name);
    }
    
    /// <summary>
    /// The addon's logger.
    /// </summary>
    public LogSink Log => log;
    
    /// <summary>
    /// The addon's type.
    /// </summary>
    public Type Type => type;
    
    /// <summary>
    /// The addon's assembly.
    /// </summary>
    public Assembly Assembly => assembly;
    
    /// <summary>
    /// The addon's name.
    /// </summary>
    public string Name => type.Name;
    
    /// <summary>
    /// Whether the addon is loaded.
    /// </summary>
    public bool IsLoaded => isLoaded;
    
    /// <summary>
    /// Whether the addon is enabled.
    /// </summary>
    public bool IsEnabled => isEnabled;

    public virtual void OnLoaded()
    {
        if (isLoaded)
            throw new InvalidOperationException("Addon is already loaded!");
        
        ObjectSerializer.RegisterSerializers(Assembly);
        
        Loader.RegisterConfigs(Assembly);
        Loader.RegisterMessages(Assembly);
        
        isLoaded = true;       
    }
    
    /// <summary>
    /// Enables the addon by setting its state to enabled.
    /// If the addon is already enabled, an exception is thrown.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the addon is already enabled.
    /// </exception>
    public virtual void OnEnabled()
    {
        if (isEnabled)
            throw new InvalidOperationException("Addon is already enabled!");
        
        ScpSlManager.RegisterModules(Assembly);

        var inits = Loader.LoadInits(Assembly);
        
        inits.ForEach(kvp =>
        {
            try
            {
                kvp.Key.Invoke(null, null);
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }           
        });
        
        log.Info("Enabled!");
        
        isEnabled = true;
    }
}