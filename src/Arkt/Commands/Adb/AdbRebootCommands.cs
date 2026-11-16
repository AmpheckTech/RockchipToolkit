using Ampheck.RockchipToolkit.Adb.Model;
using Ampheck.RockchipToolkit.Arkt.Commands.Framework;
using Ampheck.RockchipToolkit.Arkt.Support.Adb;

namespace Ampheck.RockchipToolkit.Arkt.Commands.Adb;

[CommandGroup(
    ["adb"],
    Description = "Access devices through the official ADB server.")]
internal sealed class AdbRebootCommands(AdbCommandRunner runner) {
    [CommandHandler(
        ["adb", "reboot"],
        Description = "Reboot an ADB device. Defaults to normal mode."
    )]
    private ValueTask<int> ExecuteRebootAsync(
        [CommandParam] AdbCommandParameters parameters,
        CancellationToken cancellationToken
    ) =>
        new(runner.RebootAsync(
            parameters.SerialNumber,
            AdbRebootMode.System,
            cancellationToken));

    [CommandHandler(
        ["adb", "reboot", "normal"],
        Description = "Reboot into the system.",
        Aliases = ["system"])]
    private ValueTask<int> ExecuteNormalAsync(
        [CommandParam] AdbCommandParameters parameters,
        CancellationToken cancellationToken
    ) =>
        new(runner.RebootAsync(
            parameters.SerialNumber,
            AdbRebootMode.System,
            cancellationToken));

    [CommandHandler(
        ["adb", "reboot", "recovery"],
        Description = "Reboot into recovery mode."
    )]
    private ValueTask<int> ExecuteRecoveryAsync(
        [CommandParam] AdbCommandParameters parameters,
        CancellationToken cancellationToken
    ) =>
        new(runner.RebootAsync(
            parameters.SerialNumber,
            AdbRebootMode.Recovery,
            cancellationToken));

    [CommandHandler(
        ["adb", "reboot", "loader"],
        Description = "Reboot into RockUSB Loader mode."
    )]
    private ValueTask<int> ExecuteLoaderAsync(
        [CommandParam] AdbLoaderParameters parameters,
        CancellationToken cancellationToken
    ) =>
        new(runner.RebootAsync(
            parameters.SerialNumber,
            AdbRebootMode.Bootloader,
            cancellationToken));
}
