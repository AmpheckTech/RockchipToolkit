using Ampheck.RockchipToolkit.Rpc;
using Ampheck.RockchipToolkit.Rpc.JsonRpc;
using Ampheck.RockchipToolkit.Transport.Serial;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Server;

public sealed class ToolkitSerialService(
    ILoggerFactory loggerFactory,
    int historyBytes = 256 * 1024) : IAsyncDisposable {

    private readonly SerialHubManager _manager =
        new(loggerFactory);

    public IReadOnlyList<SerialDeviceInfo> ListDevices() =>
        _manager.ListDevices();

    public async Task OpenPortAsync(
        SerialOpenParams request) {
        await using var session = await _manager.OpenSessionAsync(
                ToOptions(request),
                historyBytes)
            .ConfigureAwait(false);
        await session.WaitForConnectionAsync(
                TimeSpan.FromSeconds(5))
            .ConfigureAwait(false);
    }

    public ValueTask<SerialSession> AttachAsync(
        string? portName) {
        if (string.IsNullOrWhiteSpace(portName)) {
            throw new InvalidDataException(
                "The request contains no serial port name.");
        }

        return _manager.OpenExistingSessionAsync(
            portName);
    }

    public byte[] GetHistory(SerialHistoryParams request) {
        var maximum = request.MaximumBytes > 0
            ? request.MaximumBytes
            : historyBytes;
        if (maximum <= 0) {
            throw new InvalidDataException(
                "The serial history size must be positive.");
        }

        if (string.IsNullOrWhiteSpace(request.PortName) ||
            !_manager.TryGetHistory(
                request.PortName,
                maximum,
                out var history)) {
            throw new InvalidOperationException(
                $"Serial port '{request.PortName}' is not open.");
        }

        return history.ToArray();
    }

    public void ClearHistory(SerialPortParams request) {
        if (string.IsNullOrWhiteSpace(request.PortName)) {
            throw new InvalidDataException(
                "The request contains no serial port name.");
        }

        if (!_manager.TryClearHistory(request.PortName)) {
            throw new InvalidOperationException(
                $"Serial port '{request.PortName}' is not open.");
        }
    }

    public void EnsureOpen(string portName) {
        if (string.IsNullOrWhiteSpace(portName) ||
            !_manager.TryGetHistory(
                portName,
                1,
                out _)) {
            throw new InvalidOperationException(
                $"Serial port '{portName}' is not open.");
        }
    }

    public ValueTask<bool> CloseAsync(
        string? portName) {
        if (string.IsNullOrWhiteSpace(portName)) {
            throw new InvalidDataException(
                "The request contains no serial port name.");
        }

        return _manager.CloseAsync(portName);
    }

    public ValueTask DisposeAsync() =>
        _manager.DisposeAsync();

    private static SerialPortOptions ToOptions(
        SerialOpenParams request) {
        if (string.IsNullOrWhiteSpace(request.PortName)) {
            throw new InvalidDataException(
                "The request contains no serial port name.");
        }

        var baudRate = request.Settings.BaudRate > 0
            ? request.Settings.BaudRate
            : SerialPortDefaults.BaudRate;
        return new SerialPortOptions {
            PortName = request.PortName,
            BaudRate = baudRate,
            DataBits = request.Settings.DataBits,
            Parity = request.Settings.Parity switch {
                ToolkitSerialParity.Odd =>
                    SerialParity.Odd,
                ToolkitSerialParity.Even =>
                    SerialParity.Even,
                ToolkitSerialParity.Mark =>
                    SerialParity.Mark,
                ToolkitSerialParity.Space =>
                    SerialParity.Space,
                _ => SerialParity.None
            },
            StopBits = request.Settings.StopBits switch {
                ToolkitSerialStopBits.OnePointFive =>
                    SerialStopBits.OnePointFive,
                ToolkitSerialStopBits.Two =>
                    SerialStopBits.Two,
                _ => SerialStopBits.One
            },
            Handshake = request.Settings.Handshake switch {
                ToolkitSerialHandshake.XOnXOff =>
                    SerialHandshake.XOnXOff,
                ToolkitSerialHandshake.RequestToSend =>
                    SerialHandshake.RequestToSend,
                ToolkitSerialHandshake.RequestToSendXOnXOff =>
                    SerialHandshake.RequestToSendXOnXOff,
                _ => SerialHandshake.None
            },
            DtrEnable = request.Settings.DtrEnable,
            RtsEnable = request.Settings.RtsEnable
        };
    }
}
