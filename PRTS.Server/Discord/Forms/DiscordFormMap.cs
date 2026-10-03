using NiveraAPI.Extensions;

using System.Reflection;
using System.Collections.Concurrent;

using PRTS.Discord.Forms.Attributes;

namespace PRTS.Discord.Forms;

/// <summary>
/// Represents a mapping of form fields for a specific DiscordForm type. This class is used to store the mapping of fields and their corresponding FormFieldAttribute instances for a given DiscordForm type.
/// </summary>
public static class DiscordFormMap
{
    /// <summary>
    /// A thread-safe dictionary that maps a Type to its corresponding DiscordFormMap instance.
    /// </summary>
    public static volatile ConcurrentDictionary<Type, ConcurrentDictionary<FieldInfo, FormAttribute>> Maps = new();

    /// <summary>
    /// Gets or adds a DiscordFormMap for the specified type. If a map for the type already exists, it returns the existing map; otherwise, it creates a new map and adds it to the dictionary.
    /// </summary>
    /// <param name="type">The type for which to get or add a DiscordFormMap.</param>
    /// <returns>The DiscordFormMap for the specified type.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the type is null.</exception>
    public static ConcurrentDictionary<FieldInfo, FormAttribute> GetOrAdd(Type type)
    {
        if (type == null)
            throw new ArgumentNullException(nameof(type));

        if (Maps.TryGetValue(type, out var map))
            return map;

        map = new();

        foreach (var field in type.GetAllFields())
        {
            if (field.IsInitOnly)
                continue;

            foreach (var attribute in field.GetCustomAttributes())
            {
                if (attribute is FormAttribute formAttribute)
                {
                    map.TryAdd(field, formAttribute);
                }
            }
        }

        Maps.TryAdd(type, map);
        return map;
    }
}