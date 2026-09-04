using NiveraAPI;
using NiveraAPI.IO.Serialization;

using NiveraAPI.Logs;
using NiveraAPI.Pooling;

using PRTS.Extensions;

namespace PRTS.Profiles.Objects;

/// <summary>
/// Provides methods for serializing and deserializing profile data.
/// </summary>
public static class ProfileSerialization
{
    private static volatile LogSink log = LogManager.GetSource("Profiles", "Serialization");

    /// <summary>
    /// Indicates whether to read rewards from the profile data during deserialization.
    /// </summary>
    public static bool ReadRewards = LibraryLoader.HasArgument("ProfileReadRewards");

    /// <summary>
    /// Deserializes a session from the provided byte reader.
    /// </summary>
    /// <param name="reader">
    /// An instance of <c>ByteReader</c> containing the serialized session data.
    /// </param>
    /// <returns>
    /// A <c>ProfileSession</c> object populated with the deserialized session data.
    /// </returns>
    public static ProfileSession DeserializeSession(ByteReader reader)
    {
        var session = new ProfileSession
        {
            Id = reader.ReadString(),
            Started = reader.ReadDate(),
            Ended = reader.ReadDate()
        };

        if (ReadRewards)
            reader.ReadIntoConcurrentBag(session.Rewards);

        return session;
    }

    /// <summary>
    /// Serializes the provided session data into the given byte writer.
    /// </summary>
    /// <param name="writer">
    /// An instance of <c>ByteWriter</c> used to write the serialized session data.
    /// </param>
    /// <param name="session">
    /// The <c>ProfileSession</c> object containing the session data to be serialized.
    /// </param>
    /// <returns>
    /// The same <c>ProfileSession</c> object that was serialized.
    /// </returns>
    public static void SerializeSession(ByteWriter writer, ProfileSession session)
    {
        writer.WriteString(session.Id);
        writer.WriteDate(session.Started);
        writer.WriteDate(session.Ended);

        if (ReadRewards)
            writer.WriteConcurrentBag(session.Rewards);
    }
    
    /// <summary>
    /// Deserializes a profile from the provided byte reader.
    /// </summary>
    /// <param name="reader">
    /// An instance of <c>ByteReader</c> containing the serialized profile data.
    /// </param>
    /// <returns>
    /// A <c>ProfileInfo</c> object populated with the deserialized data.
    /// </returns>
    public static ProfileInfo DeserializeProfile(ByteReader reader)
    {
        var profile = new ProfileInfo
        {
            Id = reader.ReadString(),
            UserId = reader.ReadString(),
            DiscordId = reader.ReadUInt64(),

            CreatedAt = reader.ReadDate(),
            ModifiedAt = reader.ReadDate(),
            LastLogin = reader.ReadDate()
        };

        reader.ReadIntoConcurrentDictionary(profile.Sessions);
        reader.ReadIntoConcurrentDictionary(profile.Nicknames);
        reader.ReadIntoConcurrentDictionary(profile.Addresses);
        reader.ReadIntoConcurrentDictionary(profile.CustomData);
        reader.ReadIntoConcurrentDictionary(profile.PropertyValues);

        return profile;
    }

    /// <summary>
    /// Serializes the given profile into the provided byte writer.
    /// </summary>
    /// <param name="writer">
    /// An instance of <c>ByteWriter</c> where the profile data will be written.
    /// </param>
    /// <param name="profile">
    /// A <c>ProfileInfo</c> object that contains the data to be serialized.
    /// </param>
    /// <param name="includeProperties">
    /// A boolean value indicating whether to include the profile's properties in the serialization.
    /// </param>
    public static void SerializeProfile(ByteWriter writer, ProfileInfo profile, bool includeProperties)
    {
        writer.WriteString(profile.Id);
        writer.WriteString(profile.UserId);
        writer.WriteUInt64(profile.DiscordId);
        
        writer.WriteDate(profile.CreatedAt);
        writer.WriteDate(profile.ModifiedAt);
        writer.WriteDate(profile.LastLogin);
        
        writer.WriteConcurrentDictionary(profile.Sessions);
        writer.WriteConcurrentDictionary(profile.Nicknames);
        writer.WriteConcurrentDictionary(profile.Addresses);
        writer.WriteConcurrentDictionary(profile.CustomData);

        if (includeProperties)
        {
            foreach (var kvp in profile.Properties)
            {
                if (!profile.PropertyValues.ContainsKey(kvp.Key) || kvp.Value.isDirty)
                {
                    try
                    {
                        using (var propertyWriter = ObjectPool<ByteWriter>.Shared.Rent())
                        {
                            propertyWriter.WriteString(kvp.Value.GetType().AssemblyQualifiedName!);

                            kvp.Value.Write(propertyWriter);

                            profile.PropertyValues[kvp.Key] = propertyWriter.ToArray();
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Error($"Error while saving profile property &3{kvp.Key}&r:\n{ex}");

                        profile.PropertyValues.TryRemove(kvp.Key, out _);
                    }
                }
            }

            writer.WriteConcurrentDictionary(profile.PropertyValues);
        }
    }
}