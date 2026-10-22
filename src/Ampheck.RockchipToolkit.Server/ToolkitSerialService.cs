using Ampheck.RockchipToolkit.Rpc;
using Ampheck.Serial;
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
        ToolkitRequest request) {
        await using var session = await _manager.OpenSessionAsync(
                ToOptions(request),
                historyBytes)
            .ConfigureAwait(false);
        await session.WaitForConnectionAsync(
                TimeSpan.FromSeconds(5))
            .ConfigureAwait(false);
    }

    public ValueTask<SerialSession> AttachAsync(
        ToolkitRequest request) {
        if (string.IsNullOrWhiteSpace(request.SerialPort)) {
            throw new InvalidDataException(
                "The request contains no serial port name.");
        }

        return _manager.OpenExistingSessionAsync(
            request.SerialPort);
    }

    public byte[] GetHistory(ToolkitRequest request) {
        var maximum = request.SerialHistoryBytes ??
            historyBytes;
        if (maximum <= 0) {
            throw new InvalidDataException(
                "The serial history size must be positive.");
        }

        if (string.IsNullOrWhiteSpace(request.SerialPort) ||
            !_manager.TryGetHistory(
                request.SerialPort,
                maximum,
                out var history)) {
            throw new InvalidOperationException(
                $"Serial port '{request.SerialPort}' is not open.");
        }

        return history.ToArray();
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
        ToolkitRequest request) {
        if (string.IsNullOrWhiteSpace(request.SerialPort)) {
            throw new InvalidDataException(
                "The request contains no serial port name.");
        }

        return new SerialPortOptions {
            PortName = request.SerialPort,
            BaudRate = request.BaudRate ?? 1500000,
            DataBits = request.SerialDataBits ?? 8,
            Parity = request.SerialParity switch {
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
            StopBits = request.SerialStopBits switch {
                ToolkitSerialStopBits.OnePointFive =>
                    SerialStopBits.OnePointFive,
                ToolkitSerialStopBits.Two =>
                    SerialStopBits.Two,
                _ => SerialStopBits.One
            },
            Handshake = request.SerialHandshake switch {
                ToolkitSerialHandshake.XOnXOff =>
                    SerialHandshake.XOnXOff,
                ToolkitSerialHandshake.RequestToSend =>
                    SerialHandshake.RequestToSend,
                ToolkitSerialHandshake.RequestToSendXOnXOff =>
                    SerialHandshake.RequestToSendXOnXOff,
                _ => SerialHandshake.None
            },
            DtrEnable = request.SerialDtrEnable,
            RtsEnable = request.SerialRtsEnable
        };
    }
}
