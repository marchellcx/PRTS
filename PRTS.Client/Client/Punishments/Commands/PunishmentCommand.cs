using LabExtended.Commands;
using LabExtended.Commands.Attributes;
using LabExtended.Commands.Interfaces;

using PRTS.Extensions;
using PRTS.Client.Punishments.Enums;

namespace PRTS.Client.Punishments.Commands;

/// <summary>
/// Commands for working with punishments (bans, mutes, warnings).
/// </summary>
[Command("punishment", "Příkazy pro práci s tresty.")]
public class PunishmentCommand : CommandBase, IServerSideCommand
{
    [CommandOverload("list", "Vypíše tresty.", null)]
    private void List(
        [CommandParameter("Filtry", "Filtry pro vyhledávání trestů.")] string[] filters)
    {
        if (PunishmentModule.Singleton == null)
        {
            Fail("Služba PRTS není připojena!");
        }
        else
        {
            if (!ParseFilters(filters, out var error, out var id, out var staffId, out var targetId, out var type, out var status, out var from, out var to))
            {
                Fail($"Chyba při parsování filtrů: {error}\n" +
                    $"Dostupné filtry:\n" +
                    $"id:ID (ID trestu)\n" +
                    $"admin:ID (ID administrátora - UserID)\n" +
                    $"hráč:ID (ID cílového hráče - UserID)\n" +
                    $"typ:TYP (Typ trestu - Warn, Mute, Ban)\n" +
                    $"stav:STAV (Status trestu - Expired, Revoked, Active)\n" +
                    $"od:DATUM (Datum od)\n" +
                    $"do:DATUM (Datum do)");
                return;
            }

            PunishmentModule.Singleton.CallCmdListPunishments(id, staffId, targetId, type, status, from, to, list =>
            {
                if (list != null)
                {
                    Sender.SendRemoteAdminMessage($"Server vrátil &3{list.Count}&r trestů.", tag: "PRTS");

                    foreach (var punishment in list)
                    {
                        Sender.SendRemoteAdminMessage($"&3ID&r: {punishment.Id}\n" +
                                                      $"&3Typ&r: {punishment.Type}\n" +
                                                      $"&3Status&r: {punishment.Status}\n" +
                                                      $"&3Důvod&r: {punishment.Reason}\n" +
                                                      $"&3Expirace&r: {(punishment.IsPermanent ? "&1PERMANENTNÍ&r" : punishment.ExpiresAt.ToLocalTime().ToVeCzechString())}\n" +
                                                      $"&3ID profilu hráče&r: {punishment.TargetId}\n" +
                                                      $"&3ID profilu administrátora&r: {punishment.StaffId}", tag: "PRTS");
                    }
                }
                else
                {
                    Sender.SendRemoteAdminMessage("Server &1odmítl&r požadavek na vyhledání trestů.", tag: "PRTS", success: false);
                }
            });
        }
    }

    [CommandOverload("revoke", "Zruší trest.", null)]
    private void Revoke(
        [CommandParameter("ID", "ID banu / mutu / varování.")] string id,
        [CommandParameter("Důvod", "Důvod zrušení.")] string reason)
    {
        if (PunishmentModule.Singleton == null)
        {
            Fail("Služba PRTS není připojena!");
        }
        else
        {
            var player = Sender;

            Ok($"Požadavek na zrušení trestu s ID &3{id}&r odeslán serveru.");

            PunishmentModule.Singleton.CallCmdRevokePunishment(id, Sender.UserId, reason, info =>
            {
                if (info != null)
                {
                    player.SendRemoteAdminMessage($"&3{info.Type}&r s ID &1{info.Id}&r byl zrušen!", tag: "PRTS");
                }
                else
                {
                    player.SendRemoteAdminMessage("Server &1odmítl&r požadavek na zrušení.", tag: "PRTS", success: false);
                }
            });
        }
    }

    private static bool ParseFilters(string[] filters, out string? error, out string? id, out string? staffId, out string? targetId, out PunishmentType? type, out PunishmentStatus? status, out DateTime? from, out DateTime? to)
    {
        id = null;
        to = null;
        type = null;
        from = null;
        error = null;
        status = null;
        staffId = null;
        targetId = null;

        for (var x = 0; x < filters.Length; x++)
        {
            var filter = filters[x];

            if (string.IsNullOrWhiteSpace(filter))
                continue;

            var lower = filter.ToLowerInvariant();

            if (lower.StartsWith("admin:"))
            {
                staffId = filter.Substring(6);
            }
            else if (lower.StartsWith("id:"))
            {
                id = filter.Substring(3);
            }
            else if (lower.StartsWith("hráč:"))
            {
                targetId = filter.Substring(5);
            }
            else if (lower.StartsWith("typ:"))
            {
                var typeString = filter.Substring(4);

                if (Enum.TryParse<PunishmentType>(typeString, true, out var parsedType))
                    type = parsedType;
                else
                    error = $"Neplatný typ trestu: {typeString}";
            }
            else if (lower.StartsWith("stav:"))
            {
                var statusString = filter.Substring(5);

                if (Enum.TryParse<PunishmentStatus>(statusString, true, out var parsedStatus))
                    status = parsedStatus;
                else
                    error = $"Neplatný stav trestu: {statusString}";
            }
            else if (lower.StartsWith("od:"))
            {
                var fromString = filter.Substring(3);

                if (DateTime.TryParse(fromString, out var parsedFrom))
                    from = parsedFrom;
                else
                    error = $"Neplatný formát data OD: {fromString}";
            }
            else if (lower.StartsWith("do:"))
            {
                var toString = filter.Substring(3);

                if (DateTime.TryParse(toString, out var parsedTo))
                    to = parsedTo;
                else
                    error = $"Neplatný formát data DO: {toString}";
            }
        }

        return error == null;
    }
}