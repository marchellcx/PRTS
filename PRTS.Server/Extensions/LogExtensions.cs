using NiveraAPI.Logs;

namespace PRTS.Extensions;

/// <summary>
/// Extension methods for the <see cref="LogSink"/> class.
/// </summary>
public static class LogExtensions
{
    /// <summary>
    /// Attempts to invoke a specified action within a try-catch block.
    /// Logs any exceptions encountered during the execution of the action.
    /// </summary>
    /// <param name="log">The log sink used to record warnings or errors encountered during execution.</param>
    /// <param name="canBeIgnored">Indicates whether exceptions encountered during the execution can be ignored. If true, logs a warning; if false, logs an error.</param>
    /// <param name="action">The action to be executed within the try-catch block.</param>
    public static void TryCatch(this LogSink log, bool canBeIgnored, Action action)
    {
        if (log == null)
            return;

        if (action == null)
            return;

        try
        {
            action();
        }
        catch (Exception ex)
        {
            if (canBeIgnored)
            {
                log.Warn($"Error occured, can be ignored:\n{ex}");
            }
            else
            {
                log.Error($"Error occured!\n{ex}");
            }
        }
    }

    /// <summary>
    /// Attempts to invoke a specified asynchronous action within a try-catch block.
    /// Logs any exceptions encountered during the execution of the action.
    /// </summary>
    /// <param name="log">The log sink used to record warnings or errors encountered during execution.</param>
    /// <param name="canBeIgnored">Indicates whether exceptions encountered during the execution can be ignored. If true, logs a warning; if false, logs an error.</param>
    /// <param name="action">The asynchronous action to be executed within the try-catch block.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static async Task TryCatchAsync(this LogSink log, bool canBeIgnored, Func<Task> action)
    {
        if (log == null)
            return;

        if (action == null)
            return;

        try
        {
            await action();
        }
        catch (Exception ex)
        {
            if (canBeIgnored)
            {
                log.Warn($"Error occured, can be ignored:\n{ex}");
            }
            else
            {
                log.Error($"Error occured!\n{ex}");
            }
        }
    }
    
    /// <summary>
    /// Attempts to invoke a specified asynchronous action within a try-catch block.
    /// Logs any exceptions encountered during the execution of the action.
    /// </summary>
    /// <param name="log">The log sink used to record warnings or errors encountered during execution.</param>
    /// <param name="canBeIgnored">Indicates whether exceptions encountered during the execution can be ignored. If true, logs a warning; if false, logs an error.</param>
    /// <param name="action">The asynchronous action to be executed within the try-catch block.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static async Task TryCatchAsync(this LogSink log, bool canBeIgnored, Func<ValueTask> action)
    {
        if (log == null)
            return;

        if (action == null)
            return;

        try
        {
            await action();
        }
        catch (Exception ex)
        {
            if (canBeIgnored)
            {
                log.Warn($"Error occured, can be ignored:\n{ex}");
            }
            else
            {
                log.Error($"Error occured!\n{ex}");
            }
        }
    }
}