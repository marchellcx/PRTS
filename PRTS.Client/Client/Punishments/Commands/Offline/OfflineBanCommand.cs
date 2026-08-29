using LabExtended.Commands;
using LabExtended.Commands.Attributes;
using LabExtended.Commands.Interfaces;

using PRTS.Extensions;
using PRTS.Client.Punishments.Enums;

namespace PRTS.Client.Punishments.Commands.Offline;

/// <summary>
/// Represents a command used to ban offline players from the server.
/// </summary>
[Command("oban", "Slouží k banování offline hráčů.")]
public class OfflineBanCommand : CommandBase, IServerSideCommand
{
    [CommandOverload("Zabanuje offline hráče.", null)]
    private void Invoke(
        [CommandParameter("Hráč", "IP, ID, nebo jméno hráče.")] string target,
        [CommandParameter("Délka", "Délka banu.")] TimeSpan duration,
        [CommandParameter("Důvod", "Důvod banu.")] string reason)
    {
        if (PunishmentModule.Singleton == null)
        {
            Fail("Server PRTS není připojen!");
        }
        else
        {
            DateTime? expires = null;

            if (duration != TimeSpan.Zero)
                expires = DateTime.UtcNow + duration;

            var player = Sender;
            
            Ok("Požadavek na ban odeslán serveru.");
            
            PunishmentModule.Singleton.CallCmdIssuePunishment(Sender.UserId, target, PunishmentType.Ban, reason, expires, null,
                info =>
                {
                    if (info != null)
                    {
                        player.SendRemoteAdminMessage($"Hráč &1{target}&r byl úspěšně zabanován:\n" +
                                                      $"&3ID banu&r: {info.Id}\n" +
                                                      $"&3ID profilu hráče&r: {info.TargetId}\n" +
                                                      $"&3ID profilu administrátora&r: {info.StaffId}\n" +
                                                      $"&3Důvod&r: {info.Reason}\n" +
                                                      $"&3Expirace&r: {(info.IsPermanent ? "&1PERMANENTNÍ&r" : info.ExpiresAt.ToVeCzechString())}",
                            tag: "PRTS");
                    }
                    else
                    {
                        player.SendRemoteAdminMessage($"Požadavek na ban hráče &1{target}&r byl &1odmítnut&r serverem.", success: false, tag: "PRTS");
                    }
                });
        }
    }
}