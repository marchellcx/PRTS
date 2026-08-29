using System.Collections.Concurrent;

using NiveraAPI.IO.Serialization;

namespace PRTS.Extensions;

/// <summary>
/// Provides extension methods to extend the functionality of the ByteReader class.
/// </summary>
public static class ByteReaderExtensions
{
    /// <summary>
    /// Reads key-value pairs from the specified ByteReader and returns them as a ConcurrentDictionary.
    /// </summary>
    /// <param name="reader">The ByteReader instance to read data from.</param>
    /// <typeparam name="TKey">The type of the keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of the values in the dictionary.</typeparam>
    /// <returns>A ConcurrentDictionary containing the key-value pairs read from the ByteReader.</returns>
    public static ConcurrentDictionary<TKey, TValue> ReadConcurrentDictionary<TKey, TValue>(this ByteReader reader)
    {
        var dict = new ConcurrentDictionary<TKey, TValue>();

        reader.ReadIntoConcurrentDictionary(dict);
        return dict;
    }
    
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