using Newtonsoft.Json;

using NiveraAPI.IO.Storage;
using NiveraAPI.IO.Storage.Interfaces;

namespace PRTS.Database.Serializers;

/// <summary>
/// A serializer that uses JSON for serialization and deserialization.
/// </summary>
public class JsonSerializer : StorageSerializer
{
    /// <summary>
    /// Serializes the provided storage value into a JSON string representation.
    /// </summary>
    /// <param name="value">The storage value to serialize, which implements the IStorageValue interface.</param>
    /// <returns>A JSON string representing the serialized value.</returns>
    public override string Serialize(IStorageValue value)
    {
        return value.Serialize();
    }

    /// <summary>
    /// Deserializes the provided JSON string representation into the given storage value object.
    /// </summary>
    /// <param name="data">The JSON string to deserialize, representing the value to be stored.</param>
    /// <param name="value">The storage value object that will be populated with the deserialized data.</param>
    public override void Deserialize(string data, IStorageValue value)
    {
        value.SetValue(JsonConvert.DeserializeObject(data, value.Type)!);
    }
}