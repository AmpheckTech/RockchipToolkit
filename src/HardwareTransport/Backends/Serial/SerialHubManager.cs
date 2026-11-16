using System.Runtime.ExceptionServices;
using Ampheck.RockchipToolkit.HardwareTransport.Ports.Serial;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.HardwareTransport.Backends.Serial;

public sealed class SerialHubManager {
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

    public async ValueTask<ISerialSession> OpenSessionAsync(
        SerialPortOptions options
    ) {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _disposed) != 0,
            this);
        options.Validate();
        var hub = GetHub(options);

        return await hub.OpenSessionAsync().ConfigureAwait(false);
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

        Exception? failure = null;
        foreach (var hub in hubs) {
            try {
                await hub.DisposeAsync().ConfigureAwait(false);
            } catch (Exception error) {
                failure ??= error;
            }
        }

        if (failure is not null) {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private SerialHub GetHub(SerialPortOptions options) {
        lock (_sync) {
            if (_hubs.TryGetValue(
                    options.PortName,
                    out var existing)) {
                EnsureCompatible(existing, options);
                return existing;
            }

            var created = new SerialHub(
                options,
                _loggerFactory.CreateLogger<SerialHub>());
            _hubs.Add(options.PortName, created);
            return created;
        }
    }

    private static void EnsureCompatible(
        SerialHub hub,
        SerialPortOptions options
    ) {
        if (hub.Options != options) {
            throw new InvalidOperationException(
                $"Serial port '{options.PortName}' is already open " +
                "with different settings.");
        }
    }
}
