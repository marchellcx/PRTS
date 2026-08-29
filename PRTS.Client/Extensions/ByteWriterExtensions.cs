using System.Collections.Concurrent;

using NiveraAPI.IO.Serialization;

namespace PRTS.Extensions;

/// <summary>
/// Provides extension methods for writing data using a ByteWriter.
/// </summary>
public static class ByteWriterExtensions
{
    /// <summary>
    /// Writes the contents of a <see cref="ConcurrentDictionary{TKey, TValue}"/> to a ByteWriter,
    /// including both keys and corresponding values.
    /// </summary>
    /// <param name="writer">The <see cref="ByteWriter"/> instance used to serialize the dictionary data.</param>
    /// <param name="target">The <see cref="ConcurrentDictionary{TKey, TValue}"/> to be serialized.</param>
    /// <typeparam name="TKey">The type of the keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of the values in the dictionary.</typeparam>
    public static void WriteConcurrentDictionary<TKey, TValue>(this ByteWriter writer,
        ConcurrentDictionary<TKey, TValue> target)
    {
        writer.WriteInt32(target.Count);

        foreach (var kvp in target)
        {
            writer.Write(kvp.Key);
            writer.Write(kvp.Value);
        }       
    }
}