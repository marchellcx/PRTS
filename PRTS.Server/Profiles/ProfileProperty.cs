using NiveraAPI.IO.Serialization;
using NiveraAPI.IO.Storage;
using PRTS.Profiles.Objects;

namespace PRTS.Profiles;

/// <summary>
/// Represents a base class for a profile property, which defines data and behaviors associated
/// with a profile in the system. This class is intended to be extended by specific property implementations.
/// </summary>
public abstract class ProfileProperty
{
    internal volatile bool isDirty;
    internal volatile StorageValue<ProfileInfo> profile;

    /// <summary>
    /// Whether the property has been modified since it was last read or written.
    /// </summary>
    public bool IsDirty
    {
        get => isDirty;
        set => isDirty = value;
    }

    /// <summary>
    /// The profile that owns this property.
    /// </summary>
    public StorageValue<ProfileInfo> Profile
    {
        get => profile;
        set => profile = value;
    }
    
    /// <summary>
    /// Called when the property is added to a profile.
    /// </summary>
    public virtual void OnAdded()
    {
        
    }

    /// <summary>
    /// Called when the property is removed from a profile.
    /// </summary>
    public virtual void OnRemoved()
    {
        
    }

    /// <summary>
    /// Called when the property is updated.
    /// </summary>
    public virtual void OnUpdated()
    {
        
    }

    /// <summary>
    /// Reads the data for the profile property from the specified byte reader.
    /// </summary>
    /// <param name="reader">The byte reader from which the property data will be read.</param>
    public abstract void Read(ByteReader reader);

    /// <summary>
    /// Writes the data for the profile property to the specified byte writer.
    /// </summary>
    /// <param name="writer">The byte writer to which the property data will be written.</param>
    public abstract void Write(ByteWriter writer);
}