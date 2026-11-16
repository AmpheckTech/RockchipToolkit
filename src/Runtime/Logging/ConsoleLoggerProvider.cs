using Ampheck.RockchipToolkit.Runtime.Terminal;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Runtime.Logging;

internal sealed class ConsoleLoggerProvider : ILoggerProvider {
    private readonly ConsoleRenderer _renderer;
    private readonly LogLevel _minimumLevel;

    public ConsoleLoggerProvider(
        ConsoleRenderer renderer,
        LogLevel minimumLevel
    ) {
        _renderer = renderer;
        _minimumLevel = minimumLevel;
    }

    public ILogger CreateLogger(string categoryName) {
        var minimumLevel =
            categoryName.StartsWith(
                "Microsoft.AspNetCore",
                StringComparison.Ordinal) ||
            categoryName.StartsWith(
                "Microsoft.Extensions.Http",
                StringComparison.Ordinal)
                ? LogLevel.Warning
                : _minimumLevel;
        return new ConsoleLogger(
            _renderer,
            minimumLevel,
            categoryName);
    }

    public void Dispose() {
    }
}
