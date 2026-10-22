using Ampheck.Adb.Connection;
using Ampheck.Adb.Device;
using Microsoft.Extensions.Logging;

namespace Ampheck.Adb.Server;

internal sealed class ManagedAdbDevice : IAsyncDisposable {
    private readonly AdbDeviceInfo _info;
    private readonly Func<CancellationToken, ValueTask<AdbConnection>> _open;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _lease = new(1, 1);
    private AdbConnection? _connection;
    private bool _disposed;

    public ManagedAdbDevice(
        AdbDeviceInfo info,
        IAdbDeviceProvider provider,
        ILogger logger,
        ulong transportId) {
        _info = info;
        _open = cancellationToken =>
            provider.OpenAsync(info, cancellationToken);
        _logger = logger;
        SerialNumber = GetSerialNumber(info);
        PlatformId = info.PlatformId;
        IsUsb = true;
        TransportId = transportId;
    }

    public ManagedAdbDevice(
        string host,
        int port,
        ILogger logger,
        ulong transportId) {
        _info = new AdbDeviceInfo(0, 0, $"{host}:{port}", null);
        _open = async cancellationToken => {
            var transport = await TcpAdbTransport.ConnectAsync(
                    host,
                    port,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return new AdbConnection(transport);
        };
        _logger = logger;
        SerialNumber = $"{host}:{port}";
        PlatformId = $"tcp:{SerialNumber}";
        IsUsb = false;
        TransportId = transportId;
    }

    public string SerialNumber { get; }
    public string? PlatformId { get; }
    public AdbDeviceInfo Info => _info;
    public bool IsUsb { get; }
    public ulong TransportId { get; }
    public bool IsConnected =>
        _connection?.IsOnline == true &&
        State == "device";
    public string State { get; private set; } = "offline";
    public string? Product { get; private set; }
    public string? Model { get; private set; }
    public string? DeviceName { get; private set; }

    public async ValueTask ProbeAsync(
        CancellationToken cancellationToken) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsConnected) {
            return;
        }

        await ResetConnectionAsync().ConfigureAwait(false);

        AdbConnection? connection = null;
        try {
            connection = await _open(cancellationToken)
                .ConfigureAwait(false);
            var state = await connection.ConnectAsync(
                    waitForAuthorization: false,
                    status: null,
                    cancellationToken)
                .ConfigureAwait(false);
            if (state == AdbConnectionState.Online) {
                _connection = connection;
                connection = null;
                State = "device";
                PopulateProperties(connection: _connection);
            } else {
                State = "unauthorized";
            }
        } catch (Exception error) when (
            error is IOException or
            InvalidOperationException or
            UnauthorizedAccessException or
            System.ComponentModel.Win32Exception) {
            State = "offline";
            _logger.LogDebug(
                error,
                "Unable to probe ADB device {SerialNumber}",
                SerialNumber);
        } finally {
            if (connection is not null) {
                await connection.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    public ValueTask ResetIfDisconnectedAsync() {
        return IsConnected
            ? ValueTask.CompletedTask
            : ResetConnectionAsync();
    }

    public async ValueTask<AdbServerDeviceLease> OpenStreamAsync(
        string service,
        CancellationToken cancellationToken) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _lease.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        try {
            await EnsureOnlineAsync(cancellationToken)
                .ConfigureAwait(false);
            var stream = await _connection!.OpenServiceAsync(
                    service,
                    cancellationToken)
                .ConfigureAwait(false);
            return new AdbServerDeviceLease(stream);
        } catch {
            await ResetConnectionAsync().ConfigureAwait(false);
            throw;
        } finally {
            _lease.Release();
        }
    }

    public async ValueTask DisposeAsync() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        try {
            await ResetConnectionAsync().ConfigureAwait(false);
        } finally {
            _lease.Dispose();
        }
    }

    private async ValueTask EnsureOnlineAsync(
        CancellationToken cancellationToken) {
        if (_connection is not null &&
            State == "device") {
            return;
        }

        await ResetConnectionAsync().ConfigureAwait(false);
        var connection = await _open(cancellationToken)
            .ConfigureAwait(false);
        try {
            var state = await connection.ConnectAsync(
                    waitForAuthorization: true,
                    status: null,
                    cancellationToken)
                .ConfigureAwait(false);
            if (state != AdbConnectionState.Online) {
                throw new InvalidOperationException(
                    $"ADB authorization for '{SerialNumber}' was not granted.");
            }

            _connection = connection;
            connection = null!;
            State = "device";
            PopulateProperties(connection: _connection);
        } finally {
            if (connection is not null) {
                await connection.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private async ValueTask ResetConnectionAsync() {
        State = "offline";
        if (_connection is null) {
            return;
        }

        var connection = _connection;
        _connection = null;
        await connection.DisposeAsync().ConfigureAwait(false);
    }

    private void PopulateProperties(AdbConnection connection) {
        var fields = ParseBanner(connection.Banner);
        Product = fields.Product;
        Model = fields.Model;
        DeviceName = fields.Device;
    }

    private static (string? Product, string? Model, string? Device)
        ParseBanner(string? banner) {
        string? product = null;
        string? model = null;
        string? device = null;
        if (string.IsNullOrWhiteSpace(banner)) {
            return (product, model, device);
        }

        var separator = banner.IndexOf(':');
        var properties = separator < 0
            ? banner
            : banner[(separator + 1)..];
        foreach (var field in properties.Split(
            ',',
            StringSplitOptions.RemoveEmptyEntries)) {
            var assignment = field.IndexOf('=');
            if (assignment <= 0) {
                continue;
            }

            var key = field[..assignment];
            var value = field[(assignment + 1)..];
            switch (key) {
                case "product":
                    product = value;
                    break;
                case "model":
                    model = value;
                    break;
                case "device":
                    device = value;
                    break;
            }
        }

        return (product, model, device);
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
}
