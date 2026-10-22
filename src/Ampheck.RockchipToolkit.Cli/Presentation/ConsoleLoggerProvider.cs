using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Cli.Presentation;

internal sealed class ConsoleLoggerProvider : ILoggerProvider {
    private readonly ConsoleRenderer _renderer;
    private readonly LogLevel _minimumLevel;

    public ConsoleLoggerProvider(
        ConsoleRenderer renderer,
        LogLevel minimumLevel) {
        _renderer = renderer;
        _minimumLevel = minimumLevel;
    }

    public ILogger CreateLogger(string categoryName) {
        return new ConsoleLogger(
            _renderer,
            _minimumLevel,
            categoryName);
    }

    public void Dispose() {
    }
}
