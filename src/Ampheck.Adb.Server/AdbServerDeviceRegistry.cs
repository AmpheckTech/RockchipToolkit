using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using Ampheck.Adb.Device;
using Ampheck.Adb.Usb;
using Microsoft.Extensions.Logging;

namespace Ampheck.Adb.Server;

internal sealed class AdbServerDeviceRegistry : IAsyncDisposable {
    private readonly IAdbDeviceProvider _provider;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly ConcurrentDictionary<string, ManagedAdbDevice>
        _devices = new(StringComparer.OrdinalIgnoreCase);
    private long _nextTransportId;
    private bool _disposed;

    public AdbServerDeviceRegistry(
        AdbUsbOptions options,
        ILoggerFactory loggerFactory) {
        _logger = loggerFactory.CreateLogger<AdbServerDeviceRegistry>();
        _provider = AdbDeviceProviderFactory.Create(
            loggerFactory,
            options);
    }

    public async ValueTask RefreshAsync(
        CancellationToken cancellationToken) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _refreshGate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        try {
            // A live server owns the USB interface. Some providers cannot
            // enumerate that interface again until the owner closes it.
            if (_devices.Values.Any(device =>
                device.IsUsb && device.IsConnected)) {
                return;
            }

            foreach (var device in _devices.Values.Where(device =>
                device.IsUsb)) {
                await device.ResetIfDisconnectedAsync()
                    .ConfigureAwait(false);
            }

            var infos = await _provider.EnumerateAsync(
                    cancellationToken)
                .ConfigureAwait(false);
            var present = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var info in infos) {
                cancellationToken.ThrowIfCancellationRequested();
                var serial = GetSerialNumber(info);
                present.Add(serial);
                if (_devices.TryGetValue(serial, out var current)) {
                    if (string.Equals(
                        current.PlatformId,
                        info.PlatformId,
                        StringComparison.Ordinal)) {
                        await current.ProbeAsync(cancellationToken)
                            .ConfigureAwait(false);
                        continue;
                    }

                    await RemoveAsync(serial).ConfigureAwait(false);
                }

                var device = new ManagedAdbDevice(
                    info,
                    _provider,
                    _logger,
                    NextTransportId());
                await device.ProbeAsync(cancellationToken)
                    .ConfigureAwait(false);
                _devices[serial] = device;
            }

            var missing = _devices.Keys
                .Where(serial =>
                    _devices[serial].IsUsb &&
                    !present.Contains(serial))
                .ToArray();
            foreach (var serial in missing) {
                await RemoveAsync(serial).ConfigureAwait(false);
            }
        } finally {
            _refreshGate.Release();
        }
    }

    public IReadOnlyList<ManagedAdbDevice> List() {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _devices.Values
            .OrderBy(device => device.SerialNumber)
            .ToArray();
    }

    public async ValueTask ReleaseUsbDevicesAsync(
        CancellationToken cancellationToken) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _refreshGate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        Exception? failure = null;
        try {
            foreach (var serialNumber in _devices
                .Where(pair => pair.Value.IsUsb)
                .Select(pair => pair.Key)
                .ToArray()) {
                if (!_devices.TryRemove(
                        serialNumber,
                        out var device)) {
                    continue;
                }

                try {
                    await device.DisposeAsync()
                        .ConfigureAwait(false);
                } catch (Exception error) {
                    failure ??= error;
                }
            }
        } finally {
            _refreshGate.Release();
        }

        if (failure is not null) {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    public async ValueTask<string> ConnectNetworkAsync(
        string host,
        int port,
        CancellationToken cancellationToken) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (port is <= 0 or > ushort.MaxValue) {
            throw new ArgumentOutOfRangeException(nameof(port));
        }

        await _refreshGate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        try {
            var serialNumber = $"{host}:{port}";
            if (_devices.ContainsKey(serialNumber)) {
                return $"already connected to {serialNumber}";
            }

            var device = new ManagedAdbDevice(
                host,
                port,
                _logger,
                NextTransportId());
            await device.ProbeAsync(cancellationToken)
                .ConfigureAwait(false);
            if (device.State != "device") {
                await device.DisposeAsync().ConfigureAwait(false);
                throw new InvalidOperationException(
                    $"Unable to connect to {serialNumber}");
            }

            _devices[serialNumber] = device;
            return $"connected to {serialNumber}";
        } finally {
            _refreshGate.Release();
        }
    }

    public async ValueTask<string> DisconnectNetworkAsync(
        string host,
        int port,
        CancellationToken cancellationToken) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var serialNumber = $"{host}:{port}";
        await _refreshGate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        try {
            if (!_devices.TryGetValue(
                serialNumber,
                out var device) ||
                device.IsUsb) {
                return $"no such device {serialNumber}";
            }

            _devices.TryRemove(serialNumber, out _);
            await device.DisposeAsync().ConfigureAwait(false);
            return $"disconnected {serialNumber}";
        } finally {
            _refreshGate.Release();
        }
    }

    public async ValueTask<string> DisconnectAllNetworkAsync() {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _refreshGate.WaitAsync().ConfigureAwait(false);
        Exception? failure = null;
        try {
            var networkDevices = _devices
                .Where(pair => !pair.Value.IsUsb)
                .ToArray();
            foreach (var pair in networkDevices) {
                if (_devices.TryRemove(pair.Key, out var device)) {
                    try {
                        await device.DisposeAsync()
                            .ConfigureAwait(false);
                    } catch (Exception error) {
                        failure ??= error;
                    }
                }
            }
        } finally {
            _refreshGate.Release();
        }

        if (failure is not null) {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }

        return "disconnected all network devices";
    }

    public async ValueTask<ManagedAdbDevice> RequireAsync(
        string? serialNumber,
        CancellationToken cancellationToken) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_devices.Count == 0) {
            await RefreshAsync(cancellationToken).ConfigureAwait(false);
        }

        var devices = List();
        if (serialNumber is not null) {
            return devices.FirstOrDefault(device =>
                    device.SerialNumber.Equals(
                        serialNumber,
                        StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException(
                    $"ADB device '{serialNumber}' was not found.");
        }

        var online = devices
            .Where(device => device.State == "device")
            .ToArray();
        if (online.Length == 1) {
            return online[0];
        }

        if (online.Length > 1) {
            throw new InvalidOperationException(
                "More than one ADB device is online. Specify a serial number.");
        }

        if (devices.Count == 1) {
            return devices[0];
        }

        throw new InvalidOperationException(
            devices.Count == 0
                ? "No ADB devices were found."
                : "No online ADB device was found.");
    }

    public ManagedAdbDevice RequireByTransportId(
        ulong transportId) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _devices.Values.FirstOrDefault(device =>
                device.TransportId == transportId)
            ?? throw new InvalidOperationException(
                $"ADB transport {transportId} was not found.");
    }

    public async ValueTask DisposeAsync() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        var devices = _devices.Values.ToArray();
        _devices.Clear();
        Exception? failure = null;
        foreach (var device in devices) {
            try {
                await device.DisposeAsync().ConfigureAwait(false);
            } catch (Exception error) {
                failure ??= error;
            }
        }

        try {
            await _provider.DisposeAsync().ConfigureAwait(false);
        } catch (Exception error) {
            failure ??= error;
        }

        try {
            _refreshGate.Dispose();
        } catch (Exception error) {
            failure ??= error;
        }

        if (failure is not null) {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private async ValueTask RemoveAsync(string serialNumber) {
        if (!_devices.Remove(serialNumber, out var device)) {
            return;
        }

        await device.DisposeAsync().ConfigureAwait(false);
    }

    private static string GetSerialNumber(AdbDeviceInfo info) {
        if (!string.IsNullOrWhiteSpace(info.SerialNumber)) {
            return info.SerialNumber;
        }

        if (!string.IsNullOrWhiteSpace(info.PlatformId)) {
            return info.PlatformId;
        }

        return $"{info.VendorId:X4}:{info.ProductId:X4}";
    }

    private ulong NextTransportId() {
        return checked((ulong)Interlocked.Increment(
            ref _nextTransportId));
    }
}
