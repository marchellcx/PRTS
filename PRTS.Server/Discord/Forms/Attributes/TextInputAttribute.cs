using Discord;

namespace PRTS.Discord.Forms.Attributes;

/// <summary>
/// Represents a text input field in a Discord form.
/// </summary>
public class TextInputAttribute : FormAttribute
{
    /// <summary>
    /// The minimum length of the text input.
    /// </summary>
    public int? MinLength { get; set; }

    /// <summary>
    /// The maximum length of the text input.
    /// </summary>
    public int? MaxLength { get; set; }

    /// <summary>
    /// The placeholder text for the text input.
    /// </summary>
    public string? Placeholder { get; set; }

    /// <summary>
    /// Whether the text input is required.
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// The style of the text input.
    /// </summary>
    public TextInputStyle Style { get; set; } = TextInputStyle.Short;

    /// <summary>
    /// Initializes a new instance of the <see cref="TextInputAttribute"/> class.
    /// </summary>
    /// <param name="label">The label for the text input.</param>
    /// <param name="placeholder">The placeholder text for the text input.</param>
    /// <param name="minLength">The minimum length of the text input.</param>
    /// <param name="maxLength">The maximum length of the text input.</param>
    /// <param name="isRequired">Whether the text input is required.</param>
    /// <param name="style">The style of the text input.</param>
    public TextInputAttribute(string label, string? placeholder, int? minLength = null, int? maxLength = null, bool isRequired = false, TextInputStyle style = TextInputStyle.Short) : base(label)
    {
        Placeholder = placeholder;

        MinLength = minLength;
        MaxLength = maxLength;

        IsRequired = isRequired;

        Style = style;
    }
}