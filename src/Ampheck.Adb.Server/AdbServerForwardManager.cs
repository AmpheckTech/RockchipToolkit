using Microsoft.Extensions.Logging;

namespace Ampheck.Adb.Server;

internal sealed class AdbServerForwardManager : IAsyncDisposable {
    private readonly AdbServerDeviceRegistry _devices;
    private readonly ILogger _logger;
    private readonly Dictionary<string, AdbServerForward> _forwards =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _disposed;

    public AdbServerForwardManager(
        AdbServerDeviceRegistry devices,
        ILogger logger) {
        _devices = devices;
        _logger = logger;
    }

    public async ValueTask<string> AddAsync(
        string serialNumber,
        string localSpec,
        string remoteSpec,
        CancellationToken cancellationToken) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var localPort = ParseTcpSpec(localSpec);
        if (!remoteSpec.StartsWith(
            "tcp:",
            StringComparison.OrdinalIgnoreCase)) {
            throw new NotSupportedException(
                "Only TCP remote forwards are supported.");
        }

        await _gate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        try {
            var device = await _devices.RequireAsync(
                    serialNumber,
                    cancellationToken)
                .ConfigureAwait(false);
            var forward = new AdbServerForward(
                device.SerialNumber,
                remoteSpec,
                localPort,
                _devices,
                _logger);
            try {
                forward.Start();
            } catch {
                await forward.DisposeAsync().ConfigureAwait(false);
                throw;
            }

            var key = GetKey(
                device.SerialNumber,
                forward.LocalPort);
            if (_forwards.ContainsKey(key)) {
                await forward.DisposeAsync().ConfigureAwait(false);
                throw new InvalidOperationException(
                    $"A forward already exists for {forward.LocalSpec}.");
            }

            _forwards[key] = forward;
            return forward.LocalSpec;
        } finally {
            _gate.Release();
        }
    }

    public IReadOnlyList<(string Serial, string Local, string Remote)>
        List() {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _forwards.Values
            .Select(forward => (
                forward.SerialNumber,
                forward.LocalSpec,
                forward.RemoteSpec))
            .ToArray();
    }

    public async ValueTask RemoveAsync(
        string? serialNumber,
        string localSpec,
        CancellationToken cancellationToken) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var localPort = ParseTcpSpec(localSpec);
        await _gate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        try {
            var serial = serialNumber ?? FindSerial(localPort);
            if (serial is null ||
                !_forwards.Remove(
                    GetKey(serial, localPort),
                    out var forward)) {
                return;
            }

            await forward.DisposeAsync().ConfigureAwait(false);
        } finally {
            _gate.Release();
        }
    }

    public async ValueTask RemoveAllAsync() {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _gate.WaitAsync().ConfigureAwait(false);
        try {
            var forwards = _forwards.Values.ToArray();
            _forwards.Clear();
            foreach (var forward in forwards) {
                await forward.DisposeAsync().ConfigureAwait(false);
            }
        } finally {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        await _gate.WaitAsync().ConfigureAwait(false);
        try {
            foreach (var forward in _forwards.Values) {
                await forward.DisposeAsync().ConfigureAwait(false);
            }

            _forwards.Clear();
        } finally {
            _gate.Release();
            _gate.Dispose();
        }
    }

    private string? FindSerial(int localPort) {
        return _forwards.Values
            .FirstOrDefault(forward =>
                forward.LocalPort == localPort)
            ?.SerialNumber;
    }

    private static int ParseTcpSpec(string spec) {
        var separator = spec.IndexOf(':');
        if (separator <= 0 ||
            !spec[..separator].Equals(
                "tcp",
                StringComparison.OrdinalIgnoreCase) ||
            !int.TryParse(
                spec[(separator + 1)..],
                out var port) ||
            port is < 0 or > ushort.MaxValue) {
            throw new NotSupportedException(
                $"Unsupported TCP forward specification '{spec}'.");
        }

        return port;
    }

    private static string GetKey(
        string serialNumber,
        int localPort) {
        return $"{serialNumber}|{localPort}";
    }
}
