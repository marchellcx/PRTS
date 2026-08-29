namespace PRTS.Core.Attributes;

/// <summary>
/// Marks a method as a help page writer.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class HelpWriterAttribute : Attribute { }