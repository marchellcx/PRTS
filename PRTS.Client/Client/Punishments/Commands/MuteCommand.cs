using LabExtended.API;

using LabExtended.Commands;
using LabExtended.Commands.Attributes;
using LabExtended.Commands.Interfaces;

using PRTS.Client.Punishments.Enums;

using PRTS.Extensions;

namespace PRTS.Client.Punishments.Commands;

/// <summary>
/// Represents a command to mute a user within the system.
/// Inherits from the CommandBase class and implements the IServerSideCommand interface.
/// </summary>
[Command("mute", "Mutne hráče.")]
public class MuteCommand : CommandBase, IServerSideCommand
{
    [CommandOverload("Mutne hráče.", null)]
    private void Mute(
        [CommandParameter("Hráč", "Hráč který se má mutnout.")] ExPlayer target,
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
            
            PunishmentModule.Singleton.CallCmdIssuePunishment(Sender.UserId, target.UserId, PunishmentType.Mute, reason, expires, null,
                info =>
                {
                    if (info != null)
                    {
                        player.SendRemoteAdminMessage($"Hráč &1{target}&r byl úspěšně mutnut:\n" +
                                                      $"&3ID mutur: {info.Id}\n" +
                                                      $"&3ID profilu hráče&r: {info.TargetId}\n" +
                                                      $"&3ID profilu administrátora&r: {info.StaffId}\n" +
                                                      $"&3Důvod&r: {info.Reason}\n" +
                                                      $"&3Expirace&r: {(info.IsPermanent ? "&1PERMANENTNÍ&r" : info.ExpiresAt.ToLocalTime().ToVeCzechString())}",
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