using System.Text;
using System.Collections.Concurrent;

using Discord;
using Discord.WebSocket;
using Discord.Interactions;
using Discord.Net;
using Fergun.Interactive;
using Microsoft.Extensions.DependencyInjection;

using NiveraAPI;
using NiveraAPI.Logs;
using NiveraAPI.Console;
using NiveraAPI.Utilities;
using NiveraAPI.Extensions;
using NiveraAPI.IO.Serialization;
using PRTS.Core.Attributes;
using PRTS.Discord.MessageCache;
using PRTS.Extensions;
using ServiceCollection = NiveraAPI.Services.ServiceCollection;

namespace PRTS.Discord;

/// <summary>
/// Base class for managing a Discord bot.
/// </summary>
public class DiscordBot : ServiceCollection
{
    static DiscordBot()
    {
        ByteSerializer<CachedDiscordMessage>.Serialize = (writer, msg) =>
        {
            writer.WriteUInt64(msg.GuildId);
            writer.WriteUInt64(msg.ChannelId);
            writer.WriteUInt64(msg.MessageId);
        };

        ByteSerializer<CachedDiscordMessage>.Deserialize = reader =>
        {
            var msg = new CachedDiscordMessage();
            
            msg.GuildId = reader.ReadUInt64();
            msg.ChannelId = reader.ReadUInt64();
            msg.MessageId = reader.ReadUInt64();
            
            return msg;
        };
    }
    
    private static volatile ConcurrentDictionary<string, DiscordBot> bots = new();
    
    internal static volatile ConcurrentDictionary<string, Func<SocketModal, Task>> modals = new();
    internal static volatile ConcurrentDictionary<string, Func<SocketModal, Task?>> menus = new();
    internal static volatile ConcurrentDictionary<string, Func<SocketMessageComponent, Task?>> buttons = new();

    /// <summary>
    /// The collection of all managed bots.
    /// </summary>
    public static ConcurrentDictionary<string, DiscordBot> Bots => bots;

    /// <summary>
    /// Attempts to retrieve an instance of <see cref="DiscordBot"/> associated with the specified alias.
    /// </summary>
    /// <param name="alias">The unique alias identifying the bot instance.</param>
    /// <param name="bot">
    /// When this method returns, contains the <see cref="DiscordBot"/> instance associated with the specified alias,
    /// if the alias exists; otherwise, the value is <c>null</c>.
    /// </param>
    /// <returns>
    /// <c>true</c> if a bot instance associated with the specified alias was found; otherwise, <c>false</c>.
    /// </returns>
    public static bool TryGetBot(string alias, out DiscordBot bot)
        => bots.TryGetValue(alias, out bot);

    /// <summary>
    /// Attempts to retrieve a <see cref="SocketGuild"/> instance associated with the specified guild ID.
    /// </summary>
    /// <param name="guildId">The unique identifier of the guild to be retrieved.</param>
    /// <returns>
    /// A <see cref="SocketGuild"/> instance if a guild with the specified ID is found across all managed bots;
    /// otherwise, <c>null</c>.
    /// </returns>
    public static SocketGuild? TryGetGuild(ulong guildId)
    {
        if (bots.Count == 0)
            return null;
        
        foreach (var kvp in bots)
        {
            if (kvp.Value.Client == null)
                continue;
            
            if (kvp.Value.Client.Guilds.TryGetFirst(g => g.Id == guildId, out var guild))
                return guild;
        }

        return null;
    }

    /// <summary>
    /// Attempts to retrieve a <see cref="SocketGuildChannel"/> associated with the specified channel ID.
    /// </summary>
    /// <param name="channelId">The unique identifier of the channel to retrieve.</param>
    /// <returns>
    /// A <see cref="SocketGuildChannel"/> instance if a channel with the specified ID exists across any guilds the bot is connected to;
    /// otherwise, <c>null</c>.
    /// </returns>
    public static SocketGuildChannel? TryGetChannel(ulong channelId)
    {
        if (bots.Count == 0)
            return null;
        
        foreach (var kvp in bots)
        {
            if (kvp.Value.Client == null)
                continue;

            foreach (var guild in kvp.Value.Client.Guilds)
            {
                var channel = guild.GetChannel(channelId);

                if (channel != null)
                    return channel;
            }
        }

        return null;
    }

    /// <summary>
    /// Attempts to retrieve a channel of the specified type associated with the given channel ID.
    /// </summary>
    /// <typeparam name="T">The type of the channel to retrieve, which must be a subclass of <see cref="SocketChannel"/>.</typeparam>
    /// <param name="channelId">The unique identifier of the channel to retrieve.</param>
    /// <returns>
    /// The channel of type <typeparamref name="T"/> if found; otherwise, <c>null</c>.
    /// </returns>
    public static T? TryGetChannel<T>(ulong channelId) where T : SocketChannel
    {
        if (bots.Count == 0)
            return null;
        
        foreach (var kvp in bots)
        {
            if (kvp.Value.Client == null)
                continue;

            foreach (var guild in kvp.Value.Client.Guilds)
            {
                var channel = guild.GetChannel(channelId);

                if (channel is T castChannel)
                    return castChannel;
            }
        }

        return null;
    }

    /// <summary>
    /// Attempts to retrieve a <see cref="SocketGuildUser"/> instance associated with the specified guild ID and user ID.
    /// </summary>
    /// <param name="guildId">
    /// The unique identifier of the guild to search in. If <c>0</c>, all available guilds will be searched.
    /// </param>
    /// <param name="userId">
    /// The unique identifier of the user to retrieve from the specified guild or across all guilds.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous operation. When completed, the result is a <see cref="SocketGuildUser"/>
    /// instance if the user was found; otherwise, <c>null</c>.
    /// </returns>
    public static async Task<SocketGuildUser?> TryGetGuildUser(ulong guildId, ulong userId)
    {
        if (guildId == 0)
        {
            foreach (var kvp in bots)
            {
                if (kvp.Value.Client == null)
                    continue;

                foreach (var guild in kvp.Value.Client.Guilds)
                {
                    var user = guild.GetUser(userId);
                    
                    if (user != null)
                        return user;
                }
            }

            return null;
        }
        else
        {
            var guild = TryGetGuild(guildId);

            if (guild == null)
                return null;

            return guild.GetUser(userId);
        }
    }

    /// <summary>
    /// Attempts to retrieve a guild user and their associated roles from the specified guild or across all guilds if no guild ID is provided.
    /// </summary>
    /// <param name="guildId">
    /// The identifier of the guild. If set to <c>0</c>, the method attempts to find the user across all available guilds.
    /// </param>
    /// <param name="userId">The identifier of the user whose roles are being retrieved.</param>
    /// <returns>
    /// A <see cref="KeyValuePair{TKey, TValue}"/> containing the <see cref="SocketGuildUser"/> and a list of their associated <see cref="SocketRole"/> objects,
    /// if the user is found; otherwise, <c>null</c>.
    /// </returns>
    public static async Task<KeyValuePair<SocketGuildUser, List<SocketRole>>?> TryGetRoles(ulong guildId, ulong userId)
    {
        if (guildId == 0)
        {
            var list = new List<SocketRole>();
            var user = default(SocketGuildUser);

            foreach (var kvp in bots)
            {
                if (kvp.Value.Client == null)
                    continue;

                foreach (var guild in kvp.Value.Client.Guilds)
                {
                    user = guild.GetUser(userId);

                    if (user == null)
                        continue;

                    list.AddRange(user.Roles);
                }
            }

            return new(user!, list);
        }
        else
        {
            var user = await TryGetGuildUser(guildId, userId);

            if (user == null)
                return null;

            return new(user, user.Roles.ToList());
        }
    }

    /// <summary>
    /// Attempts to retrieve the list of role IDs associated with a user in a specific guild or across all guilds managed by the bot.
    /// </summary>
    /// <param name="guildId">
    /// The unique identifier of the guild. If set to <c>0</c>, the method retrieves roles across all guilds.
    /// </param>
    /// <param name="userId">
    /// The unique identifier of the user whose role IDs are to be retrieved.
    /// </param>
    /// <returns>
    /// A list of role IDs associated with the specified user or <c>null</c> if the user cannot be found.
    /// </returns>
    public static async Task<List<ulong>?> TryGetRoleIds(ulong guildId, ulong userId)
    {
        if (guildId == 0)
        {
            var list = new List<ulong>();

            foreach (var kvp in bots)
            {
                if (kvp.Value.Client == null)
                    continue;

                foreach (var guild in kvp.Value.Client.Guilds)
                {
                    var user = guild.GetUser(userId);

                    if (user == null)
                        continue;

                    list.AddRange(user.Roles.Select(r => r.Id));
                }
            }

            return list;
        }
        else
        {
            var user = await TryGetGuildUser(guildId, userId);

            if (user == null)
                return null;

            return user.Roles.Select(r => r.Id).ToList();
        }
    }

    private volatile IServiceCollection interactionServices =
        new Microsoft.Extensions.DependencyInjection.ServiceCollection();
    
    private volatile DiscordLog discordLog;
    private volatile DiscordSocketConfig config;
    private volatile DiscordSocketClient client;
    
    private volatile InteractiveService interactiveService;
    private volatile InteractionService interactionService;
    
    private volatile SocketGuild? primaryGuild;
    
    private ulong? primaryGuildId;
    
    /// <summary>
    /// The bot's alias.
    /// </summary>
    public string BotAlias { get; }
    
    /// <summary>
    /// The bot's logger.
    /// </summary>
    public LogSink Log { get; }
    
    /// <summary>
    /// The primary guild of the bot.
    /// </summary>
    public SocketGuild? PrimaryGuild => primaryGuild;
    
    /// <summary>
    /// The Discord client.
    /// </summary>
    public DiscordSocketClient Client => client;
    
    /// <summary>
    /// The interactive service.
    /// </summary>
    public InteractiveService Fergun => interactiveService;
    
    /// <summary>
    /// The interaction service.
    /// </summary>
    public InteractionService Interactions => interactionService;
    
    /// <summary>
    /// Whether the bot is connected to Discord.
    /// </summary>
    public bool IsConnected => client != null && client.ConnectionState == ConnectionState.Connected;
    
    /// <summary>
    /// Whether the bot is connecting to Discord.
    /// </summary>
    public bool IsConnecting => client != null && client.ConnectionState == ConnectionState.Connecting;
    
    /// <summary>
    /// Whether the bot is disconnected from Discord.
    /// </summary>
    public bool IsDisconnected => client == null || client.ConnectionState == ConnectionState.Disconnected;

    /// <summary>
    /// Whether the bot has a primary guild.
    /// </summary>
    public bool HasPrimaryGuild => primaryGuildId.HasValue;
    
    /// <summary>
    /// Whether the bot has a primary guild and it is cached.
    /// </summary>
    public bool HasFoundPrimaryGuild => HasPrimaryGuild && primaryGuild != null;
    
    /// <summary>
    /// Whether the bot should automatically reconnect to Discord if it is disconnected.
    /// </summary>
    public bool ReconnectOnDisconnect { get; set; }

    /// <summary>
    /// The activity text displayed as the bot's custom status in Discord.
    /// </summary>
    public string ActivityText
    {
        get
        {
            return client.Activity?.Details 
                   ?? client.Activity?.Name 
                   ?? string.Empty;
        }
        set
        {
            if (ActivityText == value)
                return;
            
            Task.Run(async () =>
            {
                await client.SetCustomStatusAsync(value);
            }).ContinueWithOnMain(t =>
            {
                if (t.IsFaulted)
                {
                    Log.Error($"Error while setting activity: {t.Exception?.ToString() ?? "Unknown error"}");
                }
            });
        }
    }

    /// <summary>
    /// Represents the current online status of the bot in Discord.
    /// </summary>
    public UserStatus Status
    {
        get
        {
            return client?.Status ?? UserStatus.Offline;
        }
        set
        {
            if (Status == value)
                return;
            
            Task.Run(async () =>
            {
                await client.SetStatusAsync(value);
            }).ContinueWithOnMain(t =>
            {
                if (t.IsFaulted)
                {
                    Log.Error($"Error while setting status: {t.Exception?.ToString() ?? "Unknown error"}");
                }
            });
        }
    }

    /// <summary>
    /// Creates a new instance of the <see cref="DiscordBot"/> class.
    /// </summary>
    /// <param name="alias">The bot's alias.</param>
    /// <param name="primaryGuildId">The ID of the bot's primary guild, if any.</param>
    /// <exception cref="ArgumentNullException">Thrown if alias is null or empty.</exception>
    public DiscordBot(string alias, ulong? primaryGuildId = null)
    {
        if (string.IsNullOrEmpty(alias))
            throw new ArgumentNullException(nameof(alias));
        
        BotAlias = alias;
        
        Log = LogManager.GetSource("Bots", alias);
        ReconnectOnDisconnect = LibraryLoader.HasArgument("DiscordReconnectOnDisconnect");

        this.primaryGuildId = primaryGuildId;
        this.discordLog = new(alias);
        
        bots.TryAdd(alias, this);
    }

    /// <summary>
    /// Connects the bot to Discord using the specified token.
    /// Initializes the Discord client, configures settings, and starts the connection process.
    /// </summary>
    /// <param name="token">The token used for authentication with Discord's API.</param>
    /// <exception cref="ArgumentNullException">Thrown if the provided <paramref name="token"/> is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the bot is already connected to Discord.</exception>
    public void Connect(string token)
    {
        if (string.IsNullOrEmpty(token))
            throw new ArgumentNullException(nameof(token));

        if (client != null)
            throw new InvalidOperationException("The Discord client is already connected.");
        
        Log.Info("Connecting to Discord ..");
        Log.Debug("Configuring client ..");

        config = new();
        config.LargeThreshold = 250;
        
        config.AlwaysDownloadUsers = true;
        config.AlwaysResolveStickers = true;
        config.AlwaysDownloadDefaultStickers = true;

        config.LogLevel = LogSeverity.Info;
        config.GatewayIntents = GatewayIntents.All;
            
        if (LibraryLoader.HasArgument("DiscordLogRawGateway"))
            config.IncludeRawPayloadOnGatewayErrors = true;

        if (LibraryLoader.HasArgument("DiscordLogIntentWarns"))
            config.LogGatewayIntentWarnings = true;

        if (LibraryLoader.HasArgument("DiscordLogUnknownDispatch"))
            config.SuppressUnknownDispatchWarnings = false;
        
        if (LibraryLoader.HasArgument("DiscordMessageCacheSize", out var msgCacheSizeStr)
            && int.TryParse(msgCacheSizeStr, out var msgCacheSize))
            config.MessageCacheSize = msgCacheSize;
        
        if (LibraryLoader.HasArgument("DiscordLogLevel", out var logLevelStr)
            && Enum.TryParse<LogSeverity>(logLevelStr, true, out var logLevel))
            config.LogLevel = logLevel;
        
        Log.Debug("Constructing client ..");

        client = new(config);
        
        interactionService = new(client.Rest);
        interactiveService = new(client, new() { DefaultTimeout = TimeSpan.FromMinutes(5) });

        Log.Debug("Registering events ..");
        
        RegisterEvents();
        
        Log.Debug("Starting connection ..");
        
        Task.Run(async () =>
        {
            await client.LoginAsync(TokenType.Bot, token);
            await client.StartAsync();
        }).ContinueWithOnMain(t =>
        {
            if (t.IsFaulted)
                Log.Error($"Error while connecting: {t.Exception?.Message ?? "Unknown error"}");
            else
                Log.Info("Connected to Discord!");
        });
    }

    /// <summary>
    /// Disconnects the bot by stopping the client, logging out, and disposing of the associated resources asynchronously.
    /// </summary>
    public void Disconnect()
    {
        if (client != null)
        {
            var cl = client;
            var lg = this.Log;
            
            OnDisconnected();
            UnregisterEvents();
            
            client = null!;
            config = null!;
            
            Task.Run(async () =>
            {
                await lg.TryCatchAsync(true, cl.StopAsync);
                await lg.TryCatchAsync(true, cl.LogoutAsync);
                await lg.TryCatchAsync(true, cl.DisposeAsync);
            });
        }
    }

    /// <summary>
    /// Registers a modal of the specified type with the bot's interaction service.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the modal to register. Must implement <see cref="IModal"/>.
    /// </typeparam>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the bot is not connected to Discord or if the bot does not have a primary guild.
    /// </exception>
    public void RegisterModal<T>() where T : class, IModal
    {
        if (!IsConnected)
            throw new InvalidOperationException("The bot is not connected to Discord.");
        
        if (PrimaryGuild == null)
            throw new InvalidOperationException("The bot does not have a primary guild.");

        interactionService.AddModalInfo<T>();
    }

    /// <summary>
    /// Registers the command module specified by the generic type parameter <typeparamref name="T"/> to the bot's primary guild.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the module containing the commands to be registered. This type must inherit from a compatible command module base class.
    /// </typeparam>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the bot is not connected to Discord, if the bot does not have a primary guild,
    /// or if the command module fails to register.
    /// </exception>
    public void RegisterCommands<T>(IServiceProvider? serviceProvider = null)
        where T : InteractionModuleBase<SocketInteractionContext>
    {
        if (!IsConnected)
            throw new InvalidOperationException("The bot is not connected to Discord.");

        if (PrimaryGuild == null)
            throw new InvalidOperationException("The bot does not have a primary guild.");

        serviceProvider ??= interactionServices.BuildServiceProvider();
        
        Task.Run(async () =>
        {
            ModuleInfo module = null!;

            try
            {
                module = interactionService.GetModuleInfo<T>();
            }
            catch (KeyNotFoundException)
            {
                module = await interactionService.AddModuleAsync(typeof(T), serviceProvider);
            }

            if (module == null)
                throw new InvalidOperationException("Failed to register commands.");

            return await interactionService.AddCommandsGloballyAsync(true, module.SlashCommands.ToArray());
        }).ContinueWithOnMain(task =>
        {
            if (task.IsFaulted)
            {
                Log.Error($"Error while registering commands:\n{task.Exception}");

                if (task.Exception != null)
                {
                    if (task.Exception.InnerException is HttpException httpException
                        || (task.Exception.InnerExceptions.TryGetFirst(x => x is HttpException, out var exc)
                            && (httpException = exc as HttpException) != null))
                    {
                        Log.Error($"Discord Error: &1{httpException.DiscordCode?.ToString() ?? "null"}");

                        foreach (var error in httpException.Errors)
                        {
                            Log.Error($"Path: &3{error}&r");
                            
                            foreach (var subError in error.Errors)
                            {
                                Log.Error($"&1{subError.Code}&r: &3{subError.Message}&r");
                            }
                        }
                    }
                }
            }
            else
            {
                if (task.Result != null)
                {
                    foreach (var cmd in task.Result)
                    {
                        Log.Info($"Registered command &1{cmd.Name}&r");
                    }
                }
                else
                {
                    Log.Info($"Registered commands of module &1{typeof(T).Name}&r");
                }
            }
        });
    }

    /// <summary>
    /// Unregisters commands associated with the specified interaction module type from the bot's primary guild.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the interaction module, which must derive from <see cref="InteractionModuleBase{SocketInteractionContext}"/>.
    /// </typeparam>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the bot is not connected to Discord, does not have a primary guild,
    /// or the specified module type is not registered.
    /// </exception>
    public void UnregisterCommands<T>() where T : InteractionModuleBase<SocketInteractionContext>
    {
        if (!IsConnected)
            throw new InvalidOperationException("The bot is not connected to Discord.");
        
        if (PrimaryGuild == null)
            throw new InvalidOperationException("The bot does not have a primary guild.");

        Task.Run(async () =>
        {
            ModuleInfo module = null!;
            
            try
            {
                module = interactionService.GetModuleInfo<T>();
            }
            catch (KeyNotFoundException)
            {
                return;
            }

            if (module == null)
                return;
            
            await interactionService.RemoveModuleAsync<T>();
        }).ContinueWithOnMain(task =>
        {
            if (task.IsFaulted)
                Log.Error($"Error while unregistering commands:\n{task.Exception}");
            else
                Log.Info($"Unregistered commands of module &1{typeof(T).Name}&r");
        });
    }

    /// <summary>
    /// Stops the bot by disconnecting from Discord and disposing of associated resources.
    /// </summary>
    public override void Stop()
    {
        base.Stop();
        
        Disconnect();
        
        bots.TryRemove(BotAlias, out _);
    }

    /// <summary>
    /// Registers all events associated with the bot.
    /// </summary>
    public virtual void RegisterEvents()
    {
        if (!LibraryLoader.HasArgument("DiscordDisableLog"))
        {
            client.Log += discordLog.Log;
            interactionService.Log += discordLog.Log;
        }

        client.Connected += _Connected;
        client.Disconnected += _Disconnected;

        client.GuildAvailable += _GuildAvailable;
        client.InteractionCreated += _InteractionCreated;

        client.ModalSubmitted += _ModalSubmitted;
        client.ButtonExecuted += _ButtonExecuted;
        
        interactionService.SlashCommandExecuted += _SlashCommandExecuted;
    }

    /// <summary>
    /// Unregisters all events associated with the bot.
    /// </summary>
    public virtual void UnregisterEvents()
    {
        client.Log -= discordLog.Log;
        interactionService.Log -= discordLog.Log;
        
        client.Connected -= _Connected;
        client.Disconnected -= _Disconnected;
        
        client.GuildAvailable -= _GuildAvailable;
        client.InteractionCreated -= _InteractionCreated;
        
        client.ModalSubmitted -= _ModalSubmitted;
        client.ButtonExecuted -= _ButtonExecuted;

        interactionService.SlashCommandExecuted -= _SlashCommandExecuted;
    }

    /// <summary>
    /// Called when the bot is connected to Discord.
    /// </summary>
    public virtual void OnConnected()
    {
        
    }

    /// <summary>
    /// Invoked when the bot has been disconnected from Discord. This method can be overridden
    /// to define custom behavior that should occur upon disconnection, such as cleaning up
    /// resources or logging the event.
    /// </summary>
    public virtual void OnDisconnected()
    {

    }

    /// <summary>
    /// Triggered when the bot successfully connects to and reaches a specific guild.
    /// </summary>
    /// <param name="guild">The <see cref="SocketGuild"/> instance representing the guild that has been reached.</param>
    public virtual void OnGuildReached(SocketGuild guild)
    {
    }

    /// <summary>
    /// Called when the primary guild is reached.
    /// </summary>
    public virtual void OnPrimaryGuildReached()
    {
        
    }

    /// <summary>
    /// Handles the submission of a modal dialog interaction in Discord.
    /// </summary>
    /// <param name="modal">
    /// The <see cref="SocketModal"/> object representing the modal dialog submitted by the user.
    /// </param>
    public virtual void OnModalSubmitted(SocketModal modal)
    {
    }

    private Task _Connected()
    {
        ThreadHelper.RunOnMainThread(OnConnected);
        return Task.CompletedTask;
    }
    
    private Task _Disconnected(Exception exception)
    {
        ThreadHelper.RunOnMainThread(() =>
        {
            primaryGuild = null;
                
            OnDisconnected();
        });
        
        return Task.CompletedTask;
    }

    private Task _GuildAvailable(SocketGuild guild)
    {
        ThreadHelper.RunOnMainThread(() =>
        {
            if (primaryGuildId.HasValue && guild.Id == primaryGuildId.Value)
            {
                Log.Info($"Primary guild &1{guild.Name}&r reached!");
                
                primaryGuild = guild;
                
                OnPrimaryGuildReached();
            }
            
            OnGuildReached(guild);
        });

        return Task.CompletedTask;
    }

    private Task _InteractionCreated(SocketInteraction interaction)
    {
        Task.Run(async () =>
        {
            try
            {
                var ctx = new SocketInteractionContext(client, interaction);
                
                await interactionService.ExecuteCommandAsync(ctx, interactionServices?.BuildServiceProvider());
            }
            catch (Exception ex)
            {
                ConsoleOutput.Write($"An error occured while handling an interaction in {interaction.GuildId}:\n{ex}", ConsoleColor.Red);
            }
        });

        return Task.CompletedTask;
    }

    private Task _SlashCommandExecuted(SlashCommandInfo command, IInteractionContext context, IResult result)
    {
        if (result.IsSuccess)
            return Task.CompletedTask;
        
        ThreadHelper.RunOnMainThread(() =>
        {
            switch (result.Error)
            {
                case InteractionCommandError.Exception:
                {
                    Task.Run(async () =>
                    {
                        if (context.Interaction.HasResponded)
                        {
                            await context.Interaction.ModifyOriginalResponseAsync(msg =>
                            {
                                msg.Content = $":x: | Nastala chyba:\n```{result.ErrorReason}```";
                            });
                        }
                        else
                        {
                            await context.Interaction.RespondAsync($":x: | Nastala chyba:\n```{result.ErrorReason}```");
                        }
                    });

                    Log.Error($"An error occured while executing slash command &1{command.Name}&r: &3{result.ErrorReason}&r");
                    break;
                }
            }
        });
        
        return Task.CompletedTask;
    }

    private Task _ModalSubmitted(SocketModal modal)
    {
        ThreadHelper.RunOnMainThread(() =>
        {
            OnModalSubmitted(modal);
            
            if (!string.IsNullOrEmpty(modal.Data.CustomId))
            {
                if (modals.TryRemove(modal.Data.CustomId, out var modalHandler))
                {
                    Task.Run(async () => await modalHandler(modal));
                }
                else if (menus.TryGetValue(modal.Data.CustomId, out var menuHandler))
                {
                    Task.Run(async () =>
                    {
                        var task = menuHandler(modal);

                        if (task == null)
                        {
                            menus.TryRemove(modal.Data.CustomId, out _);
                        }
                        else
                        {
                            await task;
                        }
                    });
                }
            }
        });

        return Task.CompletedTask;
    }

    private Task _ButtonExecuted(SocketMessageComponent comp)
    {
        ThreadHelper.RunOnMainThread(() =>
        {
            if (!string.IsNullOrEmpty(comp.Data.CustomId))
            {
                if (buttons.TryGetValue(comp.Data.CustomId, out var buttonHandler))
                {
                    Task.Run(async () =>
                    {
                        var task = buttonHandler(comp);

                        if (task == null)
                        {
                            buttons.TryRemove(comp.Data.CustomId, out _);
                        }
                        else
                        {
                            await task;
                        }
                    });
                }
            }
        });

        return Task.CompletedTask;
    }

    [HelpWriter]
    private static void WriteHelp(StringBuilder sb)
    {
        sb.AppendLine("= Discord =");
        sb.AppendLine("-DiscordReconnectOnDisconnect: Automatically reconnects to Discord if disconnected.");
        sb.AppendLine("-DiscordLogRawGateway: Enables raw gateway payload logging.");
        sb.AppendLine("-DiscordLogIntentWarns: Enables logging of intent warnings.");
        sb.AppendLine("-DiscordLogUnknownDispatch: Enables logging of unknown dispatch warnings.");
        sb.AppendLine("-DiscordMessageCacheSize: Sets the message cache size.");
        sb.AppendLine("-DiscordLogLevel: Sets the log level.");
        sb.AppendLine("-DiscordDisableLog: Disables logging.");
    }
}