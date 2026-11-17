using System.Globalization;
using Ampheck.RockchipToolkit.Cli.Inputs;
using Ampheck.RockchipToolkit.Cli.Terminal;
using Ampheck.RockchipToolkit.Devices;
using Ampheck.RockchipToolkit.Hardware;
using Ampheck.RockchipToolkit.Protocol.RockUsb;

namespace Ampheck.RockchipToolkit.Cli.Commands;

internal sealed partial class CliCommandTree {
    private static readonly (int Bit, string Label)[] CapabilityBits = [
        (0, "Direct LBA"),
        (1, "Vendor storage API"),
        (2, "First 4 MiB access"),
        (3, "Read LBA"),
        (4, "New vendor storage API"),
        (5, "Read UART data from RAM"),
        (6, "Read IDB configuration"),
        (7, "Read secure mode"),
        (8, "New IDB"),
        (9, "Storage media information"),
        (10, "LBA write parity"),
        (11, "Read OTP data"),
        (12, "USB 3 download"),
        (13, "Write OTP proof"),
        (14, "Write cipher key")
    ];

    private async Task<int> RunDevicesAsync(
        DeviceTarget target,
        CancellationToken cancellationToken
    ) {
        var devices = CreateDeviceCatalog();
        var result = await devices.ListDevicesAsync(target, cancellationToken)
            .ConfigureAwait(false);
        foreach (var device in result) {
            Console.WriteLine(
                $"{device.VendorId:X4}:{device.ProductId:X4} " +
                $"{device.Mode} USB {device.UsbVersion:X4} " +
                $"{device.PlatformId ?? string.Empty} " +
                $"{device.Description ?? string.Empty} " +
                $"{device.SerialNumber ?? string.Empty}".TrimEnd());
        }

        return 0;
    }

    private async Task<int> RunInfoAsync(
        DeviceTarget target,
        string? loaderLocation,
        CancellationToken cancellationToken
    ) {
        using var progress = new OperationProgressDisplay(_console);
        await using var loader = await _inputs.ResolveOptionalAsync(
                loaderLocation,
                progress,
                cancellationToken)
            .ConfigureAwait(false);
        var devices = CreateDeviceCatalog();
        var result = await devices.GetInfoAsync(
                target,
                loader?.Path,
                cancellationToken)
            .ConfigureAwait(false);
        Console.WriteLine(string.Join(
            Environment.NewLine,
            "Chip information:",
            $"  Chip code:  {result.Chip.Identifier}",
            $"  Platform:   {result.Chip.PlatformName}",
            string.Empty,
            "Flash information:",
            $"  Manufacturer: {result.Flash.Manufacturer} " +
            $"(0x{result.Flash.ManufacturerCode:X2})",
            $"  Capacity:     {FormatFlashBytes((long)result.Flash.CapacityBytes)}",
            $"  Block size:   {FormatFlashBytes(result.Flash.BlockSizeBytes)}",
            $"  Page size:    {FormatFlashBytes(result.Flash.PageSizeBytes)}",
            $"  Blocks:       {result.Flash.BlockCount:N0}",
            $"  ECC bits:     {result.Flash.EccBits}",
            $"  Access time:  {result.Flash.AccessTime}",
            $"  Flash CS:     {result.Flash.ChipSelectMask}"));
        return 0;
    }

    private async Task<int> RunChipInfoAsync(
        DeviceTarget target,
        string? loaderLocation,
        CancellationToken cancellationToken
    ) {
        using var progress = new OperationProgressDisplay(_console);
        await using var loader = await _inputs.ResolveOptionalAsync(
                loaderLocation,
                progress,
                cancellationToken)
            .ConfigureAwait(false);
        var devices = CreateDeviceCatalog();
        var result = await devices.GetChipInfoAsync(
                target,
                loader?.Path,
                cancellationToken)
            .ConfigureAwait(false);
        Console.WriteLine(string.Join(
            Environment.NewLine,
            "Chip information:",
            $"  Chip code:  {result.Identifier}",
            $"  Platform:   {result.PlatformName}"));
        return 0;
    }

    private async Task<int> RunChipIdAsync(
        DeviceTarget target,
        string? loaderLocation,
        CancellationToken cancellationToken
    ) {
        using var progress = new OperationProgressDisplay(_console);
        await using var loader = await _inputs.ResolveOptionalAsync(
                loaderLocation,
                progress,
                cancellationToken)
            .ConfigureAwait(false);
        var devices = CreateDeviceCatalog();
        var result = await devices.GetChipIdAsync(
                target,
                loader?.Path,
                cancellationToken)
            .ConfigureAwait(false);
        Console.WriteLine(result);
        return 0;
    }

    private async Task<int> RunCapabilityAsync(
        DeviceTarget target,
        string? loaderLocation,
        CancellationToken cancellationToken
    ) {
        using var progress = new OperationProgressDisplay(_console);
        await using var loader = await _inputs.ResolveOptionalAsync(
                loaderLocation,
                progress,
                cancellationToken)
            .ConfigureAwait(false);
        var devices = CreateDeviceCatalog();
        var result = await devices.GetCapabilityAsync(
                target,
                loader?.Path,
                cancellationToken)
            .ConfigureAwait(false);
        var flags = (ulong)result;
        Console.WriteLine(string.Join(
            Environment.NewLine,
            [
                "Capability information:",
                $"  Flags:       0x{flags:X16}",
                .. CapabilityBits.Select(item =>
                    $"  {item.Label}: " +
                    ((flags & (1UL << item.Bit)) != 0
                        ? "enabled"
                        : "disabled"))
            ]));
        return 0;
    }

    private async Task<int> RunLoaderAsync(
        DeviceTarget target,
        string location,
        CancellationToken cancellationToken
    ) {
        using var progress = new OperationProgressDisplay(_console);
        await using var loader = await _inputs
            .ResolveAsync(location, progress, cancellationToken)
            .ConfigureAwait(false);
        var devices = CreateDeviceCatalog();
        await devices.DownloadLoaderAsync(
                target,
                loader.Path,
                progress,
                cancellationToken)
            .ConfigureAwait(false);
        progress.Complete();
        return 0;
    }

    private async Task<int> RunResetAsync(
        DeviceTarget target,
        ResetMode mode,
        CancellationToken cancellationToken
    ) {
        var devices = CreateDeviceCatalog();
        await devices.ResetAsync(target, mode, cancellationToken)
            .ConfigureAwait(false);
        Console.WriteLine("Reset requested");
        return 0;
    }

    private static DeviceCatalog CreateDeviceCatalog() =>
        new(new UsbDeviceProviderFactory());

    private static string FormatFlashBytes(long bytes) {
        if (bytes >= 1L << 30) {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{bytes / (double)(1L << 30):F2} GiB");
        }

        if (bytes >= 1L << 20) {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{bytes / (double)(1L << 20):F0} MiB");
        }

        if (bytes >= 1L << 10) {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{bytes / (double)(1L << 10):F0} KiB");
        }

        return $"{bytes} bytes";
    }
}
