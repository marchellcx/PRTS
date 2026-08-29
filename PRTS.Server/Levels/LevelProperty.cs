using System.Collections.Concurrent;

using NiveraAPI.IO.Serialization;

using PRTS.Profiles;

namespace PRTS.Levels;

/// <summary>
/// Represents a property that stores user level and experience points.
/// </summary>
public class LevelProperty : ProfileProperty
{
    private volatile int level = 0;
    private volatile int experience = 0;

    private volatile ConcurrentBag<LevelLog> logs = new();

    /// <summary>
    /// The user's level.
    /// </summary>
    public int Level
    {
        get => level;
        set
        {
            if (value != level)
            {
                level = value;

                IsDirty = true;
            }
        }
    }

    /// <summary>
    /// The user's experience points.
    /// </summary>
    public int Experience
    {
        get => experience;
        set
        {
            if (value != experience)
            {
                experience = value;

                IsDirty = true;
            }
        }
    }

    /// <summary>
    /// A collection of <see cref="LevelLog"/> instances representing the history of level changes for the associated profile.
    /// </summary>
    public ConcurrentBag<LevelLog> Logs => logs;

    /// <summary>
    /// Retrieves the collection of level logs ordered by their timestamp.
    /// </summary>
    /// <returns>
    /// An <see cref="IEnumerable{T}"/> of <see cref="LevelLog"/> objects,
    /// sorted in ascending order by the <see cref="LevelLog.Time"/> property.
    /// </returns>
    public IEnumerable<LevelLog> TimeOrderedLogs()
        => logs.OrderBy(x => x.Time.Ticks);

    /// <summary>
    /// Retrieves the collection of level logs that satisfy the specified condition.
    /// </summary>
    /// <param name="predicate">
    /// A <see cref="Predicate{T}"/> delegate that defines the condition each <see cref="LevelLog"/> must satisfy.
    /// </param>
    /// <returns>
    /// An <see cref="IEnumerable{T}"/> of <see cref="LevelLog"/> objects that match the specified condition.
    /// </returns>
    public IEnumerable<LevelLog> FilteredLogs(Predicate<LevelLog> predicate)
        => logs.Where(x => predicate(x));

    /// <summary>
    /// Retrieves the collection of level logs that satisfy the specified predicate,
    /// ordered by their timestamp.
    /// </summary>
    /// <param name="predicate">
    /// A <see cref="Predicate{T}"/> used to filter the <see cref="LevelLog"/> objects.
    /// Logs that satisfy this predicate will be included in the results.
    /// </param>
    /// <returns>
    /// An <see cref="IEnumerable{T}"/> of <see cref="LevelLog"/> objects,
    /// filtered by the given predicate and sorted in ascending order by the <see cref="LevelLog.Time"/> property.
    /// </returns>
    public IEnumerable<LevelLog> TimeOrderedFilteredLogs(Predicate<LevelLog> predicate)
        => logs.Where(x => predicate(x)).OrderBy(x => x.Time.Ticks);

    /// <summary>
    /// Clears all level logs from the collection.
    /// </summary>
    public void ClearLogs()
    {
        logs.Clear();

        IsDirty = true;
    }
    
    /// <summary>
    /// Adds a log entry to the collection of level logs for the associated profile.
    /// </summary>
    /// <param name="log">
    /// The <see cref="LevelLog"/> instance representing the log entry to be added, which contains
    /// details about the level change, including change amount, before/after levels, reason, and timestamp.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the <paramref name="log"/> argument is null.
    /// </exception>
    public void AddLog(LevelLog log)
    {
        if (log == null)
            throw new ArgumentNullException(nameof(log));

        logs.Add(log);

        IsDirty = true;
    }

    /// <summary>
    /// Reads and deserializes level property data from the provided byte stream.
    /// </summary>
    /// <param name="reader">
    /// The <see cref="ByteReader"/> instance used to read the serialized data, which includes
    /// level information, experience points, and associated level logs.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the <paramref name="reader"/> argument is null.
    /// </exception>
    /// <exception cref="InvalidDataException">
    /// Thrown when the data read from the <paramref name="reader"/> is incomplete or improperly formatted.
    /// </exception>
    public override void Read(ByteReader reader)
    {
        level = reader.ReadInt32();
        experience = reader.ReadInt32();
        
        logs.Clear();

        var logCount = reader.ReadInt32();

        for (var x = 0; x < logCount; x++)
        {
            var log = new LevelLog
            {
                Time = reader.ReadDate(),

                ReasonId = reader.ReadString(),
                ReasonMessage = reader.ReadString(),

                Change = reader.ReadInt32(),

                LevelAfter = reader.ReadInt32(),
                LevelBefore = reader.ReadInt32()
            };

            logs.Add(log);
        }
    }

    /// <summary>
    /// Writes the serialization data for the level property, including level, experience, and associated logs, to the specified writer.
    /// </summary>
    /// <param name="writer">
    /// The <see cref="ByteWriter"/> instance used to serialize the level property data. This includes
    /// the integer values for level and experience, the log count, and the detailed properties of each
    /// level log.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the <paramref name="writer"/> argument is null.
    /// </exception>
    public override void Write(ByteWriter writer)
    {
        writer.WriteInt32(level);
        writer.WriteInt32(experience);      
        
        writer.WriteInt32(logs.Count);
        
        foreach (var log in logs)
        {
            writer.WriteDate(log.Time);
            
            writer.WriteString(log.ReasonId);
            writer.WriteString(log.ReasonMessage);
            
            writer.WriteInt32(log.Change);
            
            writer.WriteInt32(log.LevelAfter);
            writer.WriteInt32(log.LevelBefore);           
        }
    }
}