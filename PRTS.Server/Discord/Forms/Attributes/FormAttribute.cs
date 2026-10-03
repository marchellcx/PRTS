namespace PRTS.Discord.Forms.Attributes;

/// <summary>
/// Represents an attribute that can be applied to a field in a DiscordForm class to indicate that the field should be included in the form. This attribute is used to mark fields that will be rendered as form elements in the Discord modal.
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public class FormAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the label of the form field. This label is typically displayed to the user as the name or title of the field in the form.
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the form field. This description can be used to provide additional information or instructions to the user about the field's purpose or expected input.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="FormAttribute"/> class with the specified label. This constructor allows you to set the label of the form field when applying the attribute to a field in a DiscordForm class.
    /// </summary>
    /// <param name="label">The label of the form field.</param>
    public FormAttribute(string label)
    {
        Label = label;
    }   
}