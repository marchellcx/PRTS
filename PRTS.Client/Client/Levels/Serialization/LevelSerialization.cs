using NiveraAPI.IO.Serialization;

using PRTS.Client.Levels.Objects;

namespace PRTS.Client.Levels.Serialization;

/// <summary>
/// Provides serialization methods for reading LevelInfo objects from a ByteReader.
/// </summary>
public static class LevelSerialization
{
    /// <summary>
    /// Reads a LevelInfo object from the provided ByteReader.
    /// </summary>
    /// <param name="reader">The ByteReader to read data from.</param>
    /// <returns>A LevelInfo object populated with data from the reader.</returns>
    [Serializer]
    public static LevelInfo ReadLevelInfo(ByteReader reader)
    {
        var info = new LevelInfo
        {
            Level = reader.ReadInt32(),
            Experience = reader.ReadInt32(),
            IsMaxLevel = reader.ReadBool(),
            MilestoneName = reader.ReadString()
        };

        return info;
    }

    /// <summary>
    /// Reads a LevelData object from the provided ByteReader.
    /// </summary>
    /// <param name="reader">The ByteReader to read data from.</param>
    /// <returns>A LevelData object populated with data from the reader.</returns>
    [Serializer]
    public static LevelData ReadLevelData(ByteReader reader) 
    { 
        var data = new LevelData
        {
            curLevelNum = reader.ReadInt32(),

            Experience = reader.ReadInt32()
        };

        return data;
    }
}