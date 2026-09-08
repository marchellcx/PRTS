using System.Collections.Concurrent;
using System.Diagnostics;

using Discord;
using Discord.WebSocket;

using NiveraAPI.Console;
using NiveraAPI.IO.Configs;

using PRTS.Discord;

namespace PRTS.ScpSl.Discord;

/// <summary>
/// Represents a Discord bot for SCP:SL servers.
/// </summary>
public class ScpSlBot : DiscordBot
{
    private static volatile int countUpdateInterval = 5000;

    /// <summary>
    /// Gets or sets the interval in milliseconds at which the bot updates its player count status.
    /// </summary>
    [Config("scp-sl", "count-update-interval", "The interval in milliseconds at which the bot updates its player count status.")]
    public static int CountUpdateInterval
    {
        get => countUpdateInterval;
        set => countUpdateInterval = value;
    }

    private volatile ScpSlServer? server;
    private volatile ConcurrentQueue<KeyValuePair<ulong, string>> messages = new();

    private volatile bool countUpdateRequested;

    private volatile int maxPlayerCount;
    private volatile int currentPlayerCount;

    private volatile Stopwatch countUpdateWatch = new();

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
        countUpdateRequested = true;

        currentPlayerCount = players;
        maxPlayerCount = maxPlayers;
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

        countUpdateWatch.Restart();
        
        Task.Run(() => UpdateStatusAsync(client));
        Task.Run(() => UpdateMessagesAsync(client));
    }

    private async Task UpdateStatusAsync(DiscordSocketClient client)
    {
        while (client.ConnectionState == ConnectionState.Connected)
        {
            try
            {
                await Task.Delay(100);

                if (Server != null)
                {
                    if (countUpdateRequested || (countUpdateInterval > 0 && countUpdateWatch.ElapsedMilliseconds >= countUpdateInterval))
                    {
                        countUpdateWatch.Restart();

                        var text = string.Concat(currentPlayerCount, " / ", maxPlayerCount);
                        var status = UserStatus.Idle;

                        if (currentPlayerCount > 0)
                            status = UserStatus.Online;

                        if (client.Status != status)
                            await client.SetStatusAsync(status);

                        if (client.Activity == null || (client.Activity.Name != text && client.Activity.Details != text))
                            await client.SetCustomStatusAsync(text);

                        countUpdateRequested = false;
                    }
                }
                else
                {
                    if (countUpdateInterval > 0 && countUpdateWatch.ElapsedMilliseconds >= countUpdateInterval)
                    {
                        countUpdateWatch.Restart();

                        if (client.Status != UserStatus.DoNotDisturb)
                            await client.SetStatusAsync(UserStatus.DoNotDisturb);

                        if (client.Activity == null || (client.Activity.Name != "Disconnected!" && client.Activity.Details != "Disconnected!"))
                            await client.SetCustomStatusAsync("Disconnected!");
                    }
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
        while (client.ConnectionState == ConnectionState.Connected)
        {
            await Task.Delay(1000);

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

                    await Task.Delay(5000);
                }
            }
        }
    }
}