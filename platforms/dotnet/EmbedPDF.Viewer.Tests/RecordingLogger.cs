using Microsoft.Extensions.Logging;

namespace EmbedPDF.Viewer.Tests;

/// <summary>Keeps every entry logged at warning or above.</summary>
sealed class RecordingLogger : ILogger
{
    readonly Lock gate = new();
    readonly List<(LogLevel Level, string Message, Exception? Error)> entries = [];

    public IReadOnlyList<(LogLevel Level, string Message, Exception? Error)> Entries
    {
        get { lock (gate) return [.. entries]; }
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;
        lock (gate) entries.Add((logLevel, formatter(state, exception), exception));
    }
}
