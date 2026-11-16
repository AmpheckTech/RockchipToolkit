namespace Ampheck.RockchipToolkit.Adb.Model;

public sealed record AdbPushParams {
    public string? SerialNumber { get; init; }
    public required string Source { get; init; }
    public required string Destination { get; init; }
}
