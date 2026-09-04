using NiveraAPI.Utilities;

namespace PRTS.Extensions;

/// <summary>
/// Provides extension methods for the <see cref="TimeSpan"/> structure to enable
/// additional functionality such as converting durations to descriptive string
/// representations in specific languages.
/// </summary>
public static class TimeSpanExtensions
{
    /// <summary>
    /// Converts a TimeSpan to a full descriptive string representation in Czech language,
    /// including days, hours, minutes, and seconds, with appropriate grammatical forms.
    /// </summary>
    /// <param name="span">The TimeSpan object to convert to a descriptive string.</param>
    /// <returns>
    /// A string representation of the TimeSpan with detailed breakdown of days, hours,
    /// minutes, and seconds, formatted according to Czech grammar.
    /// </returns>
    public static string ToFullCzechString(this TimeSpan span)
    {
        var sb = Pools.PoolStringBuilder();

        if (span.Days > 0)
        {
            if (span.Days == 1)
            {
                sb.Append("1 den");
            }
            else if (span.Days < 5)
            {
                sb.Append($"{span.Days} dny");
            }
            else
            {
                sb.Append($"{span.Days} dní");
            }
        }

        if (span.Hours > 0)
        {
            if (sb.Length > 0)
            {
                sb.Append(", ");
            }
            
            if (span.Hours == 1)
            {
                sb.Append("1 hodina");
            }
            else if (span.Hours < 5)
            {
                sb.Append($"{span.Hours} hodiny");
            }
            else
            {
                sb.Append($"{span.Hours} hodin");
            }
        }
        
        if (span.Minutes > 0)
        {
            if (sb.Length > 0)
            {
                sb.Append(", ");
            }
            
            if (span.Minutes == 1)
            {
                sb.Append("1 minuta");
            }
            else if (span.Minutes < 5)
            {
                sb.Append($"{span.Minutes} minuty");
            }
            else
            {
                sb.Append($"{span.Minutes} minut");
            }
        }

        if (span.Seconds > 0)
        {
            if (sb.Length > 0)
            {
                sb.Append(", ");
            }
            
            if (span.Seconds == 1)
            {
                sb.Append("1 sekunda");
            }
            else if (span.Seconds < 5)
            {
                sb.Append($"{span.Seconds} sekundy");
            }
            else
            {
                sb.Append($"{span.Seconds} sekund");
            }
        }

        if (sb.Length < 1)
        {
            sb.Append("0 sekund");
        }

        return sb.ReturnStringBuilderValue();
    }
}