using Ampheck.RockchipToolkit.Runtime.Terminal;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Runtime.Logging;

internal static class LoggingFactory {
    public static ILoggerFactory Create(
        bool verbose,
        bool informational,
        ConsoleRenderer renderer
    ) {
        var minimumLevel = verbose
            ? LogLevel.Debug
            : informational
                ? LogLevel.Information
                : LogLevel.Warning;
        return LoggerFactory.Create(builder => {
            builder.ClearProviders();
            builder.SetMinimumLevel(minimumLevel);
            builder.AddProvider(
                new ConsoleLoggerProvider(renderer, minimumLevel));
        });
    }
}
