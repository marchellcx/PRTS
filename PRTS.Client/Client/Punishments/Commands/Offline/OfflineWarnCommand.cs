using LabExtended.Commands;
using LabExtended.Commands.Attributes;
using LabExtended.Commands.Interfaces;

using NiveraAPI.Extensions;

using PRTS.Extensions;
using PRTS.Client.Punishments.Enums;

namespace PRTS.Client.Punishments.Commands.Offline;

/// <summary>
/// Represents a command used to warn offline players from the server.
/// </summary>
[Command("owarn", "Slouží k varování offline hráčů.")]
public class OfflineWarnCommand : CommandBase, IServerSideCommand
{
    [CommandOverload("Udělí varování offline hráčům.", null)]
    private void Invoke(
        [CommandParameter("Hráč", "IP, ID, nebo jméno hráče.")] string target,
        [CommandParameter("Důvod", "Důvod varování.")] string reason)
    {
        if (PunishmentModule.Singleton == null)
        {
            Fail("Server PRTS není připojen!");
        }
        else
        {
            var player = Sender;
            
            Ok("Požadavek na warn odeslán serveru.");
            
            PunishmentModule.Singleton.CallCmdIssuePunishment(Sender.UserId, target, PunishmentType.Warn, reason, null, null,
                info =>
                {
                    if (info != null)
                    {
                        player.SendRemoteAdminMessage($"Hráč &1{target}&r dostal varování:\n" +
                                                      $"&3ID varování&r: {info.Id}\n" +
                                                      $"&3ID profilu hráče&r: {info.TargetId}\n" +
                                                      $"&3ID profilu administrátora&r: {info.StaffId}\n" +
                                                      $"&3Důvod&r: {info.Reason}\n" +
                                                      $"&3Expirace&r: {(info.IsPermanent ? "&1PERMANENTNÍ&r" : info.ExpiresAt.ToVeCzechString())}".FormatTrueColorString("white", true),
                            tag: "PRTS");
                    }
                    else
                    {
                        player.SendRemoteAdminMessage("Požadavek byl &1odmítnut&r serverem.".FormatTrueColorString("white", true), success: false, tag: "PRTS");
                    }
                });
        }
    }
}