namespace Ampheck.RockchipToolkit.Adb.Model;

public sealed record AdbRebootParams {
    public string? SerialNumber { get; init; }
    public AdbRebootMode Mode { get; init; } =
        AdbRebootMode.System;
}
