using Microsoft.Extensions.Logging;

namespace Ampheck.Serial;

public sealed class SerialHubManager : IAsyncDisposable {
    private readonly object _sync = new();
    private readonly Dictionary<string, SerialHub> _hubs =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ILoggerFactory _loggerFactory;
    private int _disposed;

    public SerialHubManager(ILoggerFactory loggerFactory) {
        _loggerFactory = loggerFactory ??
            throw new ArgumentNullException(
                nameof(loggerFactory));
    }

    public IReadOnlyList<SerialDeviceInfo> ListDevices() {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _disposed) != 0,
            this);
        return SerialPortDiscovery.List()
            .Select(name => new SerialDeviceInfo(name))
            .ToArray();
    }

    public ValueTask<SerialSession> OpenSessionAsync(
        SerialPortOptions options,
        int historyBytes = 256 * 1024) {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _disposed) != 0,
            this);
        options.Validate();
        if (historyBytes <= 0) {
            throw new ArgumentOutOfRangeException(
                nameof(historyBytes));
        }

        var hub = GetHub(options, historyBytes);

        return hub.OpenSessionAsync();
    }

    public ValueTask<SerialSession> OpenExistingSessionAsync(
        SerialPortOptions options) {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _disposed) != 0,
            this);
        options.Validate();
        SerialHub hub;
        lock (_sync) {
            if (!_hubs.TryGetValue(
                    options.PortName,
                    out hub!)) {
                throw new InvalidOperationException(
                    $"Serial port '{options.PortName}' is not open.");
            }

            EnsureCompatible(hub, options);
        }

        return hub.OpenSessionAsync();
    }

    public ValueTask<SerialSession> OpenExistingSessionAsync(
        string portName) {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _disposed) != 0,
            this);
        ArgumentException.ThrowIfNullOrWhiteSpace(portName);
        SerialHub hub;
        lock (_sync) {
            if (!_hubs.TryGetValue(portName, out hub!)) {
                throw new InvalidOperationException(
                    $"Serial port '{portName}' is not open.");
            }
        }

        return hub.OpenSessionAsync();
    }

    public bool TryGetHistory(
        string portName,
        int maximumBytes,
        out ReadOnlyMemory<byte> history) {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _disposed) != 0,
            this);
        ArgumentException.ThrowIfNullOrWhiteSpace(portName);
        if (maximumBytes <= 0) {
            throw new ArgumentOutOfRangeException(
                nameof(maximumBytes));
        }

        lock (_sync) {
            if (_hubs.TryGetValue(portName, out var hub)) {
                history = hub.GetHistorySnapshot(maximumBytes);
                return true;
            }
        }

        history = ReadOnlyMemory<byte>.Empty;
        return false;
    }

    public async ValueTask<bool> CloseAsync(string portName) {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _disposed) != 0,
            this);
        ArgumentException.ThrowIfNullOrWhiteSpace(portName);
        SerialHub? hub;
        lock (_sync) {
            if (!_hubs.Remove(portName, out hub)) {
                return false;
            }
        }

        await hub.DisposeAsync().ConfigureAwait(false);
        _loggerFactory.CreateLogger<SerialHubManager>().LogInformation(
            "Serial port {PortName} closed",
            portName);
        return true;
    }

    public async ValueTask DisposeAsync() {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) {
            return;
        }

        SerialHub[] hubs;
        lock (_sync) {
            hubs = [.. _hubs.Values];
            _hubs.Clear();
        }

        foreach (var hub in hubs) {
            await hub.DisposeAsync().ConfigureAwait(false);
        }
    }

    private SerialHub GetHub(
        SerialPortOptions options,
        int historyBytes) {
        lock (_sync) {
            if (_hubs.TryGetValue(
                    options.PortName,
                    out var existing)) {
                EnsureCompatible(existing, options);
                return existing;
            }

            var created = new SerialHub(
                options,
                historyBytes,
                _loggerFactory.CreateLogger<SerialHub>());
            _hubs.Add(options.PortName, created);
            return created;
        }
    }

    private static void EnsureCompatible(
        SerialHub hub,
        SerialPortOptions options) {
        if (hub.Options != options) {
            throw new InvalidOperationException(
                $"Serial port '{options.PortName}' is already open " +
                "with different settings.");
        }
    }
}
