using NiveraAPI.IO.Configs;

using System.Text;
using System.Collections.Concurrent;

using Discord;
using Discord.WebSocket;

using NiveraAPI.Logs;
using NiveraAPI.Utilities;
using NiveraAPI.Extensions;

using PRTS.Main;
using PRTS.Levels;
using PRTS.Discord;
using PRTS.Profiles;
using PRTS.Extensions;

using PRTS.Punishments;
using PRTS.Punishments.Enums;

using PRTS.ScpSl.Discord;
using PRTS.ScpSl.Modules.Plugins;

using PRTS.Profiles.Objects;
using PRTS.Core.Attributes;
using PRTS.Discord.MessageCache;

using NiveraAPI.IO.Storage;

using Fergun.Interactive;
using Fergun.Interactive.Pagination;

namespace PRTS.ScpSl;

public class ScpSlMonitor
{
    /// <summary>
    /// The configuration for the SCP:SL monitor.
    /// </summary>
    public class MonitorConfig
    {
        /// <summary>
        /// The channel ID for the monitor.
        /// </summary>
        public ulong ChannelId { get; set; } = 0;

        /// <summary>
        /// The alias for the monitor.
        /// </summary>
        public string Alias { get; set; } = string.Empty;

        /// <summary>
        /// The name for the monitor.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The address for the monitor.
        /// </summary>
        public string Address { get; set; } = string.Empty;
    }

    /// <summary>
    /// The list of monitor configurations for the SCP:SL monitor.
    /// </summary>
    [Config("scp-sl", "status-monitor-list", "A list of status monitors..")]
    public static List<MonitorConfig> Configs { get; set; } = new()
    {
        new()
        {
            ChannelId = 0,
            Alias = "default",
            Name = "Default Monitor"
        },

        new()
        {
            ChannelId = 0,
            Alias = "test",
            Name = "Test Monitor"
        }
    };

    /// <summary>
    /// The interval in milliseconds for updating the status of the monitors.
    /// </summary>
    [Config("scp-sl", "status-update-interval", "The interval in milliseconds for updating the status of the monitors.")]
    public static volatile int StatusUpdateInterval = 10000;

    /// <summary>
    /// The threshold for the server's TPS (ticks per second) to be considered low.
    /// </summary>
    [Config("scp-sl", "tps-threshold", "The threshold for the server's TPS (ticks per second) to be considered low.")]
    public static volatile int TpsThreshold = 15;

    /// <summary>
    /// The dictionary of monitors for the SCP:SL monitor.
    /// </summary>
    public static volatile ConcurrentDictionary<string, ScpSlMonitor> Monitors = new();

    private volatile LogSink log = LogManager.GetSource("ScpSl", "Monitor");

    /// <summary>
    /// The channel ID for the SCP:SL monitor.
    /// </summary>
    public ulong ChannelId = 0;

    /// <summary>
    /// The alias for the SCP:SL monitor.
    /// </summary>
    public volatile string Alias = string.Empty;

    /// <summary>
    /// The name for the SCP:SL monitor.
    /// </summary>
    public volatile string Name = string.Empty;

    /// <summary>
    /// The address for the SCP:SL monitor.
    /// </summary>
    public volatile string Address = string.Empty;

    /// <summary>
    /// The server instance for the SCP:SL monitor.
    /// </summary>
    public volatile ScpSlServer? Server;

    /// <summary>
    /// The cached Discord message for the SCP:SL monitor.
    /// </summary>
    public volatile CachedDiscordMessage? Message;

    /// <summary>
    /// Indicates whether the SCP:SL monitor is connected to a server.
    /// </summary>
    public bool IsConnected => Server != null;

    /// <summary>
    /// Indicates whether the SCP:SL monitor is experiencing low TPS (ticks per second).
    /// </summary>
    public bool IsLowTps => Server != null && Server.Tps <= TpsThreshold;

    /// <summary>
    /// Indicates whether the SCP:SL monitor is experiencing high latency.
    /// </summary>
    public bool IsHighLatency => Server != null && Server.LatencyProvider.Latency >= ScpSlLatency.LatencyThreshold;

    /// <summary>
    /// Gets a string representation of the number of players on the server, in the format "current players / maximum players".
    /// </summary>
    public string PlayersString => $"{Server.Players.Count} / {Server.MaxPlayers}";

    /// <summary>
    /// Gets a string representation of the plugins installed on the server, in the format "plugin1, plugin2, plugin3".
    /// </summary>
    public volatile string PluginsString = string.Empty;

    /// <summary>
    /// Starts the update loop for the SCP:SL monitor, which periodically updates the status of the monitor in Discord.
    /// </summary>
    public void Start()
    {
        Task.Run(UpdateAsync);
    }

    /// <summary>
    /// Builds the embed for the SCP:SL monitor, which contains information about the server's status, players, and plugins.
    /// </summary>
    /// <param name="builder">The embed builder to populate with the server's information.</param>
    public void BuildEmbed(EmbedBuilder builder)
    {
        if (Server != null)
        {
            if (IsLowTps || IsHighLatency)
            {
                builder.WithColor(Color.Orange);
                builder.WithAuthor($"⚠️ | {Name}");
            }
            else
            {
                builder.WithColor(Color.Green);
                builder.WithAuthor($"✅ | {Name}");
            }

            builder.WithTitle($"👮 | {PlayersString}");

            if (IsLowTps)
                builder.AddField(":warning: | TPS", Server.Tps);
            else
                builder.AddField(":stopwatch: | TPS", Server.Tps);

            if (Server.IsRoundLocked)
                builder.AddField(":lock: | Stav kola", "Zamčeno");

            if (Server.IsLobbyLocked)
                builder.AddField(":lock: | Stav lobby", "Zamčeno");

            var provider = Server.LatencyProvider;

            if (IsHighLatency)
            {
                builder.AddField(":warning: | Ping",
                    $"**Aktuální**: {provider.HighestLatency}ms\n" +
                    $"**Nejvyšší**: {provider.HighestLatency}ms\n" +
                    $"**Nejnižší**: {provider.LowestLatency}ms\n" +
                    $"**Průměrný**: {provider.AverageLatency}ms");
            }
            else
            {
                builder.AddField(":globe_with_meridians: | Ping",
                    $"**Aktuální**: {provider.HighestLatency}ms\n" +
                    $"**Nejvyšší**: {provider.HighestLatency}ms\n" +
                    $"**Nejnižší**: {provider.LowestLatency}ms\n" +
                    $"**Průměrný**: {provider.AverageLatency}ms");
            }

            if (Server.PluginManagerModule != null)
            {
                var plugins = Server.PluginManagerModule.Plugins.OrderBy(x => x.Name.Length);
                var pluginsBuilder = Pools.PoolStringBuilder();

                foreach (var plugin in plugins)
                    pluginsBuilder.AppendLine($"- **[{plugin.Name}]** v{plugin.Version}");

                builder.AddField(":gear: | Seznam pluginů", pluginsBuilder.ReturnStringBuilderValue());
            }

            if (Server.Players.Count > 0)
            {
                var players = Server.Players.OrderBy(kvp => kvp.Value.Nick.Length);
                var playersBuilder = Pools.PoolStringBuilder();

                foreach (var kvp in players)
                {
                    if (kvp.Value.Profile == null)
                    {
                        playersBuilder.AppendLine($"- **[{kvp.Value.Country}]** {kvp.Value.Nick} *({kvp.Value.Ping} ms)*");
                    }
                    else
                    {
                        var level = LevelManager.GetLevelForXp(kvp.Value.Level?.Experience ?? 0);

                        if (kvp.Value.Profile.Value.DiscordId != 0)
                        {
                            playersBuilder.AppendLine($"- **[{kvp.Value.Country} - {level.Level}]** <@{kvp.Value.Profile.Value.DiscordId}> *({kvp.Value.Ping} ms)*");
                        }
                        else
                        {
                            playersBuilder.AppendLine($"- **[{kvp.Value.Country} - {level.Level}]** {kvp.Value.Nick} *({kvp.Value.Ping} ms)*");
                        }
                    }
                }

                builder.WithDescription(playersBuilder.ReturnStringBuilderValue());
            }
        }
        else
        {
            builder.WithColor(Color.Red);
            builder.WithAuthor($"⛔ | {Name}");
            builder.WithTitle("❌ | Offline!");
        }

        builder.WithFooter(Address);
        builder.WithCurrentTimestamp();
    }

    /// <summary>
    /// Builds the components for the SCP:SL monitor, which includes buttons for server commands and player management.
    /// </summary>
    /// <param name="builder">The component builder to populate with the server's command and punishment buttons.</param>
    public void BuildComponents(ComponentBuilderV2 builder)
    {
        var commandRow = new ActionRowBuilder();
        var punishmentRow = new ActionRowBuilder();
        var punishmentSearchRow = new ActionRowBuilder();
        var levelRow = new ActionRowBuilder();

        commandRow.WithButton("Příkaz", $"Monitor_{Alias}_Command", ButtonStyle.Primary, Emoji.Parse(":calling:"));
        commandRow.WithButton("Vypnout", $"Monitor_{Alias}_Shutdown", ButtonStyle.Danger, Emoji.Parse(":octagonal_sign:"));
        commandRow.WithButton("Restartovat", $"Monitor_{Alias}_Restart", ButtonStyle.Success, Emoji.Parse(":arrows_counterclockwise:"));
        commandRow.WithButton("Restartovat kolo", $"Monitor_{Alias}_RestartRound", ButtonStyle.Success, Emoji.Parse(":arrows_counterclockwise:"));
        commandRow.WithButton("Upravit round / lobby lock", $"Monitor_{Alias}_UpdateLocks", ButtonStyle.Secondary, Emoji.Parse(":lock:"));

        punishmentRow.WithButton("Kick", $"Monitor_{Alias}_Kick", ButtonStyle.Primary, Emoji.Parse(":boot:"));
        punishmentRow.WithButton("Ban", $"Monitor_{Alias}_Ban", ButtonStyle.Danger, Emoji.Parse(":no_entry:"));
        punishmentRow.WithButton("Mute", $"Monitor_{Alias}_Mute", ButtonStyle.Secondary, Emoji.Parse(":mute:"));
        punishmentRow.WithButton("Warn", $"Monitor_{Alias}_Warn", ButtonStyle.Secondary, Emoji.Parse(":warning:"));

        punishmentSearchRow.WithButton("Zrušit trest", $"Monitor_{Alias}_Revoke", ButtonStyle.Secondary, Emoji.Parse(":x:"));
        punishmentSearchRow.WithButton("Vyhledat trest", $"Monitor_{Alias}_Search", ButtonStyle.Secondary, Emoji.Parse(":mag:"));

        levelRow.WithButton("Upravit level", $"Monitor_{Alias}_EditLevel", ButtonStyle.Secondary, Emoji.Parse(":star:"));
        levelRow.WithButton("Resetovat level", $"Monitor_{Alias}_ResetLevel", ButtonStyle.Danger, Emoji.Parse(":x:"));

        builder.WithActionRow(commandRow);
        builder.WithActionRow(punishmentRow);
        builder.WithActionRow(punishmentSearchRow);
        builder.WithActionRow(levelRow);
    }

    private void OnServerCommand(SocketMessageComponent component)
    {
        Task.Run(async () =>
        {
            try
            {
                if (!component.HasPermission("SendRemoteCommands"))
                {
                    await component.RespondAsync(":x: | Nemáte oprávnění k odesílání příkazů na server", ephemeral: true);
                    return;
                }

                var builder = new ModalBuilder();

                builder.WithTitle("Spustit příkaz na serveru");
                builder.WithCustomId($"ModalCommand_{Alias}");

                builder.AddTextInput("Příkaz", "CommandInput", placeholder: "Zadejte příkaz, který chcete spustit na serveru", required: true);

                var modal = await component.AwaitModalResponseAsync(builder, TimeSpan.FromMinutes(2));

                if (modal == null)
                {
                    log.Error($"Received null modal");
                    return;
                }

                if (!modal.Data.Components.TryGetFirst(x => x.CustomId == "CommandInput", out var commandInput))
                {
                    await modal.RespondAsync(":x: | Nebyl nalezen text input v modalu", ephemeral: true);
                    return;
                }

                if (ScpSlCommands.ProhibitedCommands.Any(str => commandInput.Value.StartsWith(str, StringComparison.OrdinalIgnoreCase)))
                {
                    await modal.RespondAsync(":x: | Tento příkaz je zakázán", ephemeral: true);
                    return;
                }

                if (ScpSlCommands.WhitelistedCommands.Length > 0 && !ScpSlCommands.WhitelistedCommands.Any(str => commandInput.Value.StartsWith(str, StringComparison.OrdinalIgnoreCase)))
                {
                    await modal.RespondAsync(":x: | Tento příkaz není na whitelistu", ephemeral: true);
                    return;
                }

                await modal.DeferAsync(true);

                var command = commandInput.Value;
                var response = await Server.AwaitServerEntityResponseAsync<ScpSlServer, string>((s, callback) => s.CallRpcInvokeCommand(command, callback), TimeSpan.FromMinutes(2));

                if (!string.IsNullOrEmpty(response))
                {
                    await modal.FollowupAsync($":white_check_mark: | Příkaz `{command}` byl úspěšně odeslán na server. Odpověď serveru:\n```{response}```", ephemeral: true);
                }
                else
                {
                    await modal.FollowupAsync($":x: | Příkaz `{command}` byl odeslán na server, ale server neodpověděl.", ephemeral: true);
                }
            }
            catch (Exception ex)
            {
                log.Error($"Error while executing command: {ex}");

                await component.RespondAsync($":x: | Došlo k chybě při odesílání příkazu na server: {ex.Message}", ephemeral: true);
            }
        });
    }

    private void OnServerShutdown(SocketMessageComponent component)
    {
        Task.Run(async () =>
        {
            try
            {
                if (!component.HasPermission("ShutdownServer"))
                {
                    await component.RespondAsync(":x: | Nemáte oprávnění k vypnutí serveru", ephemeral: true);
                    return;
                }

                await component.DeferAsync(true);
                await ThreadHelper.RunOnMainThread(() => Server.CallRpcShutdown());

                await component.FollowupAsync(":white_check_mark: | Server byl úspěšně vypnut", ephemeral: true);
            }
            catch (Exception ex)
            {
                log.Error($"Error while shutting down server: {ex}");

                await component.RespondAsync($":x: | Došlo k chybě při vypínání serveru: {ex.Message}", ephemeral: true);
            }
        });
    }

    private void OnServerRestart(SocketMessageComponent component)
    {
        Task.Run(async () =>
        {
            try
            {
                if (!component.HasPermission("RestartServer"))
                {
                    await component.RespondAsync(":x: | Nemáte oprávnění k restartování serveru", ephemeral: true);
                    return;
                }

                await component.DeferAsync(true);
                await ThreadHelper.RunOnMainThread(() => Server.CallRpcRestart());

                await component.FollowupAsync(":white_check_mark: | Server byl úspěšně restartován", ephemeral: true);
            }
            catch (Exception ex)
            {
                log.Error($"Error while restarting server: {ex}");

                await component.RespondAsync($":x: | Došlo k chybě při restartování serveru: {ex.Message}", ephemeral: true);
            }
        });
    }

    private void OnServerRoundRestart(SocketMessageComponent component)
    {
        Task.Run(async () =>
        {
            try
            {
                if (!component.HasPermission("RestartRound"))
                {
                    await component.RespondAsync(":x: | Nemáte oprávnění k restartování kola", ephemeral: true);
                    return;
                }

                await component.DeferAsync(true);
                await ThreadHelper.RunOnMainThread(() => Server.CallRpcRestartRound());

                await component.FollowupAsync(":white_check_mark: | Kolo bylo úspěšně restartováno", ephemeral: true);
            }
            catch (Exception ex)
            {
                log.Error($"Error while restarting round: {ex}");

                await component.RespondAsync($":x: | Došlo k chybě při restartování kola: {ex.Message}", ephemeral: true);
            }
        });
    }

    private void OnServerUpdateLocks(SocketMessageComponent component)
    {
        Task.Run(async () =>
        {
            try
            {
                if (!component.HasPermission("UpdateRoundOrLobbyLock"))
                {
                    await component.RespondAsync(":x: | Nemáte oprávnění k aktualizaci zámků kola nebo lobby", ephemeral: true);
                    return;
                }

                var builder = new ModalBuilder();

                builder.WithTitle("Upravit zámky kola a lobby");
                builder.WithCustomId($"ModalUpdateLocks_{Alias}");

                var roundLockCheckBox = new CheckboxBuilder()
                    .WithCustomId("RoundLockCheckbox")
                    .WithDefaultState(Server.IsRoundLocked);

                var lobbyLockCheckBox = new CheckboxBuilder()
                    .WithCustomId("LobbyLockCheckbox")
                    .WithDefaultState(Server.IsLobbyLocked);

                builder.AddCheckBox("Zámek kola", roundLockCheckBox, "Zaškrtněte, pokud chcete zamknout kolo");
                builder.AddCheckBox("Zámek lobby", lobbyLockCheckBox, "Zaškrtněte, pokud chcete zamknout lobby");

                var response = await component.AwaitModalResponseAsync(builder, TimeSpan.FromMinutes(2));

                if (response == null)
                {
                    log.Error($"Received null modal");
                    return;
                }

                var roundLock = response.Data.Components.TryGetFirst(x => x.CustomId == "RoundLockCheckbox", out var roundLockComponent) && roundLockComponent.BoolValue.HasValue && roundLockComponent.BoolValue.Value;
                var lobbyLock = response.Data.Components.TryGetFirst(x => x.CustomId == "LobbyLockCheckbox", out var lobbyLockComponent) && lobbyLockComponent.BoolValue.HasValue && lobbyLockComponent.BoolValue.Value;

                await response.RespondAsync($":white_check_mark: | Zámky kola a lobby byly úspěšně aktualizovány. Kolo: {(roundLock ? "Zamčeno" : "Odemčeno")}, Lobby: {(lobbyLock ? "Zamčeno" : "Odemčeno")}", ephemeral: true);

                await ThreadHelper.RunOnMainThread(() =>
                {
                    Server.CallRpcSetRoundLock(roundLock);
                    Server.CallRpcSetLobbyLock(lobbyLock);
                });
            }
            catch (Exception ex)
            {
                log.Error($"Error while updating locks: {ex}");
                await component.RespondAsync($":x: | Došlo k chybě při aktualizaci zámků kola nebo lobby: {ex.Message}", ephemeral: true);
            }
        });
    }

    private void OnServerKick(SocketMessageComponent component)
    {
        Task.Run(async () =>
        {
            try
            {
                if (!component.HasPermission("KickPlayers"))
                {
                    await component.RespondAsync(":x: | Nemáte oprávnění k vykopnutí hráče", ephemeral: true);
                    return;
                }

                if (Server.Players.Count < 1)
                {
                    await component.RespondAsync(":x: | Na serveru není žádný hráč k vykopnutí", ephemeral: true);
                    return;
                }

                var builder = new ModalBuilder();
                var menuBuilder = new SelectMenuBuilder();

                menuBuilder.WithCustomId("KickPlayerSelect");
                menuBuilder.WithMinValues(1);
                menuBuilder.WithRequired(true);

                foreach (var kvp in Server.Players)
                {
                    var optionBuilder = new SelectMenuOptionBuilder();

                    optionBuilder.WithValue(kvp.Key);
                    optionBuilder.WithLabel($"{kvp.Value.Nick} ({kvp.Value.UserId})");

                    menuBuilder.AddOption(optionBuilder);
                }

                builder.WithCustomId($"ServerKick_{Alias}");
                builder.WithTitle("Výběr hráče k vykopnutí");

                builder.AddTextInput("Důvod", "KickReason", placeholder: "Zadejte důvod vykopnutí hráče", required: true);
                builder.AddSelectMenu("Výběr hráče", menuBuilder, "Vyberte hráče k vykopnutí");

                var response = await component.AwaitModalResponseAsync(builder, TimeSpan.FromMinutes(2));

                if (!response.TryGetComponent("KickReason", out var reasonInput))
                {
                    await response.RespondAsync(":x: | Nebyl nalezen text input pro důvod vykopnutí", ephemeral: true);
                    return;
                }

                if (!response.TryGetComponent("KickPlayerSelect", out var playerSelect))
                {
                    await response.RespondAsync(":x: | Nebyl nalezen select menu pro výběr hráče", ephemeral: true);
                    return;
                }

                var players = Server.Players
                    .Where(kvp => playerSelect.Values.Contains(kvp.Key))
                    .Select(kvp => kvp.Key);

                if (players.Count() < 1)
                {
                    await response.RespondAsync(":x: | Nebyl vybrán žádný hráč k vykopnutí", ephemeral: true);
                    return;
                }

                var reason = reasonInput.Value;

                await response.DeferAsync(true);
                await ThreadHelper.RunOnMainThread(() => Server.CallRpcKick(reason, players));

                await response.FollowupAsync($":white_check_mark: | Hráči `{string.Join(", ", players)}` byli úspěšně vykopnuti ze serveru s důvodem: `{reason}`", ephemeral: true);    
            }
            catch (Exception ex)
            {
                log.Error($"Error while kicking player: {ex}");
                await component.RespondAsync($":x: | Došlo k chybě při vykopávání hráče: {ex.Message}", ephemeral: true);
            }
        });
    }

    private void OnServerBan(SocketMessageComponent component)
    {
        Task.Run(async () =>
        {
            try
            {
                if (!component.HasPermission("BanPlayers"))
                {
                    await component.RespondAsync(":x: | Nemáte oprávnění k zabanování hráče", ephemeral: true);
                    return;
                }

                if (Server.Players.Count < 1)
                {
                    await component.RespondAsync(":x: | Na serveru není žádný hráč k zabanování", ephemeral: true);
                    return;
                }

                if (!ProfileManager.TryGetProfile(x => x.DiscordId == component.User.Id, out var staffProfile))
                {
                    await component.RespondAsync(":x: | Nebyl nalezen profil pro vaše Discord ID", ephemeral: true);
                    return;
                }

                var builder = new ModalBuilder();
                var menuBuilder = new SelectMenuBuilder();

                menuBuilder.WithCustomId("BanPlayerSelect");

                menuBuilder.WithMinValues(1);
                menuBuilder.WithMaxValues(1);

                menuBuilder.WithRequired(true);

                foreach (var kvp in Server.Players)
                {
                    var optionBuilder = new SelectMenuOptionBuilder();

                    optionBuilder.WithValue(kvp.Key);
                    optionBuilder.WithLabel($"{kvp.Value.Nick} ({kvp.Value.UserId})");

                    menuBuilder.AddOption(optionBuilder);
                }

                builder.WithCustomId($"ServerBan_{Alias}");
                builder.WithTitle("Výběr hráče k zabanování");

                builder.AddTextInput("Důvod", "BanReason", placeholder: "Zadejte důvod zabanování hráče", required: true);
                builder.AddTextInput("Délka", "BanDuration", placeholder: "Zadejte délku banování hráče (např. 1d, 2h, 30m) (0s je PERMANENTNÍ)", required: true);

                builder.AddSelectMenu("Výběr hráče", menuBuilder, "Vyberte hráče k zabanování");

                var response = await component.AwaitModalResponseAsync(builder, TimeSpan.FromMinutes(2));

                if (!response.TryGetComponent("BanReason", out var reasonInput))
                {
                    await response.RespondAsync(":x: | Nebyl nalezen text input pro důvod zabanování", ephemeral: true);
                    return;
                }

                if (!response.TryGetComponent("BanDuration", out var durationInput))
                {
                    await response.RespondAsync(":x: | Nebyl nalezen text input pro délku banování", ephemeral: true);
                    return;
                }

                if (!response.TryGetComponent("BanPlayerSelect", out var playerSelect))
                {
                    await response.RespondAsync(":x: | Nebyl nalezen select menu pro výběr hráče", ephemeral: true);
                    return;
                }

                if (!TimeUtils.TryParseTime(durationInput.Value, out var duration))
                {
                    await response.RespondAsync(":x: | Délka banování není ve správném formátu", ephemeral: true);
                    return;
                }

                var player = Server.Players.FirstOrDefault(x => playerSelect.Values.Contains(x.Key));

                if (player.Key == null)
                {
                    await response.RespondAsync(":x: | Nebyl vybrán žádný hráč k zabanování", ephemeral: true);
                    return;
                }

                if (player.Value.Profile == null)
                {
                    await response.RespondAsync(":x: | Vybraný hráč nemá profil, nelze ho zabanovat", ephemeral: true);
                    return;
                }

                var reason = reasonInput.Value;

                var confirmButton = new ButtonBuilder()
                    .WithLabel("Potvrdit")
                    .WithCustomId($"ConfirmBan_{Alias}_{player.Key}")
                    .WithStyle(ButtonStyle.Danger)
                    .WithEmote(Emoji.Parse(":white_check_mark:"));

                var cancelButton = new ButtonBuilder()
                    .WithLabel("Zrušit")
                    .WithCustomId($"CancelBan_{Alias}_{player.Key}")
                    .WithStyle(ButtonStyle.Secondary)
                    .WithEmote(Emoji.Parse(":x:"));

                var embed = new EmbedBuilder()
                    .WithTitle("Potvrzení banování hráče")
                    .AddField(":man_detective: Hráč", $"{player.Value.Nick} ({player.Key})")
                    .AddField(":question: Důvod", reason)
                    .AddField(":hourglass: Délka", duration <= TimeSpan.Zero ? "PERMANENTNÍ" : duration.ToFullCzechString())
                    .WithColor(Color.Orange);

                var button = await response.AwaitButtonsAsync(embed, [confirmButton, cancelButton], true, TimeSpan.FromMinutes(2));

                if (button == null)
                {
                    await response.FollowupAsync(":x: | Banování hráče bylo zrušeno (čas vypršel)", ephemeral: true);
                    return;
                }

                if (button.Data.CustomId.StartsWith("CancelBan"))
                {
                    await button.RespondAsync(":x: | Banování hráče bylo zrušeno", ephemeral: true);
                    return;
                }

                var info = PunishmentManager.IssuePunishment(staffProfile.Value, player.Value.Profile.Value, Punishments.Enums.PunishmentType.Ban, duration <= TimeSpan.Zero ? null : DateTime.UtcNow.Add(duration), "Discord", null, reason);

                if (info != null)
                    await button.RespondAsync($":white_check_mark: | Hráči `{player.Key}` byl úspěšně udělen ban ze serveru s důvodem: `{reason}` (ID: `{info.Id}`)", ephemeral: true);
                else
                    await button.RespondAsync($":x: | Nepodařilo se udělit ban hráči `{player.Key}`", ephemeral: true);
            }
            catch (Exception ex)
            {
                log.Error($"Error while banning player: {ex}");
                await component.RespondAsync($":x: | Došlo k chybě při banování hráče: {ex.Message}", ephemeral: true);
            }
        });
    }

    private void OnServerMute(SocketMessageComponent component)
    {
        Task.Run(async () =>
        {
            try
            {
                if (!component.HasPermission("MutePlayers"))
                {
                    await component.RespondAsync(":x: | Nemáte oprávnění ke ztlumení hráče", ephemeral: true);
                    return;
                }

                if (Server.Players.Count < 1)
                {
                    await component.RespondAsync(":x: | Na serveru není žádný hráč k ztlumení", ephemeral: true);
                    return;
                }

                if (!ProfileManager.TryGetProfile(x => x.DiscordId == component.User.Id, out var staffProfile))
                {
                    await component.RespondAsync(":x: | Nebyl nalezen profil pro vaše Discord ID", ephemeral: true);
                    return;
                }

                var builder = new ModalBuilder();
                var menuBuilder = new SelectMenuBuilder();

                menuBuilder.WithCustomId("MutePlayerSelect");

                menuBuilder.WithMinValues(1);
                menuBuilder.WithMaxValues(1);

                menuBuilder.WithRequired(true);

                foreach (var kvp in Server.Players)
                {
                    var optionBuilder = new SelectMenuOptionBuilder();

                    optionBuilder.WithValue(kvp.Key);
                    optionBuilder.WithLabel($"{kvp.Value.Nick} ({kvp.Value.UserId})");

                    menuBuilder.AddOption(optionBuilder);
                }

                builder.WithCustomId($"ServerMute_{Alias}");
                builder.WithTitle("Výběr hráče k ztlumení");

                builder.AddTextInput("Důvod", "MuteReason", placeholder: "Zadejte důvod ztlumení hráče", required: true);
                builder.AddTextInput("Délka", "MuteDuration", placeholder: "Zadejte délku ztlumení hráče (např. 1d, 2h, 30m) (0s je PERMANENTNÍ)", required: true);

                builder.AddSelectMenu("Výběr hráče", menuBuilder, "Vyberte hráče k ztlumení");

                var response = await component.AwaitModalResponseAsync(builder, TimeSpan.FromMinutes(2));

                if (!response.TryGetComponent("MuteReason", out var reasonInput))
                {
                    await response.RespondAsync(":x: | Nebyl nalezen text input pro důvod ztlumení", ephemeral: true);
                    return;
                }

                if (!response.TryGetComponent("MuteDuration", out var durationInput))
                {
                    await response.RespondAsync(":x: | Nebyl nalezen text input pro délku ztlumení", ephemeral: true);
                    return;
                }

                if (!response.TryGetComponent("MutePlayerSelect", out var playerSelect))
                {
                    await response.RespondAsync(":x: | Nebyl nalezen select menu pro výběr hráče", ephemeral: true);
                    return;
                }

                if (!TimeUtils.TryParseTime(durationInput.Value, out var duration))
                {
                    await response.RespondAsync(":x: | Délka ztlumení není ve správném formátu", ephemeral: true);
                    return;
                }

                var player = Server.Players.FirstOrDefault(x => playerSelect.Values.Contains(x.Key));

                if (player.Key == null)
                {
                    await response.RespondAsync(":x: | Nebyl vybrán žádný hráč k ztlumení", ephemeral: true);
                    return;
                }

                if (player.Value.Profile == null)
                {
                    await response.RespondAsync(":x: | Vybraný hráč nemá profil, nelze ho ztlumit", ephemeral: true);
                    return;
                }

                var reason = reasonInput.Value;

                var confirmButton = new ButtonBuilder()
                    .WithLabel("Potvrdit")
                    .WithCustomId($"ConfirmMute_{Alias}_{player.Key}")
                    .WithStyle(ButtonStyle.Danger)
                    .WithEmote(Emoji.Parse(":white_check_mark:"));

                var cancelButton = new ButtonBuilder()
                    .WithLabel("Zrušit")
                    .WithCustomId($"CancelMute_{Alias}_{player.Key}")
                    .WithStyle(ButtonStyle.Secondary)
                    .WithEmote(Emoji.Parse(":x:"));

                var embed = new EmbedBuilder()
                    .WithTitle("Potvrzení ztlumení hráče")
                    .AddField(":man_detective: Hráč", $"{player.Value.Nick} ({player.Key})")
                    .AddField(":question: Důvod", reason)
                    .AddField(":hourglass: Délka", duration <= TimeSpan.Zero ? "PERMANENTNÍ" : duration.ToFullCzechString())
                    .WithColor(Color.Orange);

                var button = await response.AwaitButtonsAsync(embed, [confirmButton, cancelButton], true, TimeSpan.FromMinutes(2));

                if (button == null)
                {
                    await response.FollowupAsync(":x: | Ztlumení hráče bylo zrušeno (čas vypršel)", ephemeral: true);
                    return;
                }

                if (button.Data.CustomId.StartsWith("CancelMute"))
                {
                    await button.RespondAsync(":x: | Ztlumení hráče bylo zrušeno", ephemeral: true);
                    return;
                }

                var info = PunishmentManager.IssuePunishment(staffProfile.Value, player.Value.Profile.Value, Punishments.Enums.PunishmentType.Mute, duration <= TimeSpan.Zero ? null : DateTime.UtcNow.Add(duration), "Discord", null, reason);

                if (info != null)
                    await button.RespondAsync($":white_check_mark: | Hráči `{player.Key}` byl úspěšně udělen mute ze serveru s důvodem: `{reason}` (ID: `{info.Id}`)", ephemeral: true);
                else
                    await button.RespondAsync($":x: | Nepodařilo se udělit mute hráči `{player.Key}`", ephemeral: true);
            }   
            catch (Exception ex)
            {
                log.Error($"Error while muting player: {ex}");
                await component.RespondAsync($":x: | Došlo k chybě při ztlumení hráče: {ex.Message}", ephemeral: true);
            }
        });
    }

    private void OnServerWarn(SocketMessageComponent component)
    {
        Task.Run(async () =>
        {
            try
            {
                if (!component.HasPermission("WarnPlayers"))
                {
                    await component.RespondAsync(":x: | Nemáte oprávnění k varování hráče", ephemeral: true);
                    return;
                }

                if (Server.Players.Count < 1)
                {
                    await component.RespondAsync(":x: | Na serveru není žádný hráč k varování", ephemeral: true);
                    return;
                }

                if (!ProfileManager.TryGetProfile(x => x.DiscordId == component.User.Id, out var staffProfile))
                {
                    await component.RespondAsync(":x: | Nebyl nalezen profil pro vaše Discord ID", ephemeral: true);
                    return;
                }

                var builder = new ModalBuilder();
                var menuBuilder = new SelectMenuBuilder();

                menuBuilder.WithCustomId("WarnPlayerSelect");

                menuBuilder.WithMinValues(1);
                menuBuilder.WithMaxValues(1);

                menuBuilder.WithRequired(true);

                foreach (var kvp in Server.Players)
                {
                    var optionBuilder = new SelectMenuOptionBuilder();

                    optionBuilder.WithValue(kvp.Key);
                    optionBuilder.WithLabel($"{kvp.Value.Nick} ({kvp.Value.UserId})");

                    menuBuilder.AddOption(optionBuilder);
                }

                builder.WithCustomId($"ServerWarn_{Alias}");
                builder.WithTitle("Výběr hráče k varování");

                builder.AddTextInput("Důvod", "WarnReason", placeholder: "Zadejte důvod varování hráče", required: true);
                builder.AddSelectMenu("Výběr hráče", menuBuilder, "Vyberte hráče k varování");

                async Task Response(SocketModal modal)
                {
                    if (!modal.TryGetComponent("WarnReason", out var reasonInput))
                    {
                        await modal.RespondAsync(":x: | Nebyl nalezen text input pro důvod varování", ephemeral: true);
                        return;
                    }

                    if (!modal.TryGetComponent("WarnPlayerSelect", out var playerSelect))
                    {
                        await modal.RespondAsync(":x: | Nebyl nalezen select menu pro výběr hráče", ephemeral: true);
                        return;
                    }

                    var player = Server.Players.FirstOrDefault(x => playerSelect.Values.Contains(x.Key));

                    if (player.Key == null)
                    {
                        await modal.RespondAsync(":x: | Nebyl vybrán žádný hráč k varování", ephemeral: true);
                        return;
                    }

                    if (player.Value.Profile == null)
                    {
                        await modal.RespondAsync(":x: | Vybraný hráč nemá profil, nelze ho varovat", ephemeral: true);
                        return;
                    }

                    var reason = reasonInput.Value;

                    var confirmButton = new ButtonBuilder()
                        .WithLabel("Potvrdit")
                        .WithCustomId($"ConfirmWarn_{Alias}_{player.Key}")
                        .WithStyle(ButtonStyle.Danger)
                        .WithEmote(Emoji.Parse(":white_check_mark:"));

                    var cancelButton = new ButtonBuilder()
                        .WithLabel("Zrušit")
                        .WithCustomId($"CancelWarn_{Alias}_{player.Key}")
                        .WithStyle(ButtonStyle.Secondary)
                        .WithEmote(Emoji.Parse(":x:"));

                    var embed = new EmbedBuilder()
                        .WithTitle("Potvrzení varování hráče")
                        .AddField(":man_detective: Hráč", $"{player.Value.Nick} ({player.Key})")
                        .AddField(":question: Důvod", reason)
                        .WithColor(Color.Orange);

                    var button = await modal.AwaitButtonsAsync(embed, [confirmButton, cancelButton], true, TimeSpan.FromMinutes(2));

                    if (button == null)
                    {
                        await modal.FollowupAsync(":x: | Varování hráče bylo zrušeno (čas vypršel)", ephemeral: true);
                        return;
                    }

                    if (button.Data.CustomId.StartsWith("CancelWarn"))
                    {
                        await button.RespondAsync(":x: | Varování hráče bylo zrušeno", ephemeral: true);
                        return;
                    }

                    var info = PunishmentManager.IssuePunishment(staffProfile.Value, player.Value.Profile.Value, Punishments.Enums.PunishmentType.Warn, null, "Discord", null, reason);

                    if (info != null)
                        await button.RespondAsync($":white_check_mark: | Hráči `{player.Key}` byl úspěšně udělen varování ze serveru s důvodem: `{reason}` (ID: `{info.Id}`)", ephemeral: true);
                    else
                        await button.RespondAsync($":x: | Nepodařilo se udělit varování hráči `{player.Key}`", ephemeral: true);
                }

                await component.RespondMenuAsync(builder, Response);
            }
            catch (Exception ex)
            {
                log.Error($"Error while warning player: {ex}");
                await component.RespondAsync($":x: | Došlo k chybě při varování hráče: {ex.Message}", ephemeral: true);
            }
        });
    }

    private void OnSearchPunishments(SocketMessageComponent component)
    {
        Task.Run(async () =>
        {
            try
            {
                if (!component.HasPermission("SearchPunishments"))
                {
                    await component.RespondAsync(":x: | Nemáte oprávnění k vyhledávání trestů", ephemeral: true);
                    return;
                }

                var builder = new ModalBuilder();

                var checkBoxGroup = new CheckboxGroupBuilder()
                    .WithCustomId("SearchPunishmentFilters")
                    .WithRequired(false);

                checkBoxGroup.AddOption("Pouze aktivní tresty", "SearchOnlyActive", "Zaškrtněte, pokud chcete vyhledávat pouze aktivní tresty", false);
                checkBoxGroup.AddOption("Pouze permanentní tresty", "SearchOnlyPermanent", "Zaškrtněte, pokud chcete vyhledávat pouze permanentní tresty", false);

                var typeMenuBuilder = new SelectMenuBuilder();

                typeMenuBuilder.WithCustomId("SearchPunishmentTypeSelect");

                typeMenuBuilder.WithMinValues(1);
                typeMenuBuilder.WithMaxValues(3);

                typeMenuBuilder.AddOption("Ban", "Ban", "Trest typu Ban", Emoji.Parse(":no_entry:"), true);
                typeMenuBuilder.AddOption("Mute", "Mute", "Trest typu Mute", Emoji.Parse(":mute:"), true);
                typeMenuBuilder.AddOption("Warn", "Warn", "Trest typu Warn", Emoji.Parse(":warning:"), true);

                builder.WithTitle($"Filtry pro hledání trestů");
                builder.WithCustomId($"ModalSearchPunishments_{Alias}");

                builder.AddCheckBoxGroup("Filtry", checkBoxGroup, "Vyberte filtry pro hledání trestů");

                builder.AddTextInput("Hráč (nick nebo ID)", "SearchPlayer", placeholder: "Zadejte nick nebo ID hráče, jehož tresty chcete vyhledat", required: false);
                builder.AddTextInput("Administrátor (nick nebo ID)", "SearchAdmin", placeholder: "Zadejte nick nebo ID administrátora, jehož tresty chcete vyhledat", required: false);
                builder.AddTextInput("Datum", "SearchDate", placeholder: "Zadejte datum od (..yyyy-MM-dd nebo yyyy-MM-dd..yyyy-MM-dd nebo yyyy-MM-dd..)", required: false);

                builder.AddSelectMenu("Vyberte typ trestu", typeMenuBuilder, "Vyberte typ trestu");

                var response = await component.AwaitModalResponseAsync(builder, TimeSpan.FromMinutes(2));

                if (response == null)
                {
                    log.Error($"Received null modal");
                    return;
                }

                StorageValue<ProfileInfo>? playerProfile = null;
                StorageValue<ProfileInfo>? adminProfile = null;

                DateTime? dateFrom = null;
                DateTime? dateTo = null;

                var searchPlayer = response.Data.Components.TryGetFirst(x => x.CustomId == "SearchPlayer", out var playerInput) ? playerInput.Value : null;
                var searchAdmin = response.Data.Components.TryGetFirst(x => x.CustomId == "SearchAdmin", out var adminInput) ? adminInput.Value : null;
                var searchDate = response.Data.Components.TryGetFirst(x => x.CustomId == "SearchDate", out var dateInput) ? dateInput.Value : null;
                var searchCheckboxes = response.Data.Components.TryGetFirst(x => x.CustomId == "SearchPunishmentFilters", out var checkboxGroup) ? checkboxGroup.Values : new List<string>();

                var hasSearchPlayer = !string.IsNullOrWhiteSpace(searchPlayer) && ProfileManager.TryGetProfile(x => x.Nicknames.Any(kvp => kvp.Key.Equals(searchPlayer, StringComparison.OrdinalIgnoreCase)) || x.UserId.Equals(searchPlayer, StringComparison.OrdinalIgnoreCase), out playerProfile);
                var hasSearchAdmin = !string.IsNullOrWhiteSpace(searchAdmin) && ProfileManager.TryGetProfile(x => x.Nicknames.Any(kvp => kvp.Key.Equals(searchAdmin, StringComparison.OrdinalIgnoreCase)) || x.UserId.Equals(searchAdmin, StringComparison.OrdinalIgnoreCase), out adminProfile);
                var hasSearchDate = !string.IsNullOrWhiteSpace(searchDate) && Utils.TryParseDateRange(searchDate, out dateFrom, out dateTo);

                var selectedTypes = response.Data.Components.TryGetFirst(x => x.CustomId == "SearchPunishmentTypeSelect", out var typeSelect) ? typeSelect.Values : new List<string>();
                var selectedTypesParsed = selectedTypes.Select(x => Enum.TryParse<PunishmentType>(x, out var type) ? type : (PunishmentType?)null).Where(x => x.HasValue).Select(x => x.Value).ToList();

                var onlyActive = searchCheckboxes.Contains("SearchOnlyActive");
                var onlyPermanent = searchCheckboxes.Contains("SearchOnlyPermanent");

                var punishments = PunishmentManager.GetPunishments(x =>
                    (!hasSearchPlayer || (x.TargetId == playerProfile.Value.Id)) &&
                    (!hasSearchAdmin || (x.StaffId == adminProfile.Value.Id)) &&
                    (!dateFrom.HasValue || x.IssuedAt >= dateFrom) &&
                    (!dateTo.HasValue || x.IssuedAt <= dateTo) &&
                    (selectedTypesParsed.Count == 0 || selectedTypesParsed.Contains(x.Type)) &&
                    (!onlyActive || x.IsActive) &&
                    (!onlyPermanent || x.IsPermanent)
                );

                if (punishments.Count == 0)
                {
                    await response.RespondAsync(":x: | Nebyly nalezeny žádné tresty odpovídající zadaným filtrům", ephemeral: true);
                    return;
                }

                var pages = new StaticPaginatorBuilder();

                pages.WithUsers(component.User);

                foreach (var punishment in punishments)
                {
                    var page = new PageBuilder();

                    page.AddField(":main_detective: Administrátor", PunishmentManager.GetStaffString(punishment));
                    page.AddField(":bust_in_silhouette: Hráč", PunishmentManager.GetTargetString(punishment));
                    page.AddField(":question: Důvod", punishment.Reason);
                    page.AddField(":hourglass: Datum udělení", punishment.IssuedAt.ToLocalTime().ToVeCzechString());

                    if (punishment.IsActive)
                    {
                        if (punishment.IsPermanent)
                        {
                            page.WithColor(Color.Red);
                            page.AddField(":hourglass: Datum expirace", "PERMANENTNÍ");

                            switch (punishment.Type)
                            {
                                case PunishmentType.Warn:
                                    page.WithTitle($":warning: | Permanentní varování");
                                    break;

                                case PunishmentType.Mute:
                                    page.WithTitle($":mute: | Permanentní ztlumení");
                                    break;

                                case PunishmentType.Ban:
                                    page.WithTitle($":no_entry: | Permanentní ban");
                                    break;
                            }
                        }
                        else
                        {
                            page.WithColor(Color.Orange);
                            page.AddField(":hourglass: Datum expirace", punishment.ExpiresAt.ToLocalTime().ToVeCzechString());

                            switch (punishment.Type)
                            {
                                case PunishmentType.Warn:
                                    page.WithTitle($":warning: | Aktivní varování");
                                    break;

                                case PunishmentType.Mute:
                                    page.WithTitle($":mute: | Aktivní ztlumení");
                                    break;

                                case PunishmentType.Ban:
                                    page.WithTitle($":no_entry: | Aktivní ban");
                                    break;
                            }
                        }
                    }
                    else
                    {
                        page.WithColor(Color.DarkGrey);
                        page.AddField(":white_check_mark: Datum expirace", punishment.ExpiresAt.ToLocalTime().ToVeCzechString());

                        switch (punishment.Type)
                        {
                            case PunishmentType.Warn:
                                page.WithTitle($":warning: | Expirované varování");
                                break;

                            case PunishmentType.Mute:
                                page.WithTitle($":mute: | Expirované ztlumení");
                                break;

                            case PunishmentType.Ban:
                                page.WithTitle($":no_entry: | Expirovaný ban");
                                break;
                        }
                    }

                    if (punishment.IsRevoked)
                    {
                        page.WithColor(Color.DarkGrey);

                        page.AddField(":link: Administrátor zrušení", PunishmentManager.GetRevokerString(punishment));

                        page.AddField(":x: Datum zrušení", punishment.RevokedAt.ToLocalTime().ToVeCzechString());
                        page.AddField(":x: Důvod zrušení", punishment.RevokedReason);
                    }

                    page.WithFooter($"ID: {punishment.Id}");
                    pages.AddPage(page);
                }

                await MainBotInstance.Instance.Fergun.SendPaginatorAsync(pages.Build(), response, null, InteractionResponseType.ChannelMessageWithSource, true);
            }
            catch (Exception ex)
            {
                log.Error($"Error while searching punishments: {ex}");
                await component.RespondAsync($":x: | Došlo k chybě při vyhledávání trestů: {ex.Message}", ephemeral: true);
            }
        });
    }

    private void OnRevokePunishment(SocketMessageComponent component)
    {
        Task.Run(async () =>
        {
            try
            {
                if (!component.HasPermission("RevokePunishments"))
                {
                    await component.RespondAsync(":x: | Nemáte oprávnění k zrušení trestu", ephemeral: true);
                    return;
                }

                if (!ProfileManager.TryGetProfile(x => x.DiscordId == component.User.Id, out var staffProfile))
                {
                    await component.RespondAsync(":x: | Nebyl nalezen profil pro vaše Discord ID", ephemeral: true);
                    return;
                }

                var builder = new ModalBuilder();

                builder.WithTitle("Zrušení trestu hráče");
                builder.WithCustomId($"ModalRevoke_{Alias}");

                builder.AddTextInput("ID trestu", "RevokeId", placeholder: "Zadejte ID trestu, který chcete zrušit", required: true);
                builder.AddTextInput("Důvod", "RevokeReason", placeholder: "Zadejte důvod zrušení trestu", required: true);

                var response = await component.AwaitModalResponseAsync(builder, TimeSpan.FromMinutes(2));

                if (response == null)
                {
                    log.Error($"Received null modal");
                    return;
                }

                if (!response.Data.Components.TryGetFirst(x => x.CustomId == "RevokeId", out var idInput))
                {
                    await response.RespondAsync(":x: | Nebyl nalezen text input pro ID trestu", ephemeral: true);
                    return;
                }

                if (!response.Data.Components.TryGetFirst(x => x.CustomId == "RevokeReason", out var reasonInput))
                {
                    await response.RespondAsync(":x: | Nebyl nalezen text input pro důvod zrušení trestu", ephemeral: true);
                    return;
                }

                if (!PunishmentManager.TryGetPunishment(x => x.Id == idInput.Value, out var punishment))
                {
                    await response.RespondAsync($":x: | Nebyl nalezen trest s ID `{idInput.Value}`", ephemeral: true);
                    return;
                }

                var permission = string.Concat(
                    "Manage",
                    punishment.Value.IsPermanent ? "Permanent" : "Temporary",
                    punishment.Value.Type.ToString());

                if (!response.HasPermission(permission))
                {
                    await response.RespondAsync($":x: | Nemáte oprávnění `{permission}`", ephemeral: true);
                    return;
                }

                if (!punishment.Value.IsActive)
                {
                    await response.RespondAsync($":x: | Trest s ID `{idInput.Value}` již není aktivní", ephemeral: true);
                    return;
                }

                var result = PunishmentManager.RevokePunishment(punishment, staffProfile.Value, reasonInput.Value);

                if (result)
                    await response.RespondAsync($":white_check_mark: | Trest s ID `{idInput.Value}` byl úspěšně zrušen s důvodem: `{reasonInput.Value}`", ephemeral: true);
                else
                    await response.RespondAsync($":x: | Nepodařilo se zrušit trest s ID `{idInput.Value}`", ephemeral: true);
            }
            catch (Exception ex)
            {
                log.Error($"Error while revoking punishment: {ex}");
                await component.RespondAsync($":x: | Došlo k chybě při zrušení trestu: {ex.Message}", ephemeral: true);
            }
        });
    }

    private void OnEditLevel(SocketMessageComponent component)
    {
        Task.Run(async () =>
        {
            if (!component.HasPermission("EditLevel"))
            {
                await component.RespondAsync(":x: | Nemáte oprávnění k úpravě levelu.", ephemeral: true);
                return;
            }

            if (Server.Players.Count < 1)
            {
                await component.RespondAsync(":x: | Na serveru není žádný hráč k upravení levelu", ephemeral: true);
                return;
            }

            var modalBuilder = new ModalBuilder();
            var menuBuilder = new SelectMenuBuilder();

            menuBuilder.WithCustomId("LevelEditPlayerSelect");
            menuBuilder.WithMinValues(1);
            menuBuilder.WithRequired(true);

            foreach (var kvp in Server.Players)
            {
                var optionBuilder = new SelectMenuOptionBuilder();

                optionBuilder.WithValue(kvp.Key);
                optionBuilder.WithLabel($"{kvp.Value.Nick} ({kvp.Value.UserId})");

                menuBuilder.AddOption(optionBuilder);
            }

            modalBuilder.WithCustomId($"ServerLevelEdit_{Alias}");
            modalBuilder.WithTitle("Výběr hráče k upravení levelu");

            modalBuilder.AddTextInput("Počet", "LevelEditAmount", placeholder: "Zadejte počet XP k odebrání / přidání.", required: true);
            modalBuilder.AddSelectMenu("Výběr hráče", menuBuilder, "Vyberte hráče k upravení levelu");

            var response = await component.AwaitModalResponseAsync(modalBuilder, TimeSpan.FromMinutes(2));

            if (!response.TryGetComponent("LevelEditAmount", out var amountInput))
            {
                await response.RespondAsync(":x: | Nebyl nalezen text input pro počet XP k upravení", ephemeral: true);
                return;
            }

            if (!response.TryGetComponent("LevelEditPlayerSelect", out var playerSelect))
            {
                await response.RespondAsync(":x: | Nebyl nalezen select menu pro výběr hráče", ephemeral: true);
                return;
            }

            var players = Server.Players.Where(kvp => playerSelect.Values.Contains(kvp.Key));

            if (!players.Any())
            {
                await response.RespondAsync(":x: | Nebyl vybrán žádný hráč k upravení levelu", ephemeral: true);
                return;
            }

            if (!int.TryParse(amountInput.Value, out var xpAmount))
            {
                await response.RespondAsync(":x: | Počet XP k upravení není ve správném formátu", ephemeral: true);
                return;
            }

            var builder = new StringBuilder();

            foreach (var player in players)
            {
                if (!LevelManager.TryGetLevels(player.Value.UserId, true, out var levelProperty))
                {
                    builder.AppendLine($"{player.Value.Nick} ({player.Value.UserId}): :x: | Hráč nemá žádný level");
                    continue;
                }

                var result = LevelManager.ModifyXpSteam(player.Value.UserId, $"Upraveno {component.User.Username} ({component.User.Id})", xpAmount);
                var level = LevelManager.GetLevelForXp(levelProperty.Experience);

                builder.AppendLine($"- {player.Value.Nick} ({player.Value.UserId}): {result} (Level: {level} [{levelProperty.Experience} XP])");
            }

            await response.RespondAsync(builder.ToString(), ephemeral: true);

            builder.Clear();
        });
    }

    private void OnResetLevel(SocketMessageComponent component)
    {
        Task.Run(async () =>
        {
            if (!component.HasPermission("ResetLevel"))
            {
                await component.RespondAsync(":x: | Nemáte oprávnění k resetování levelu.", ephemeral: true);
                return;
            }

            if (Server.Players.Count < 1)
            {
                await component.RespondAsync(":x: | Na serveru není žádný hráč k resetování levelu.", ephemeral: true);
                return;
            }

            var modalBuilder = new ModalBuilder();
            var menuBuilder = new SelectMenuBuilder();

            menuBuilder.WithCustomId("ResetLevelPlayerSelect");
            menuBuilder.WithMinValues(1);
            menuBuilder.WithRequired(true);

            foreach (var kvp in Server.Players)
            {
                var optionBuilder = new SelectMenuOptionBuilder();

                optionBuilder.WithValue(kvp.Key);
                optionBuilder.WithLabel($"{kvp.Value.Nick} ({kvp.Value.UserId})");

                menuBuilder.AddOption(optionBuilder);
            }

            modalBuilder.WithCustomId($"ServerResetLevel_{Alias}");
            modalBuilder.WithTitle("Výběr hráče k resetování levelu");
            modalBuilder.AddSelectMenu("Výběr hráče", menuBuilder, "Vyberte hráče k resetování levelu");

            var response = await component.AwaitModalResponseAsync(modalBuilder, TimeSpan.FromMinutes(2));

            if (!response.TryGetComponent("ResetLevelPlayerSelect", out var playerSelect))
            {
                await response.RespondAsync(":x: | Nebyl nalezen select menu pro výběr hráče", ephemeral: true);
                return;
            }

            var players = Server.Players.Where(kvp => playerSelect.Values.Contains(kvp.Key));

            if (!players.Any())
            {
                await response.RespondAsync(":x: | Nebyl vybrán žádný hráč k resetování levelu", ephemeral: true);
                return;
            }

            var builder = new StringBuilder();

            foreach (var player in players)
            {
                if (!LevelManager.TryGetLevels(player.Value.UserId, true, out var levelProperty))
                {
                    builder.AppendLine($"{player.Value.Nick} ({player.Value.UserId}): :x: | Hráč nemá žádný level");
                    continue;
                }

                var result = LevelManager.ResetXp(player.Value.UserId, $"Resetováno uživatelem {response.User.Username} ({response.User.Id})");
                var level = LevelManager.GetLevelForXp(levelProperty.Experience);

                builder.AppendLine($"- {(result ? ":white_check_mark:" : ":x:")} {player.Value.Nick} ({player.Value.UserId}): {result} (Level: {level} [{levelProperty.Experience} XP])");
            }

            await response.RespondAsync(builder.ToString(), ephemeral: true);

            builder.Clear();
        });
    }

    private void OnServerConnected()
    {

    }

    private void OnServerDisconnected()
    {

    }

    private async Task<SocketTextChannel?> GetChannelAsync()
    {
        if (MainBotInstance.Instance == null
            || MainBotInstance.Instance.Client == null
            || !MainBotInstance.Instance.IsConnected)
            return null;

        var channelId = Volatile.Read(ref ChannelId);

        if (channelId == 0)
            return null;

        var channel = await MainBotInstance.Instance.Client.GetChannelAsync(channelId);
        return channel as SocketTextChannel;
    }

    private async Task UpdateAsync()
    {
        async Task ResolveOrPostMessage()
        {
            try
            {
                if (Message == null)
                {
                    var channel = await GetChannelAsync();

                    if (channel == null)
                    {
                        log.Error($"Could not find channel with ID &r{ChannelId}&r");
                        return;
                    }

                    var embed = new EmbedBuilder();
                    var components = new ComponentBuilderV2();

                    BuildEmbed(embed);
                    BuildComponents(components);

                    var message = await channel.SendMessageAsync(
                        embed: embed.Build(),
                        components: components.Build());

                    Message = message.CacheMessage($"Status_{Alias}");
                }
                else if (!Message.WasResolved)
                {
                    await Message.TryResolveAsync(Message.GuildId, Message.ChannelId, Message.MessageId);

                    if (!Message.WasResolved)
                    {
                        var channel = await GetChannelAsync();

                        if (channel == null)
                        {
                            log.Error($"Could not find channel with ID &r{ChannelId}&r");
                            return;
                        }

                        if (Server != null)
                        {
                            var embed = new EmbedBuilder();
                            var components = new ComponentBuilderV2();

                            BuildEmbed(embed);
                            BuildComponents(components);

                            

                            var message = await channel.SendMessageAsync(
                                embed: embed.Build(),
                                components: components.Build());

                            Message = message.CacheMessage($"Status_{Alias}");
                        }
                        else
                        {
                            var embed = new EmbedBuilder();

                            BuildEmbed(embed);

                            var message = await channel.SendMessageAsync(embed: embed.Build());

                            Message = message.CacheMessage($"Status_{Alias}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }

        await ResolveOrPostMessage();

        while (true)
        {
            await Task.Delay(StatusUpdateInterval);

            try
            {
                if (Message == null || !Message.WasResolved)
                {
                    await ResolveOrPostMessage();
                    continue;
                }

                if (Server != null)
                {
                    var embed = new EmbedBuilder();
                    var components = new ComponentBuilderV2();

                    BuildEmbed(embed);
                    BuildComponents(components);

                    await Message.Message!.ModifyAsync(msg =>
                    {
                        msg.Embed = embed.Build();
                        msg.Components = components.Build();
                    });
                }
                else
                {
                    var embed = new EmbedBuilder();

                    BuildEmbed(embed);

                    await Message.Message!.ModifyAsync(msg =>
                    {
                        msg.Embed = embed.Build();
                        msg.Components = null;
                    });
                }
            }
            catch (Exception ex)
            {
                await ResolveOrPostMessage();

                log.Error(ex);
            }
        }
    }

    private static void OnIdentified(ScpSlServer server)
    {
        if (Monitors.TryGetValue(server.ServerAlias, out var monitor))
        {
            monitor.Server = server;
            monitor.OnServerConnected();
        }
    }

    private static void OnDestroyed(ScpSlServer server)
    {
        if (Monitors.TryGetValue(server.ServerAlias, out var monitor))
        {
            monitor.OnServerDisconnected();
            monitor.Server = null;
        }
    }

    private static void OnPluginsReceived(PluginManagerModule pluginManagerModule)
    {
        if (Monitors.TryGetValue(pluginManagerModule.Server.ServerAlias, out var monitor))
        {
            var sb = Pools.PoolStringBuilder();

            foreach (var plugin in pluginManagerModule.Plugins)
                sb.AppendLine($"- [{plugin.Version}] **{plugin.Name}** *({plugin.Description})*");

            monitor.PluginsString = sb.ReturnStringBuilderValue();
        }
    }

    internal static void OnButton(SocketMessageComponent component)
    {
        try
        {
            if (!component.Data.CustomId.TrySplit('_', true, 3, out var segments))
                return;

            if (segments[0] != "Monitor")
                return;

            if (!Monitors.TryGetValue(segments[1], out var monitor))
            {
                Task.Run(async () => await component.RespondAsync($":x: | Nebyl nalezen monitor s ID `{segments[1]}`"));
                return;
            }

            if (monitor.Server == null)
            {
                Task.Run(async () => await component.RespondAsync($":x: | Monitor `{segments[1]}` není připojen k žádnému serveru"));
                return;
            }

            switch (segments[2])
            {
                case "Command":
                    monitor.OnServerCommand(component);
                    break;

                case "Shutdown":
                    monitor.OnServerShutdown(component);
                    break;

                case "Restart":
                    monitor.OnServerRestart(component);
                    break;

                case "Kick":
                    monitor.OnServerKick(component);
                    break;

                case "Ban":
                    monitor.OnServerBan(component);
                    break;

                case "Mute":
                    monitor.OnServerMute(component);
                    break;

                case "Warn":
                    monitor.OnServerWarn(component);
                    break;

                case "Revoke":
                    monitor.OnRevokePunishment(component);
                    break;

                case "Search":
                    monitor.OnSearchPunishments(component);
                    break;

                case "RestartRound":
                    monitor.OnServerRoundRestart(component);
                    break;

                case "UpdateLocks":
                    monitor.OnServerUpdateLocks(component);
                    break;

                case "EditLevel":
                    monitor.OnEditLevel(component);
                    break;

                case "ResetLevel":
                    monitor.OnResetLevel(component);
                    break;
            }
        }
        catch (Exception ex)
        {
            Task.Run(async () => await component.RespondAsync($":x: | Došlo k chybě při zpracování interakce s tlačítkem: {ex.Message}", ephemeral: true));
        }
    }

    [Init]
    private static void Initialize()
    {
        ScpSlServer.Destroyed += OnDestroyed;
        ScpSlServer.Identified += OnIdentified;

        PluginManagerModule.PluginsReceived += OnPluginsReceived;

        foreach (var config in Configs)
        {
            var monitor = new ScpSlMonitor();

            monitor.Name = config.Name;
            monitor.Alias = config.Alias;
            monitor.Address = config.Address;
            monitor.ChannelId = config.ChannelId;

            if (CachedDiscordMessageStorage.TryGetMessage($"Status_{config.Alias}", out var message))
            {
                message.TryResolve();

                monitor.Message = message;
            }

            Monitors.TryAdd(config.Alias, monitor);

            monitor.Start();
        }
    }
}