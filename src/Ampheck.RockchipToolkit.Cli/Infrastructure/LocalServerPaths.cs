namespace Ampheck.RockchipToolkit.Cli.Infrastructure;

internal sealed record LocalServerPaths(
    string Directory,
    string StatePath,
    string LogPath);
