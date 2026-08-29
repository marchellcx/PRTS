using Discord;
using Discord.Rest;

using NiveraAPI.IO.Storage;

using PRTS.Database.Attributes;
using PRTS.Database.Serializers;

namespace PRTS.Discord.MessageCache;

/// <summary>
/// Provides functionality for caching and retrieving Discord messages within a shared storage directory.
/// This class offers methods to store messages using a unique identifier and retrieve them for later use.
/// </summary>
public static class CachedDiscordMessageStorage
{
    /// <summary>
    /// The storage directory for cached Discord messages.
    /// </summary>
    [DbStorage("cached-discord-messages", typeof(ByteReaderWriterSerializer<CachedDiscordMessage>))]
    public static volatile StorageDirectory Messages;

    /// <summary>
    /// Caches a Discord message in storage using the specified message ID.
    /// </summary>
    /// <param name="message">The <see cref="IUserMessage"/> instance representing the Discord message to cache.</param>
    /// <param name="messageId">The unique identifier to associate with the cached message in storage.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="message"/> is <see langword="null"/> or if <paramref name="messageId"/> is null or empty.
    /// </exception>
    public static CachedDiscordMessage CacheMessage(this IMessage message, string messageId)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));
        
        if (message is not RestUserMessage userMessage)
            throw new ArgumentException("Message must be a RestUserMessage", nameof(message));
        
        if (string.IsNullOrEmpty(messageId))
            throw new ArgumentNullException(nameof(messageId));
        
        return Messages.AddStorageValue(messageId, () => new CachedDiscordMessage(userMessage)).Value;
    }

    /// <summary>
    /// Attempts to retrieve a cached Discord message from storage using the specified message ID.
    /// </summary>
    /// <param name="messageId">The unique identifier of the message to retrieve.</param>
    /// <param name="cachedDiscordMessage">
    /// When this method returns, contains the <see cref="CachedDiscordMessage"/> associated with the given message ID,
    /// if it exists in storage. This parameter is passed uninitialized.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if a message with the specified ID exists in storage; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool TryGetMessage(string messageId, out CachedDiscordMessage cachedDiscordMessage)
    {
        cachedDiscordMessage = null!;

        if (Messages == null)
            return false;
        
        return Messages.TryGetValue(messageId, out cachedDiscordMessage);
    }

    private static void StorageInit_Messages()
    {
        foreach (var kvp in Messages.Values)
        {
            if (kvp.Value is not StorageValue<CachedDiscordMessage> castValue)
                continue;

            castValue.Value.TryResolve();
        }
    }
}