using System.Collections.Concurrent;

namespace PRTS.Discord;

/// <summary>
/// Represents a rate limiter for Discord messages, allowing a maximum number of messages per user within a specified time interval.
/// </summary>
public class DiscordMessageRateLimit
{
    /// <summary>
    /// Represents a single user's message timestamps within the sliding window.
    /// </summary>
    public sealed class UserWindow
    {
        /// <summary>
        /// Lock object for synchronizing access to this user's timestamps.
        /// </summary>
        public volatile object Sync = new();

        /// <summary>
        /// Timestamps of the user's messages, in order of arrival.
        /// </summary>
        public volatile ConcurrentQueue<DateTimeOffset> Timestamps = new();
    }

    private volatile ConcurrentDictionary<ulong, UserWindow> windows = new();

    /// <summary>
    /// The time interval for the sliding window. Messages older than this interval are ignored.
    /// </summary>
    public TimeSpan Interval;

    /// <summary>
    /// The maximum number of messages allowed per user within the specified interval.
    /// </summary>
    public volatile int MaxMessages;

    /// <summary>
    /// Attempts to allow a message for the specified user. Returns true if the message is allowed (i.e., the user has not exceeded the rate limit), or false if the user has exceeded the limit.
    /// </summary>
    /// <param name="userId">The ID of the user sending the message.</param>
    /// <returns>True if the message is allowed, false if the user has exceeded the rate limit.</returns>
    public bool TryAllow(ulong userId)
    {
        var now = DateTimeOffset.UtcNow;
        var window = windows.GetOrAdd(userId, static _ => new UserWindow());

        lock (window.Sync)
        {
            Prune(window.Timestamps, now);

            if (window.Timestamps.Count >= MaxMessages)
                return false;

            window.Timestamps.Enqueue(now);
            return true;
        }
    }

    /// <summary>
    /// Cleans up idle users who have not sent messages for a specified duration.
    /// </summary>
    /// <param name="idleFor">The duration of inactivity after which a user is considered idle.</param>
    public void CleanupIdleUsers(TimeSpan idleFor)
    {
        var cutoff = DateTimeOffset.UtcNow - idleFor;

        foreach (var kvp in windows)
        {
            lock (kvp.Value.Sync)
            {
                Prune(kvp.Value.Timestamps, DateTimeOffset.UtcNow);

                if (kvp.Value.Timestamps.Count == 0 ||
                    kvp.Value.Timestamps.Count > 0 && IsIdle(kvp.Value, cutoff))
                {
                    windows.TryRemove(kvp.Key, out _);
                }
            }
        }
    }

    private void Prune(ConcurrentQueue<DateTimeOffset> timestamps, DateTimeOffset now)
    {
        var oldestAllowed = now - Interval;

        while (timestamps.TryPeek(out var peek) && peek < oldestAllowed)
            timestamps.TryDequeue(out _);
    }

    private static bool IsIdle(UserWindow window, DateTimeOffset cutoff)
    {
        if (window.Timestamps.Count == 0)
            return true;

        DateTimeOffset newest = default;

        foreach (var ts in window.Timestamps)
            newest = ts;

        return newest < cutoff;
    }
}
