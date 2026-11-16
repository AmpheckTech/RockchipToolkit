using Ampheck.RockchipToolkit.Arkt.Terminal;
using Ampheck.RockchipToolkit.Core.Devices;
using Ampheck.RockchipToolkit.Features.Devices.Services;
using Ampheck.RockchipToolkit.HardwareTransport.Protocol;
using Ampheck.RockchipToolkit.Runtime.Terminal;

namespace Ampheck.RockchipToolkit.Arkt.Support.Devices;

internal sealed class DeviceCommandRunner(
    DeviceService devices,
    ConsoleRenderer console
) {
    public async Task<int> RunDevicesAsync(
        DeviceTarget target,
        CancellationToken cancellationToken = default
    ) {
        var result = await devices.ListAsync(
                target,
                cancellationToken)
            .ConfigureAwait(false);
        if (result.Devices.Length > 0) {
            Console.WriteLine(
                OutputFormatter.FormatDevices(result));
        }

        return 0;
    }

    public async Task<int> RunInfoAsync(
        DeviceTarget target,
        string? loaderLocation,
        CancellationToken cancellationToken = default
    ) {
        var result = await devices.InfoAsync(
                target,
                loaderLocation,
                cancellationToken)
            .ConfigureAwait(false);
        Console.WriteLine(OutputFormatter.FormatInfo(result));
        return 0;
    }

    public async Task<int> RunChipInfoAsync(
        DeviceTarget target,
        string? loaderLocation,
        CancellationToken cancellationToken = default
    ) {
        var result = await devices.ChipInfoAsync(
                target,
                loaderLocation,
                cancellationToken)
            .ConfigureAwait(false);
        Console.WriteLine(OutputFormatter.FormatChipInfo(result));
        return 0;
    }

    public async Task<int> RunChipIdAsync(
        DeviceTarget target,
        string? loaderLocation,
        CancellationToken cancellationToken = default
    ) {
        var result = await devices.ChipIdAsync(
                target,
                loaderLocation,
                cancellationToken)
            .ConfigureAwait(false);
        Console.WriteLine(result.ChipId);
        return 0;
    }

    public async Task<int> RunCapabilityAsync(
        DeviceTarget target,
        string? loaderLocation,
        CancellationToken cancellationToken = default
    ) {
        var result = await devices.CapabilityAsync(
                target,
                loaderLocation,
                cancellationToken)
            .ConfigureAwait(false);
        Console.WriteLine(
            OutputFormatter.FormatCapabilities(result));
        return 0;
    }

    public async Task<int> RunLoaderAsync(
        DeviceTarget target,
        string location,
        CancellationToken cancellationToken = default
    ) {
        using var progress = new OperationProgressDisplay(console);
        await devices.LoaderAsync(
                target,
                location,
                progress,
                cancellationToken)
            .ConfigureAwait(false);
        progress.Complete();
        return 0;
    }

    public async Task<int> RunResetAsync(
        DeviceTarget target,
        ResetMode mode,
        CancellationToken cancellationToken = default
    ) {
        await devices.ResetAsync(
                target,
                mode,
                cancellationToken)
            .ConfigureAwait(false);
        Console.WriteLine("Reset requested");
        return 0;
    }
}
