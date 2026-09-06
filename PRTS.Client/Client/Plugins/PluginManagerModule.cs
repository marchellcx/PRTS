using NiveraAPI.IO.Network.Entities.Attributes;
using NiveraAPI.IO.Serialization;

using System.Net.Http;
using System.Reflection;

using LabApi.Features.Wrappers;

using LabApi.Loader;
using LabApi.Loader.Features.Paths;
using LabApi.Loader.Features.Plugins;

using LabExtended.Utilities;
using LabExtended.Extensions;
using PRTS.Client.Plugins.Objects;

namespace PRTS.Client.Plugins;

/// <summary>
/// Represents a plugin management module within the PRTS client system,
/// allowing functionality such as downloading and managing plugins.
/// </summary>
[ServerType("PRTS.ScpSl.Modules.Plugins.PluginManagerModule")]
public class PluginManagerModule : PrtsModule
{
    [IndexField] private static ushort cmd_CmdReceiveDownloadStatus = 0;

    /// <summary>
    /// Retrieves a list of enabled plugins and writes their information to the provided <see cref="ByteWriter"/> instance.
    /// </summary>
    /// <param name="_">The <see cref="ByteReader"/> instance, unused in this method.</param>
    /// <param name="writer">The <see cref="ByteWriter"/> instance used to serialize and write the list of plugin details.</param>
    [ClientRpc(true)]
    public void RpcGetPlugins(ByteReader _, ByteWriter writer)
    {
        var list = new List<PluginInfo>();
        
        foreach (var plugin in PluginLoader.EnabledPlugins)
        {
            var info = new PluginInfo
            {
                Name = plugin.Name,
                File = plugin.FilePath,
                Author = plugin.Author,
                Version = plugin.Version.ToString(),
                Description = plugin.Description
            };

            list.Add(info);
        }
        
        writer.WriteList(list);
    }

    /// <summary>
    /// Removes a specified plugin by its name and writes the result of the operation to the provided <see cref="ByteWriter"/> instance.
    /// </summary>
    /// <param name="reader">The <see cref="ByteReader"/> instance used to read the name of the plugin to be removed.</param>
    /// <param name="writer">The <see cref="ByteWriter"/> instance used to write a boolean indicating whether the plugin was successfully removed.</param>
    [ClientRpc(true)]
    public void RpcRemovePlugin(ByteReader reader, ByteWriter writer)
    {
        var name = reader.ReadString();

        if (!PluginLoader.EnabledPlugins.TryGetFirst(p => p.Name == name, out var plugin))
        {
            writer.WriteBool(false);
            
            Log.Warn($"Could not remove plugin &1{name}&r: not found");
        }
        else
        {
            try
            {
                Log.Info($"Removing plugin &1{plugin.Name}&r");
                
                plugin.Disable();
                
                PluginLoader.Plugins.Remove(plugin);
                PluginLoader.EnabledPlugins.Remove(plugin);
                
                if (File.Exists(plugin.FilePath))
                    File.Delete(plugin.FilePath);
                
                Log.Info($"Removed plugin &1{plugin.Name}&r");
            }
            catch (Exception ex)
            {
                Log.Error($"Error while removing plugin &1{plugin.Name}&r:\n{ex}");
            }
            
            writer.WriteBool(true);
        }
    }

    /// <summary>
    /// Initiates the download of a plugin from a specified URL and stores it in the designated plugin directory.
    /// </summary>
    /// <param name="reader">The <see cref="ByteReader"/> instance to read incoming data, including the plugin URL and other parameters.</param>
    /// <param name="_">The <see cref="ByteWriter"/> instance, unused in this method.</param>
    [ClientRpc(false)]
    public void RpcDownloadPlugin(ByteReader reader, ByteWriter _)
    {
        var url = reader.ReadString();
        var index = reader.ReadByte();

        var name = string.Empty;
        
        try
        {
            var uri = new Uri(url);

            name = Path.GetFileName(uri.AbsolutePath);
            
            var extension = Path.GetExtension(name);

            if (extension != ".dll")
                name = name.Replace(extension, ".dll");
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to parse URL &1{url}&r:\n{ex}");
            
            CallCmdReceiveDownloadStatus(index, false, ex.ToString());
            return;
        }
        
        Log.Info($"Downloading plugin &1{name}&r from &1{url}&r");
        
        Task.Run(async () =>
        {
            using (var client = new HttpClient())
            {
                return await client.GetByteArrayAsync(url);
            }
        }).ContinueWithOnMain(task =>
        {
            if (task.IsFaulted)
            {
                Log.Error($"Failed while downloading plugin:\n{task.Exception?.ToString() ?? "Unknown error"}");
                
                CallCmdReceiveDownloadStatus(index, false, task.Exception?.ToString() ?? "Unknown error");
                return;
            }

            var path = Path.Combine(PathManager.Plugins.FullName, Server.Port.ToString(), name);
            var info = new FileInfo(path);

            File.WriteAllBytes(path, task.Result);
            
            CallCmdReceiveDownloadStatus(index, true, null);
            
            Log.Info($"Downloaded plugin &1{name}&r (&6{task.Result.Length}&r byte(s))");

            var pluginsNow = PluginLoader.Plugins.ToDictionary();
            
            PluginLoader.LoadPlugins([info]);

            var newPlugins = new Dictionary<Plugin, Assembly>();

            foreach (var kvp in PluginLoader.Plugins)
            {
                if (!pluginsNow.ContainsKey(kvp.Key))
                {
                    newPlugins[kvp.Key] = kvp.Value;
                    
                    Log.Info($"Loaded plugin &1{kvp.Key.Name}&r");
                }
            }

            if (newPlugins.Count > 0)
            {
                foreach (var kvp in newPlugins)
                {
                    Log.Info($"Enabling plugin &1{kvp.Key.Name}&r");

                    try
                    {
                        PluginLoader.EnablePlugin(kvp.Key);
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"Could not enable plugin &1{kvp.Key.Name}&r:\n{ex}");
                    }

                    Log.Info($"Enabled plugin &1{kvp.Key.Name}&r");
                }
            }
            else
            {
                Log.Warn("No new plugins were loaded!");
            }
            
            newPlugins.Clear();
        });
    }

    /// <summary>
    /// Sends the download status and an optional message back to the server, providing feedback on the outcome of a plugin download operation.
    /// </summary>
    /// <param name="index">The identifier for the plugin download operation, used to correlate the status with the specific task.</param>
    /// <param name="status">A boolean indicating whether the download operation was successful (true) or not (false).</param>
    /// <param name="message">An optional message providing additional details or error information related to the download operation.</param>
    public void CallCmdReceiveDownloadStatus(byte index, bool status, string? message)
    {
        SendRemoteCallback(cmd_CmdReceiveDownloadStatus, writer =>
        {
            writer.WriteByte(index);
            writer.WriteBool(status);
            writer.WriteString(message!);
        });
    }
}