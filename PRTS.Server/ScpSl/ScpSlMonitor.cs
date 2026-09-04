using NiveraAPI.IO.Configs;

using System.Collections.Concurrent;

using PRTS.Core.Attributes;
using PRTS.Discord.MessageCache;

using Discord;
using Discord.WebSocket;

using PRTS.Main;

using NiveraAPI.Logs;
using NiveraAPI.Utilities;

using PRTS.ScpSl.Modules.Plugins;

using NiveraAPI.Extensions;

using PRTS.Discord;
using PRTS.ScpSl.Discord;
using PRTS.Punishments;
using PRTS.Profiles;
using PRTS.Extensions;

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
                var pluginsBuilder = Pools.PoolStringBuilder();

                foreach (var plugin in Server.PluginManagerModule.Plugins)
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
                        playersBuilder.AppendLine($"- [{kvp.Value.Country}] **{kvp.Value.Nick}** ({kvp.Value.Ping} ms)");
                    }
                    else
                    {
                        if (kvp.Value.Profile.Value.DiscordId != 0)
                        {
                            playersBuilder.AppendLine($"- [{kvp.Value.Country}] **{kvp.Value.Nick}** ({kvp.Value.Ping} ms, level {kvp.Value.Level?.Level ?? 0}) - <@{kvp.Value.Profile.Value.DiscordId}>");

                        }
                        else
                        {
                            playersBuilder.AppendLine($"- [{kvp.Value.Country}] **{kvp.Value.Nick}** ({kvp.Value.Ping} ms, level {kvp.Value.Level?.Level ?? 0})");
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

        commandRow.WithButton("Příkaz", $"Monitor_{Alias}_Command", ButtonStyle.Primary, Emoji.Parse(":calling:"));
        commandRow.WithButton("Vypnout", $"Monitor_{Alias}_Shutdown", ButtonStyle.Danger, Emoji.Parse(":octagonal_sign:"));
        commandRow.WithButton("Restartovat", $"Monitor_{Alias}_Restart", ButtonStyle.Success, Emoji.Parse(":arrows_counterclockwise:"));

        punishmentRow.WithButton("Kick", $"Monitor_{Alias}_Kick", ButtonStyle.Primary, Emoji.Parse(":boot:"));
        punishmentRow.WithButton("Ban", $"Monitor_{Alias}_Ban", ButtonStyle.Danger, Emoji.Parse(":no_entry:"));
        punishmentRow.WithButton("Mute", $"Monitor_{Alias}_Mute", ButtonStyle.Secondary, Emoji.Parse(":mute:"));
        punishmentRow.WithButton("Warn", $"Monitor_{Alias}_Warn", ButtonStyle.Secondary, Emoji.Parse(":warning:"));

        builder.WithActionRow(commandRow);
        builder.WithActionRow(punishmentRow);
    }

    private void OnServerCommand(SocketMessageComponent component)
    {
        Task.Run(async () =>
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
        });
    }

    private void OnServerShutdown(SocketMessageComponent component)
    {
        Task.Run(async () =>
        {
            if (!component.HasPermission("ShutdownServer"))
            {
                await component.RespondAsync(":x: | Nemáte oprávnění k vypnutí serveru", ephemeral: true);
                return;
            }

            await component.DeferAsync(true);
            await ThreadHelper.RunOnMainThread(() => Server.CallRpcShutdown());

            await component.FollowupAsync(":white_check_mark: | Server byl úspěšně vypnut", ephemeral: true);
        });
    }

    private void OnServerRestart(SocketMessageComponent component)
    {
        Task.Run(async () =>
        {
            if (!component.HasPermission("RestartServer"))
            {
                await component.RespondAsync(":x: | Nemáte oprávnění k restartování serveru", ephemeral: true);
                return;
            }

            await component.DeferAsync(true);
            await ThreadHelper.RunOnMainThread(() => Server.CallRpcRestart());

            await component.FollowupAsync(":white_check_mark: | Server byl úspěšně restartován", ephemeral: true);
        });
    }

    private void OnServerKick(SocketMessageComponent component)
    {
        Task.Run(async () =>
        {
            if (!component.HasPermission("KickPlayers"))
            {
                await component.RespondAsync(":x: | Nemáte oprávnění k vykopnutí hráče", ephemeral: true);
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

            async Task Response(SocketModal modal)
            {
                if (!modal.TryGetComponent("KickReason", out var reasonInput))
                {
                    await modal.RespondAsync(":x: | Nebyl nalezen text input pro důvod vykopnutí", ephemeral: true);
                    return;
                }

                if (!modal.TryGetComponent("KickPlayerSelect", out var playerSelect))
                {
                    await modal.RespondAsync(":x: | Nebyl nalezen select menu pro výběr hráče", ephemeral: true);
                    return;
                }

                var players = Server.Players
                    .Where(kvp => playerSelect.Values.Contains(kvp.Key))
                    .Select(kvp => kvp.Key);

                if (players.Count() < 1)
                {
                    await modal.RespondAsync(":x: | Nebyl vybrán žádný hráč k vykopnutí", ephemeral: true);
                    return;
                }

                var reason = reasonInput.Value;

                await modal.DeferAsync(true);
                await ThreadHelper.RunOnMainThread(() => Server.CallRpcKick(reason, players));

                await modal.FollowupAsync($":white_check_mark: | Hráči `{string.Join(", ", players)}` byli úspěšně vykopnuti ze serveru s důvodem: `{reason}`", ephemeral: true);
            }

            await component.RespondMenuAsync(builder, Response);
        });
    }

    private void OnServerBan(SocketMessageComponent component)
    {
        Task.Run(async () =>
        {
            if (!component.HasPermission("BanPlayers"))
            {
                await component.RespondAsync(":x: | Nemáte oprávnění k zabanování hráče", ephemeral: true);
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

            async Task Response(SocketModal modal)
            {
                if (!modal.TryGetComponent("BanReason", out var reasonInput))
                {
                    await modal.RespondAsync(":x: | Nebyl nalezen text input pro důvod zabanování", ephemeral: true);
                    return;
                }

                if (!modal.TryGetComponent("BanDuration", out var durationInput))
                {
                    await modal.RespondAsync(":x: | Nebyl nalezen text input pro délku banování", ephemeral: true);
                    return;
                }

                if (!modal.TryGetComponent("BanPlayerSelect", out var playerSelect))
                {
                    await modal.RespondAsync(":x: | Nebyl nalezen select menu pro výběr hráče", ephemeral: true);
                    return;
                }

                if (!TimeUtils.TryParseTime(durationInput.Value, out var duration))
                {
                    await modal.RespondAsync(":x: | Délka banování není ve správném formátu", ephemeral: true);
                    return;
                }

                var player = Server.Players.FirstOrDefault(x => playerSelect.Values.Contains(x.Key));

                if (player.Key == null)
                {
                    await modal.RespondAsync(":x: | Nebyl vybrán žádný hráč k zabanování", ephemeral: true);
                    return;
                }

                if (player.Value.Profile == null)
                {
                    await modal.RespondAsync(":x: | Vybraný hráč nemá profil, nelze ho zabanovat", ephemeral: true);
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

                var button = await modal.AwaitButtonsAsync(embed, [confirmButton, cancelButton], true, TimeSpan.FromMinutes(2));

                if (button == null)
                {
                    await modal.FollowupAsync(":x: | Banování hráče bylo zrušeno (čas vypršel)", ephemeral: true);
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

            await component.RespondMenuAsync(builder, Response);
        });
    }

    private void OnServerMute(SocketMessageComponent component)
    {
        Task.Run(async () =>
        {
            if (!component.HasPermission("MutePlayers"))
            {
                await component.RespondAsync(":x: | Nemáte oprávnění ke ztlumení hráče", ephemeral: true);
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

            async Task Response(SocketModal modal)
            {
                if (!modal.TryGetComponent("MuteReason", out var reasonInput))
                {
                    await modal.RespondAsync(":x: | Nebyl nalezen text input pro důvod ztlumení", ephemeral: true);
                    return;
                }

                if (!modal.TryGetComponent("MuteDuration", out var durationInput))
                {
                    await modal.RespondAsync(":x: | Nebyl nalezen text input pro délku ztlumení", ephemeral: true);
                    return;
                }

                if (!modal.TryGetComponent("MutePlayerSelect", out var playerSelect))
                {
                    await modal.RespondAsync(":x: | Nebyl nalezen select menu pro výběr hráče", ephemeral: true);
                    return;
                }

                if (!TimeUtils.TryParseTime(durationInput.Value, out var duration))
                {
                    await modal.RespondAsync(":x: | Délka ztlumení není ve správném formátu", ephemeral: true);
                    return;
                }

                var player = Server.Players.FirstOrDefault(x => playerSelect.Values.Contains(x.Key));

                if (player.Key == null)
                {
                    await modal.RespondAsync(":x: | Nebyl vybrán žádný hráč k ztlumení", ephemeral: true);
                    return;
                }

                if (player.Value.Profile == null)
                {
                    await modal.RespondAsync(":x: | Vybraný hráč nemá profil, nelze ho ztlumit", ephemeral: true);
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

                var button = await modal.AwaitButtonsAsync(embed, [confirmButton, cancelButton], true, TimeSpan.FromMinutes(2));

                if (button == null)
                {
                    await modal.FollowupAsync(":x: | Ztlumení hráče bylo zrušeno (čas vypršel)", ephemeral: true);
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

            await component.RespondMenuAsync(builder, Response);
        });
    }

    private void OnServerWarn(SocketMessageComponent component)
    {
        Task.Run(async () =>
        {
            if (!component.HasPermission("WarnPlayers"))
            {
                await component.RespondAsync(":x: | Nemáte oprávnění k varování hráče", ephemeral: true);
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