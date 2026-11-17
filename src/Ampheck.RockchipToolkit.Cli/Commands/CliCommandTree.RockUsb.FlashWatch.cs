using Ampheck.RockchipToolkit.Cli.Terminal;
using Ampheck.RockchipToolkit.Devices;
using Ampheck.RockchipToolkit.Hardware;
using Ampheck.RockchipToolkit.Transport.Usb;

namespace Ampheck.RockchipToolkit.Cli.Commands;

internal sealed partial class CliCommandTree {
    private static readonly TimeSpan WatchPollInterval =
        TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan WatchConfirmInterval =
        TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan WatchFilePollInterval =
        TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan WatchDepartureConfirmInterval =
        TimeSpan.FromSeconds(1);

    private async Task<int> RunFlashWatchAsync(
        string source,
        string? loaderLocation,
        DeviceTarget target,
        bool resetAfterFlash,
        bool verbose,
        CancellationToken cancellationToken
    ) {
        if (!IsHttpLocation(source) &&
            !File.Exists(source) &&
            !Directory.Exists(source)) {
            throw new FileNotFoundException(
                $"Firmware source '{source}' was not found.",
                source);
        }

        if (loaderLocation is not null &&
            !IsHttpLocation(loaderLocation) &&
            !File.Exists(loaderLocation)) {
            throw new FileNotFoundException(
                $"Loader source '{loaderLocation}' was not found.",
                loaderLocation);
        }

        await using var provider = new UsbDeviceProviderFactory().Create(
            target.Transport,
            target.VendorId,
            target.ProductId);
        var seenKeys = await ReadMaskRomKeysAsync(
                provider,
                target,
                cancellationToken)
            .ConfigureAwait(false);
        string? lastWarning = null;
        var flashing = false;
        _console.WriteStatus("Watching for new MaskROM devices");

        try {
            while (true) {
                await Task.Delay(
                        WatchPollInterval,
                        cancellationToken)
                    .ConfigureAwait(false);

                IReadOnlyList<DeviceDescriptor> current;
                try {
                    current = await ListWatchDevicesAsync(
                            provider,
                            target,
                            cancellationToken)
                        .ConfigureAwait(false);
                } catch (Exception error) when (
                    error is not OperationCanceledException) {
                    var warning = verbose
                        ? error.ToString()
                        : error.Message;
                    if (!string.Equals(
                            lastWarning,
                            warning,
                            StringComparison.Ordinal)) {
                        Console.Error.WriteLine(
                            $"warning: USB enumeration failed: {warning}");
                        lastWarning = warning;
                    }

                    continue;
                }

                var maskRom = current
                    .Where(device =>
                        device.Mode == RockchipUsbMode.MaskRom)
                    .ToArray();
                var maskRomKeys = maskRom
                    .Select(WatchDeviceKey)
                    .ToHashSet(StringComparer.Ordinal);
                seenKeys.IntersectWith(maskRomKeys);
                var pending = maskRom
                    .Where(device =>
                        !seenKeys.Contains(WatchDeviceKey(device)))
                    .ToArray();

                if (current.Count == 1 &&
                    maskRom.Length == 1 &&
                    pending.Length == 1) {
                    var detected = maskRom[0];
                    var key = WatchDeviceKey(detected);
                    await Task.Delay(
                            WatchConfirmInterval,
                            cancellationToken)
                        .ConfigureAwait(false);
                    current = await ListWatchDevicesAsync(
                            provider,
                            target,
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (current.Count != 1 ||
                        current[0].Mode != RockchipUsbMode.MaskRom ||
                        !string.Equals(
                            WatchDeviceKey(current[0]),
                            key,
                            StringComparison.Ordinal)) {
                        continue;
                    }

                    seenKeys.Add(key);
                    lastWarning = null;
                    flashing = true;
                    var succeeded = await FlashWatchedDeviceAsync(
                            source,
                            loaderLocation,
                            target,
                            current[0],
                            resetAfterFlash,
                            verbose,
                            cancellationToken)
                        .ConfigureAwait(false);
                    flashing = false;
                    if (!succeeded) {
                        _console.WriteStatus(
                            "Watch stopped after flash failure");
                        return 1;
                    }

                    _console.WriteStatus(
                        "Waiting for the device to disconnect");
                    await WaitForDeviceDepartureAsync(
                            provider,
                            target,
                            verbose,
                            cancellationToken)
                        .ConfigureAwait(false);
                    seenKeys.Clear();
                    _console.WriteStatus("Watching for new MaskROM devices");
                    continue;
                }

                if (pending.Length > 0) {
                    var warning = current.Count > 1
                        ? "A new MaskROM was detected, but multiple " +
                          "RockUSB devices are connected. Leave exactly " +
                          "one device connected."
                        : "Waiting for a stable MaskROM device.";
                    if (!string.Equals(
                            lastWarning,
                            warning,
                            StringComparison.Ordinal)) {
                        _console.WriteStatus(warning);
                        lastWarning = warning;
                    }

                    continue;
                }

                lastWarning = null;
            }
        } catch (OperationCanceledException) when (
            cancellationToken.IsCancellationRequested) {
            _console.WriteStatus(flashing
                ? "Flash cancelled"
                : "Watch stopped");
            return flashing ? 1 : 0;
        }
    }

    private static async ValueTask WaitForDeviceDepartureAsync(
        IUsbDeviceProvider provider,
        DeviceTarget target,
        bool verbose,
        CancellationToken cancellationToken
    ) {
        string? lastWarning = null;
        DateTimeOffset? absentSince = null;
        while (true) {
            await Task.Delay(
                    WatchPollInterval,
                    cancellationToken)
                .ConfigureAwait(false);
            try {
                var current = await ListWatchDevicesAsync(
                        provider,
                        target,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (current.Count == 0) {
                    var now = DateTimeOffset.UtcNow;
                    absentSince ??= now;
                    if (now - absentSince.Value >=
                        WatchDepartureConfirmInterval) {
                        return;
                    }
                } else {
                    absentSince = null;
                }

                lastWarning = null;
            } catch (Exception error) when (
                error is not OperationCanceledException) {
                var warning = verbose
                    ? error.ToString()
                    : error.Message;
                if (!string.Equals(
                        lastWarning,
                        warning,
                        StringComparison.Ordinal)) {
                    Console.Error.WriteLine(
                        $"warning: USB enumeration failed: {warning}");
                    lastWarning = warning;
                }
            }
        }
    }

    private async Task<bool> FlashWatchedDeviceAsync(
        string source,
        string? loaderLocation,
        DeviceTarget target,
        DeviceDescriptor device,
        bool resetAfterFlash,
        bool verbose,
        CancellationToken cancellationToken
    ) {
        try {
            if (File.Exists(source)) {
                await WaitForStableFileAsync(
                        source,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            if (loaderLocation is not null &&
                File.Exists(loaderLocation)) {
                await WaitForStableFileAsync(
                        loaderLocation,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            _console.WriteStatus(
                $"New MaskROM {WatchDeviceLabel(device)}; flashing {source}");

            await FlashOnceAsync(
                    source,
                    loaderLocation,
                    target,
                    resetAfterFlash,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        } catch (Exception error) when (
            error is not OperationCanceledException) {
            Console.Error.WriteLine(verbose
                ? error.ToString()
                : $"error: {error.Message}");
            return false;
        }
    }

    private static async ValueTask<HashSet<string>> ReadMaskRomKeysAsync(
        IUsbDeviceProvider provider,
        DeviceTarget target,
        CancellationToken cancellationToken
    ) {
        var current = await ListWatchDevicesAsync(
                provider,
                target,
                cancellationToken)
            .ConfigureAwait(false);
        return current
            .Where(device =>
                device.Mode == RockchipUsbMode.MaskRom)
            .Select(WatchDeviceKey)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static async ValueTask<IReadOnlyList<DeviceDescriptor>>
        ListWatchDevicesAsync(
            IUsbDeviceProvider provider,
            DeviceTarget target,
            CancellationToken cancellationToken
        ) {
        var devices = await provider.EnumerateAsync(cancellationToken)
            .ConfigureAwait(false);
        return [.. devices
            .Where(device =>
                device.Mode != RockchipUsbMode.None &&
                (!target.VendorId.HasValue ||
                    device.VendorId == target.VendorId) &&
                (!target.ProductId.HasValue ||
                    device.ProductId == target.ProductId))
            .Select(device => {
                var identity = RockchipUsbCatalog.Resolve(
                    device.VendorId,
                    device.ProductId);
                return new DeviceDescriptor {
                    VendorId = device.VendorId,
                    ProductId = device.ProductId,
                    UsbVersion = device.UsbVersion,
                    Mode = device.Mode,
                    SerialNumber = device.SerialNumber,
                    PlatformId = device.PlatformId,
                    Description = identity is null
                        ? null
                        : $"{identity.Family} {identity.Description}"
                };
            })];
    }

    private static async ValueTask WaitForStableFileAsync(
        string path,
        CancellationToken cancellationToken
    ) {
        var previous = TryReadFileStamp(path);
        while (true) {
            await Task.Delay(
                    WatchFilePollInterval,
                    cancellationToken)
                .ConfigureAwait(false);
            var current = TryReadFileStamp(path);
            if (current is null) {
                previous = null;
                continue;
            }

            if (current == previous) {
                return;
            }

            previous = current;
        }
    }

    private static (
        long Length,
        DateTime LastWriteTimeUtc
    )? TryReadFileStamp(
        string path
    ) {
        var file = new FileInfo(path);
        file.Refresh();
        if (!file.Exists) {
            return null;
        }

        return (file.Length, file.LastWriteTimeUtc);
    }

    private static string WatchDeviceKey(DeviceDescriptor device) {
        if (!string.IsNullOrWhiteSpace(device.PlatformId)) {
            return $"platform:{device.PlatformId}";
        }

        if (!string.IsNullOrWhiteSpace(device.SerialNumber)) {
            return $"serial:{device.SerialNumber}";
        }

        return $"usb:{device.VendorId:X4}:{device.ProductId:X4}:" +
            $"{device.UsbVersion:X4}";
    }

    private static string WatchDeviceLabel(DeviceDescriptor device) {
        var identity = device.PlatformId ??
            device.SerialNumber ??
            $"{device.VendorId:X4}:{device.ProductId:X4}";
        return device.Description is null
            ? identity
            : $"{identity} ({device.Description})";
    }

    private static bool IsHttpLocation(string location) {
        return Uri.TryCreate(
                location,
                UriKind.Absolute,
                out var uri) &&
            uri.Scheme is "http" or "https";
    }
}
