namespace Ampheck.RockchipToolkit.Cli.Infrastructure;

internal readonly record struct LocalServerState(
    int ProcessId,
    long StartTimeUtcTicks);
