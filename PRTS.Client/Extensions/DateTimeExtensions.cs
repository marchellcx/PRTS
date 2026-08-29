using NiveraAPI.Utilities;

namespace PRTS.Extensions;

/// <summary>
/// Provides extension methods for the <see cref="DateTime"/> structure to enable
/// custom formatting and operations, particularly for Czech date representations.
/// </summary>
public static class DateTimeExtensions
{
    /// <summary>
    /// An array of month names in the Czech language, used for converting and formatting
    /// dates into Czech date strings.
    /// </summary>
    public static string[] VeMonths =
    [
        "ledna",
        "února",
        "března",
        "dubna",
        "května",
        "června",
        "července",
        "září",
        "října",
        "listopadu",
        "prosince"
    ];

    /// <summary>
    /// Gets the start of the current day in UTC, with the time set to 00:00:00.
    /// </summary>
    public static DateTime DayStart
    {
        get
        {
            var now = DateTime.UtcNow;
            return new DateTime(now.Year, now.Month, now.Day, 0, 0, 0);
        }
    }

    /// <summary>
    /// Gets the end of the current day in UTC, with the time set to 23:59:59.
    /// </summary>
    public static DateTime DayEnd
    {
        get
        {
            var now = DateTime.UtcNow;
            return new DateTime(now.Year, now.Month, now.Day, 23, 59, 59);
        }
    }

    /// <summary>
    /// Gets the start of the current month in UTC, with the time set to 00:00:00 on the first day of the month.
    /// </summary>
    public static DateTime MonthStart
    {
        get
        {
            var now = DateTime.UtcNow;
            return new DateTime(now.Year, now.Month, 1, 0, 0, 0);
        }
    }

    /// <summary>
    /// Gets the end of the current month in UTC, with the time set to 23:59:59 on the last day of the month.
    /// </summary>
    public static DateTime MonthEnd
    {
        get
        {
            var now = DateTime.UtcNow;
            var daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);

            return new DateTime(now.Year, now.Month, daysInMonth, 23, 59, 59);
        }
    }

    /// <summary>
    /// Converts the specified <see cref="DateTime"/> instance to a string representation
    /// in the Czech format, including the day, month, year, and optionally, the hour and minute.
    /// </summary>
    /// <param name="date">The <see cref="DateTime"/> instance to convert to a Czech formatted string.</param>
    /// <returns>A string representation of the <see cref="DateTime"/> in Czech format.</returns>
    public static string ToVeCzechString(this DateTime date)
    {
        var sb = Pools.PoolStringBuilder();
        var month = VeMonths[date.Month - 1];
        
        sb.Append($"{date.Day}. {month} {date.Year}");

        if (date.Hour != 0)
        {
            if (date.Hour > 1 && date.Hour < 5)
            {
                sb.Append(" ve");
            }
            else
            {
                sb.Append(" v");
            }
            
            sb.Append($" {date.Hour:00}");
            
            if (date.Minute != 0)
            {
                sb.Append($":{date.Minute:00}");
            }    
        }

        return sb.ReturnStringBuilderValue();
    }
}