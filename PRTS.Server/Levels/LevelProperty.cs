using NiveraAPI.IO.Serialization;

using PRTS.Profiles;

namespace PRTS.Levels;

/// <summary>
/// Represents a property that stores user level and experience points.
/// </summary>
public class LevelProperty : ProfileProperty
{
    private volatile int experience = 0;

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
        experience = reader.ReadInt32();
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
        writer.WriteInt32(experience);      
    }
}