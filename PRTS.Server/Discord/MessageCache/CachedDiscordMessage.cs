using Discord;
using Discord.Rest;
using Discord.WebSocket;

namespace PRTS.Discord.MessageCache;

/// <summary>
/// Represents a cached Discord message with associated metadata, including the guild,
/// channel, and message identifiers. Provides the ability to resolve and populate
/// the corresponding <see cref="SocketGuild"/>, <see cref="SocketTextChannel"/>,
/// and <see cref="IUserMessage"/> objects from the Discord API context.
/// </summary>
public class CachedDiscordMessage
{
    private volatile SocketGuild? guild;
    private volatile SocketTextChannel? channel;
    private volatile RestUserMessage? message;
    
    /// <summary>
    /// The ID of the guild where the message was sent.
    /// </summary>
    public ulong GuildId { get; set; }
    
    /// <summary>
    /// The ID of the channel where the message was sent.
    /// </summary>
    public ulong ChannelId { get; set; }
    
    /// <summary>
    /// The ID of the message.
    /// </summary>
    public ulong MessageId { get; set; }

    /// <summary>
    /// The resolved message object.
    /// </summary>
    public RestUserMessage? Message => message;
    
    /// <summary>
    /// The resolved guild object.
    /// </summary>
    public SocketGuild? Guild => guild;

    /// <summary>
    /// The resolved channel object.
    /// </summary>
    public SocketTextChannel? Channel => channel;

    /// <summary>
    /// Indicates whether all required objects have been resolved.
    /// </summary>
    public bool WasResolved => Message != null;

    /// <summary>
    /// Creates a new instance of <see cref="CachedDiscordMessage"/>.
    /// </summary>
    public CachedDiscordMessage()
    {
        
    }

    /// <summary>
    /// Creates a new instance of <see cref="CachedDiscordMessage"/> with the provided identifiers.
    /// </summary>
    public CachedDiscordMessage(ulong guildId, ulong channelId, ulong messageId)
    {
        GuildId = guildId;
        ChannelId = channelId;
        MessageId = messageId;       
    }

    /// <summary>
    /// Creates a new instance of <see cref="CachedDiscordMessage"/> with the provided message.
    /// </summary>
    public CachedDiscordMessage(RestUserMessage message)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));
        
        this.message = message;
        this.channel = message.Channel as SocketTextChannel;
        this.guild = this.channel?.Guild;       
        
        GuildId = this.guild?.Id ?? 0;
        ChannelId = this.channel?.Id ?? 0;
        MessageId = message.Id;      
    }

    /// <summary>
    /// Attempts to resolve and populate the associated Discord Guild, Channel, and Message
    /// objects synchronously using the relevant identifiers (GuildId, ChannelId, and MessageId).
    /// This method delegates the resolution process to an asynchronous task, ensuring that
    /// the resolution operation is initiated without directly awaiting or blocking.
    /// </summary>
    public void TryResolve()
    {
        var guildId = GuildId;
        var channelId = ChannelId;
        var messageId = MessageId;       
        
        Task.Run(async () => await TryResolveAsync(guildId, channelId, messageId));       
    }

    /// <summary>
    /// Modifies the message using the provided <see cref="MessageProperties"/> action.
    /// This operation requires the message to be resolved prior to modification.
    /// </summary>
    /// <param name="action">A delegate that specifies the modifications to apply to the message.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the <paramref name="action"/> parameter is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the message could not be resolved before attempting to modify it.</exception>
    public async Task ModifyAsync(Action<MessageProperties> action)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));

        if (!WasResolved)
            await TryResolveAsync(GuildId, ChannelId, MessageId);
        
        if (!WasResolved)
            throw new InvalidOperationException("Message was not resolved!");
        
        await Message!.ModifyAsync(action);       
    }

    /// <summary>
    /// Attempts to resolve the associated Discord Guild, Channel, and Message objects
    /// based on the provided identifiers (GuildId, ChannelId, and MessageId) within
    /// the current application context.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task completes
    /// when the Guild, Channel, and Message are resolved or the search operation ends
    /// without finding a match.</returns>
    public async Task TryResolveAsync(ulong guildId, ulong channelId, ulong messageId)
    {
        foreach (var kvp in DiscordBot.Bots)
        {
            if (kvp.Value.Client == null)
                continue;

            foreach (var guild in kvp.Value.Client.Guilds)
            {
                if (guild.Id != guildId)
                    continue;
                
                this.guild = guild;
                
                foreach (var channel in guild.TextChannels)
                {
                    if (channel.Id != channelId)
                        continue;

                    this.channel = channel;
                    this.message = await channel.GetMessageAsync(messageId) as RestUserMessage;
                }
            }
        }
    }
}