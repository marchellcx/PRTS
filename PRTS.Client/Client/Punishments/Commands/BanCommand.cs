using LabExtended.API;

using LabExtended.Commands;
using LabExtended.Commands.Attributes;
using LabExtended.Commands.Interfaces;

using PRTS.Extensions;
using PRTS.Client.Punishments.Enums;

namespace PRTS.Client.Punishments.Commands;

/// <summary>
/// Represents a command to ban a user within the system.
/// Inherits from the CommandBase class and implements the IServerSideCommand interface.
/// </summary>
[Command("ban", "Udělí hráči ban.")]
public class BanCommand : CommandBase, IServerSideCommand
{
    [CommandOverload("Udělí hráči ban.", null)]
    private void Ban(
        [CommandParameter("Hráč", "Hráč který má dostat ban.")] ExPlayer target,
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
            
            Ok($"Požadavek na ban hráče &1{target.ToCommandString()}&r odeslán serveru (důvod: &3{reason}&r, délka: &3{(expires.HasValue ? duration.ToFullCzechString() : "PERMANENTNÍ")}&r)");
            
            PunishmentModule.Singleton.CallCmdIssuePunishment(Sender.UserId, target.UserId, PunishmentType.Ban, reason, expires, null,
                info =>
                {
                    if (info != null)
                    {
                        player.SendRemoteAdminMessage($"Hráč &1{target}&r byl úspěšně zabanován:\n" +
                                                      $"&3ID banu&r: {info.Id}\n" +
                                                      $"&3ID profilu hráče&r: {info.TargetId}\n" +
                                                      $"&3ID profilu administrátora&r: {info.StaffId}\n" +
                                                      $"&3Důvod&r: {info.Reason}\n" +
                                                      $"&3Expirace&r: {(info.IsPermanent ? "&1PERMANENTNÍ&r" : info.ExpiresAt.ToLocalTime().ToVeCzechString())}",
                            tag: "PRTS");
                    }
                    else
                    {
                        player.SendRemoteAdminMessage("Požadavek byl &1odmítnut&r serverem.", success: false, tag: "PRTS");
                    }
                });
        }
    }
}