using LabExtended.Commands;
using LabExtended.Commands.Attributes;
using LabExtended.Commands.Interfaces;

using PRTS.Extensions;
using PRTS.Client.Punishments.Enums;

namespace PRTS.Client.Punishments.Commands.Offline;

/// <summary>
/// Represents a command used to mute offline players from the server.
/// </summary>
[Command("omute", "Slouží k mutování offline hráčů.")]
public class OfflineMuteCommand : CommandBase, IServerSideCommand
{
    [CommandOverload("Mutne offline hráče.", null)]
    private void Invoke(
        [CommandParameter("Hráč", "IP, ID, nebo jméno hráče.")] string target,
        [CommandParameter("Délka", "Délka mutu.")] TimeSpan duration,
        [CommandParameter("Důvod", "Důvod mutu.")] string reason)
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

            Ok("Požadavek na mute odeslán serveru.");
            
            PunishmentModule.Singleton.CallCmdIssuePunishment(Sender.UserId, target, PunishmentType.Mute, reason, expires, null,
                info =>
                {
                    if (info != null)
                    {
                        player.SendRemoteAdminMessage($"Hráč &1{target}&r byl úspěšně mutnut:\n" +
                                                      $"&3ID mutu&r: {info.Id}\n" +
                                                      $"&3ID profilu hráče&r: {info.TargetId}\n" +
                                                      $"&3ID profilu administrátora&r: {info.StaffId}\n" +
                                                      $"&3Důvod&r: {info.Reason}\n" +
                                                      $"&3Expirace&r: {(info.IsPermanent ? "&1PERMANENTNÍ&r" : info.ExpiresAt.ToVeCzechString())}",
                            tag: "PRTS");
                    }
                    else
                    {
                        player.SendRemoteAdminMessage($"Požadavek na mute hráče &1{target}&r byl &1odmítnut&r serverem.", success: false, tag: "PRTS");
                    }
                });
        }
    }
}