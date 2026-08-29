using NiveraAPI.IO.Serialization;

using NiveraAPI.IO.Storage;
using NiveraAPI.IO.Storage.Interfaces;

namespace PRTS.Database.Serializers;

/// <summary>
/// A generic serializer for storage values that utilizes byte-level operations
/// for serialization and deserialization, leveraging Base64 encoding for data representation.
/// </summary>
/// <typeparam name="T">The type of data that the serializer operates on.</typeparam>
public class ByteReaderWriterSerializer<T> : StorageSerializer
{
    /// <summary>
    /// Serializes the provided <paramref name="value"/> into a Base64 encoded string.
    /// </summary>
    /// <param name="value">The storage value to be serialized. Must be of type <see cref="StorageValue{T}"/>.</param>
    /// <returns>A Base64 encoded string representation of the serialized value.</returns>
    /// <exception cref="ArgumentException">Thrown when the provided value is not of type <see cref="StorageValue{T}"/>.</exception>
    public override string Serialize(IStorageValue value)
    {
        if (value is not StorageValue<T> castValue)
            throw new ArgumentException("Value is not a StorageValue<T>");
        
        using (var writer = ByteWriter.Get())
        {
            writer.Write(castValue.Value);
            return Convert.ToBase64String(writer.ToArray());
        }
    }

    /// <summary>
    /// Deserializes the provided <paramref name="data"/> into the specified <paramref name="value"/>.
    /// </summary>
    /// <param name="data">The Base64 encoded string containing the serialized data.</param>
    /// <param name="value">The storage value where the deserialized data will be set. Must be of type <see cref="StorageValue{T}"/>.</param>
    /// <exception cref="ArgumentException">Thrown when the provided value is not of type <see cref="StorageValue{T}"/>.</exception>
    /// <exception cref="FormatException">Thrown when the provided data is not a valid Base64 encoded string.</exception>
    public override void Deserialize(string data, IStorageValue value)
    {
        using (var reader = ByteReader.Get(Convert.FromBase64String(data)))
            value.SetValue(reader.Read<T>()!);
    }
}