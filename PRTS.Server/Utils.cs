using NiveraAPI.Logs;
using NiveraAPI.Utilities;

namespace PRTS;

/// <summary>
/// Utility methods.
/// </summary>
public static class Utils
{
    private static volatile LogSink log = LogManager.GetSource("Core", "Utils");

    /// <summary>
    /// Tries to parse a date range from the given input string. The input can be in the following formats:
    /// ..yyyy-MM-dd
    /// yyyy-MM-dd..
    /// yyyy-MM-dd..yyyy-MM-dd
    /// yyyy-MM-dd
    /// </summary>
    /// <param name="input">The input string representing the date range.</param>
    /// <param name="from">The parsed start date of the range, or null if not specified.</param>
    /// <param name="to">The parsed end date of the range, or null if not specified.</param>
    /// <returns>True if the input was successfully parsed; otherwise, false.</returns>
    public static bool TryParseDateRange(string input, out DateTime? from, out DateTime? to)
    {
        to = null;
        from = null;

        if (string.IsNullOrEmpty(input))
            return false;

        if (input.StartsWith("..") && DateTime.TryParse(input.Substring(2), out var parsedTo))
        {
            to = parsedTo;
            return true;
        }

        if (input.EndsWith("..") && DateTime.TryParse(input.Substring(0, input.Length - 2), out var parsedFrom))
        {
            from = parsedFrom;
            return true;
        }

        var parts = input.Split("..");

        if (parts.Length == 2 && DateTime.TryParse(parts[0], out var parsedFromRange) && DateTime.TryParse(parts[1], out var parsedToRange))
        {
            to = parsedToRange;
            from = parsedFromRange;

            return true;
        }

        if (DateTime.TryParse(input, out var parsedDate))
        {
            to = parsedDate;
            from = parsedDate;

            return true;
        }

        return false;
    }


    /// <summary>
    /// Ensures that the provided action is executed on the main thread.
    /// If the current thread is not the main thread, the action will be scheduled to run on the main thread. 
    /// If the current thread is already the main thread, the action will be executed immediately.
    /// </summary>
    /// <param name="action">The action to execute on the main thread.</param>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="action"/> parameter is null.</exception>
    public static void RequireMain(Action action)
    {
        if (action == null)
            throw new ArgumentNullException("action");

        if (!ThreadHelper.IsMainThread)
            ThreadHelper.RunOnMainThread(action);
        else
            action();
    }

    /// <summary>
    /// Ensures that the provided action is executed on the main thread asynchronously.
    /// </summary>
    /// <param name="action">The action to execute on the main thread.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="action"/> parameter is null.</exception>
    public static async Task RequireMainAsync(Action action)
    {
        if (action == null)
            throw new ArgumentNullException("action");

        if (!ThreadHelper.IsMainThread)
            await ThreadHelper.RunOnMainThread(action);
        else
            action();
    }

    /// <summary>
    /// Logs an informational message to the logging sink.
    /// </summary>
    /// <param name="source">The source of the log message, typically representing the module or component emitting the log.</param>
    /// <param name="msg">The message to log, which can be any object that provides relevant information.</param>
    public static void Info(string source, object msg)
    {
        log.Info(source, msg);
    }

    /// <summary>
    /// Logs a warning message to the logging sink.
    /// </summary>
    /// <param name="source">The source of the log message, typically representing the module or component emitting the log.</param>
    /// <param name="msg">The message to log, which can be any object that provides relevant information.</param>
    public static void Warn(string source, object msg)
    {
        log.Warn(source, msg);
    }

    /// <summary>
    /// Logs an error message to the logging sink.
    /// </summary>
    /// <param name="source">The source of the log message, typically representing the module or component emitting the log.</param>
    /// <param name="msg">The message to log, which can be any object that provides relevant information.</param>
    public static void Error(string source, object msg)
    {
        log.Error(source, msg);
    }

    /// <summary>
    /// Logs a debug message to the logging sink.
    /// </summary>
    /// <param name="source">The source of the log message, typically representing the module or component emitting the log.</param>
    /// <param name="msg">The message to log, which can be any object that provides relevant information.</param>
    public static void Debug(string source, object msg)
    {
        log.Debug(source, msg);
    }
}