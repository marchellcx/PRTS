using Discord;
using Discord.Rest;
using Discord.WebSocket;

using NiveraAPI.Logs;
using NiveraAPI.Extensions;

using NiveraAPI.IO.Configs;
using NiveraAPI.IO.Storage;

using PRTS.Database;
using PRTS.Database.Attributes;
using PRTS.Database.Serializers;

using PRTS.Discord;
using PRTS.Discord.MessageCache;

using PRTS.Main;
using PRTS.Extensions;

using PRTS.Profiles;
using PRTS.Profiles.Objects;

using PRTS.Punishments.Enums;
using PRTS.Punishments.Objects;

using PRTS.ScpSl;
using PRTS.ScpSl.Modules.Reports;
using PRTS.ScpSl.Modules.Punishments;

using Fergun.Interactive;

using NiveraAPI.Utilities;

namespace PRTS.Punishments;

/// <summary>
/// 
/// </summary>
public static class PunishmentManager
{
    private static volatile Dictionary<PunishmentType, ulong> channels = new()
    {
        [PunishmentType.Warn] = 0,
        [PunishmentType.Mute] = 0,
        [PunishmentType.Ban] = 0,
    };
    
    private static volatile LogSink log = LogManager.GetSource("Punishments", "Manager");

    /// <summary>
    /// The ID of the channel where punishment messages are posted.
    /// </summary>
    [Config("punishments", "channel-id", "The ID of the channel where punishment messages are posted.")]
    public static Dictionary<PunishmentType, ulong> Channels
    {
        get => channels;
        set => channels = value;
    }
    
    /// <summary>
    /// Represents the storage directory for managing punishment data within the system.
    /// </summary>
    [DbStorage("punishments", typeof(ByteReaderWriterSerializer<PunishmentInfo>))]
    public static volatile StorageDirectory Punishments;

    /// <summary>
    /// Retrieves a list of punishments that satisfy a specified condition from the stored punishments.
    /// </summary>
    /// <param name="predicate">
    /// A function that defines the condition a punishment must satisfy to be included in the result.
    /// </param>
    /// <returns>
    /// A list of <see cref="PunishmentInfo"/> objects that meet the specified condition. If no punishments match, returns an empty list.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the <paramref name="predicate"/> parameter is null.
    /// </exception>
    public static List<PunishmentInfo> GetPunishments(Predicate<PunishmentInfo> predicate)
    {
        if (predicate == null)
            throw new ArgumentNullException(nameof(predicate));
        
        var list = new List<PunishmentInfo>();

        if (Punishments != null)
        {
            foreach (var kvp in Punishments.Values)
            {
                if (kvp.Value is not StorageValue<PunishmentInfo> castValue)
                    continue;
                
                if (predicate(castValue.Value))
                    list.Add(castValue.Value);
            }
        }

        return list;
    }

    /// <summary>
    /// Attempts to find a punishment that matches a given predicate from the stored punishments.
    /// </summary>
    /// <param name="predicate">
    /// A function defining the condition that a punishment must satisfy.
    /// </param>
    /// <param name="punishment">
    /// When the method returns, contains the punishment that meets the specified condition,
    /// if such a punishment is found; otherwise, the value is null. This parameter is passed uninitialized.
    /// </param>
    /// <returns>
    /// true if a matching punishment is found; otherwise, false.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the <paramref name="predicate"/> parameter is null.
    /// </exception>
    public static bool TryGetPunishment(Predicate<PunishmentInfo> predicate,
        out StorageValue<PunishmentInfo> punishment)
    {
        if (predicate == null)
            throw new ArgumentNullException(nameof(predicate));

        punishment = null!;

        if (Punishments == null)
            return false;

        foreach (var kvp in Punishments.Values)
        {
            if (kvp.Value is not StorageValue<PunishmentInfo> value)
                continue;
            
            if (!predicate(value.Value))
                continue;

            punishment = value;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Revokes an active punishment and updates its status to revoked.
    /// </summary>
    /// <param name="punishment">
    /// The punishment to be revoked. Must be active and not null.
    /// </param>
    /// <param name="staff">
    /// The profile of the staff member revoking the punishment. Cannot be null.
    /// </param>
    /// <param name="reason">
    /// The reason for revoking the punishment. Cannot be null or empty.
    /// </param>
    /// <returns>
    /// A boolean value indicating whether the punishment was successfully revoked.
    /// Returns false if the punishment is not active.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the <paramref name="punishment"/>, <paramref name="staff"/>,
    /// or <paramref name="reason"/> parameter is null or, in the case of
    /// <paramref name="reason"/>, empty.
    /// </exception>
    public static bool RevokePunishment(StorageValue<PunishmentInfo> punishment, ProfileInfo staff, string reason)
    {
        if (punishment == null)
            throw new ArgumentNullException(nameof(punishment));

        if (staff == null)
            throw new ArgumentNullException(nameof(staff));
        
        if (string.IsNullOrEmpty(reason))
            throw new ArgumentNullException(nameof(reason));

        if (!punishment.Value.IsActive)
            return false;
        
        punishment.Value.Status = PunishmentStatus.Revoked;
        punishment.Value.RevokedAt = DateTime.UtcNow;
        punishment.Value.RevokedId = staff.Id;
        punishment.Value.RevokedReason = reason;

        punishment.IsDirty = true;
        
        Task.Run(async () => await UpdatePunishmentMessageAsync(punishment.Value));

        BroadcastRemovedPunishment(punishment);        
        return true;
    }

    /// <summary>
    /// Issues a punishment to a target profile with specified details.
    /// </summary>
    /// <param name="staff">
    /// The staff member issuing the punishment. Must not be null.
    /// </param>
    /// <param name="target">
    /// The profile of the target receiving the punishment. Must not be null.
    /// </param>
    /// <param name="type">
    /// The type of punishment being issued (e.g., Warn, Mute, Ban).
    /// </param>
    /// <param name="expiresAt">
    /// The optional expiration date and time of the punishment. If null, it will default to a minimal value.
    /// </param>
    /// <param name="server">
    /// The identifier of the server where the punishment is issued. Must not be null or empty.
    /// </param>
    /// <param name="report">
    /// The report associated with the punishment, if applicable. This is optional and can be null.
    /// </param>
    /// <param name="reason">
    /// The reason for issuing the punishment. Must not be null or empty.
    /// </param>
    /// <param name="appliedServers">
    /// The optional array of server identifiers where this punishment is applied. Can be null.
    /// </param>
    /// <returns>
    /// A <see cref="PunishmentInfo"/> instance containing details about the issued punishment. Returns null if the punishment cannot be issued.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the <paramref name="staff"/>, <paramref name="target"/>, <paramref name="server"/>, or <paramref name="reason"/> parameter is null or empty.
    /// </exception>
    public static PunishmentInfo? IssuePunishment(ProfileInfo staff, ProfileInfo target, PunishmentType type,
        DateTime? expiresAt, string server, ReportInfo? report, string reason, string[]? appliedServers = null)
    {
        if (staff == null)
            throw new ArgumentNullException(nameof(staff));

        if (target == null)
            throw new ArgumentNullException(nameof(target));

        if (string.IsNullOrEmpty(server))
            throw new ArgumentNullException(nameof(server));
        
        if (string.IsNullOrEmpty(reason))
            throw new ArgumentNullException(nameof(reason));

        log.Debug($"Issuing punishment to &1{target.Id}&r for &1{reason}&r on &1{server}&r by &1{staff.Id}&r");

        if (Punishments == null)
        {
            log.Warn("Cannot issue punishment: Punishments storage is not initialized!");
            return null;
        }

        var info = new PunishmentInfo
        {
            Id = DbManager.NewId,
            Reason = reason,

            ServerId = server,

            StaffId = staff.Id,
            TargetId = target.Id,

            Type = type,
            Status = PunishmentStatus.Active,

            IssuedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt ?? DateTime.MinValue
        };

        if (report != null)
            info.ReportId = report.Id;

        if (appliedServers != null)
            info.AppliedServers = appliedServers;
        
        var value = Punishments.AddStorageValue(info.Id, () => info);
        
        BroadcastNewPunishment(value);
        
        Task.Run(async () => await PostPunishmentAsync(value));
        return info;
    }

    /// <summary>
    /// Expires an active punishment, marking it as expired and updating its status accordingly.
    /// </summary>
    /// <param name="punishment">The punishment to expire. Must represent an active, non-permanent punishment.</param>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="punishment"/> parameter is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the specified <paramref name="punishment"/> is not active or represents a permanent punishment.</exception>
    public static void ExpirePunishment(StorageValue<PunishmentInfo> punishment)
    {
        if (punishment == null)
            throw new ArgumentNullException(nameof(punishment));

        if (!punishment.Value.IsActive)
            throw new ArgumentException("Punishment is not active", nameof(punishment));
        
        if (punishment.Value.IsPermanent)
            throw new ArgumentException("Cannot expire a permanent punishment", nameof(punishment));
        
        punishment.Value.Status = PunishmentStatus.Expired;
        punishment.IsDirty = true;

        Task.Run(async () => await UpdatePunishmentMessageAsync(punishment.Value));     
        
        Utils.RequireMain(() => BroadcastRemovedPunishment(punishment));
    }

    /// <summary>
    /// Retrieves a string representation of the staff member associated with a given punishment.
    /// </summary>
    /// <param name="punishment">The punishment for which to retrieve the target's string representation.</param>
    /// <returns>A string representation of the target associated with the punishment.</returns>
    public static string GetTargetString(PunishmentInfo punishment)
    {
        if (!punishment.TryGetTargetProfile(out var targetProfile))
            return "Neznámý hráč";

        if (targetProfile.Value.DiscordId != 0)
            return MentionUtils.MentionUser(targetProfile.Value.DiscordId);
        else
            return targetProfile.Value.GetNickname();
    }

    /// <summary>
    /// Retrieves a string representation of the staff member who revoked a given punishment.
    /// </summary>
    /// <param name="punishment">The punishment for which to retrieve the revoker's string representation.</param>
    /// <returns>A string representation of the staff member who revoked the punishment.</returns>
    public static string GetRevokerString(PunishmentInfo punishment)
    {
        if (!punishment.TryGetRevokedProfile(out var revokerProfile))
            return "Neznámý hráč";

        if (revokerProfile.Value.DiscordId != 0)
            return MentionUtils.MentionUser(revokerProfile.Value.DiscordId);
        else
            return revokerProfile.Value.GetNickname();
    }

    /// <summary>
    /// Retrieves a string representation of the staff member associated with a given punishment.
    /// </summary>
    /// <param name="punishment">The punishment for which to retrieve the staff member's string representation.</param>
    /// <returns>A string representation of the staff member associated with the punishment.</returns>
    public static string GetStaffString(PunishmentInfo punishment)
    {
        if (!punishment.TryGetStaffProfile(out var staffProfile))
            return "Neznámý administrátor";

        if (staffProfile.Value.DiscordId != 0)
            return MentionUtils.MentionUser(staffProfile.Value.DiscordId);
        else
            return staffProfile.Value.GetNickname();
    }

    private static async Task PostPunishmentAsync(StorageValue<PunishmentInfo> punishmentInfo)
    {
        if (MainBotInstance.Instance?.Client == null)
        {
            log.Warn($"Cannot post punishment message for punishment &1{punishmentInfo.Value.Id}&r: Main bot instance is null");
            return;
        }

        if (!Channels.TryGetValue(punishmentInfo.Value.Type, out var channelId)
            || channelId == 0)
        {
            log.Warn($"Cannot post punishment message for punishment &1{punishmentInfo.Value.Id}&r: Channel not configured");
            return;
        }
        
        var channel = await MainBotInstance.Instance.Client.GetChannelAsync(channelId);

        if (channel is not SocketTextChannel textChannel)
        {
            log.Warn($"Cannot update punishment message for punishment &1{punishmentInfo.Value.Id}&r: Channel not found");
            return;
        }

        var builder = new EmbedBuilder();
        
        BuildEmbed(punishmentInfo.Value, builder);
        BuildComponents(punishmentInfo.Value, out var comps);

        try
        {
            var message = await textChannel.SendMessageAsync(embed: builder.Build(), components: comps?.Build());

            if (message != null)
            {
                var cachedId = $"Punishment_{punishmentInfo.Value.Id}";
                var cached = message.CacheMessage($"Punishment_{punishmentInfo.Value.Id}");

                punishmentInfo.Value.CachedMessageId = cachedId;
                punishmentInfo.IsDirty = true;

                log.Debug($"Posted punishment message for punishment &1{punishmentInfo.Value.Id}&r");
            }
            else
            {
                log.Warn($"Could not post punishment message for punishment &1{punishmentInfo.Value.Id}&r");
            }
        }
        catch (Exception ex)
        {
            log.Error($"Error posting punishment message for punishment &1{punishmentInfo.Value.Id}&r: {ex}");
        }
    }

    private static async Task UpdatePunishmentMessageAsync(PunishmentInfo punishmentInfo)
    {
        if (MainBotInstance.Instance?.Client == null)
        {
            log.Warn($"Cannot update punishment message for punishment &1{punishmentInfo.Id}&r: Main bot instance is null");
            return;
        }
        
        if (punishmentInfo.TryGetMessage(out var cached))
        {
            var channel = await MainBotInstance.Instance.Client.GetChannelAsync(cached.ChannelId);

            if (channel is not SocketTextChannel textChannel)
            {
                log.Warn($"Cannot update punishment message for punishment &1{punishmentInfo.Id}&r: Channel not found");
                return;
            }
            
            var message = await textChannel.GetMessageAsync(cached.MessageId);

            if (message is not RestUserMessage userMessage)
            {
                log.Warn($"Cannot update punishment message for punishment &1{punishmentInfo.Id}&r: Message not found");
                return;
            }

            var embed = new EmbedBuilder();
            
            BuildEmbed(punishmentInfo, embed);
            BuildComponents(punishmentInfo, out var comps);
            
            await userMessage.ModifyAsync(msg =>
            {
                msg.Embed = embed.Build();
                msg.Components = comps?.Build();
            });
        }
        else
        {
            log.Warn($"Could not find cached message for punishment &1{punishmentInfo.Id}&r");
        }
    }

    private static void BuildEmbed(PunishmentInfo punishmentInfo, EmbedBuilder builder)
    {
        builder.WithCurrentTimestamp();
        builder.WithFooter($"ID: {punishmentInfo.Id}");

        if (punishmentInfo.Type == PunishmentType.Warn)
        {
            builder.WithColor(Color.Gold);
            builder.WithTitle(":warning: Varování");
        }
        else if (punishmentInfo.Type == PunishmentType.Mute)
        {
            builder.WithColor(Color.Orange);
            builder.WithTitle(":mute: Mute");
        }
        else
        {
            builder.WithColor(Color.Red);
            builder.WithTitle(":hammer: Ban");
        }
        
        if (punishmentInfo.TryGetTargetProfile(out var target))
        {
            builder.AddField(":link: Hráč",
                $"**ID**: {target.Value.Id}\n" +
                $"**UID**: {target.Value.UserId}\n" +
                $"**IP**: {target.Value.GetAddress()}\n" +
                $"**Jméno**: {target.Value.GetNickname()} *(<@{target.Value.DiscordId}>)*");
        }
        else
        {
            builder.AddField(":link: Hráč", $"**ID**: {punishmentInfo.TargetId}");
            
            log.Warn($"Could not find target profile for punishment &1{punishmentInfo.Id}&r");
        }

        if (punishmentInfo.TryGetStaffProfile(out var staff))
        {
            builder.AddField(":link: Administrátor",
                $"**ID**: {staff.Value.Id}\n" +
                $"**Jméno**: {staff.Value.GetNickname()} *(<@{staff.Value.DiscordId}>)*");
        }
        else
        {
            log.Warn($"Could not find staff profile for punishment &1{punishmentInfo.Id}&r");
            
            builder.AddField(":link: Administrátor", $"**ID**: {punishmentInfo.StaffId}");
        }

        if (punishmentInfo.IsRevoked)
        {
            if (punishmentInfo.RevokedId != punishmentInfo.StaffId)
            {
                if (punishmentInfo.TryGetRevokedProfile(out var revoked))
                {
                    builder.AddField(":link: Zrušil",
                        $"**ID**: {revoked.Value.Id}\n" +
                        $"**Jméno**: {revoked.Value.GetNickname()} *(<@{revoked.Value.DiscordId}>)*");
                }
                else
                {
                    log.Warn($"Could not find revoked profile for punishment &1{punishmentInfo.Id}&r");
                }
            }
            else
            {
                builder.AddField(":link: Zrušil",
                    $"**ID**: {staff.Value.Id}\n" +
                    $"**Jméno**: {staff.Value.GetNickname()} *(<@{staff.Value.DiscordId}>)*");
            }
        }

        builder.AddField(":calendar: Datum udělení", punishmentInfo.IssuedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss"));
        builder.AddField(":grey_question: Důvod", $"```{punishmentInfo.Reason}```");

        if (punishmentInfo.IsRevoked)
        {
            builder.AddField(":calendar: Datum zrušení", punishmentInfo.RevokedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss"));

            if (!string.IsNullOrEmpty(punishmentInfo.RevokedReason))
                builder.AddField(":question: Důvod zrušení", $"```{punishmentInfo.RevokedReason}```");
        }
        else
        {
            if (punishmentInfo.IsPermanent)
            {
                builder.AddField(":x: Datum expirace", "**PERMANENTNÍ**");
            }
            else
            {
                if (punishmentInfo.IsExpired)
                {
                    builder.WithColor(Color.DarkGrey);
                    builder.AddField(":white_check_mark: Datum expirace", punishmentInfo.ExpiresAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss"));
                }
                else
                {
                    builder.AddField(":calendar: Datum expirace", punishmentInfo.ExpiresAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss"));

                    if (punishmentInfo.AppliedServers.Length > 0)
                        builder.AddField(":globe_with_meridians: Platné servery", string.Join("\n- ", punishmentInfo.AppliedServers));
                }
            }
        }
    }

    private static void BuildComponents(PunishmentInfo punishmentInfo, out ComponentBuilder? builder)
    {
        if (punishmentInfo.IsRevoked || punishmentInfo.IsExpired)
        {
            builder = null;
            return;
        }

        builder = new();
        builder.WithButton("Zrušit", $"PunishmentRevoke_{punishmentInfo.Id}", ButtonStyle.Danger, emote: Emoji.Parse(":x:"));
    }
    
    private static void BroadcastNewPunishment(StorageValue<PunishmentInfo> punishment)
    {
        if (!punishment.Value.TryGetTargetProfile(out var target))
        {
            log.Warn($"Could not find target profile for punishment &1{punishment.Value.Id}&r");
            return;
        }
        
        foreach (var kvp in ScpSlManager.Servers)
        {
            if (kvp.Value?.Manager != null)
            {
                if (kvp.Value.Manager.TryGetFirstEntity<PunishmentModule>(out var punishmentModule))
                {
                    punishmentModule.CallRpcPunishmentIssued(target.Value.UserId, punishment.Value);
                }
            }
        }
    }

    private static void BroadcastRemovedPunishment(StorageValue<PunishmentInfo> mute)
    {
        if (!mute.Value.TryGetTargetProfile(out var target))
        {
            log.Warn($"Could not find target profile for mute &1{mute.Value.Id}&r");
            return;
        }
        
        var active = GetPunishments(p => p.IsActive && p.TargetId == mute.Value.TargetId);

        foreach (var kvp in ScpSlManager.Servers)
        {
            if (kvp.Value?.Manager != null)
            {
                if (kvp.Value.Manager.TryGetFirstEntity<PunishmentModule>(out var punishmentModule))
                {
                    punishmentModule.CallRpcPunishmentRemoved(target.Value.UserId, active
                        .Where(p => p.AppliedServers.Length < 1 || p.AppliedServers.Contains(kvp.Value.ServerAlias))
                        .ToList());
                }
            }
        }
        
        active.Clear();
    }

    private static void AppendPunishments(ProfileInfo profile, Func<PageBuilder> pageFactory, List<IPageBuilder> pages)
    {
        var punishments = GetPunishments(x => x.TargetId == profile.Id);

        var activePunishments = punishments.Where(x => x.IsActive).ToList();
        var permanentPunishments = activePunishments.Where(x => x.IsPermanent).ToList();

        PageBuilder? punishmentsPage = null;

        if (activePunishments.Count > 0)
        {
            var activeWarns = activePunishments.Where(x => x.Type is PunishmentType.Warn).ToList();

            if (activeWarns.Count > 0)
            {
                var orderedWarns = activeWarns.OrderByDescending(x => x.IssuedAt).ToList();

                var warnDescBuilder = Pools.PoolStringBuilder();
                var warnBuilder = Pools.PoolStringBuilder();

                foreach (var activeWarn in orderedWarns)
                {
                    warnBuilder.Clear();
                    warnBuilder.AppendLine($":warning: Varování");
                    warnBuilder.AppendLine($"**Administrátor**: {GetStaffString(activeWarn)}");
                    warnBuilder.AppendLine($"**Datum udělení**: {activeWarn.IssuedAt.ToLocalTime().ToVeCzechString()}");
                    warnBuilder.AppendLine($"**Důvod**: {activeWarn.Reason}");
                    warnBuilder.AppendLine();

                    if (warnDescBuilder.Length + warnBuilder.Length >= 4096)
                        break;

                    warnDescBuilder.Append(warnBuilder);
                }

                punishmentsPage ??= pageFactory();
                punishmentsPage.WithDescription(warnDescBuilder.ReturnStringBuilderValue());

                warnBuilder.ReturnStringBuilder();
            }

            if (permanentPunishments.TryGetFirst(x => x.Type is PunishmentType.Mute, out var permanentMute))
            {
                punishmentsPage ??= pageFactory();
                punishmentsPage.AddField(":mute: Permanentní mute",
                    $"**Administrátor**: {GetStaffString(permanentMute)}\n" +
                    $"**Datum udělení**: {permanentMute.IssuedAt.ToLocalTime().ToVeCzechString()}\n" +
                    $"**Důvod**: {permanentMute.Reason}");
            }
            else if (activePunishments.TryGetFirst(x => x.Type is PunishmentType.Mute, out var activeMute))
            {
                punishmentsPage ??= pageFactory();
                punishmentsPage.AddField(":mute: Aktivní mute",
                    $"**Administrátor**: {GetStaffString(activeMute)}\n" +
                    $"**Datum udělení**: {activeMute.IssuedAt.ToLocalTime().ToVeCzechString()}\n" +
                    $"**Platnost do**: {activeMute.ExpiresAt.ToLocalTime().ToVeCzechString()}\n" +
                    $"**Důvod**: {activeMute.Reason}");
            }

            if (permanentPunishments.TryGetFirst(x => x.Type is PunishmentType.Ban, out var permanentBan))
            {
                punishmentsPage ??= pageFactory();
                punishmentsPage.AddField(":no_entry: Permanentní ban",
                    $"**Administrátor**: {GetStaffString(permanentBan)}\n" +
                    $"**Datum udělení**: {permanentBan.IssuedAt.ToLocalTime().ToVeCzechString()}\n" +
                    $"**Důvod**: {permanentBan.Reason}");
            }
            else if (activePunishments.TryGetFirst(x => x.Type is PunishmentType.Ban, out var activeBan))
            {
                punishmentsPage ??= pageFactory();
                punishmentsPage.AddField(":no_entry: Aktivní ban",
                    $"**Administrátor**: {GetStaffString(activeBan)}\n" +
                    $"**Datum udělení**: {activeBan.IssuedAt.ToLocalTime().ToVeCzechString()}\n" +
                    $"**Platnost do**: {activeBan.ExpiresAt.ToLocalTime().ToVeCzechString()}\n" +
                    $"**Důvod**: {activeBan.Reason}");
            }
        }
        else if (punishments.Count < 1)
        {
            (pages[0] as PageBuilder)?.AddField(":white_check_mark: Žádné tresty", "Tento hráč nemá žádné tresty.");
        }
        else if (punishments.Count > 0 && activePunishments.Count < 1)
        {
            (pages[0] as PageBuilder)?.AddField(":white_check_mark: Žádné aktivní tresty", $"Tento hráč nemá žádné aktivní tresty *({punishments.Count} expirovaných trestů)*.");
        }
    }

    private static async Task UpdatePunishmentsAsync()
    {
        while (true)
        {
            await Task.Delay(100);

            try
            {
                foreach (var kvp in Punishments.Values)
                {
                    try
                    {
                        if (kvp.Value is not StorageValue<PunishmentInfo> value)
                            continue;

                        if (!value.Value.IsActive)
                            continue;

                        if (value.Value.IsPermanent)
                            continue;

                        if (value.Value.expieryFlagged)
                            continue;

                        if (DateTime.UtcNow >= value.Value.ExpiresAt)
                        {
                            value.Value.expieryFlagged = true;

                            log.Debug($"Flagging punishment &1{value.Value.Id}&r for expiration");

                            ExpirePunishment(value);
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Error($"Error processing punishment &1{kvp.Key}&r:\n{ex}");
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error($"Error updating punishments:\n{ex}");
            }
        }
    }

    internal static void OnButtonExecuted(SocketMessageComponent component)
    {
        if (!component.Data.CustomId.TrySplit('_', true, 2, out var segments))
            return;

        if (segments[0] != "PunishmentRevoke")
            return;

        if (!TryGetPunishment(x => x.Id == segments[1], out var punishment))
        {
            Task.Run(async () =>
            {
                await component.RespondAsync($":x: | Trest s tímto ID nebyl nalezen!");
            });
            
            log.Warn($"Could not find punishment with ID &1{segments[1]}&r");
            return;
        }

        if (!ProfileManager.TryGetProfile(x => x.DiscordId == component.User.Id, out var staff))
        {
            Task.Run(async () =>
            {
                await component.RespondAsync(":x: | Tvůj profil nebyl nalezen!");
            });
            
            log.Warn($"Could not find staff profile for user &1{component.User.Id}&r");
            return;
        }

        var permission = string.Concat(
            "Manage",
            punishment.Value.IsPermanent ? "Permanent" : "Temporary",
            punishment.Value.Type.ToString());
        
        if (!staff.Value.HasPermission(permission))
        {
            Task.Run(async () =>
            {
                await component.RespondAsync($":x: | Chybějící permise: `{permission}`");
            });
            
            log.Warn($"User is missing permission: `{permission}`");
            return;
        }

        if (punishment.Value.IsRevoked)
        {
            Task.Run(async () =>
            {
                await component.RespondAsync(":x: | Tento trest již expiroval.");
            });

            return;
        }

        if (punishment.Value.IsExpired)
        {
            Task.Run(async () =>
            {
                await component.RespondAsync(":x: Tento trest již byl zrušen.");
            });

            return;
        }

        Task.Run(async () =>
        {
            var modal = new ModalBuilder();

            modal.WithTitle("Informace");
            modal.WithCustomId($"PunishmentRevoke_{punishment.Value.Id}");
            modal.AddTextInput("Důvod zrušení", "RevokeReason", TextInputStyle.Short, "Důvod ..", required: true);

            var response = await component.AwaitModalResponseAsync(modal);

            if (response == null
                || !response.Data.Components.TryGetFirst(x => x.CustomId == "RevokeReason", out var comp))
            {
                log.Warn($"Could not find revoke reason component for punishment &1{punishment.Value.Id}&r");
                return new KeyValuePair<SocketModal, string>(response, string.Empty);
            }

            return new(response, comp.Value);
        }).ContinueOnMainThread(kvp =>
        {
            if (!string.IsNullOrEmpty(kvp.Value))
            {
                if (RevokePunishment(punishment, staff.Value, kvp.Value))
                {
                    Task.Run(async () =>
                    {
                        await kvp.Key.RespondAsync(
                            $":white_check_mark: | Trest s ID `{punishment.Value.Id}` byl zrušen!");
                    });
                }
                else
                {
                    Task.Run(async () =>
                    {
                        await kvp.Key.RespondAsync(
                            $":x: | Trest s ID `{punishment.Value.Id}` se nepodařilo zrušit.");
                    });
                }
            }
        });
    }

    private static void StorageInit_Punishments()
    {
        log.Info($"Loaded &1{Punishments.ValueCount}&r punishment(s)");

        ProfileManager.ProfileEmbedBuilder += AppendPunishments;

        Task.Run(UpdatePunishmentsAsync);
    }
}