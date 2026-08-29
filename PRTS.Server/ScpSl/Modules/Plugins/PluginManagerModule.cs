using NiveraAPI.IO.Network.Entities.Attributes;
using NiveraAPI.IO.Serialization;

namespace PRTS.ScpSl.Modules.Plugins;

/// <summary>
/// Represents a module responsible for managing plugins within the system.
/// This module handles plugin-related actions such as retrieving, downloading,
/// and removing plugins, and registering or unregistering associated commands.
/// </summary>
[ClientType("PRTS.Client.Plugins.PluginManagerModule")]
public class PluginManagerModule : ScpSlModule
{
    static PluginManagerModule()
    {
        ByteSerializer<PluginInfo>.Serialize = (writer, info) =>
        {
            writer.WriteString(info.Name);
            writer.WriteString(info.File);
            writer.WriteString(info.Author);
            writer.WriteString(info.Version);
            writer.WriteString(info.Description);
        };

        ByteSerializer<PluginInfo>.Deserialize = reader =>
        {
            var info = new PluginInfo();

            info.Name = reader.ReadString();
            info.File = reader.ReadString();
            info.Author = reader.ReadString();
            info.Version = reader.ReadString();
            info.Description = reader.ReadString();

            return info;
        };
    }
    
    [IndexField] private static ushort rpc_RpcGetPlugins = 0;
    [IndexField] private static ushort rpc_RpcRemovePlugin = 0;
    [IndexField] private static ushort rpc_RpcDownloadPlugin = 0;

    private volatile byte downloadStatusIndex = 0;
    
    private volatile PluginInfo[] plugins = [];
    private volatile Action<KeyValuePair<bool, string>>?[] downloadStatuses = new Action<KeyValuePair<bool, string>>?[byte.MaxValue];
    
    /// <summary>
    /// Gets the list of currently loaded plugins.
    /// </summary>
    public PluginInfo[] Plugins => plugins;

    /// <summary>
    /// Called when the server has spawned.
    /// </summary>
    public override void OnServerSpawned()
    {
        base.OnServerSpawned();
        
        Log.Info("Plugin manager module spawned!");       
    }

    /// <summary>
    /// Called when the client's entity spawn has been confirmed.
    /// </summary>
    public override void OnClientConfirmed()
    {
        base.OnClientConfirmed();
        
        CallRpcGetPlugins(array =>
        {
            plugins = array;
            
            Log.Info($"Loaded {plugins.Length} plugins!");
        });
    }

    /// <summary>
    /// Called when the module is destroyed.
    /// </summary>
    public override void OnDestroyed()
    {
        base.OnDestroyed();

        plugins = [];
        
        Log.Info("Plugin manager module destroyed!");       
    }

    /// <summary>
    /// Initiates a remote procedure call to retrieve a list of available plugins
    /// and returns the list through the specified callback.
    /// </summary>
    /// <param name="callback">The callback to handle the retrieved list of plugins. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="callback"/> is null.</exception>
    public void CallRpcGetPlugins(Action<PluginInfo[]> callback)
    {
        if (callback == null)
            throw new ArgumentNullException(nameof(callback));
        
        SendRemoteCallback(rpc_RpcGetPlugins, 
            _ => { },
            reader => callback(reader.ReadArray<PluginInfo>()));
    }

    /// <summary>
    /// Initiates a remote procedure call to remove a specified plugin and returns the result through the specified callback.
    /// </summary>
    /// <param name="pluginName">The name of the plugin to be removed. Must not be null or empty.</param>
    /// <param name="callback">The callback to handle the status of the plugin removal operation. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="pluginName"/> is null or empty, or when <paramref name="callback"/> is null.</exception>
    public void CallRpcRemovePlugin(string pluginName, Action<bool> callback)
    {
        if (string.IsNullOrEmpty(pluginName))
            throw new ArgumentNullException(nameof(pluginName));
        
        if (callback == null)
            throw new ArgumentNullException(nameof(callback));

        SendRemoteCallback(rpc_RpcRemovePlugin,
            writer => writer.WriteString(pluginName),
            reader =>
            {
                var status = reader.ReadBool();
                
                callback(status);

                if (!status)
                    return;
                
                CallRpcGetPlugins(array => plugins = array);
            });
    }

    /// <summary>
    /// Initiates a remote procedure call to download a plugin using the provided URL and returns the download status through the specified callback.
    /// </summary>
    /// <param name="pluginUrl">The URL of the plugin to download. Must not be null or empty.</param>
    /// <param name="callback">The callback to handle the plugin download status. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="pluginUrl"/> is null or empty, or when <paramref name="callback"/> is null.</exception>
    public void CallRpcDownloadPlugin(string pluginUrl, Action<KeyValuePair<bool, string>> callback)
    {
        if (string.IsNullOrEmpty(pluginUrl))
            throw new ArgumentNullException(nameof(pluginUrl));
        
        if (callback == null)
            throw new ArgumentNullException(nameof(callback));

        SendRemoteCallback(rpc_RpcDownloadPlugin,
            writer =>
            {
                writer.WriteString(pluginUrl);
                writer.WriteByte(downloadStatusIndex);
                
                downloadStatuses[downloadStatusIndex] = callback;

                if (downloadStatusIndex == byte.MaxValue)
                    downloadStatusIndex = 0;
                else
                    downloadStatusIndex += 1;
            });
    }

    /// <summary>
    /// Handles the receipt of a plugin download status update from the client and
    /// invokes the associated callback with the provided status.
    /// </summary>
    /// <param name="reader">
    /// The byte reader used to deserialize the index and status of the plugin download from the client's message.
    /// </param>
    /// <param name="_">
    /// The byte writer for the response. Currently unused in this method.
    /// </param>
    [ServerCmd]
    public void CmdReceiveDownloadStatus(ByteReader reader, ByteWriter _)
    {
        var index = reader.ReadByte();
        var status = reader.ReadBool();
        var message = reader.ReadString();
        var callback = downloadStatuses[index];
        
        callback?.Invoke(new(status, message));

        downloadStatuses[index] = null;
    }
}