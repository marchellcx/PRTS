using Discord;
using Discord.Interactions;

using NiveraAPI.Console;
using NiveraAPI.IO.Configs;

using PRTS.Discord;

namespace PRTS.ScpSl.Discord;

public class ScpSlCommands : InteractionModuleBase<SocketInteractionContext>
{
    /// <summary>
    /// List of commands that are prohibited from being used by administrators.
    /// </summary>
    [Config("scp-sl", "prohibited-commands", "List of commands that are prohibited from being used by administrators.")]
    public static string[] ProhibitedCommands { get; set; } = [];

    /// <summary>
    /// List of commands that are whitelisted for administrators.
    /// </summary>
    [Config("scp-sl", "whitelisted-commands", "List of commands that are whitelisted for administrators.")]
    public static string[] WhitelistedCommands { get; set; } = [];
}