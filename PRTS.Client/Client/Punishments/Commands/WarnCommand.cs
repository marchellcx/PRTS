using LabExtended.API;

using LabExtended.Commands;
using LabExtended.Commands.Attributes;
using LabExtended.Commands.Interfaces;

using PRTS.Extensions;
using PRTS.Client.Punishments.Enums;

namespace PRTS.Client.Punishments.Commands;

/// <summary>
/// Represents a command to warn a user within the system.
/// Inherits from the CommandBase class and implements the IServerSideCommand interface.
/// </summary>
[Command("warn", "Udělí hráči varování.")]
public class WarnCommand : CommandBase, IServerSideCommand
{
    [CommandOverload("Udělí hráči varování.", null)]
    private void Warn(
        [CommandParameter("Hráč", "Hráč který má dostat varování.")] ExPlayer target,
        [CommandParameter("Důvod", "Důvod varování.")] string reason)
    {
        if (PunishmentModule.Singleton == null)
        {
            Fail("Server PRTS není připojen!");
        }
        else
        {
            var player = Sender;
            
            Ok($"Požadavek na warn hráče &1{target.ToCommandString()}&r odeslán serveru.");
            
            PunishmentModule.Singleton.CallCmdIssuePunishment(Sender.UserId, target.UserId, PunishmentType.Warn, reason, null, null,
                info =>
                {
                    if (info != null)
                    {
                        player.SendRemoteAdminMessage($"Hráč &1{target}&r byl úspěšně varován:\n" +
                                                      $"&3ID varování&r: {info.Id}\n" +
                                                      $"&3ID profilu hráče&r: {info.TargetId}\n" +
                                                      $"&3ID profilu administrátora&r: {info.StaffId}\n" +
                                                      $"&3Důvod&r: {info.Reason}\n" +
                                                      $"&3Expirace&r: {(info.IsPermanent ? "&1PERMANENTNÍ&r" : info.ExpiresAt.ToVeCzechString())}",
                            tag: "PRTS");
                    }
                    else
                    {
                        player.SendRemoteAdminMessage($"Požadavek na warn hráče &1{target.ToCommandString()}&r byl &1odmítnut&r serverem.", success: false, tag: "PRTS");
                    }
                });
        }
    }
}