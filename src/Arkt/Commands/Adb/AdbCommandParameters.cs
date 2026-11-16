using Ampheck.RockchipToolkit.Arkt.Commands.Framework;

namespace Ampheck.RockchipToolkit.Arkt.Commands.Adb;

internal class AdbCommandParameters {
    [CommandParam(
        ["--serial", "-s"],
        Description = "ADB device serial number.")]
    public string? SerialNumber { get; init; }

    [CommandParam(
        ["--verbose", "-v"],
        Description = "Enable informational and debug logs.")]
    public bool Verbose { get; init; }
}
