using System.Collections.Concurrent;

using NiveraAPI.IO.Serialization;

namespace PRTS.Extensions;

/// <summary>
/// Provides extension methods for reading data using a ByteReader.
/// </summary>
public static class ByteReaderExtensions
{
    /// <summary>
    /// Reads key-value pairs from the specified ByteReader and adds them to the provided ConcurrentDictionary.
    /// </summary>
    /// <param name="reader">The ByteReader instance to read data from.</param>
    /// <param name="target">The ConcurrentDictionary to populate with the read key-value pairs.</param>
    /// <typeparam name="TKey">The type of the keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of the values in the dictionary.</typeparam>
    public static void ReadIntoConcurrentDictionary<TKey, TValue>(this ByteReader reader,
        ConcurrentDictionary<TKey, TValue> target)
    {
        target.Clear();
        
        var count = reader.ReadInt32();

        for (var x = 0; x < count; x++)
        {
            var key = reader.Read<TKey>();
            var value = reader.Read<TValue>();
            
            target.TryAdd(key, value);
        }
    }
}