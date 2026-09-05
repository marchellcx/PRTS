using Discord;
using Discord.Rest;
using Discord.WebSocket;
using NiveraAPI.Extensions;
using NiveraAPI.IO.Configs;

using NiveraAPI.IO.Storage;
using NiveraAPI.IO.Serialization;

using NiveraAPI.IO.Network.Entities.Attributes;
using NiveraAPI.Pooling;
using NiveraAPI.Utilities;
using PRTS.Database;
using PRTS.Database.Attributes;
using PRTS.Database.Serializers;

using PRTS.Discord;
using PRTS.Discord.MessageCache;
using PRTS.Extensions;
using PRTS.Main;
using PRTS.Profiles;
using PRTS.Profiles.Objects;

namespace PRTS.ScpSl.Modules.Reports;

/// <summary>
/// Represents a module for handling player reports.
/// </summary>
[ClientType("PRTS.Client.Reports.ReportModule")]
public class ReportModule : ScpSlModule
{
    [IndexField] private static ushort rpc_RpcReportRejected;
    [IndexField] private static ushort rpc_RpcReportResolved;

    /// <summary>
    /// The ID of the channel where reports will be posted.
    /// </summary>
    [Config("reports", "channel-id", "The ID of the channel where reports will be posted.")]
    public static ulong ReportChannelId { get; set; } = 0;

    /// <summary>
    /// The ID of the role to ping when a report is submitted.
    /// </summary>
    [Config("reports", "role-ping-id", "The ID of the role to ping when a report is submitted.")]
    public static ulong ReportRolePingId { get; set; } = 0;

    /// <summary>
    /// The directory containing the player reports.
    /// </summary>
    [DbStorage("player-reports", typeof(ByteReaderWriterSerializer<ReportInfo>))]
    public static volatile StorageDirectory Reports;

    /// <summary>
    /// Retrieves all reports that are currently in the "Waiting" status.
    /// </summary>
    /// <returns>
    /// An array of <see cref="StorageValue{ReportInfo}"/> representing the reports
    /// with a status of <see cref="ReportStatus.Waiting"/>.
    /// </returns>
    public static StorageValue<ReportInfo>[] GetWaitingReports()
        => GetMatchingReports(r => r.Status == ReportStatus.Waiting);

    /// <summary>
    /// Retrieves all reports that are currently in the "Resolved" status.
    /// </summary>
    /// <returns>
    /// An array of <see cref="StorageValue{ReportInfo}"/> representing the reports
    /// with a status of <see cref="ReportStatus.Resolved"/>.
    /// </returns>
    public static StorageValue<ReportInfo>[] GetResolvedReports()
        => GetMatchingReports(r => r.Status == ReportStatus.Resolved);

    /// <summary>
    /// Retrieves all reports that are currently in the "Rejected" status.
    /// </summary>
    /// <returns>
    /// An array of <see cref="StorageValue{ReportInfo}"/> representing the reports
    /// with a status of <see cref="ReportStatus.Rejected"/>.
    /// </returns>
    public static StorageValue<ReportInfo>[] GetRejectedReports()
        => GetMatchingReports(r => r.Status == ReportStatus.Rejected);

    /// <summary>
    /// Retrieves all reports made against the specified player.
    /// </summary>
    /// <param name="playerId">The unique identifier of the player whose reports are to be retrieved. This can be their profile ID, user ID, or Discord ID.</param>
    /// <returns>
    /// An array of <see cref="StorageValue{ReportInfo}"/> containing the reports made against the specified player.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if the <paramref name="playerId"/> is null or empty.
    /// </exception>
    public static StorageValue<ReportInfo>[] GetReportsMadeOn(string playerId)
    {
        if (string.IsNullOrEmpty(playerId))
            throw new ArgumentNullException(nameof(playerId));

        if (!ProfileManager.TryGetProfile(x
                => x.Id == playerId
                   || x.UserId == playerId
                   || x.DiscordId.ToString() == playerId, out var playerProfile))
            return [];

        return GetMatchingReports(r => r.ReportedId == playerProfile.Value.Id);
    }

    /// <summary>
    /// Retrieves all reports submitted by the specified player.
    /// </summary>
    /// <param name="playerId">The unique identifier of the player whose submitted reports are to be retrieved. This can be their profile ID, user ID, or Discord ID.</param>
    /// <returns>
    /// An array of <see cref="StorageValue{ReportInfo}"/> containing the reports submitted by the specified player.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if the <paramref name="playerId"/> is null or empty.
    /// </exception>
    public static StorageValue<ReportInfo>[] GetReportsSubmittedBy(string playerId)
    {
        if (string.IsNullOrEmpty(playerId))
            throw new ArgumentNullException(nameof(playerId));

        if (!ProfileManager.TryGetProfile(x
                => x.Id == playerId
                   || x.UserId == playerId
                   || x.DiscordId.ToString() == playerId, out var playerProfile))
            return [];

        return GetMatchingReports(r => r.ReporterId == playerProfile.Value.Id);
    }

    /// <summary>
    /// Retrieves all reports that have been resolved by the specified staff member.
    /// </summary>
    /// <param name="staffId">The unique identifier of the staff member who resolved the reports. This can be their profile ID, user ID, or Discord ID.</param>
    /// <returns>
    /// An array of <see cref="StorageValue{ReportInfo}"/> containing the reports resolved by the specified staff member.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if the <paramref name="staffId"/> is null or empty.
    /// </exception>
    public static StorageValue<ReportInfo>[] GetReportsResolvedBy(string staffId)
    {
        if (string.IsNullOrEmpty(staffId))
            throw new ArgumentNullException(nameof(staffId));

        if (!ProfileManager.TryGetProfile(x
                => x.Id == staffId
                   || x.UserId == staffId
                   || x.DiscordId.ToString() == staffId, out var staffProfile))
            return [];

        return GetMatchingReports(r => r.Status is ReportStatus.Resolved && r.StaffId == staffProfile.Value.Id);
    }
    
    /// <summary>
    /// Retrieves all reports matching the specified predicate.
    /// </summary>
    /// <param name="predicate">The predicate to evaluate for each report. Only reports satisfying this predicate will be included in the result.</param>
    /// <returns>
    /// An array of <see cref="StorageValue{ReportInfo}"/> containing the reports that match the specified criteria.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if the <paramref name="predicate"/> is null.
    /// </exception>
    public static StorageValue<ReportInfo>[] GetMatchingReports(Predicate<ReportInfo> predicate)
    {
        if (predicate == null)
            throw new ArgumentNullException(nameof(predicate));       
        
        var list = Pools.PoolList<StorageValue<ReportInfo>>();

        if (Reports != null)
        {
            foreach (var kvp in Reports.Values)
            {
                if (kvp.Value is not StorageValue<ReportInfo> castValue)
                    continue;
                
                if (castValue.Value.Status != ReportStatus.Resolved)
                    continue;
                
                if (!predicate(castValue.Value))
                    continue;
                
                list.Add(castValue);               
            }
        }
        
        return ListObjectPool<StorageValue<ReportInfo>>.ReturnToArray(list);
    }
    
    /// <summary>
    /// Attempts to retrieve a report by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the report to retrieve.</param>
    /// <param name="report">The retrieved report, if found.</param>
    /// <returns>
    /// True if the report is successfully found; otherwise, false.
    /// </returns>
    public static bool TryGetReport(string id, out StorageValue<ReportInfo> report)
    {
        report = null!;

        if (Reports == null)
            return false;

        return Reports.TryGetStorageValue(id, out report);
    }

    /// <summary>
    /// Sends a remote procedure call to notify that a report has been resolved.
    /// </summary>
    /// <param name="report">The report that has been resolved.</param>
    /// <param name="resolvingStaff">The profile information of the staff member resolving the report.</param>
    /// <exception cref="ArgumentNullException">Thrown when the provided report or resolvingStaff parameter is null.</exception>
    public void CallRpcReportResolved(ReportInfo report, ProfileInfo resolvingStaff)
    {
        if (report == null)
            throw new ArgumentNullException(nameof(report));

        if (resolvingStaff == null)
            throw new ArgumentNullException(nameof(resolvingStaff));       
        
        SendRemoteCallback(rpc_RpcReportResolved, writer =>
        {
            writer.Write(report);
            writer.Write(resolvingStaff);           
        });
    }
    
    /// <summary>
    /// Sends a remote procedure call to notify that a report has been rejected.
    /// </summary>
    /// <param name="report">The report that has been rejected.</param>
    /// <param name="rejectingStaff">The profile information of the staff member rejecting the report.</param>
    /// <param name="rejectionReason">The reason provided for rejecting the report.</param>
    /// <exception cref="ArgumentNullException">Thrown when any of the provided parameters are null or invalid.</exception>
    public void CallRpcReportRejected(ReportInfo report, ProfileInfo rejectingStaff, string rejectionReason)
    {
        if (report == null)
            throw new ArgumentNullException(nameof(report));

        if (rejectingStaff == null)
            throw new ArgumentNullException(nameof(rejectingStaff));
        
        if (string.IsNullOrEmpty(rejectionReason))
            throw new ArgumentNullException(nameof(rejectionReason));       
        
        SendRemoteCallback(rpc_RpcReportRejected, writer =>
        {
            writer.Write(report);
            writer.Write(rejectingStaff);
            writer.WriteString(rejectionReason);
        });
    }

    /// <summary>
    /// Rejects a player report, marking it as rejected, and logs the action with staff details and rejection reason.
    /// </summary>
    [ServerCmd]
    public void CmdRejectReport(ByteReader reader)
    {
        var reportId = reader.ReadString();
        var staffUserId = reader.ReadString();
        var rejectionReason = reader.ReadString();     
        
        Log.Info($"Rejecting report &1{reportId}&r by &1{staffUserId}&r: &3{rejectionReason}&r");

        if (!TryGetReport(reportId, out var report))
        {
            Log.Warn($"Could not find report &1{reportId}&r");
            return;
        }
        
        if (!ProfileManager.TryGetProfileByUserId(staffUserId, out var staffProfile))
        {
            Log.Error($"Could not find profile for staff &1{staffUserId}&r");
            return;
        }
        
        if (report.Value.Status != ReportStatus.Waiting)
        {
            Log.Warn($"Report &1{reportId}&r is not in the &1Waiting&r status");
            return;
        }
        
        report.Value.Status = ReportStatus.Rejected;
        report.Value.ResolvedAt = DateTime.UtcNow;
        report.Value.StaffId = staffProfile.Value.Id;
        report.Value.StaffResponse = rejectionReason;
        
        report.IsDirty = true;
        
        CallRpcReportRejected(report.Value, staffProfile.Value, rejectionReason);
        
        Log.Info($"Report &1{reportId}&r rejected by &1{staffUserId}&r: &3{rejectionReason}&r");

        if (!ProfileManager.TryGetProfileById(report.Value.ReportedId, out var reportedProfile))
        {
            Log.Error($"Could not find profile for reported &1{report.Value.ReportedId}&r");
            return;
        }

        if (!ProfileManager.TryGetProfileById(report.Value.ReporterId, out var reporterProfile))
        {
            Log.Error($"Could not find profile for reporter &1{report.Value.ReporterId}&r");
            return;
        }
        
        Task.Run(async () =>
        {
            if (report.Value.TryGetMessage(out var message))
            {
                if (!message.WasResolved)
                    await message.TryResolveAsync(message.GuildId, message.ChannelId, message.MessageId);

                if (!message.WasResolved)
                    return;
                
                await UpdateReportAsync(report.Value, reporterProfile.Value, reportedProfile.Value, staffProfile.Value, message.Message!);
            }
        });
    }

    /// <summary>
    /// Resolves a report by updating its status to <see cref="ReportStatus.Resolved"/>.
    /// Additionally, associates the report with the staff member responsible for the resolution
    /// and records the timestamp of the resolution.
    /// </summary>
    [ServerCmd]
    public void CmdResolveReport(ByteReader reader)
    {
        var reportId = reader.ReadString();
        var staffUserId = reader.ReadString();      
        
        Log.Info($"Resolving report &1{reportId}&r by &1{staffUserId}&r");

        if (!TryGetReport(reportId, out var report))
        {
            Log.Warn($"Could not find report &1{reportId}&r");
            return;
        }
        
        if (!ProfileManager.TryGetProfileByUserId(staffUserId, out var staffProfile))
        {
            Log.Error($"Could not find profile for staff &1{staffUserId}&r");
            return;
        }
        
        if (report.Value.Status != ReportStatus.Waiting)
        {
            Log.Warn($"Report &1{reportId}&r is not in the &1Waiting&r status");
            return;
        }
        
        report.Value.Status = ReportStatus.Resolved;
        report.Value.ResolvedAt = DateTime.UtcNow;
        report.Value.StaffId = staffProfile.Value.Id;
        
        report.IsDirty = true;
        
        CallRpcReportResolved(report.Value, staffProfile.Value);     
        
        Log.Info($"Report &1{reportId}&r resolved by &1{staffUserId}&r");

        if (!ProfileManager.TryGetProfileById(report.Value.ReportedId, out var reportedProfile))
        {
            Log.Error($"Could not find profile for reported &1{report.Value.ReportedId}&r");
            return;
        }

        if (!ProfileManager.TryGetProfileById(report.Value.ReporterId, out var reporterProfile))
        {
            Log.Error($"Could not find profile for reporter &1{report.Value.ReporterId}&r");
            return;
        }
        
        Task.Run(async () =>
        {
            if (report.Value.TryGetMessage(out var message))
            {
                if (!message.WasResolved)
                    await message.TryResolveAsync(message.GuildId, message.ChannelId, message.MessageId);

                if (!message.WasResolved)
                    return;
                
                await UpdateReportAsync(report.Value, reporterProfile.Value, reportedProfile.Value, staffProfile.Value, message.Message!);
            }
        });
    }
    
    /// <summary>
    /// Submits a player report to the system and stores it in the database.
    /// </summary>
    /// <returns>The status of the report submission.</returns>
    [ServerCmd(true)]
    public void CmdSubmitReport(ByteReader reader, ByteWriter writer)
    {
        var reporterId = reader.ReadString();
        var reporterRole = reader.ReadString();
        
        var reportedId = reader.ReadString();
        var reportedRole = reader.ReadString();
        
        var reason = reader.ReadString();       
        
        Log.Info($"Submitting report for &1{reportedId}&r by &1{reporterId}&r: &1{reason}&r");
        
        if (!ProfileManager.TryGetProfileByUserId(reporterId, out var reporterProfile))
        {
            Log.Error($"Could not find profile for reporter &1{reporterId}&r");
            
            writer.WriteByte((byte)ReportStatus.Rejected);
            return;
        }

        if (!ProfileManager.TryGetProfileByUserId(reportedId, out var reportedProfile))
        {
            Log.Error($"Could not find profile for reported &1{reportedId}&r");
            
            writer.WriteByte((byte)ReportStatus.Rejected);
            return;
        }

        var report = new ReportInfo
        {
            Id = DbManager.NewId,
            ServerId = Server.ServerAlias,

            ReporterId = reporterProfile.Value.Id,
            ReportedId = reportedProfile.Value.Id,

            SubmittedAt = DateTime.UtcNow,
            ResolvedAt = DateTime.MinValue,

            Status = ReportStatus.Waiting,

            ReporterRole = reporterRole,
            ReportedRole = reportedRole,

            Reason = reason
        };

        var value = Reports.AddStorageValue(report.Id, () => report);
        
        Log.Info($"Posting report &3{report.Id}&r to Discord channel &3{ReportChannelId}&r");
        
        Task.Run(async () => await PostReportAsync(report, reporterProfile.Value, reportedProfile.Value)).ContinueOnMainThread(msg =>
        {
            if (msg != null)
            {
                var cachedId = $"ReportMessage_{report.Id}";

                msg.CacheMessage(cachedId);

                report.CachedMessageId = cachedId;

                value.IsDirty = true;
            }
            else
            {
                Log.Error($"Failed to post report to Discord channel &3{ReportChannelId}&r");
            }
        });
        
        Log.Info($"Report &3{report.Id}&r submitted for &1{reportedId}&r by &1{reporterId}&r");
        
        writer.WriteByte((byte)ReportStatus.Waiting);       
    }

    /// <summary>
    /// Posts a report to the designated Discord channel asynchronously.
    /// </summary>
    /// <param name="report">The report information to be posted.</param>
    /// <returns>A task representing the asynchronous operation. The result contains the sent message, or null if the channel could not be found.</returns>
    public static async Task<RestUserMessage?> PostReportAsync(ReportInfo report, ProfileInfo reporterProfile, ProfileInfo targetProfile)
    {
        if (MainBotInstance.Instance == null)
        {
            Utils.Error("ReportModule / PostReportAsync", "MainBotInstance is null");
            return null;
        }

        var channel = await MainBotInstance.Instance.Client.GetChannelAsync(ReportChannelId);

        if (channel is not SocketTextChannel textChannel)
        {
            Utils.Error("ReportModule / PostReportAsync", $"Could not find channel with ID &1{ReportChannelId}&r");
            return null;
        }
        
        var embed = new EmbedBuilder();

        embed.WithTitle(":warning: Nový report");
        embed.WithAuthor(report.ServerId);
        embed.WithColor(Color.LightOrange);
        embed.WithCurrentTimestamp();
        embed.WithFooter($"ID: {report.Id}");

        embed.AddField(":link: Nahlašovatel",
            $"**ID**: {reporterProfile.Id}\n" +
            $"**UID**: {reporterProfile.UserId}\n" +
            $"**IP**: {reporterProfile.GetAddress()}\n" +
            $"**Jméno**: {reporterProfile.GetNickname()}");
        
        embed.AddField(":link: Hráč",
            $"**ID**: {targetProfile.Id}\n" +
            $"**UID**: {targetProfile.UserId}\n" +
            $"**IP**: {targetProfile.GetAddress()}\n" +
            $"**Jméno**: {targetProfile.GetNickname()}");
        
        embed.AddField(":grey_question: Důvod", $"```{report.Reason}```");
        
        var comps = new ComponentBuilder();

        comps.WithButton("Hotovo", $"ReportResolved_{report.Id}", ButtonStyle.Success, Emoji.Parse(":white_check_mark:"));
        comps.WithButton("Zamítnout", $"ReportRejected_{report.Id}", ButtonStyle.Danger, Emoji.Parse(":x:"));

        string? message = null;

        if (ReportRolePingId != 0)
            message = MentionUtils.MentionRole(ReportRolePingId);

        return await textChannel.SendMessageAsync(
            text: message,
            embed: embed.Build(), 
            components: comps.Build());       
    }

    /// <summary>
    /// Asynchronously updates the details of a specified report in the Discord message associated with it.
    /// </summary>
    /// <param name="report">
    /// The <see cref="ReportInfo"/> instance containing information about the report to be updated.
    /// </param>
    /// <param name="reporterProfile">
    /// A <see cref="ProfileInfo"/> object representing the profile of the player who reported the issue.
    /// </param>
    /// <param name="targetProfile">
    /// A <see cref="ProfileInfo"/> object representing the profile of the player who is being reported.
    /// </param>
    /// <param name="staffProfile">
    /// A <see cref="ProfileInfo"/> object representing the profile of the staff member resolving or handling the report.
    /// </param>
    /// <param name="message">
    /// The <see cref="SocketUserMessage"/> instance representing the message in which the report is displayed.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> that represents the asynchronous operation of updating the report message.
    /// </returns>
    public static async Task UpdateReportAsync(ReportInfo report, ProfileInfo reporterProfile,
        ProfileInfo targetProfile,
        ProfileInfo staffProfile, SocketUserMessage message)
    {
        await message.ModifyAsync(msg =>
        {
            var embed = new EmbedBuilder();
            
            embed.WithAuthor(report.ServerId);
            embed.WithFooter($"ID: {report.Id}");

            embed.AddField(":link: Nahlašovatel",
                $"**ID**: {reporterProfile.Id}\n" +
                $"**UID**: {reporterProfile.UserId}\n" +
                $"**IP**: {reporterProfile.GetAddress()}\n" +
                $"**Jméno**: {reporterProfile.GetNickname()}");
        
            embed.AddField(":link: Hráč",
                $"**ID**: {targetProfile.Id}\n" +
                $"**UID**: {targetProfile.UserId}\n" +
                $"**IP**: {targetProfile.GetAddress()}\n" +
                $"**Jméno**: {targetProfile.GetNickname()}");

            embed.AddField(":grey_question: Důvod", $"```{report.Reason}```");
            
            embed.AddField(":man_police_officer: Administrátor", 
                $"**ID**: {staffProfile.Id}\n" +
                $"**Jméno**: {staffProfile.GetNickname()}\n" +
                $"**Datum**: {report.ResolvedAt.ToString("g")}");

            if (report.Status == ReportStatus.Resolved)
            {
                embed.WithTitle(":white_check_mark: | Vyřešený report");
                embed.WithColor(Color.Green);
            }
            else
            {
                embed.WithTitle(":x: | Zamítnutý report");
                embed.WithColor(Color.Red);           
                embed.AddField(":question: Důvod zamítnutí", $"```{report.StaffResponse}```");
            }
            
            msg.Components = null;
            msg.Embed = embed.Build();       
        });
    }

    /// <summary>
    /// Updates the details of a report and modifies the associated message with the updated information.
    /// </summary>
    /// <param name="report">The report to be updated, containing its current status and details.</param>
    /// <param name="reporterProfile">The profile of the user who submitted the report.</param>
    /// <param name="targetProfile">The profile of the user targeted by the report.</param>
    /// <param name="staffProfile">The profile of the staff member handling the report.</param>
    /// <param name="message">The message to be updated with the new report details.</param>
    /// <returns>
    /// A task representing the asynchronous operation of updating the report.
    /// </returns>
    public static async Task UpdateReportAsync(ReportInfo report, ProfileInfo reporterProfile,
        ProfileInfo targetProfile,
        ProfileInfo staffProfile, RestUserMessage message)
    {
        await message.ModifyAsync(msg =>
        {
            var embed = new EmbedBuilder();
            
            embed.WithAuthor(report.ServerId);
            embed.WithFooter($"ID: {report.Id}");

            embed.AddField(":link: Nahlašovatel",
                $"**ID**: {reporterProfile.Id}\n" +
                $"**UID**: {reporterProfile.UserId}\n" +
                $"**IP**: {reporterProfile.GetAddress()}\n" +
                $"**Jméno**: {reporterProfile.GetNickname()}");
        
            embed.AddField(":link: Hráč",
                $"**ID**: {targetProfile.Id}\n" +
                $"**UID**: {targetProfile.UserId}\n" +
                $"**IP**: {targetProfile.GetAddress()}\n" +
                $"**Jméno**: {targetProfile.GetNickname()}");

            embed.AddField(":grey_question: Důvod", $"```{report.Reason}```");
            
            embed.AddField(":man_police_officer: Administrátor", 
                $"**ID**: {staffProfile.Id}\n" +
                $"**Jméno**: {staffProfile.GetNickname()}\n" +
                $"**Datum**: {report.ResolvedAt.ToString("g")}");

            if (report.Status == ReportStatus.Resolved)
            {
                embed.WithTitle(":white_check_mark: | Vyřešený report");
                embed.WithColor(Color.Green);
            }
            else
            {
                embed.WithTitle(":x: | Zamítnutý report");
                embed.WithColor(Color.Red);           
                embed.AddField(":question: Důvod zamítnutí", $"```{report.StaffResponse}```");
            }
            
            msg.Components = null;
            msg.Embed = embed.Build();       
        });
    }
    
    internal static void OnButtonExecuted(SocketMessageComponent component)
    {
        if (string.IsNullOrEmpty(component.Data.CustomId))
            return;

        if (!component.Data.CustomId.TrySplit('_', true, 2, out var segments))
            return;

        if (component.Message == null || component.User == null)
            return;
        
        var isResolved = segments[0] == "ReportResolved";
        var isRejected = segments[0] == "ReportRejected";

        if (!isRejected && !isResolved)
            return;

        if (!TryGetReport(segments[1], out var report))
        {
            Task.Run(async () =>
            {
                await component.Message.ModifyAsync(msg =>
                {
                    msg.Components = null;
                });
            });
            
            Utils.Warn("ReportModule / OnButtonExecuted", $"Could not find report with ID &1{segments[1]}&r");
            return;
        }

        if (!ProfileManager.TryGetProfileById(report.Value.ReporterId, out var reporterProfile))
        {
            Utils.Warn("ReportModule / OnButtonExecuted", $"Could not find profile for reporter &1{report.Value.ReporterId}&r");
            return;
        }

        if (!ProfileManager.TryGetProfileById(report.Value.ReportedId, out var reportedProfile))
        {
            Utils.Warn("ReportModule / OnButtonExecuted", $"Could not find profile for reported &1{report.Value.ReportedId}&r");
            return;       
        }

        if (!ProfileManager.TryGetProfile(x => x.DiscordId == component.User.Id, out var staffProfile))
        {
            Task.Run(async () =>
            {
                await component.RespondAsync(":x: | Nemáš oprávnění pro tuto akci - tvůj profil nebyl nalezen.", ephemeral: true);
            });

            return;
        }

        if (!staffProfile.Value.HasPermission("ManageReports"))
        {
            Task.Run(async () =>
            {
                await component.RespondAsync(":x: Nemáš oprávnění pro tuto akci.", ephemeral: true);
            });

            return;
        }

        if (report.Value.Status != ReportStatus.Waiting)
        {
            Task.Run(async () =>
            {
                await component.Message.ModifyAsync(msg =>
                {
                    msg.Components = null;
                });
            });
            
            Utils.Warn("ReportModule / OnButtonExecuted", $"Report &1{report.Value.Id}&r is not in the &1Waiting&r status");
            return;
        }
        
        if (isResolved)
        {
            report.Value.Status = ReportStatus.Resolved;
            report.Value.ResolvedAt = DateTime.UtcNow;
            report.Value.StaffId = staffProfile.Value.Id;

            if (ScpSlManager.TryGetEntityByAlias<ReportModule>(report.Value.ServerId, out var reportModule))
                reportModule.CallRpcReportResolved(report.Value, staffProfile.Value);       
            
            report.IsDirty = true;
            
            Task.Run(async () =>
            {
                await component.RespondAsync($":white_check_mark: | Report `{report.Value.Id}` označen jako hotový!", ephemeral: true);
                await UpdateReportAsync(report.Value, reporterProfile.Value, reportedProfile.Value, staffProfile.Value, component.Message);
            });
            
            Utils.Info("ReportModule / OnButtonExecuted", $"Report &3{report.Value.Id}&r &2resolved&r by &3{staffProfile.Value.Id}&r");       
        }
        else
        {
            Task.Run(async () =>
            {
                var modal = new ModalBuilder();

                modal.WithTitle("Důvod");
                modal.WithCustomId($"ReportRejectionReason_{report.Value.Id}");
                modal.AddTextInput("Důvod", "RejectionReason", TextInputStyle.Short, "Důvod ..", null, null, true);

                var response = await component.AwaitModalResponseAsync(modal, TimeSpan.FromMinutes(2));

                if (response == null ||
                    !response.Data.Components.TryGetFirst(x => x.CustomId == "RejectionReason", out var textInput))
                {
                    Utils.Warn("ReportModule / OnButtonExecuted", "Rejection reason not provided (no component)");
                    return new KeyValuePair<SocketModal, string>(response!, string.Empty);
                }

                return new KeyValuePair<SocketModal, string>(response, textInput.Value);
            }).ContinueOnMainThread(kvp =>
            {
                if (!string.IsNullOrEmpty(kvp.Value))
                {
                    report.Value.Status = ReportStatus.Rejected;
                    report.Value.ResolvedAt = DateTime.UtcNow;
                    report.Value.StaffId = staffProfile.Value.Id;
                    report.Value.StaffResponse = kvp.Value;
                    
                    if (ScpSlManager.TryGetEntityByAlias<ReportModule>(report.Value.ServerId, out var reportModule))
                        reportModule.CallRpcReportRejected(report.Value, staffProfile.Value, kvp.Value);

                    report.IsDirty = true;

                    Task.Run(async () =>
                    {
                        await UpdateReportAsync(report.Value, reporterProfile.Value, reportedProfile.Value, staffProfile.Value, component.Message);
                        await kvp.Key.RespondAsync($":white_check_mark: | Report `{report.Value.Id}` zamítnut!", ephemeral: true);
                    });
                    
                    Utils.Info("ReportModule / OnButtonExecuted", $"Report &3{report.Value.Id}&r &1rejected&r by &&r{staffProfile.Value.Id}&r: &3{kvp.Value}&r");
                }
                else
                {
                    Utils.Warn("ReportModule / OnButtonExecuted", "Rejection reason not provided (empty)");
                }
            });
        }
    }
}