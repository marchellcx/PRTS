namespace PRTS.ScpSl.Modules.Reports;

/// <summary>
/// Represents the status of a report.
/// </summary>
public enum ReportStatus
{
    /// <summary>
    /// The report is resolved.
    /// </summary>
    Resolved,
    
    /// <summary>
    /// The report is rejected.
    /// </summary>
    Rejected,
    
    /// <summary>
    /// The report is pending.
    /// </summary>
    Waiting
}