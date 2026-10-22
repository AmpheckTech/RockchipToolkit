namespace Ampheck.Adb.Cli;

internal sealed record ClientSettings(
    string? SerialNumber,
    string Host,
    int Port,
    bool Direct,
    bool Verbose);
