using NiveraAPI.Logs;

namespace PRTS;

/// <summary>
/// Utility methods.
/// </summary>
public static class Utils
{
    private static volatile LogSink log = LogManager.GetSource("Core", "Utils");
    
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