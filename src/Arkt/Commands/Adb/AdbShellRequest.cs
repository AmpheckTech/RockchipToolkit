namespace Ampheck.RockchipToolkit.Arkt.Commands.Adb;

internal sealed record AdbShellRequest(
    string? SerialNumber,
    string? Command
);
