using System.Collections.Concurrent;
using System.Reflection;

using NiveraAPI.IO.Configs;

using NiveraAPI.IO.Network;
using NiveraAPI.IO.Network.Entities;
using NiveraAPI.IO.Network.Database.Server;

using NiveraAPI.Logs;
using NiveraAPI.Services;
using NiveraAPI.Utilities;

using PRTS.Main;
using PRTS.ScpSl.Discord;

namespace PRTS.ScpSl;

/// <summary>
/// Manages SCP:SL server connections.
/// </summary>
public static class ScpSlManager
{
    private static volatile LogSink log = LogManager.GetSource("ScpSl", "Manager");

    /// <summary>
    /// The ID of the Discord guild that the SCP:SL manager connects to.
    /// </summary>
    [Config("scp-sl", "guild-id", "The ID of the Discord guild to connect to.")]
    public static ulong GuildId { get; set; } = 0;

    /// <summary>
    /// A dictionary of Discord bot tokens.
    /// </summary>
    [Config("scp-sl", "tokens", "A list of tokens for per-server Discord bots.")]
    public static Dictionary<string, string> Tokens { get; set; } = new()
    {
        { "example", "example" }
    };

    /// <summary>
    /// A static property that contains a collection of all registered modules in the system.
    /// Each module must inherit from <see cref="ScpSlModule"/> and is dynamically discovered
    /// and registered using the <see cref="ScpSlManager.RegisterModules(Assembly)"/> method.
    /// </summary>
    public static volatile ConcurrentBag<Type> Modules = new();

    /// <summary>
    /// A dictionary of Discord bots that are currently connected to the server.
    /// </summary>
    public static volatile ConcurrentDictionary<string, ScpSlBot> Bots = new();

    /// <summary>
    /// A thread-safe collection that maps network connections to their corresponding SCP:SL server instances.
    /// </summary>
    public static volatile ConcurrentDictionary<NetConnection, ScpSlServer> Servers = new();

    /// <summary>
    /// Attempts to retrieve an SCP:SL server instance based on its alias.
    /// </summary>
    /// <param name="alias">The alias used to identify the server. Must not be null or empty.</param>
    /// <param name="server">When this method returns, contains the server matching the specified alias if found; otherwise, null.</param>
    /// <returns>
    /// <c>true</c> if a server matching the alias was found; otherwise, <c>false</c>.
    /// </returns>
    public static bool TryGetServerByAlias(string alias, out ScpSlServer server)
    {
        server = null!;

        foreach (var kvp in Servers)
        {
            if (string.IsNullOrEmpty(kvp.Value?.ServerAlias))
                continue;
            
            if (kvp.Value.IsDestroyed || kvp.Value.Manager?.Connection == null)
                continue;
            
            if (kvp.Value.ServerAlias != alias)
                continue;
            
            server = kvp.Value;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Attempts to retrieve an entity instance of type <typeparamref name="T"/> from an SCP:SL server based on its alias.
    /// </summary>
    /// <typeparam name="T">The type of the entity to retrieve. Must derive from <see cref="Entity"/>.</typeparam>
    /// <param name="alias">The alias used to identify the server. Must not be null or empty.</param>
    /// <param name="entity">When this method returns, contains the entity of type <typeparamref name="T"/>
    /// if found in the corresponding server; otherwise, the default value of <typeparamref name="T"/>.</param>
    /// <returns>
    /// <c>true</c> if an entity of type <typeparamref name="T"/> matching the alias was found; otherwise, <c>false</c>.
    /// </returns>
    public static bool TryGetEntityByAlias<T>(string alias, out T entity) where T : Entity
    {
        entity = default!;

        if (!TryGetServerByAlias(alias, out var server)
            || server?.Manager == null)
            return false;
        
        return server.Manager.TryGetFirstEntity(out entity);
    }

    /// <summary>
    /// Invokes the specified action on each entity of type <typeparamref name="T"/> that exists within active SCP:SL server connections.
    /// </summary>
    /// <param name="action">The action to be performed on each entity. Must not be null.</param>
    /// <typeparam name="T">The type of entity to process. Must derive from <see cref="Entity"/>.</typeparam>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="action"/> is null.</exception>
    public static void BroadcastEntities<T>(Action<T> action) where T : Entity
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));

        foreach (var conn in Network.NetServer.Connections)
        {
            if (conn.Value == null || !conn.Value.IsRunning)
                continue;
            
            if (!conn.Value.TryGetService<EntityManager>(out var entityManager))
                continue;
            
            if (!entityManager.TryGetFirstEntity<T>(out var entity))
                continue;
            
            action(entity);
        }
    }

    /// <summary>
    /// Broadcasts an action to all entities of the specified type that satisfy a given condition.
    /// </summary>
    /// <typeparam name="T">The type of entities to target. Must derive from <see cref="Entity"/>.</typeparam>
    /// <param name="predicate">A condition that determines whether an entity should be targeted. Must not be null.</param>
    /// <param name="action">The action to perform on each targeted entity. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="predicate"/> or <paramref name="action"/> is null.</exception>
    public static void BroadcastEntities<T>(Predicate<T> predicate, Action<T> action) where T : Entity
    {
        if (predicate == null)
            throw new ArgumentNullException(nameof(predicate));

        if (action == null)
            throw new ArgumentNullException(nameof(action));

        foreach (var conn in Network.NetServer.Connections)
        {
            if (conn.Value is not { IsRunning: true })
                continue;
            
            if (!conn.Value.TryGetService<EntityManager>(out var entityManager))
                continue;
            
            if (!entityManager.TryGetFirstEntity<T>(out var entity))
                continue;

            if (!predicate(entity))
                continue;
            
            action(entity);
        }
    }

    /// <summary>
    /// Finds and registers all modules in the specified assembly that inherit from <see cref="ScpSlModule"/>.
    /// </summary>
    /// <param name="assembly">The assembly to search for modules. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when the provided <paramref name="assembly"/> is null.</exception>
    public static void RegisterModules(Assembly assembly)
    {
        if (assembly == null)
            throw new ArgumentNullException(nameof(assembly));
        
        var types = assembly.GetTypes();

        foreach (var type in types)
        {
            if (type.IsAbstract || type.IsInterface || type.IsSealed)
                continue;
            
            if (type == typeof(ScpSlModule))
                continue;
            
            if (!type.IsSubclassOf(typeof(ScpSlModule)))
                continue;

            if (!Modules.Contains(type))
            {
                Modules.Add(type);
                
                if (!EntityManager.IsRegistered(type))
                {
                    EntityManager.RegisterEntity(type, () => (ScpSlModule)StaticNonGenericConstructor.Construct(type));

                    log.Info($"Found module &1{type}&r!");
                }
            }
        }
    }

    /// <summary>
    /// Starts the SCP:SL server manager.
    /// </summary>
    public static void Start()
    {
        log.Info("Start", "Initializing ..");
        
        EntityManager.RegisterEntity<ScpSlServer>(() => new ScpSlServer());
        
        Network.NetServer.Connected += OnConnected;
        Network.NetServer.Disconnected += OnDisconnected;
        
        log.Info("Start", "Starting Discord bots ..");

        foreach (var kvp in Tokens)
        {
            if (kvp.Key != "example")
            {
                if (kvp.Key == "main")
                {
                    log.Info("Start", "Starting main bot ..");

                    new MainBotInstance(GuildId == 0 ? null : GuildId).Connect(kvp.Value);
                }
                else
                {
                    var bot = new ScpSlBot(kvp.Key, GuildId == 0 ? null : GuildId);

                    Bots[kvp.Key] = bot;

                    log.Info("Start", $"Starting Discord bot &1{kvp.Key}&r!");

                    bot.Connect(kvp.Value);
                }
            }
        }
    }

    private static void OnConnected(NetConnection connection)
    {
        try
        {
            connection.AddService(new DbUser());
            
            var server = connection.GetService<EntityManager>()
                                   .SpawnEntity<ScpSlServer>();
            
            Servers.TryAdd(connection, server);
        }
        catch (Exception ex)
        {
            log.Error("OnServerIdentify", $"An error occured while accepting server identification, disconnecting!\n{ex}");

            try
            {
                connection.Disconnect();
            }
            catch
            {
                // ignored
            }

            return;
        }
        
        log.Info($"&1{connection.EndPoint}&r connected.");
    }

    private static void OnDisconnected(NetConnection connection)
    {
        log.Info($"&1{connection.EndPoint}&r disconnected!");

        var endpoint = connection.EndPoint!.ToString();

        foreach (var kvp in Servers)
        {
            if (kvp.Value.endpoint == endpoint)
            {
                Servers.TryRemove(kvp);

                if (kvp.Value.DiscordBot != null)
                {
                    kvp.Value.DiscordBot.Server = null;
                    kvp.Value.DiscordBot.OnServerDisconnected();
                }

                kvp.Value.DiscordBot = null;

                try
                {
                    kvp.Value.Manager?.Connection?.StopAllServices(true);
                }
                catch
                {
                    // ignored
                }

                try
                {
                    kvp.Value.Manager?.Stop();
                }
                catch
                {
                    // ignored
                }
            }
        }
    }
}