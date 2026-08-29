using NiveraAPI.IO.Storage;

using PRTS.Profiles.Objects;

namespace PRTS.Profiles;

/// <summary>
/// Extension methods for <see cref="StorageValue{T}"/> that provide additional functionality for profiles.
/// </summary>
public static class ProfileExtensions
{
    /// <summary>
    /// Retrieves an existing property of the specified type from the profile by name, or adds a new one if it does not exist.
    /// </summary>
    /// <typeparam name="T">The type of the property to retrieve or add. This type must inherit from <see cref="ProfileProperty"/> and have a parameterless constructor.</typeparam>
    /// <param name="profile">The profile from which the property is retrieved or added. The profile serves as a storage reference for the property.</param>
    /// <param name="name">The name of the property to retrieve or add. This is used as the key to identify the property within the profile.</param>
    /// <param name="propertySetup">
    /// An optional callback that allows additional configuration of the property after it has been created but before it is added to the profile.
    /// </param>
    /// <returns>
    /// The retrieved or newly added instance of the property of type <typeparamref name="T"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="profile"/> argument is null.</exception>
    public static T GetOrAddProperty<T>(this StorageValue<ProfileInfo> profile, string name,
        Action<T>? propertySetup = null)
        where T : ProfileProperty, new()
    {
        if (profile == null)
            throw new ArgumentNullException(nameof(profile));

        if (profile.Value.TryGetProperty<T>(name, out var profileProperty))
            return profileProperty;

        profileProperty = new();
        profileProperty.Profile = profile;
        
        propertySetup?.Invoke(profileProperty);

        profile.Value.Properties.TryAdd(name, profileProperty);
        
        profileProperty.OnAdded();

        profile.IsDirty = true;
        return profileProperty;
    }
}