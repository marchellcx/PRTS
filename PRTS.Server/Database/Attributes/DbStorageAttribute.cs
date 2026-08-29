namespace PRTS.Database.Attributes;

/// <summary>
/// Represents an attribute used to specify database storage metadata for a field.
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public class DbStorageAttribute : Attribute
{
    /// <summary>
    /// The name of the field in the database.
    /// </summary>
    public string? Name { get; set; }
    
    /// <summary>
    /// The type of the serializer.
    /// </summary>
    public Type? Serializer { get; }

    /// <summary>
    /// An attribute used to specify database storage metadata for a field.
    /// This can include the storage name and the serializer type to be used
    /// when persisting or retrieving the field's data.
    /// </summary>
    public DbStorageAttribute(string? name = null, Type? serializer = null)
    {
        Name = name;
        Serializer = serializer;
    }
}