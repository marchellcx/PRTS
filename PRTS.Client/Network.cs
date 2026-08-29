using System.Net;
using System.Reflection;

using LabApi.Loader;

using LabExtended.Core;

using NiveraAPI;

using NiveraAPI.IO.Configs;

using NiveraAPI.IO.Network;
using NiveraAPI.IO.Network.Entities;
using NiveraAPI.IO.Network.Database.Client;

using NiveraAPI.IO.Serialization;

using PRTS.Client;

using PRTS.Client.Profiles.Objects;
using PRTS.Client.Profiles.Sessions;

using PRTS.Client.Reports.Objects;
using PRTS.Client.Punishments.Objects;
using PRTS.Client.Plugins.Objects;

namespace PRTS;

/// <summary>
/// Provides functionality to manage a network connection.
/// Includes configuration options for IP address, port, receive threads, and automatic reconnection.
/// Handles the establishment, maintenance, and disconnection of a client-server connection.
/// </summary>
public static class Network
{
    private static IPEndPoint target;
    
    #region Config
    /// <summary>
    /// Gets or sets the IP address of the server.
    /// </summary>
    [Config("network", "ip", "The IP address of the server.")]
    public static string Ip { get; set; } = "127.0.0.1";

    /// <summary>
    /// Gets or sets the port number of the server.
    /// </summary>
    [Config("network", "port", "The port of the server.")]
    public static int Port { get; set; } = 8888;

    /// <summary>
    /// Gets or sets a value indicating whether debug logs should be enabled.
    /// </summary>
    [Config("network", "debug-logs", "Whether to enable debug logs.")]
    public static bool DebugLogs { get; set; } 
    
    /// <summary>
    /// Gets or sets a value indicating whether the client should automatically reconnect
    /// to the server if the connection is lost.
    /// </summary>
    [Config("network", "reconnect", "Whether to automatically reconnect to the server if disconnected.")]
    public static bool ShouldReconnect { get; set; } = true;
    
    /// <summary>
    /// Gets or sets the maximum number of retransmissions to attempt.
    /// </summary>
    [Config("network", "max-retransmissions", "The maximum number of retransmissions to attempt.")]
    public static int MaxRetransmissions { get; set; } = 3;

    /// <summary>
    /// Gets or sets the username used to access the database.
    /// </summary>
    [Config("database", "user", "The user to access the database.")]
    public static string DbUser { get; set; } = "user";

    /// <summary>
    /// Gets or sets the password used to access the database.
    /// </summary>
    [Config("database", "password", "The password to access the database.")]
    public static string DbPassword { get; set; } = "password";
    #endregion

    /// <summary>
    /// Gets the client used for managing network connections.
    /// </summary>
    public static NetClient? Client { get; private set; }

    /// <summary>
    /// Gets the database client for managing database-related operations within the network context.
    /// </summary>
    public static DbClient? Database { get; private set; }

    /// <summary>
    /// Gets the entity manager for managing networked entities.
    /// </summary>
    public static EntityManager? Entities { get; private set; }

    /// <summary>
    /// Gets the active instance of the PrtsClient.
    /// </summary>
    public static PrtsClient? Prts => PrtsClient.Active;

    /// <summary>
    /// Establishes a connection to the server using the configured IP address and port.
    /// If the IP address is invalid, a warning message will be logged.
    /// Sets up the network client, including event handlers for connection and disconnection events.
    /// Utilizes the configured number of receive threads and reconnect behavior.
    /// Logs information and handles errors during the connection attempt.
    /// </summary>
    public static void Connect()
    {
        try
        {
            if (!IPAddress.TryParse(Ip, out var ip))
            {
                ApiLog.Warn("PRTS", "Invalid IP address: " + Ip);
                return;
            }

            RegisterParsers();

            EntityManager.RegisterEntity<PrtsClient>(() => new PrtsClient());

            foreach (var kvp in PluginLoader.Plugins)
            {
                foreach (var type in kvp.Value.GetTypes())
                {
                    if (type != typeof(PrtsModule)
                        && type.IsSubclassOf(typeof(PrtsModule))
                        && !EntityManager.IsRegistered(type))
                    {
                        EntityManager.RegisterEntity(type, () => (PrtsModule)Activator.CreateInstance(type));
                    }
                }
            }

            target = new IPEndPoint(ip, Port);

            Client = new();

            Client.DebugLogs = DebugLogs;
            Client.MaxRetransmissions = MaxRetransmissions;

            var dbConfig = new DbConfig();

            dbConfig.User = DbUser;
            dbConfig.Password = DbPassword;

            Client.AddService(dbConfig);
            Client.Services.Add(typeof(EntityManager));

            Client.Connected += OnConnected;
            Client.Disconnected += OnDisconnected;

            LibraryUpdate.Register(Client.Update);

            Client.Start();
            Client.Connect(target);
        }
        catch (Exception ex)
        {
            ApiLog.Error("PRTS", $"Error while connecting to server:\n{ex}");
        }
    }

    /// <summary>
    /// Reinitializes the network client by disconnecting the current connection,
    /// if active, and attempting to establish a new connection using the existing configuration.
    /// Temporarily disables automatic reconnection to avoid interference during the process and
    /// restores it upon completion.
    /// </summary>
    public static void Reconnect()
    {
        var should = ShouldReconnect;

        ShouldReconnect = false;
        
        DestroyClient();
        
        Connect();
        
        ShouldReconnect = should;       
    }

    /// <summary>
    /// Registers all modules in the provided assembly that are subclasses of the PrtsModule type.
    /// Ensures that the discovered module types are not abstract and activates them during the registration process.
    /// Populates the EntityManager with the registered entities for runtime use.
    /// </summary>
    /// <param name="assembly">The assembly to scan for module types to register. Cannot be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when the provided assembly is null.</exception>
    public static void RegisterModules(Assembly assembly)
    {
        if (assembly == null)
            throw new ArgumentNullException(nameof(assembly));       
        
        foreach (var type in assembly.GetTypes())
        {
            if (type.IsSubclassOf(typeof(PrtsModule))
                && type != typeof(PrtsModule)
                && !EntityManager.IsRegistered(type))
            {
                EntityManager.RegisterEntity(type, () => (PrtsModule)Activator.CreateInstance(type));
                
                ApiLog.Info("PRTS", $"Registered module &1{type.Name}&r!");
            }
        }
    }

    private static void OnConnected()
    {
        var dbConfig = new DbConfig();

        dbConfig.User = DbUser;
        dbConfig.Password = DbPassword;
        
        Client.Connection.AddService(dbConfig);
        Client.Connection.AddService(Database = new DbClient());
        
        Entities = Client.Connection.Services.First(s => s is EntityManager) as EntityManager;
        
        ApiLog.Info("PRTS", "Connected to server!");
    }
    
    private static void OnDisconnected()
    {
        ApiLog.Warn("PRTS", "Disconnected from server!");

        try
        {
            if (Entities != null && Entities.IsRunning)
            {
                Entities.Stop();
            }

            Entities = null!;
        }
        catch
        {
            // ignored
        }
        
        try
        {
            if (Database != null && Database.IsRunning)
            {
                Database.Stop();
            }

            Database = null!;
        }
        catch
        {
            // ignored
        }

        if (ShouldReconnect && target != null)
        {
            DestroyClient();
            
            Client = new();

            Client.DebugLogs = DebugLogs;
            Client.MaxRetransmissions = MaxRetransmissions;

            var dbConfig = new DbConfig();

            dbConfig.User = DbUser;
            dbConfig.Password = DbPassword;

            Client.AddService(dbConfig);
            Client.Services.Add(typeof(EntityManager));

            Client.Connected += OnConnected;
            Client.Disconnected += OnDisconnected;

            LibraryUpdate.Register(Client.Update);

            Client.Start();
            Client.Connect(target);
        }
        else
        {
            DestroyClient();
        }
    }

    private static void RegisterParsers()
    {
        ByteSerializer<ReportInfo>.Deserialize = ReportSerialization.DeserializeReportInfo;     
        ByteSerializer<ProfileInfo>.Deserialize = ProfileSerialization.DeserializeProfile;
        ByteSerializer<ProfileSession>.Deserialize = ProfileSerialization.DeserializeSession;
        
        ByteSerializer<PunishmentInfo>.Serialize = PunishmentSerialization.WritePunishmentInfo;
        ByteSerializer<PunishmentInfo>.Deserialize = PunishmentSerialization.ReadPunishmentInfo;
        
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

    private static void DestroyClient()
    {
        if (Client != null)
        {
            try
            {
                LibraryUpdate.Unregister(Client.Update);

                Client.Connected -= OnConnected;
                Client.Disconnected -= OnDisconnected;

                try
                {
                    Client.Stop();
                }
                catch
                {
                    // ignored
                }

                Database = null;
                Entities = null;

                Client = null;
            }
            catch (Exception ex)
            {
                ApiLog.Error(ex);
            }
        }
    }
}