using LabExtended.API;

using LabExtended.Commands;
using LabExtended.Commands.Attributes;
using LabExtended.Commands.Interfaces;

using NiveraAPI.Extensions;

using PRTS.Client.Levels.Enums;

namespace PRTS.Client.Levels;

/// <summary>
/// Provides commands for managing the level module in the application.
/// </summary>
[Command("level", "Management of the level module.")]
public class LevelCommands : CommandBase, IServerSideCommand
{
    [CommandOverload("reset", "Resets the XP of a player.", null)]
    private void Reset(        
        [CommandParameter("Hráč", "ID hráče.")]
        [CommandParameter(ParserType = typeof(ExPlayer), ParserProperty = "UserId")]
        [CommandParameter(ParserType = typeof(string))]
        string userId,
        
        [CommandParameter("ID", "ID důvodu.")] string reasonId,
        [CommandParameter("Zpráva", "Informační zpráva k důvodu.")] string reasonMessage)
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

        LevelModule.Singleton.CallCmdResetXp(userId, reasonId, reasonMessage, result =>
        {
            if (player?.ReferenceHub != null)
            {
                if (result)
                {
                    player.SendRemoteAdminMessage($"XP hráče {userId} resetováno.", tag: "PRTS");
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
        
        LevelModule.Singleton.CallCmdGetLevel(userId, levels =>
        {
            if (player?.ReferenceHub != null)
            {
                if (levels == null)
                {
                    player.SendRemoteAdminMessage($"Nepodařilo se získat level pro hráče {userId}", tag: "PRTS",
                        success: false);
                }
                else
                {
                    player.SendRemoteAdminMessage(
                        $"Hráč {userId} má level {levels.Value.Level} ({levels.Value.Experience} XP)", tag: "PRTS",
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

        [CommandParameter("XP", "Změna XP.")] int xp,
        
        [CommandParameter("ID", "ID důvodu.")] string reasonId,
        [CommandParameter("Zpráva", "Informační zpráva k důvodu.")] string reasonMessage)
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
        
        LevelModule.Singleton.CallCmdModifyXp(userId, reasonId, reasonMessage, xp, result =>
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