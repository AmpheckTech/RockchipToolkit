namespace Ampheck.RockchipToolkit.Cli.Commands;

internal sealed record ServerNodaemonArguments(
    string Listen,
    string? Token,
    string? HostName,
    int IdleSeconds,
    int MaximumBlobMiB,
    int SerialHistoryKiB);
