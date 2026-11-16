using System.Text;
using Ampheck.RockchipToolkit.Adb.Model;
using Ampheck.RockchipToolkit.Adb.Services;
using Ampheck.RockchipToolkit.Arkt.Commands.Adb;
using Ampheck.RockchipToolkit.Arkt.Support.Serial;
using Ampheck.RockchipToolkit.Arkt.Terminal;
using Ampheck.RockchipToolkit.Features.Devices.Model;
using Ampheck.RockchipToolkit.Runtime.Terminal;

namespace Ampheck.RockchipToolkit.Arkt.Support.Adb;

internal sealed partial class AdbCommandRunner(
    AdbToolkitService adb,
    ConsoleRenderer console
) {
    public async Task<int> ListDevicesAsync(
        string? serialNumber,
        CancellationToken cancellationToken = default
    ) {
        await EnsureServerAsync(cancellationToken).ConfigureAwait(false);
        var result = await adb.DevicesAsync(
                serialNumber,
                cancellationToken)
            .ConfigureAwait(false);
        foreach (var device in result.Devices) {
            Console.WriteLine(
                $"{device.SerialNumber}\t{device.State}\t" +
                FormatDetails(device));
        }

        Console.Out.Flush();
        return 0;
    }

    public async Task<int> ShellAsync(
        AdbShellRequest options,
        CancellationToken cancellationToken = default
    ) {
        await EnsureServerAsync(cancellationToken).ConfigureAwait(false);
        if (options.Command is null) {
            await OpenInteractiveShellAsync(
                    options.SerialNumber,
                    cancellationToken)
                .ConfigureAwait(false);
            return 0;
        }

        var result = await adb.ShellAsync(
                new AdbShellParams {
                    SerialNumber = options.SerialNumber,
                    Command = options.Command
                },
                cancellationToken)
            .ConfigureAwait(false);
        Console.Out.Write(result);
        if (result.Length > 0 &&
            !result.EndsWith('\n')) {
            Console.WriteLine();
        }

        Console.Out.Flush();
        return 0;
    }

    public async Task<int> RebootAsync(
        string? serialNumber,
        AdbRebootMode mode,
        CancellationToken cancellationToken = default
    ) {
        await EnsureServerAsync(cancellationToken).ConfigureAwait(false);
        var name = mode switch {
            AdbRebootMode.Recovery => "recovery",
            AdbRebootMode.Bootloader => "loader",
            _ => "normal"
        };
        console.WriteStatus($"Requesting {name} reboot");
        await adb.RebootAsync(
                new AdbRebootParams {
                    SerialNumber = serialNumber,
                    Mode = mode
                },
                cancellationToken)
            .ConfigureAwait(false);
        console.WriteStatus(
            $"{name} reboot requested",
            complete: true);
        return 0;
    }

    private async ValueTask EnsureServerAsync(
        CancellationToken cancellationToken
    ) {
        await adb.EnsureServerAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task OpenInteractiveShellAsync(
        string? serialNumber,
        CancellationToken cancellationToken
    ) {
        await using var shell = await adb.OpenShellAsync(
                serialNumber,
                cancellationToken)
            .ConfigureAwait(false);
        var output = shell.ReadAsync(
            value => {
                var bytes = Encoding.UTF8.GetBytes(value);
                SerialTerminalRunner.WriteOutputAsync(bytes)
                    .AsTask()
                    .GetAwaiter()
                    .GetResult();
            },
            cancellationToken);
        await SerialTerminalRunner.RunAsync(
                (data, token) => shell.WriteAsync(data, token),
                output,
                console,
                cancellationToken,
                "Interactive ADB shell. Use Ctrl+] to leave.")
            .ConfigureAwait(false);
    }

    private static string FormatDetails(
        AdbDeviceDescriptor device
    ) {
        var values = new[] {
            device.Product is null ? null : $"product:{device.Product}",
            device.Model is null ? null : $"model:{device.Model}",
            device.DeviceName is null ? null : $"device:{device.DeviceName}"
        };
        return string.Join(' ', values.Where(value => value is not null));
    }
}
