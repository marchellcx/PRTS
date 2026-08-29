using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Handlers;

using LabExtended.API;
using LabExtended.Core;

using NiveraAPI.IO.Serialization;
using NiveraAPI.IO.Network.Entities.Attributes;

using PRTS.Client.Profiles.Objects;
using PRTS.Client.Reports.Objects;
using PRTS.Client.Reports.Enums;

namespace PRTS.Client.Reports;

/// <summary>
/// Represents a module for handling player reports within the PRTS client system.
/// </summary>
[ServerType("PRTS.ScpSl.Modules.Reports.ReportModule")]
public class ReportModule : PrtsModule
{
    [IndexField] private static ushort cmd_CmdResolveReport;
    [IndexField] private static ushort cmd_CmdRejectReport;
    [IndexField] private static ushort cmd_CmdSubmitReport;

    /// <summary>
    /// Invoked when the client instance of the module is fully spawned.
    /// Performs initialization tasks such as setting up event listeners for player reports.
    /// </summary>
    public override void OnClientSpawned()
    {
        base.OnClientSpawned();
        
        PlayerEvents.ReportedPlayer += OnPlayerReport;
        PlayerEvents.ReportedCheater += OnCheaterReport;
    }

    /// <summary>
    /// Called when the module instance is being destroyed.
    /// Performs cleanup tasks such as detaching event handlers related to player reports.
    /// </summary>
    public override void OnDestroyed()
    {
        base.OnDestroyed();
        
        PlayerEvents.ReportedPlayer -= OnPlayerReport;
        PlayerEvents.ReportedCheater -= OnCheaterReport;
    }

    /// <summary>
    /// Submits a dummy report for testing purposes.
    /// Uses placeholder data for the reporter, reported user, and the report details.
    /// </summary>
    /// <param name="player">The player object containing information about the player to be reported.</param>
    public void SubmitDummyReport(ExPlayer player)
    {
        CallCmdSubmitReport(player.UserId, player.Role.ToString(), player.UserId, player.Role.ToString(), "Dummy report");      
    }

    /// <summary>
    /// Rejects a specific report by providing the report identifier, the staff member's identifier, and the reason for rejection.
    /// </summary>
    /// <param name="reportId">The unique identifier of the report to be rejected.</param>
    /// <param name="staffUserId">The unique identifier of the staff member rejecting the report.</param>
    /// <param name="rejectionReason">The reason provided for rejecting the report.</param>
    public void CallCmdRejectReport(string reportId, string staffUserId, string rejectionReason)
    {
        SendRemoteCallback(cmd_CmdRejectReport, writer =>
        {
            writer.WriteString(reportId);
            writer.WriteString(staffUserId);
            writer.WriteString(rejectionReason);
        });       
    }
    
    /// <summary>
    /// Resolves a specific report by providing the report identifier and the identifier of the staff member resolving it.
    /// </summary>
    /// <param name="reportId">The unique identifier of the report to be resolved.</param>
    /// <param name="staffUserId">The unique identifier of the staff member resolving the report.</param>
    public void CallCmdResolveReport(string reportId, string staffUserId)
    {
        SendRemoteCallback(cmd_CmdResolveReport, writer =>
        {
            writer.WriteString(reportId);
            writer.WriteString(staffUserId);
        });
    }
    
    /// <summary>
    /// Sends a report to the server with details about the reporter, the reported user, and the reason.
    /// </summary>
    /// <param name="reporterId">The unique identifier of the reporter submitting the report.</param>
    /// <param name="reporterRole">The role or position of the reporter in the system.</param>
    /// <param name="reportedId">The unique identifier of the user being reported.</param>
    /// <param name="reportedRole">The role or position of the reported user in the system.</param>
    /// <param name="reason">The reason or context for the report being submitted.</param>
    public void CallCmdSubmitReport(string reporterId, string reporterRole, string reportedId, string reportedRole,
        string reason)
    {
        ApiLog.Info($"Submitting report by &1{reporterId}&r on &3{reportedId}&r: &3{reason}&r");      
        
        SendRemoteCallback(cmd_CmdSubmitReport, writer =>
        {
            writer.WriteString(reporterId);
            writer.WriteString(reporterRole);
            writer.WriteString(reportedId);
            writer.WriteString(reportedRole);
            writer.WriteString(reason);
        }, reader =>
        {
            var status = (ReportStatus)reader.ReadByte();

            if (status is ReportStatus.Rejected)
            {
                ApiLog.Warn($"Server rejected report by &1{reporterId}&r on &3{reportedId}&r: &3{reason}&r");
            }
            else
            {
                ApiLog.Info($"Server accepted report by &1{reporterId}&r on &3{reportedId}&r: &3{reason}&r");
            }
        });
    }

    /// <summary>
    /// Notifies the client that a report has been resolved.
    /// </summary>
    [ClientRpc]
    public void RpcReportResolved(ByteReader reader)
    {
        var info = reader.Read<ReportInfo>();
        var resolvingStaff = reader.Read<ProfileInfo>();
        
        ApiLog.Info($"Discord resolved report &3{info.Id}&r by &1{resolvingStaff.UserId}&r on &3{info.ReportedId}&r: &3{info.Reason}&r");       
    }

    /// <summary>
    /// Notifies the client that a report has been rejected.
    /// </summary>
    [ClientRpc]
    public void RpcReportRejected(ByteReader reader)
    {
        var info = reader.Read<ReportInfo>();
        var rejectingStaff = reader.Read<ProfileInfo>();
        var rejectionReason = reader.ReadString();      
        
        ApiLog.Warn($"Discord rejected report &3{info.Id}&r by &1{rejectingStaff.UserId}&r on &3{info.ReportedId}&r: &3{rejectionReason}&r");      
    }

    private void OnPlayerReport(PlayerReportedPlayerEventArgs args)
    { 
        CallCmdSubmitReport(args.Player.UserId, args.Player.Role.ToString(), args.Target.UserId, 
            args.Target.Role.ToString(), args.Reason);
    }

    private void OnCheaterReport(PlayerReportedCheaterEventArgs args)
    {
        CallCmdSubmitReport(args.Player.UserId, args.Player.Role.ToString(), args.Target.UserId, 
            args.Target.Role.ToString(), args.Reason);
    }
}