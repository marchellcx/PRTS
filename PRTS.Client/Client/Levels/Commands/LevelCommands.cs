using LabExtended.API;

using LabExtended.Commands;
using LabExtended.Commands.Attributes;
using LabExtended.Commands.Interfaces;

using NiveraAPI.ScpSl;
using NiveraAPI.Extensions;

using PRTS.Client.Levels.Enums;

namespace PRTS.Client.Levels;

/// <summary>
/// Provides commands for managing the level module in the application.
/// </summary>
[Command("level", "Management of the level module.")]
public class LevelCommands : CommandBase, IServerSideCommand
{
    [CommandOverload("multiplier", "Sets the XP multiplier.", null)]
    private void Multiplier(
        [CommandParameter("Multiplikátor", "Nový multiplikátor XP (1 = reset).")] int multiplier)
    {
        if (LevelModule.Singleton == null)
        {
            Fail("Služba PRTS není připojena!");
            return;
        }

        LevelModule.ExperienceMultiplier = multiplier;
        Loader.SaveConfig();

        Ok($"Multiplikátor XP nastaven na &3{multiplier}&r.");
    }

    [CommandOverload("reset", "Resets the XP of a player.", null)]
    private void Reset(        
        [CommandParameter("Hráč", "ID hráče.")]
        [CommandParameter(ParserType = typeof(ExPlayer), ParserProperty = "UserId")]
        [CommandParameter(ParserType = typeof(string))]
        string userId)
    {
        if (LevelModule.Singleton == null)
        {
            Fail("Služba PRTS není připojena!");
            return;
        }

        if (!userId.TrySplit('@', false, 2, out _))
        {
            Fail("ID není v platném formátu.");
            return;
        }

        var player = Sender;

        Ok($"Požadavek odeslán serveru. Hráč {userId} bude resetován.");

        LevelModule.Singleton.CallCmdResetXp(userId, $"Resetováno uživatelem {Sender.Nickname}", result =>
        {
            if (player?.ReferenceHub != null)
            {
                if (result)
                {
                    player.SendRemoteAdminMessage($"XP hráče &3{userId}&r resetováno.", tag: "PRTS");
                }
                else
                {
                    player.SendRemoteAdminMessage("Profil hráče nebyl nalezen.", tag: "PRTS", success: false);
                }
            }
        });
    }
    
    [CommandOverload("view", "Views the level of a player.", null)]
    private void View(        
        [CommandParameter("Hráč", "ID hráče.")]
        [CommandParameter(ParserType = typeof(ExPlayer), ParserProperty = "UserId")]
        [CommandParameter(ParserType = typeof(string))]
        string userId)
    {
        if (LevelModule.Singleton == null)
        {
            Fail("Služba PRTS není připojena!");
            return;
        }

        if (!userId.TrySplit('@', false, 2, out _))
        {
            Fail("ID není v platném formátu.");
            return;
        }

        var player = Sender;
        
        LevelModule.Singleton.CallCmdGetPlayerLevel(userId, level =>
        {
            if (player?.ReferenceHub != null)
            {
                if (level == null)
                {
                    player.SendRemoteAdminMessage($"Nepodařilo se získat level pro hráče &3{userId}&r", tag: "PRTS",
                        success: false);
                }
                else
                {
                    player.SendRemoteAdminMessage(
                        $"Hráč &3{userId}&r má level &3{level.CurLevel?.Level ?? -1}&r (&6{level.Experience}&r XP)", tag: "PRTS",
                        success: true);
                }
            }
        });
        
        Ok("Požadavek odeslán serveru.");
    }
    
    [CommandOverload("modify", "Modifies the XP of a player.", null)]
    private void Modify(
        [CommandParameter("Hráč", "ID hráče.")]
        [CommandParameter(ParserType = typeof(ExPlayer), ParserProperty = "UserId")]
        [CommandParameter(ParserType = typeof(string))]
        string userId,

        [CommandParameter("XP", "Změna XP (nezahrnuje multiplikátor).")] int xp)
    {
        if (LevelModule.Singleton == null)
        {
            Fail("Služba PRTS není připojena!");
            return;
        }

        if (!userId.TrySplit('@', false, 2, out _))
        {
            Fail($"ID není v platném formátu.");
            return;
        }

        var player = Sender;
        
        LevelModule.Singleton.CallCmdModifyXp(userId, $"Upraveno uživatelem {Sender.Nickname}", xp, result =>
        {
            if (player?.ReferenceHub != null)
            {
                var message = "";
                
                switch (result)
                {
                    case LevelModifyResult.Ok:
                        message = "XP upraveno.";
                        break;
                    
                    case LevelModifyResult.LevelUp:
                        message = "XP upraveno; hráč dostal level-up.";
                        break;
                    
                    case LevelModifyResult.LevelDown:
                        message = "XP upraveno; hráč ztratil level.";
                        break;
                    
                    case LevelModifyResult.ProfileNotFound:
                        message = "Profil hráče nebyl nalezen.";
                        break;
                }

                player.SendRemoteAdminMessage(message, result != LevelModifyResult.ProfileNotFound, tag: "PRTS");
            }
        });
        
        Ok("Požadavek odeslán.");
    }
}