using System.Net;
using System.Net.Sockets;
using NiveraAPI;

using NiveraAPI.IO.Configs;

using NiveraAPI.IO.Network;
using NiveraAPI.IO.Network.Entities;

using NiveraAPI.IO.Network.Database.Enums;
using NiveraAPI.IO.Network.Database.Server;

using NiveraAPI.Logs;

using NiveraAPI.Rest.Server;

using NiveraAPI.Steam.Auth;

namespace PRTS;

/// <summary>
/// Provides functionality for managing the network configuration and initialization
/// of the application. This includes database, REST API, and server components.
/// </summary>
public static class Network
{
    #region Network Config
    /// <summary>
    /// Whether to enable debug logs.
    /// </summary>
    [Config("network", "debug-logs", "Whether to enable debug logs.")]
    public static bool DebugLogs { get; set; }
    
    /// <summary>
    /// The port to listen on.
    /// </summary>
    [Config("network", "port", "The port to listen on.")]
    public static int Port { get; set; } = 8888;
    
    /// <summary>
    /// The maximum number of retransmissions to attempt.
    /// </summary>
    [Config("network", "max-retransmissions", "The maximum number of retransmissions to attempt.")]
    public static int MaxRetransmissions { get; set; } = 3;
    
    /// <summary>
    /// A list of IP addresses that are allowed to connect.
    /// </summary>
    [Config("network", "ip-whitelist", "A list of IP addresses that are allowed to connect.")]
    public static string[] IpWhitelist
    {
        get => ipWhitelist;
        set => ipWhitelist = value;
    }
    #endregion
    
    #region DbManager Config
    /// <summary>
    /// The default database permissions for all connected servers.
    /// </summary>
    [Config("database", "permissions", "Default database permissions for all connected servers.")]
    public static DbPerms DbPerms { get; set; } = DbPerms.None;

    /// <summary>
    /// Whether the database should require a password to access.
    /// </summary>
    [Config("database", "protected", "Whether the database should require a password to access.")]
    public static bool DbProtected { get; set; } = true;
    
    /// <summary>
    /// The password to access the database.
    /// </summary>
    [Config("database", "password", "The password to access the database.")]
    public static string DbPassword { get; set; } = string.Empty;

    /// <summary>
    /// The directory to store the database files.
    /// </summary>
    [Config("database", "directory", "The directory to store the database files.")]
    public static string DbDirectory { get; set; } = $"{Directory.GetCurrentDirectory()}/server-database";

    /// <summary>
    /// A list of users who can access the database.
    /// </summary>
    [Config("database", "users", "A list of users who can access the database.")]
    public static List<string> DbUsers { get; set; } = new();

    /// <summary>
    /// A list of users who can access the database without a password.
    /// </summary>
    [Config("database", "sudo-users", "A list of users who can access the database without a password.")]
    public static List<string> DbSudoUsers { get; set; } = new();
    
    /// <summary>
    /// A list of users and their permissions.
    /// </summary>
    [Config("database", "user-permissions", "A list of users and their permissions.")]
    public static Dictionary<string, DbPerms> DbUserPerms { get; set; } = new();
    #endregion
    
    #region Rest Config
    /// <summary>
    /// The prefix for the REST API.
    /// </summary>
    [Config("rest", "prefix", "The prefix for the REST API.")]
    public static string RestPrefix { get; set; } = "http://127.0.0.1:8888";

    /// <summary>
    /// The header to use for the IP address.
    /// </summary>
    [Config("rest", "ip-header", "The header to use for the IP address.")]
    public static string RestIpHeader { get; set; } = "X-Real-IP";
    #endregion

    private static volatile string[] ipWhitelist = [];

    /// <summary>
    /// The assigned logger instance.
    /// </summary>
    public static LogSink Log { get; private set; }
    
    /// <summary>
    /// The active server instance.
    /// </summary>
    public static NetServer NetServer { get; private set; }
    
    /// <summary>
    /// The active REST server instance.
    /// </summary>
    public static RestServer RestServer { get; private set; }
    
    /// <summary>
    /// The Steam authentication manager.
    /// </summary>
    public static SteamAuthManager SteamAuth { get; private set; }
    
    /// <summary>
    /// Starts the network manager.
    /// </summary>
    public static void Start()
    {
        Log ??= LogManager.GetSource("Network", "Manager");
        Log.Info("Starting ..");
        
        try
        {
            var dbConfig = new DbConfig();
            var dbServer = new DbServer();

            dbConfig.Users = DbUsers;
            dbConfig.Password = DbPassword;
            dbConfig.SudoUsers = DbSudoUsers;
            dbConfig.Directory = DbDirectory;
            dbConfig.Permissions = DbUserPerms;
            dbConfig.IsProtected = DbProtected;
            dbConfig.DefaultPermissions = DbPerms;
            
            Log.Info("Configuring REST server ..");

            RestServer = new(RestPrefix);
            RestServer.RealIpHeader = RestIpHeader;

            dbServer.RestUrl = "/database";
            dbServer.RestMethods = [HttpMethod.Get, HttpMethod.Post, HttpMethod.Put, HttpMethod.Delete];

            RestServer.Start();
            
            Log.Info($"REST server listening at &1{RestPrefix}&r");

            RestServer.AddService(dbConfig);
            RestServer.AddService(dbServer);
            RestServer.AddService(SteamAuth = new());

            SteamAuth.CallbackRoute = "/steam";
            SteamAuth.SessionCheckDelay = 500;
            
            Log.Info("Configuring server ..");

            NetServer = new();
            
            if (LibraryLoader.HasArgument("NetworkDebug"))
                NetServer.DebugLogs = true;
                
            NetServer.DebugLogs = DebugLogs;
            NetServer.Predicate = Predicate;
            NetServer.MaxRetransmissions = MaxRetransmissions;
            
            NetServer.Start();
            
            NetServer.AddService(dbConfig);
            NetServer.AddService(dbServer);
            
            NetServer.ProvidedServices.Add(typeof(EntityManager));

            LibraryUpdate.Register(NetServer.Update);
            
            NetServer.Listen(Port);
            
            Log.Info("Started!");
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }
    }
    
    /// <summary>
    /// Stops the network manager.
    /// </summary>
    public static void Stop()
    {
        Log.Info("Stopping ..");
        
        if (RestServer != null)
        {
            RestServer.Stop();
            RestServer = null!;
        }

        if (NetServer != null)
        {
            LibraryUpdate.Unregister(NetServer.Update);
            
            NetServer.Stop();
            NetServer = null!;
        }
        
        Log.Info("Stopped!");
    }

    private static bool Predicate(TcpClient client)
    {
        if (client.Client.RemoteEndPoint is not IPEndPoint endpoint)
        {
            Log.Warn("Connected client does not have an IP address!");
            return false;
        }

        if (ipWhitelist.Length > 0 && !ipWhitelist.Contains(endpoint.Address.ToString()))
        {
            Log.Warn($"Rejecting connection from &1{endpoint}&r (IP Whitelist)!");
            return false;
        }

        return true;
    }
}