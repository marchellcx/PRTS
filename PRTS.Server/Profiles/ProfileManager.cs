using Fergun.Interactive;

using NiveraAPI;
using NiveraAPI.Extensions;

using NiveraAPI.IO.Storage;
using NiveraAPI.IO.Serialization;

using NiveraAPI.Logs;
using NiveraAPI.Pooling;

using PRTS.Database.Attributes;
using PRTS.Database.Serializers;

using PRTS.Profiles.Objects;
using PRTS.Database;

namespace PRTS.Profiles;

/// <summary>
/// Provides functionality for managing user profiles and their associated properties.
/// </summary>
public static class ProfileManager
{
    private static volatile LogSink log = LogManager.GetSource("Profiles", "Manager");

    /// <summary>
    /// An event used to provide a mechanism for constructing profile embed pages.
    /// This event allows subscribers to generate customized page builders based on
    /// the provided profile information and a list of current page builders.
    /// It facilitates dynamic assembly of embed pages associated with user profiles.
    /// </summary>
    public static event Action<ProfileInfo, Func<PageBuilder>, List<IPageBuilder>>? ProfileEmbedBuilder;
    
    /// <summary>
    /// Represents a centralized storage directory that manages player profile data.
    /// This storage is configured with a specific database storage attribute and serializer
    /// to handle persistence and retrieval of profile-related information.
    /// </summary>
    [DbStorage("player-profiles", typeof(ByteReaderWriterSerializer<ProfileInfo>))]
    public static volatile StorageDirectory Profiles;

    /// <summary>
    /// Gets the dictionary of custom properties associated with profiles.
    /// </summary>
    public static Dictionary<string, Type> Properties { get; } = new();

    /// <summary>
    /// Attempts to remove the profile associated with the specified user ID.
    /// </summary>
    /// <param name="userId">
    /// The unique identifier of the user whose profile should be removed.
    /// </param>
    /// <returns>
    /// <c>true</c> if the profile was successfully removed; otherwise, <c>false</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="userId"/> is <c>null</c> or an empty string.
    /// </exception>
    public static bool TryRemoveProfileByUserId(string userId)
    {
        if (string.IsNullOrEmpty(userId))
            throw new ArgumentNullException(nameof(userId));

        if (!TryGetProfileByUserId(userId, out var profile))
            return false;

        foreach (var kvp in profile.Value.Properties)
        {
            try
            {
                kvp.Value.OnRemoved();
            }
            catch (Exception ex)
            {
                log.Error($"Failed while removing property &1{kvp.Key}&r from profile:\n{ex}");
            }
        }
        
        profile.Value.Sessions.Clear();
        profile.Value.Addresses.Clear();
        profile.Value.Nicknames.Clear();
        profile.Value.Properties.Clear();
        profile.Value.CustomData.Clear();
        profile.Value.PropertyValues.Clear();
        
        return Profiles.RemoveStorageValue(profile.Name);
    }

    /// <summary>
    /// Creates a new profile associated with the specified Discord ID or retrieves an existing one if it already exists.
    /// </summary>
    /// <param name="discordId">The unique identifier of the Discord user for whom the profile is being created or retrieved.</param>
    /// <returns>A <see cref="StorageValue{ProfileInfo}"/> containing the profile information. If a profile already exists for the specified Discord ID, it will return the existing profile. Otherwise, it creates and returns a new profile.</returns>
    public static StorageValue<ProfileInfo> GetOrAddProfileWithDiscordId(ulong discordId)
    {
        if (TryGetProfile(x => x.DiscordId == discordId, out var profile))
            return profile;

        var id = DbManager.NewId;
        var value = Profiles.AddStorageValue(id, () => new ProfileInfo());

        value.Value.Id = id;
        value.Value.DiscordId = discordId;
        value.Value.CreatedAt = DateTime.UtcNow;
        value.Value.ModifiedAt = DateTime.UtcNow;

        foreach (var kvp in Properties)
        {
            try
            {
                if (Activator.CreateInstance(kvp.Value) is ProfileProperty profileProperty)
                {
                    profileProperty.Profile = value;
                    profileProperty.OnAdded();

                    value.Value.Properties.TryAdd(kvp.Key, profileProperty);
                }
                else
                {
                    log.Warn($"Could not create property &1{kvp.Value.Name}&r when adding new profile!");
                }
            }
            catch (Exception ex)
            {
                log.Error($"Could not add profile property to new profile:\n{ex}");
            }
        }

        log.Debug($"Created new profile with ID &1{id}&r for Discord ID &3{discordId}&r!");
        return value;
    }

    /// <summary>
    /// Creates a new profile associated with the specified user ID or retrieves an existing one if it already exists.
    /// </summary>
    /// <param name="userId">
    /// The unique identifier of the user for whom the profile is being created or retrieved.
    /// </param>
    /// <returns>
    /// A <see cref="StorageValue{ProfileInfo}"/> containing the profile information. If a profile already exists for
    /// the specified user ID, it will return the existing profile. Otherwise, it creates and returns a new profile.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="userId"/> is <c>null</c> or an empty string.
    /// </exception>
    public static StorageValue<ProfileInfo> AddProfileWithUserId(string userId)
    {
        if (string.IsNullOrEmpty(userId))
            throw new ArgumentNullException(nameof(userId));
        
        if (TryGetProfileByUserId(userId, out var profile))
            return profile;

        var id = DbManager.NewId;
        var value = Profiles.AddStorageValue(id, () => new ProfileInfo());

        value.Value.Id = id;
        value.Value.UserId = userId;

        value.Value.CreatedAt = DateTime.UtcNow;
        value.Value.ModifiedAt = DateTime.UtcNow;

        foreach (var kvp in Properties)
        {
            try
            {
                if (Activator.CreateInstance(kvp.Value) is ProfileProperty profileProperty)
                {
                    value.Value.Properties.TryAdd(kvp.Key, profileProperty);
                }
                else
                {
                    log.Warn($"Could not create property &1{kvp.Value.Name}&r when adding new profile!");
                }
            }
            catch (Exception ex)
            {
                log.Error($"Could not add profile property to new profile:\n{ex}");
            }
        }

        log.Debug($"Created new profile with ID &1{id}&r for user ID &3{userId}&r!");
        return value;
    }

    /// <summary>
    /// Adds a new property to a profile or updates an existing property if it already exists,
    /// based on a given predicate and key.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the property being added or updated, which must inherit from <see cref="ProfileProperty"/>.
    /// </typeparam>
    /// <param name="predicate">
    /// A predicate function used to locate the profile in which the property should be added or updated.
    /// </param>
    /// <param name="key">
    /// The unique key associated with the property to be added or updated.
    /// </param>
    /// <param name="updateAction">
    /// An action to apply updates to the property if it already exists.
    /// </param>
    /// <returns>
    /// <c>true</c> if a profile matching the predicate was found and the property was successfully added or updated;
    /// <c>false</c> otherwise.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="predicate"/> is <c>null</c>.
    /// </exception>
    public static bool AddOrUpdateProperty<T>(Predicate<ProfileInfo> predicate, string key, Action<T> updateAction)
        where T : ProfileProperty
    {
        if (predicate == null)
            throw new ArgumentNullException(nameof(predicate));

        if (!TryGetProfile(predicate, out var profile))
            return false;

        if (profile.Value.Properties.TryGetValue(key, out var property)
            && property is T castProperty)
        {
            updateAction(castProperty);
            return true;
        }
        
        castProperty = Activator.CreateInstance<T>();
        castProperty.Profile = profile;

        castProperty.OnAdded();
        
        profile.Value.Properties.TryAdd(key, castProperty);
        profile.IsDirty = true;
        
        return true;
    }

    /// <summary>
    /// Retrieves or adds a property to a profile that matches the specified criteria.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the property being retrieved or added, which must inherit from <see cref="ProfileProperty"/>.
    /// </typeparam>
    /// <param name="predicate">
    /// A predicate function used to identify the profile that should be checked for the property.
    /// </param>
    /// <param name="key">
    /// The unique key associated with the property to be retrieved or added.
    /// </param>
    /// <param name="profile">
    /// When this method returns, contains the matching profile, if a match was found; otherwise, <c>null</c>.
    /// This parameter is passed uninitialized.
    /// </param>
    /// <param name="property">
    /// When this method returns, contains the property of type <typeparamref name="T"/> if it exists in the profile;
    /// otherwise, <c>null</c>. If the property does not exist and the method succeeds, it will be added.
    /// This parameter is passed uninitialized.
    /// </param>
    /// <returns>
    /// <c>true</c> if a profile matching the predicate was found and the property was retrieved or added successfully;
    /// <c>false</c> otherwise.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="predicate"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the profile storage is not initialized properly.
    /// </exception>
    public static bool GetOrAddProperty<T>(Predicate<ProfileInfo> predicate, string key,
        out StorageValue<ProfileInfo> profile, out T property) where T : ProfileProperty
    {
        if (predicate == null)
            throw new ArgumentNullException(nameof(predicate));

        profile = null!;
        property = null!;

        if (Profiles == null)
            return false;
        
        foreach (var kvp in Profiles.Values)
        {
            if (kvp.Value is not StorageValue<ProfileInfo> castValue)
                continue;
            
            if (!predicate(castValue.Value))
                continue;

            if (!castValue.Value.Properties.TryGetValue(key, out property))
            {
                if (Activator.CreateInstance<T>() is not { } instance)
                    throw new InvalidOperationException("Could not create property instance!");
                
                property = instance;
                property.profile = castValue;
                
                castValue.Value.Properties.TryAdd(key, instance);
                
                property.OnAdded();
                
                castValue.IsDirty = true;
            }
            
            profile = castValue;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Adds or updates a custom key-value pair in the profile's custom data for a specified user.
    /// </summary>
    /// <param name="userId">
    /// The unique identifier of the user whose profile should be updated.
    /// </param>
    /// <param name="key">
    /// The key for the custom data entry to be added or updated.
    /// </param>
    /// <param name="value">
    /// The value to be associated with the given key in the custom data.
    /// </param>
    /// <returns>
    /// <c>true</c> if the profile for the specified user was found and the custom data was successfully added or updated;
    /// <c>false</c> otherwise.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="userId"/> is <c>null</c> or empty.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="userId"/> is not in the correct format (e.g., missing the expected "@type" component).
    /// </exception>
    public static bool AddOrUpdateCustomData(string userId, string key, string value)
    {
        if (TryGetProfileByUserId(userId, out var profile))
        {
            profile.Value.CustomData[key] = value;
            profile.IsDirty = true;
            
            return true;
        }

        return false;
    }

    /// <summary>
    /// Attempts to retrieve a custom data value associated with a given key in the user's profile.
    /// </summary>
    /// <param name="userId">
    /// The unique identifier of the user whose profile contains the custom data.
    /// </param>
    /// <param name="key">
    /// The key associated with the custom data value to retrieve.
    /// </param>
    /// <param name="value">
    /// When this method returns, contains the custom data value associated with the specified key
    /// if the key exists; otherwise, contains an empty string.
    /// </param>
    /// <returns>
    /// <c>true</c> if the user's profile is found, and the key exists within the custom data;
    /// <c>false</c> otherwise.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="userId"/> is <c>null</c> or empty.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="userId"/> is not in the expected format.
    /// </exception>
    public static bool TryGetCustomData(string userId, string key, out string value)
    {
        if (TryGetProfileByUserId(userId, out var profile))
        {
            value = profile.Value.CustomData.TryGetValue(key, out var result) ? result : string.Empty;
            return true;
        }

        value = string.Empty;
        return false;
    }
    
    /// <summary>
    /// Adds a new profile or updates an existing profile with the given user information.
    /// </summary>
    /// <param name="userId">
    /// The unique identifier of the user associated with the profile.
    /// </param>
    /// <param name="joinedNick">
    /// The nickname to be added or updated for the profile.
    /// </param>
    /// <param name="joinedIp">
    /// The IP address to be added or updated for the profile.
    /// </param>
    /// <returns>
    /// <c>true</c> if a new profile was added; <c>false</c> if an existing profile was updated.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="userId"/>, <paramref name="joinedNick"/>, or <paramref name="joinedIp"/> is null or empty.
    /// </exception>
    public static StorageValue<ProfileInfo> AddOrUpdateProfile(string userId, string joinedNick, string joinedIp)
    {
        if (string.IsNullOrEmpty(userId))
            throw new ArgumentNullException(nameof(userId));

        if (joinedNick == null)
            throw new ArgumentNullException(nameof(joinedNick));

        if (joinedIp == null)
            throw new ArgumentNullException(nameof(joinedIp));

        if (TryGetProfileByUserId(userId, out var value))
        {
            var profile = value.Value;

            profile.LastLogin = DateTime.UtcNow;
            profile.ModifiedAt = profile.LastLogin;

            if (!string.IsNullOrEmpty(joinedNick))
            {
                if (profile.Nicknames.Count == 0)
                {
                    profile.Nicknames.TryAdd(joinedNick, profile.ModifiedAt);

                    log.Debug($"Added new nickname &1{joinedNick}&r to profile &3{profile.Id}&r!");
                }
                else
                {
                    var lastNick = profile.Nicknames.Last();

                    if (lastNick.Key != joinedNick)
                    {
                        profile.Nicknames.TryAdd(joinedNick, profile.ModifiedAt);

                        log.Debug($"Added new nickname &1{joinedNick}&r to profile &3{profile.Id}&r!");
                    }
                }
            }

            if (!string.IsNullOrEmpty(joinedIp))
            {
                if (profile.Addresses.Count == 0)
                {
                    profile.Addresses.TryAdd(joinedIp, profile.ModifiedAt);

                    log.Debug($"Added new IP &1{joinedIp}&r to profile &3{profile.Id}&r!");
                }
                else
                {
                    var lastIp = profile.Addresses.Last();

                    if (lastIp.Key != joinedIp)
                    {
                        profile.Addresses.TryAdd(joinedIp, profile.ModifiedAt);

                        log.Debug($"Added new IP &1{joinedIp}&r to profile &3{profile.Id}&r!");
                    }
                }
            }

            log.Debug($"Updated profile &3{profile.Id}&r!");
            
            value.IsDirty = true;
            return value;
        }

        var id = DbManager.NewId;

        value = Profiles.AddStorageValue(id, () => new ProfileInfo());

        value.Value.Id = id;
        value.Value.UserId = userId;

        value.Value.CreatedAt = DateTime.UtcNow;
        value.Value.ModifiedAt = DateTime.UtcNow;

        if (!string.IsNullOrEmpty(joinedIp))
            value.Value.Addresses.TryAdd(joinedIp, DateTime.UtcNow);
        
        if (!string.IsNullOrEmpty(joinedNick))
            value.Value.Nicknames.TryAdd(joinedNick, DateTime.UtcNow);

        foreach (var kvp in Properties)
        {
            try
            {
                if (Activator.CreateInstance(kvp.Value) is ProfileProperty profileProperty)
                {
                    value.Value.Properties.TryAdd(kvp.Key, profileProperty);
                }
                else
                {
                    log.Warn($"Could not create property &1{kvp.Value.Name}&r when adding new profile!");
                }
            }
            catch (Exception ex)
            {
                log.Error($"Could not add profile property to new profile:\n{ex}");
            }
        }

        value.IsDirty = true;

        log.Debug($"Created new profile with ID &1{id}&r for user &3{userId}&r!");
        return value;
    }

    /// <summary>
    /// Attempts to retrieve a profile by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the profile to retrieve.</param>
    /// <param name="profile">
    /// When this method returns, contains the profile associated with the specified
    /// identifier, if the profile is found; otherwise, contains null. This parameter
    /// is passed uninitialized.
    /// </param>
    /// <returns>
    /// <c>true</c> if the profile with the specified identifier was found; otherwise, <c>false</c>.
    /// </returns>
    public static bool TryGetProfileById(string id, out StorageValue<ProfileInfo> profile)
    {
        profile = null!;

        if (Profiles == null)
            return false;
        
        return Profiles.TryGetStorageValue(id, out profile);
    }

    public static bool TryGetProfileByDiscordId(ulong discordId, out StorageValue<ProfileInfo> profile)
            => TryGetProfile(x => x.DiscordId == discordId, out profile);

    /// <summary>
    /// Attempts to retrieve a profile by the user's unique identifier.
    /// </summary>
    /// <param name="userId">
    /// The unique user identifier associated with the profile to retrieve.
    /// </param>
    /// <param name="profile">
    /// When this method returns, contains the profile associated with the specified
    /// user identifier, if the profile is found; otherwise, contains null. This parameter
    /// is passed uninitialized.
    /// </param>
    /// <returns>
    /// <c>true</c> if a profile with the specified user identifier was found; otherwise, <c>false</c>.
    /// </returns>
    public static bool TryGetProfileByUserId(string userId, out StorageValue<ProfileInfo> profile)
    {
        if (string.IsNullOrEmpty(userId))
            throw new ArgumentNullException(nameof(userId));

        if (string.Equals(userId, "ID_DEDICATED", StringComparison.OrdinalIgnoreCase))
            return TryGetProfile(x => x.UserId == "SERVER", out profile);
        
        if (!userId.TrySplit('@', true, 2, out _))
            throw new ArgumentException("User ID must be in the format ID@type", nameof(userId));
        
        return TryGetProfile(x => x.UserId == userId, out profile);
    }

    /// <summary>
    /// Attempts to retrieve a profile by a specified nickname.
    /// </summary>
    /// <param name="nick">
    /// The nickname used to identify the profile. The nickname comparison is case-insensitive.
    /// </param>
    /// <param name="profile">
    /// When this method returns, contains the profile associated with the specified
    /// nickname, if a matching profile is found; otherwise, contains null. This parameter
    /// is passed uninitialized.
    /// </param>
    /// <returns>
    /// <c>true</c> if a profile with the specified nickname was found; otherwise, <c>false</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the provided <paramref name="nick"/> is null or empty.
    /// </exception>
    public static bool TryGetProfileByNick(string nick, out StorageValue<ProfileInfo> profile)
    {
        if (string.IsNullOrEmpty(nick))
            throw new ArgumentNullException(nameof(nick));

        return TryGetProfile(x => x.Nicknames.Any(
            kvp => string.Equals(kvp.Key, nick, StringComparison.OrdinalIgnoreCase)), out profile);
    }

    /// <summary>
    /// Attempts to retrieve a profile by its associated IP address.
    /// </summary>
    /// <param name="ip">
    /// The IP address associated with the profile to retrieve. This value must not be null or empty.
    /// </param>
    /// <param name="profile">
    /// When this method returns, contains the profile associated with the specified
    /// IP address, if such a profile is found; otherwise, contains null. This parameter
    /// is passed uninitialized.
    /// </param>
    /// <returns>
    /// <c>true</c> if a profile associated with the specified IP address was found; otherwise, <c>false</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the <paramref name="ip"/> parameter is null or empty.
    /// </exception>
    public static bool TryGetProfileByIp(string ip, out StorageValue<ProfileInfo> profile)
    {
        if (string.IsNullOrEmpty(ip))
            throw new ArgumentNullException(nameof(ip));

        return TryGetProfile(x => x.Addresses.Any(kvp => kvp.Key == ip), out profile);
    }

    /// <summary>
    /// Attempts to retrieve a profile and associated custom data value based on a specified key and optional predicate.
    /// </summary>
    /// <param name="key">
    /// The key associated with the custom data to search for.
    /// </param>
    /// <param name="valuePredicate">
    /// An optional predicate function to evaluate the custom data value. If <c>null</c>, any value for the specified key
    /// will match.
    /// </param>
    /// <param name="profile">
    /// When this method returns, contains the profile that matches the specified criteria, if found; otherwise, <c>null</c>.
    /// </param>
    /// <param name="value">
    /// When this method returns, contains the custom data value associated with the specified key, if found; otherwise,
    /// an empty string.
    /// </param>
    /// <returns>
    /// <c>true</c> if a profile with a matching key and value satisfying the predicate is found; <c>false</c> otherwise.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="key"/> is <c>null</c> or an empty string.
    /// </exception>
    public static bool TryGetByCustomData(string key, Predicate<string>? valuePredicate,
        out StorageValue<ProfileInfo> profile, out string value)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentNullException(nameof(key));

        profile = null!;
        value = string.Empty;

        if (!TryGetProfile(
                x => x.CustomData.TryGetValue(key, out var result) &&
                     (valuePredicate == null || valuePredicate(result)), out profile))
            return false;
        
        value = profile.Value.CustomData[key];
        return true;
    }

    /// <summary>
    /// Attempts to retrieve a profile that satisfies the specified predicate.
    /// </summary>
    /// <param name="predicate">
    /// A predicate used to evaluate the profiles. The predicate must return <c>true</c>
    /// for the desired profile to be retrieved.
    /// </param>
    /// <param name="profile">
    /// When this method returns, contains the profile that satisfies the specified predicate,
    /// if such a profile is found; otherwise, contains null. This parameter is passed uninitialized.
    /// </param>
    /// <returns>
    /// <c>true</c> if a profile satisfying the specified predicate was found; otherwise, <c>false</c>.
    /// </returns>
    public static bool TryGetProfile(Predicate<ProfileInfo> predicate,  out StorageValue<ProfileInfo> profile)
    {
        if (predicate == null)
            throw new ArgumentNullException(nameof(predicate));
        
        profile = null!;

        if (Profiles == null)
            return false;
        
        foreach (var kvp in Profiles.Values)
        {
            if (kvp.Value is not StorageValue<ProfileInfo> castValue)
                continue;
            
            if (!predicate(castValue.Value))
                continue;
            
            profile = castValue;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Attempts to retrieve a property with the specified key from a profile that satisfies the given predicate.
    /// </summary>
    /// <param name="predicate">
    /// A predicate function used to locate the profile from which the property should be retrieved.
    /// </param>
    /// <param name="key">
    /// The unique key associated with the property to be retrieved.
    /// </param>
    /// <param name="profile">
    /// When this method returns, contains the profile from which the property was retrieved, if the operation was successful; otherwise, <c>null</c>.
    /// </param>
    /// <param name="property">
    /// When this method returns, contains the retrieved property, if the operation was successful; otherwise, <c>null</c>.
    /// </param>
    /// <returns>
    /// <c>true</c> if a profile satisfying the predicate was found and the property with the specified key was successfully retrieved;
    /// otherwise, <c>false</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="predicate"/> is <c>null</c>.
    /// </exception>
    public static bool TryGetProperty(Predicate<ProfileInfo> predicate, string key,
        out StorageValue<ProfileInfo> profile, out ProfileProperty property)
    {
        if (predicate == null)
            throw new ArgumentNullException(nameof(predicate));

        profile = null!;
        property = null!;

        if (Profiles == null)
            return false;
        
        foreach (var kvp in Profiles.Values)
        {
            if (kvp.Value is not StorageValue<ProfileInfo> castValue)
                continue;
            
            if (!predicate(castValue.Value))
                continue;
            
            if (!castValue.Value.Properties.TryGetValue(key, out property))
                continue;
            
            profile = castValue;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Ensures that a specified property exists in all profiles, creating it if necessary.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the property to ensure. Must inherit from <see cref="ProfileProperty"/>.
    /// </typeparam>
    /// <param name="key">
    /// The unique key identifying the property to ensure.
    /// </param>
    /// <param name="constructor">
    /// A function used to construct a new instance of the property if it does not already exist.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="key"/> is <c>null</c> or empty, or when <paramref name="constructor"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the <paramref name="constructor"/> fails to create a valid property instance.
    /// </exception>
    public static void EnsureProperty<T>(string key, Func<T> constructor) where T : ProfileProperty
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentNullException(nameof(key));

        if (constructor == null)
            throw new ArgumentNullException(nameof(constructor));

        if (Profiles == null)
            return;

        foreach (var kvp in Profiles.Values)
        {
            if (kvp.Value is not StorageValue<ProfileInfo> castValue)
                continue;
            
            if (castValue.Value.Properties.ContainsKey(key))
                continue;
            
            var property = constructor();
            
            if (property == null)
                throw new InvalidOperationException("Could not create property!");

            property.profile = castValue;
            
            castValue.Value.Properties.TryAdd(key, property);
            
            property.OnAdded();

            castValue.IsDirty = true;
        }
    }

    internal static void InvokeEmbedBuilder(ProfileInfo profile, Func<PageBuilder> factory, List<IPageBuilder> builders)
    {
        try
        {
            ProfileEmbedBuilder?.Invoke(profile, factory, builders);
        }
        catch (Exception ex)
        {
            log.Error(ex);
            
            builders.Clear();
        }
    }
    
    private static void OnUpdate()
    {
        if (Profiles == null)
            return;

        if (Profiles.ValueCount < 1)
            return;

        foreach (var kvp in Profiles.Values)
        {
            if (kvp.Value is not StorageValue<ProfileInfo> castValue)
                continue;

            if (castValue.Value?.Properties?.Count < 1)
                continue;

            foreach (var propKvp in castValue.Value!.Properties!)
            {
                try
                {
                    if (propKvp.Value.Profile != null)
                    {
                        propKvp.Value.OnUpdated();

                        if (propKvp.Value.IsDirty)
                        {
                            kvp.Value.IsDirty = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.Error($"Error while updating property &1{propKvp.Key}&r in profile:\n{ex}");
                }
            }
        }
    }
    
    private static void StorageInit_Profiles()
    {
        log.Debug("Loading profiles ..");

        if (LibraryLoader.HasArgument("ProfilesReset"))
        {
            log.Warn($"Resetting all profiles!");

            var syncedProfiles = new Dictionary<string, ulong>();

            foreach (var kvp in Profiles.Values)
            {
                if (kvp.Value is not StorageValue<ProfileInfo> castValue)
                    continue;

                if (castValue.Value.DiscordId != 0)
                    syncedProfiles.TryAdd(castValue.Value.UserId, castValue.Value.DiscordId);
            }

            log.Info($"Preserving {syncedProfiles.Count} profiles!");

            Profiles.ClearValues(true);

            foreach (var kvp in syncedProfiles)
            {
                var profile = new ProfileInfo
                {
                    Id = DbManager.NewId,

                    UserId = kvp.Key,
                    DiscordId = kvp.Value,

                    CreatedAt = DateTime.UtcNow,
                    LastLogin = DateTime.UtcNow,
                    ModifiedAt = DateTime.UtcNow
                };

                Profiles.AddStorageValue(profile.Id, () => profile);

                log.Info($"Preserved profile &3{profile.UserId}&r (&6{profile.DiscordId}&r)!");
            }
        }
        
        if (!TryGetProfile(x => x.UserId == "SERVER", out var serverProfile))
        {
            var profile = new ProfileInfo
            {
                Id = DbManager.NewId,
                UserId = "SERVER",

                CreatedAt = DateTime.UtcNow,
                LastLogin = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow
            };

            profile.Nicknames.TryAdd("SERVER", profile.ModifiedAt);
            profile.Addresses.TryAdd("0.0.0.0", profile.ModifiedAt);
            
            Profiles.AddStorageValue(profile.Id, () => profile);
            
            log.Debug($"Created new profile for server &3{profile.Id}&r!");
        }
        else
        {
            serverProfile.Value.LastLogin = DateTime.UtcNow;
            serverProfile.Value.ModifiedAt = DateTime.UtcNow;

            serverProfile.IsDirty = true;
            
            log.Debug($"Found existing profile for server &3{serverProfile.Value.Id}&r!");
        }
        
        log.Debug("Loading profile properties ..");

        string? removeProperty = null;

        LibraryLoader.HasArgument("ProfileRemoveProperty", out removeProperty);

        var removeSessions = LibraryLoader.HasArgument("ProfileRemoveSessions");

        using var propertyReader = ObjectPool<ByteReader>.Shared.Rent();

        foreach (var kvp in Profiles.Values)
        {
            if (kvp.Value is not StorageValue<ProfileInfo> castValue)
                continue;

            if (removeSessions)
            {
                if (castValue.Value.Sessions.Count > 0)
                {
                    castValue.Value.Sessions.Clear();
                    castValue.IsDirty = true;

                    log.Warn($"Removed all sessions from profile &3{castValue.Value.Id}&r!");
                }
            }
            else
            {
                foreach (var xvp in castValue.Value.Sessions)
                {
                    if (xvp.Value.Ended == DateTime.MinValue || xvp.Value.Started == DateTime.MinValue)
                    {
                        castValue.Value.Sessions.TryRemove(xvp.Key, out _);
                        castValue.IsDirty = true;

                        log.Warn($"Removed invalid session &1{xvp.Key}&r from profile &3{castValue.Value.Id}&r!");
                    }
                }
            }
            
            foreach (var propertyKvp in castValue.Value.PropertyValues)
            {
                try
                {
                    if (removeProperty != null && removeProperty == propertyKvp.Key)
                    {
                        castValue.Value.PropertyValues.TryRemove(propertyKvp.Key, out _);
                        castValue.IsDirty = true;

                        log.Warn($"Removed property &1{propertyKvp.Key}&r from profile &3{castValue.Value.Id}&r!");
                        continue;
                    }

                    propertyReader.Reset(propertyKvp.Value, 0, propertyKvp.Value.Length);

                    var typeName = propertyReader.ReadString();
                    var type = Type.GetType(typeName);

                    if (type == null)
                    {
                        log.Warn($"Could not find property type &1{typeName}&r when loading profile &3{castValue.Value.Id}&r!");
                        continue;
                    }

                    if (Activator.CreateInstance(type) is not ProfileProperty property)
                    {
                        log.Warn($"Could not create property &1{type.Name}&r when loading profile &3{castValue.Value.Id}&r!");
                        continue;
                    }

                    property.Profile = castValue;

                    castValue.Value.Properties.TryAdd(propertyKvp.Key, property);

                    property.OnAdded();
                    property.Read(propertyReader);

                    property.IsDirty = false;
                }
                catch (Exception ex)
                {
                    log.Error($"Error while loading profile property &3{propertyKvp.Key}&r:\n{ex}");

                    castValue.Value.Properties.TryRemove(propertyKvp.Key, out _);
                }
            }
        }

        LibraryUpdate.Register(OnUpdate);
        
        log.Info($"Loaded &3{Profiles.ValueCount}&r profiles!");
    }
}