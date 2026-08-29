using NiveraAPI.IO.Serialization;

using PRTS.Extensions;
using PRTS.Client.Profiles.Sessions;

namespace PRTS.Client.Profiles.Objects;

/// <summary>
/// Provides methods for serializing and deserializing profile data.
/// </summary>
public static class ProfileSerialization
{
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

        return session;
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

        return profile;
    }
}