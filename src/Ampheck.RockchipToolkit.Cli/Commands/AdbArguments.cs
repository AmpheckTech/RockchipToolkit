using Ampheck.RockchipToolkit.Rpc.JsonRpc;

namespace Ampheck.RockchipToolkit.Cli.Commands;

internal sealed record AdbDeviceArguments(
    string? SerialNumber,
    DeviceTargetArguments LoaderTarget);

internal sealed record AdbShellArguments(
    string? SerialNumber,
    string? Command,
    string[] Arguments);

internal sealed record AdbTransferArguments(
    string? SerialNumber,
    string Source,
    string Destination);

internal sealed record AdbRebootArguments(
    string? SerialNumber,
    ToolkitAdbRebootMode Mode,
    DeviceTargetArguments LoaderTarget);
