using System.Text;
using Ampheck.RockchipToolkit.Adb.Model;
using Ampheck.RockchipToolkit.Adb.Server;
using Ampheck.RockchipToolkit.Adb.Sync;

namespace Ampheck.RockchipToolkit.Adb.Client;

public sealed partial class AdbClient {
    private async Task<AdbShell> OpenServerShellAsync(
        AdbDevice device,
        CancellationToken cancellationToken
    ) {
        var shell = await AdbServerClient.OpenShellAsync(
                device.SerialNumber,
                command: null,
                cancellationToken,
                _options.Server)
            .ConfigureAwait(false);
        return new AdbShell(shell);
    }

    private async Task<string> ExecuteShellCommandAsync(
        AdbDevice device,
        string command,
        CancellationToken cancellationToken
    ) {
        await using var shell =
            await AdbServerClient.OpenShellAsync(
                    device.SerialNumber,
                    command,
                    cancellationToken,
                    _options.Server)
                .ConfigureAwait(false);
        var output = new StringBuilder();
        await shell.ReadAsync(
                text => output.Append(text),
                cancellationToken)
            .ConfigureAwait(false);
        return output.ToString();
    }

    private async Task<AdbSyncTransfer> OpenSyncAsync(
        AdbDevice device,
        CancellationToken cancellationToken
    ) {
        var stream = await AdbServerClient.OpenSyncAsync(
                device.SerialNumber,
                cancellationToken,
                _options.Server)
            .ConfigureAwait(false);
        return new AdbSyncTransfer(stream);
    }

    private static AdbDevice SelectSingle(
        IEnumerable<AdbDevice> devices,
        string? serialNumber,
        bool mustBeOnline
    ) {
        var matching = FilterBySerial(devices, serialNumber)
            .Where(device => !mustBeOnline || device.IsOnline)
            .ToArray();
        if (matching.Length == 0) {
            throw new InvalidOperationException(
                serialNumber is null
                    ? mustBeOnline
                        ? "No online ADB device was found."
                        : "No ADB device was found."
                    : $"ADB device '{serialNumber}' " +
                      (mustBeOnline
                          ? "is not online."
                          : "was not found."));
        }

        if (matching.Length > 1) {
            throw new InvalidOperationException(
                "Multiple ADB devices were found. Specify a serial number.");
        }

        return matching[0];
    }

    private static IReadOnlyList<AdbDevice> FilterBySerial(
        IEnumerable<AdbDevice> devices,
        string? serialNumber
    ) {
        return serialNumber is null
            ? devices.ToArray()
            : devices
                .Where(device => device.SerialNumber.Equals(
                    serialNumber,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
    }

    private static string GetServerRebootMode(AdbRebootMode mode) {
        return mode switch {
            AdbRebootMode.System => string.Empty,
            AdbRebootMode.Bootloader => "bootloader",
            AdbRebootMode.Recovery => "recovery",
            AdbRebootMode.Sideload => "sideload",
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
    }
}
