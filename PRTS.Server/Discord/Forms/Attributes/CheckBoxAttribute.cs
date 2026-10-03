namespace PRTS.Discord.Forms.Attributes;

/// <summary>
/// Represents a checkbox form attribute that can be used to define a checkbox input in a Discord form.
/// </summary>
public class CheckBoxAttribute : FormAttribute
{
    /// <summary>
    /// Gets or sets a value indicating whether the checkbox is checked.
    /// </summary>
    public bool IsChecked { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckBoxAttribute"/> class with the specified label and checked state.
    /// </summary>
    /// <param name="label">The label for the checkbox.</param>
    /// <param name="isChecked">Indicates whether the checkbox is checked.</param>
    /// <exception cref="ArgumentException">Thrown when the label is null or empty.</exception>
    public CheckBoxAttribute(string label, bool isChecked = false) : base(label)
    {
        IsChecked = isChecked;
    }
}