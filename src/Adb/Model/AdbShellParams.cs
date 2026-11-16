namespace Ampheck.RockchipToolkit.Adb.Model;

public sealed record AdbShellParams {
    public string? SerialNumber { get; init; }
    public required string Command { get; init; }
}
