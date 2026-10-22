using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Cli.Presentation;

internal sealed class ConsoleLogger : ILogger {
    private readonly ConsoleRenderer _renderer;
    private readonly LogLevel _minimumLevel;
    private readonly string _category;

    public ConsoleLogger(
        ConsoleRenderer renderer,
        LogLevel minimumLevel,
        string category) {
        _renderer = renderer;
        _minimumLevel = minimumLevel;
        _category = category;
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel) {
        return logLevel != LogLevel.None &&
            logLevel >= _minimumLevel;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter) {
        if (!IsEnabled(logLevel)) {
            return;
        }

        _renderer.WriteLog(
            DateTimeOffset.Now,
            logLevel,
            _category,
            formatter(state, exception),
            exception);
    }
}
