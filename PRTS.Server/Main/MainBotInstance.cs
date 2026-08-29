using Discord;
using Discord.WebSocket;

using NiveraAPI.Utilities;

using PRTS.Discord;
using PRTS.Punishments;

using PRTS.ScpSl.Modules.Reports;

namespace PRTS.Main;

/// <summary>
/// Represents the main bot instance that extends the functionality of a Discord bot.
/// Handles operations such as managing roles and interacting with the primary guild.
/// This singleton class functions as the centralized entry point for bot-related operations within the PRTS system.
/// </summary>
public class MainBotInstance : DiscordBot
{
    /// <summary>
    /// A publicly accessible, static property that holds the singleton instance of the <see cref="MainBotInstance"/> class.
    /// This property ensures that there is only a single instance of the bot at any given time and prevents multiple instances
    /// from being created. It allows centralized access to the main bot functionalities and ensures that operations such as
    /// role management and guild interactions can be performed through a single shared instance.
    /// </summary>
    public static MainBotInstance Instance { get; private set; }
    
    internal MainBotInstance(ulong? primaryGuildId = null) : base("main", primaryGuildId)
    {
        if (Instance != null)
            throw new InvalidOperationException("An instance of MainBotInstance already exists.");

        Instance = this;
    }

    /// <summary>
    /// Executes actions when the bot is disconnected from Discord.
    /// </summary>
    public override void OnDisconnected()
    {
        base.OnDisconnected();
        
        Client.ButtonExecuted -= _OnButtonExecuted;
    }

    /// <summary>
    /// Executes actions when the bot successfully connects to the primary guild.
    /// </summary>
    public override void OnPrimaryGuildReached()
    {
        base.OnPrimaryGuildReached();
        
        Client.ButtonExecuted += _OnButtonExecuted;

        Status = UserStatus.Idle;
        
        ActivityText = "Primitive Rhodes Island Terminal Service";
        
        RegisterCommands<MainCommands>();
    }

    private Task _OnButtonExecuted(SocketMessageComponent component)
    {
        ThreadHelper.RunOnMainThread(() =>
        {
            ReportModule.OnButtonExecuted(component);    
            PunishmentManager.OnButtonExecuted(component);
        });
        
        return Task.CompletedTask;
    }
}