using NiveraAPI.IO.Configs;
using NiveraAPI.IO.Serialization;

using PRTS.Profiles;

using System.Collections.Concurrent;

namespace PRTS.Levels.Properties;

/// <summary>
/// Represents a property that holds a log of level changes for a player profile. This property is used to track the history of level changes, including the time of change, reason for change, amount of change, and experience prior to the change.
/// </summary>
public class LevelLogsProperty : ProfileProperty
{
    /// <summary>
    /// Gets or sets the maximum number of level log entries to keep in the log. This value is configurable and determines how many recent level change events are retained in the log for a player profile.
    /// </summary>
    [Config("level-manager", "max-logs", "The maximum number of level log entries to keep in the log.")]
    public static int MaxLogs { get; set; } = 100;

    /// <summary>
    /// Represents a single entry in the level log, containing information about a level change event.
    /// </summary>
    public struct LevelLogEntry
    {
        /// <summary>
        /// The UTC time when the level change occurred.
        /// </summary>
        public readonly DateTime UtcTime;

        /// <summary>
        /// The reason for the level change, if any. This can be null if no specific reason is provided.
        /// </summary>
        public readonly string? Reason;

        /// <summary>
        /// The amount of change in the level. This can be positive (level up) or negative (level down).
        /// </summary>
        public readonly int Change;

        /// <summary>
        /// The experience prior to the level change. This represents the experience points the player had before the level change occurred.
        /// </summary>
        public readonly int Experience;

        /// <summary>
        /// Initializes a new instance of the LevelLogEntry struct with the specified parameters.
        /// </summary>
        /// <param name="utcTime">The UTC time when the level change occurred.</param>
        /// <param name="reason">The reason for the level change, if any. This can be null if no specific reason is provided.</param>
        /// <param name="change">The amount of change in the level. This can be positive (level up) or negative (level down).</param>
        /// <param name="experience">The experience prior to the level change. This represents the experience points the player had before the level change occurred.</param>
        public LevelLogEntry(DateTime utcTime, string? reason, int change, int experience)
        {
            UtcTime = utcTime;
            Reason = reason;
            Change = change;
            Experience = experience;
        }
    }

    /// <summary>
    /// A thread-safe collection that holds the log entries for level changes. 
    /// Each entry contains information about a specific level change event, including the time of change, reason for change, amount of change, and experience prior to the change.
    /// </summary>
    public volatile ConcurrentBag<LevelLogEntry> Logs = new();

    /// <summary>
    /// Adds a new log entry to the Logs collection with the specified current experience, experience change, and reason for the change. The log entry is timestamped with the current UTC time.
    /// </summary>
    /// <param name="currentExperience">The experience points the player had before the level change occurred.</param>
    /// <param name="experienceChange">The amount of change in the level. This can be positive (level up) or negative (level down).</param>
    /// <param name="reason">The reason for the level change, if any. This can be null if no specific reason is provided.</param>
    public void AddLog(int currentExperience, int experienceChange, string? reason)
    {
        if (MaxLogs > 0 && Logs.Count >= MaxLogs)
            Logs.Clear();

        Logs.Add(new(DateTime.UtcNow, reason, experienceChange, currentExperience));

        IsDirty = true;
    }

    /// <summary>
    /// Clears all log entries from the Logs collection. This method removes all existing level change history for the player profile.
    /// </summary>
    public void ClearLogs()
    {
        if (Logs.Count > 0)
        {
            Logs.Clear();

            IsDirty = true;
        }
    }

    /// <summary>
    /// Reads the level log entries from a ByteReader and populates the Logs collection.
    /// </summary>
    /// <param name="reader">The ByteReader to read the level log entries from.</param>
    public override void Read(ByteReader reader)
    {
        Logs.Clear();

        var count = reader.ReadInt32();

        for (var x  = 0; x < count; x++)
        {
            var utcTime = reader.ReadDate();
            var reason = reader.ReadString();
            var change = reader.ReadInt32();
            var experience = reader.ReadInt32();

            Logs.Add(new LevelLogEntry(utcTime, reason, change, experience));
        }
    }

    /// <summary>
    /// Writes the level log entries from the Logs collection to a ByteWriter.
    /// </summary>
    /// <param name="writer">The ByteWriter to write the level log entries to.</param>
    public override void Write(ByteWriter writer)
    {
        writer.WriteInt32(Logs.Count);

        foreach (var log in Logs)
        {
            writer.WriteDate(log.UtcTime);
            writer.WriteString(log.Reason!);
            writer.WriteInt32(log.Change);
            writer.WriteInt32(log.Experience);
        }
    }
}