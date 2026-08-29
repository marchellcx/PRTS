using PRTS.Discord.MessageCache;

namespace PRTS.ScpSl.Modules.Reports;

/// <summary>
/// Represents information about a report.
/// </summary>
public class ReportInfo
{
    private volatile string id = string.Empty;
    private volatile string staffId = string.Empty;
    private volatile string serverId = string.Empty;
    
    private volatile string reporterId = string.Empty;
    private volatile string reportedId = string.Empty;

    private volatile string reporterRole = string.Empty;
    private volatile string reportedRole = string.Empty;

    private volatile string reason = string.Empty;
    private volatile string staffResponse = string.Empty;
    private volatile string cachedMessageId = string.Empty;

    private volatile ReportStatus status = ReportStatus.Waiting;

    /// <summary>
    /// The ID of the report.
    /// </summary>
    public string Id
    {
        get => id;
        set => id = value;
    }

    /// <summary>
    /// The ID of the staff member who handled the report.
    /// </summary>
    public string StaffId
    {
        get => staffId;
        set => staffId = value;
    }

    /// <summary>
    /// The ID of the server where the report was made.
    /// </summary>
    public string ServerId
    {
        get => serverId;
        set => serverId = value;
    }

    /// <summary>
    /// The ID of the player who reported.
    /// </summary>
    public string ReporterId
    {
        get => reporterId;
        set => reporterId = value;
    }

    /// <summary>
    /// The ID of the player who was reported.
    /// </summary>
    public string ReportedId
    {
        get => reportedId;
        set => reportedId = value;
    }

    /// <summary>
    /// The time of the report.
    /// </summary>
    public DateTime SubmittedAt { get; set; }
    
    /// <summary>
    /// The time the report was resolved.
    /// </summary>
    public DateTime ResolvedAt { get; set; }

    /// <summary>
    /// The status of the report.
    /// </summary>
    public ReportStatus Status
    {
        get => status;
        set => status = value;
    }

    /// <summary>
    /// The role of the reporting player.
    /// </summary>
    public string ReporterRole
    {
        get => reporterRole;
        set => reporterRole = value;
    }

    /// <summary>
    /// The role of the reported player.
    /// </summary>
    public string ReportedRole
    {
        get => reportedRole;
        set => reportedRole = value;
    }

    /// <summary>
    /// The reason of the report.
    /// </summary>
    public string Reason
    {
        get => reason;
        set => reason = value;
    }

    /// <summary>
    /// The staff response to the report.
    /// </summary>
    public string StaffResponse
    {
        get => staffResponse;
        set => staffResponse = value;
    }

    /// <summary>
    /// The ID of the cached Discord message that was sent to the report channel.
    /// </summary>
    public string CachedMessageId
    {
        get => cachedMessageId;
        set => cachedMessageId = value;
    }

    /// <summary>
    /// Tries to retrieve a cached Discord message based on the cached message ID.
    /// </summary>
    /// <param name="message">When this method returns, contains the cached Discord message if one is found; otherwise, null.</param>
    /// <returns>
    /// true if a cached message with the specified ID exists; otherwise, false.
    /// </returns>
    public bool TryGetMessage(out CachedDiscordMessage message)
    {
        message = null!;

        if (string.IsNullOrEmpty(cachedMessageId))
            return false;
        
        return CachedDiscordMessageStorage.TryGetMessage(cachedMessageId, out message);
    }
}