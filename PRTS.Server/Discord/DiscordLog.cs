using Discord;

using NiveraAPI.Logs;

using LogMessage = Discord.LogMessage;

namespace PRTS.Discord;

/// <summary>
/// Represents a log sink for Discord.
/// </summary>
public class DiscordLog
{
    /// <summary>
    /// The log sink.
    /// </summary>
    public volatile LogSink Sink;

    /// <summary>
    /// Creates a new instance of the <see cref="DiscordLog"/> class.
    /// </summary>
    public DiscordLog(string name)
    {
        Sink = LogManager.GetSource("DiscordLib", name);
    }

    /// <summary>
    /// Processes a log message and routes it to the appropriate sink based on its severity level.
    /// </summary>
    /// <param name="msg">The log message containing information such as source, severity, and content.</param>
    /// <returns>A completed task representing the asynchronous operation.</returns>
    public Task Log(LogMessage msg)
    {
        switch (msg.Severity)
        {
            case LogSeverity.Info:
                Sink.Info(msg.Source, msg.Message);
                break;
            
            case LogSeverity.Warning:
                Sink.Warn(msg.Source, msg.Message);
                break;
            
            case LogSeverity.Verbose:
            case LogSeverity.Debug:
                Sink.Debug(msg.Source, msg.Message);
                break;
            
            case LogSeverity.Error:
            case LogSeverity.Critical:
                Sink.Error(msg.Source, msg.Message ?? msg.Exception?.ToString() ?? string.Empty);
                
                if (msg.Exception != null)
                    Sink.Error(msg.Source, msg.Exception.ToString());

                break;
        }
        
        return Task.CompletedTask;
    }
}