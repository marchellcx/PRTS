namespace PRTS.Core.Attributes;

/// <summary>
/// Marks a method as an initialization method.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class InitAttribute : Attribute
{
    /// <summary>
    /// Gets the order of the initialization method.
    /// </summary>
    public int Order { get; } = 0;
    
    /// <summary>
    /// Creates a new instance of the <see cref="InitAttribute"/> class.
    /// </summary>
    /// <param name="order">The order of the method.</param>
    public InitAttribute(int order = 0) 
        => Order = order;
}