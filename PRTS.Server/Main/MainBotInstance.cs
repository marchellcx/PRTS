using Discord;
using Discord.WebSocket;

using NiveraAPI.Utilities;

using PRTS.Discord;
using PRTS.Punishments;

using PRTS.ScpSl;
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
    /// Gets the singleton instance of the MainBotInstance.
    /// </summary>
    public static volatile MainBotInstance Instance;

    /// <summary>
    /// Event triggered when the bot is ready and fully initialized.
    /// </summary>
    public static event Action? Ready;
    
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

        Ready?.Invoke();
    }

    private Task _OnButtonExecuted(SocketMessageComponent component)
    {
        ThreadHelper.RunOnMainThread(() =>
        {
            ScpSlMonitor.OnButton(component);
            ReportModule.OnButtonExecuted(component);    
            PunishmentManager.OnButtonExecuted(component);
        });
        
        return Task.CompletedTask;
    }
}