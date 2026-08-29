using System.Collections.Concurrent;

using Discord;
using Discord.WebSocket;

using NiveraAPI.Console;

using PRTS.Discord;
using PRTS.ScpSl.Modules.Plugins;

namespace PRTS.ScpSl.Discord;

/// <summary>
/// Represents a Discord bot for SCP:SL servers.
/// </summary>
public class ScpSlBot : DiscordBot
{
    private volatile ScpSlServer? server;
    private volatile ConcurrentQueue<KeyValuePair<ulong, string>> messages = new();
    
    /// <summary>
    /// Creates a new instance of the <see cref="ScpSlBot"/> class.
    /// </summary>
    public ScpSlBot(string alias, ulong? primaryGuildId = null) : base(alias, primaryGuildId)
    {
        
    }

    /// <summary>
    /// The server associated with the bot.
    /// </summary>
    public ScpSlServer? Server
    {
        get => server;
        set => server = value;
    }

    /// <summary>
    /// Updates the current and maximum player count for the bot and adjusts the bot's status
    /// and activity text accordingly.
    /// </summary>
    /// <param name="players">The current number of players on the server.</param>
    /// <param name="maxPlayers">The maximum number of players allowed on the server.</param>
    public void UpdatePlayerCount(int players, int maxPlayers)
    {
        if (players < 1 && Status != UserStatus.Idle)
            Status = UserStatus.Idle;
        else if (players > 0 && Status != UserStatus.Online)
            Status = UserStatus.Online;

        ActivityText = string.Concat(players, " / ", maxPlayers);
    }

    /// <summary>
    /// Queues a text message to be sent to a specified Discord channel.
    /// </summary>
    /// <param name="channelId">The ID of the Discord channel where the message should be sent.</param>
    /// <param name="message">The text message content to be queued.</param>
    public void QueueTextMessage(ulong channelId, string message)
        => messages.Enqueue(new(channelId, message));

    /// <summary>
    /// Called when the bot is connected to a server.
    /// </summary>
    public virtual void OnServerConnected()
    {

    }
    
    /// <summary>
    /// Called when the bot is disconnected from a server.
    /// </summary>
    public virtual void OnServerDisconnected()
    {
        Server = null!;
        
        Status = UserStatus.DoNotDisturb;
        
        ActivityText = "Disconnected!";
    }

    /// <summary>
    /// Executes actions when the primary guild associated with the bot is successfully reached.
    /// Invokes the base implementation and triggers an asynchronous update of queued messages.
    /// </summary>
    public override void OnPrimaryGuildReached()
    {
        base.OnPrimaryGuildReached();

        var client = Client;

        RegisterCommands<ScpSlCommands>();
        
        Status = UserStatus.DoNotDisturb;
        
        ActivityText = "Disconnected!";
        
        Task.Run(() => UpdateStatusAsync(client));
        Task.Run(() => UpdateMessagesAsync(client));
    }

    private async Task UpdateStatusAsync(DiscordSocketClient client)
    {
        while (client.ConnectionState == ConnectionState.Connected)
        {
            try
            {
                await Task.Delay(2000);

                if (Server != null)
                {
                    var text = string.Concat(Server.Players, " / ", Server.MaxPlayers);
                    var status = UserStatus.Idle;

                    if (Server.Players > 0)
                        status = UserStatus.Online;

                    if (client.Status != status)
                        await client.SetStatusAsync(status);
                    
                    await client.SetCustomStatusAsync(text);
                }
                else
                {
                    if (client.Status != UserStatus.DoNotDisturb)
                        await client.SetStatusAsync(UserStatus.DoNotDisturb);
                    
                    await client.SetCustomStatusAsync("Disconnected!");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to update status:\n{ex}");

                await Task.Delay(8000);
            }
        }
    }

    private async Task UpdateMessagesAsync(DiscordSocketClient client)
    {
        Log.Info("Started message update thread");
        
        while (client.ConnectionState == ConnectionState.Connected)
        {
            while (messages.TryDequeue(out var message))
            {
                await Task.Delay(100);
                
                try
                {
                    if (string.IsNullOrWhiteSpace(message.Value))
                    {
                        ConsoleOutput.Write($"Attempted to send an empty message to channel {message.Key}!", ConsoleColor.Yellow);
                        return;
                    }

                    if (!IsConnected)
                    {
                        ConsoleOutput.Write("Error while sending Discord message: Discord bot not connected!", ConsoleColor.Yellow);
                        continue;
                    }

                    var channel = await client.GetChannelAsync(message.Key);

                    if (channel == null)
                    {
                        ConsoleOutput.Write($"Error while sending Discord message to channel {message.Key}: Channel not found!", ConsoleColor.Yellow);
                        continue;
                    }

                    if (channel is not SocketTextChannel textChannel)
                    {
                        ConsoleOutput.Write($"Error while sending Discord message to channel {message.Key}: Channel is not a text channel!", ConsoleColor.Yellow);
                        continue;
                    }

                    await textChannel.SendMessageAsync(message.Value, false, null, null, AllowedMentions.None);
                }
                catch (Exception ex)
                {
                    ConsoleOutput.Write($"Error while sending Discord message to channel {message.Key}:\n{ex}", ConsoleColor.Red);
                }
            }
        }
        
        Log.Info("Stopped message update thread");
    }
}