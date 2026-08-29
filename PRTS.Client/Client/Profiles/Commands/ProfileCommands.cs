using LabExtended.API;

using LabExtended.Commands;
using LabExtended.Commands.Attributes;
using LabExtended.Commands.Interfaces;

using NiveraAPI.Utilities;

using PRTS.Extensions;

namespace PRTS.Client.Profiles.Commands;

/// <summary>
/// Represents a set of commands related to player profiles, allowing users to retrieve and display profile information based on a given user ID.
/// </summary>
[Command("profile", "Profile-related commands")]
public class ProfileCommands : CommandBase, IServerSideCommand
{
    [CommandOverload("Zobrazí informace o profilu hráče na základě zadaného ID.", null)]
    private void Show(
        [CommandParameter(ParserType = typeof(ExPlayer), ParserProperty = "UserId")]
        [CommandParameter("ID", "ID hráče pro zobrazení profilu (UserID, PlayerID, IP)")]
        string userId)
    {
        if (ProfileModule.Singleton == null)
        {
            Fail($"Služba PRTS není připojena!");
            return;
        }

        Ok($"Požadavek odeslán (profil ID: &3{userId}&r).");

        var player = Sender;

        ProfileModule.Singleton.CallCmdGetProfile(userId, profile =>
        {
            if (profile == null)
            {
                player.SendRemoteAdminMessage($"Profil hráče s ID &3{userId}&r nebyl nalezen.", success: false, tag: "PRTS");
            }
            else
            {
                var sb = Pools.PoolStringBuilder();

                sb.AppendLine();
                sb.AppendLine($"Profil &3{profile.Id}&r:");
                sb.AppendLine($"User ID: &3{profile.UserId}&r");
                sb.AppendLine($"Discord ID: &3{profile.DiscordId}&r");

                sb.AppendLine($"Datum vytvoření: &3{profile.CreatedAt.ToLocalTime().ToVeCzechString()}&r");
                sb.AppendLine($"Datum poslední aktualizace: &3{profile.ModifiedAt.ToLocalTime().ToVeCzechString()}&r");
                sb.AppendLine($"Datum posledního přihlášení: &3{profile.LastLogin.ToLocalTime().ToVeCzechString()}&r");

                sb.AppendLine($"Počet jmen: &3{profile.Nicknames.Count}&r ({profile.GetNickname() ?? "null"}");
                sb.AppendLine($"Počet IP adres: &3{profile.Addresses.Count}&r ({profile.GetAddress() ?? "null"}");
                sb.AppendLine($"Počet sessions: &3{profile.Sessions.Count}&r");

                sb.AppendLine($"Playtime - celkem: &3{profile.GetTotalPlaytime().ToFullCzechString()}&r (od {profile.CreatedAt.ToLocalTime().ToVeCzechString()})");
                sb.AppendLine($"Playtime - dnes: &3{profile.GetTotalPlaytime(DateTimeExtensions.DayStart, DateTimeExtensions.DayEnd).ToFullCzechString()}&r");
                sb.AppendLine($"Playtime - tento týden: &3{profile.GetTotalPlaytime(DateTimeExtensions.DayStart.AddDays(-7), DateTimeExtensions.DayEnd).ToFullCzechString()}&r");
                sb.AppendLine($"Playtime - tento měsíc: &3{profile.GetTotalPlaytime(DateTimeExtensions.MonthStart, DateTimeExtensions.MonthEnd).ToFullCzechString()}&r");

                if (profile.CustomData.Count > 0)
                {

                    sb.AppendLine($"Vlastní data: &3{profile.CustomData.Count}&r");

                    foreach (var kvp in profile.CustomData)
                        sb.AppendLine($"- {kvp.Key}: &3{kvp.Value}&r");
                }

                player.SendRemoteAdminMessage(sb.ReturnStringBuilderValue(), success: true, tag: "PRTS");
            }
        });
    }
}
