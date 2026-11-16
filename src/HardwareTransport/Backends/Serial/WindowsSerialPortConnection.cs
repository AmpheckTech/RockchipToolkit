#if WINDOWS
using IoPorts = System.IO.Ports;
using Ampheck.RockchipToolkit.HardwareTransport.Ports.Serial;

namespace Ampheck.RockchipToolkit.HardwareTransport.Backends.Serial;

internal sealed class WindowsSerialPortConnection(
    SerialPortOptions options
) : ISerialPortConnection {
    private IoPorts.SerialPort? _port;

    public void Open() {
        var port = new IoPorts.SerialPort(
            options.PortName,
            options.BaudRate,
            ToParity(options.Parity),
            options.DataBits,
            ToStopBits(options.StopBits)) {
            Handshake = ToHandshake(options.Handshake),
            DtrEnable = options.DtrEnable,
            RtsEnable = options.RtsEnable,
            ReadTimeout = 100,
            WriteTimeout = 5000
        };
        try {
            port.Open();
            _port = port;
        } catch {
            port.Dispose();
            throw;
        }
    }

    public int Read(
        byte[] buffer,
        int offset,
        int count
    ) =>
        RequirePort().Read(
            buffer,
            offset,
            count);

    public void Write(ReadOnlyMemory<byte> data) {
        var bytes = data.ToArray();
        RequirePort().Write(
            bytes,
            0,
            bytes.Length);
    }

    public void Dispose() {
        Interlocked.Exchange(
            ref _port,
            null)?.Dispose();
    }

    private IoPorts.SerialPort RequirePort() =>
        _port ??
        throw new IOException(
            "The serial port is not open.");

    private static IoPorts.Parity ToParity(
        SerialParity parity
    ) =>
        parity switch {
            SerialParity.Odd => IoPorts.Parity.Odd,
            SerialParity.Even => IoPorts.Parity.Even,
            SerialParity.Mark => IoPorts.Parity.Mark,
            SerialParity.Space => IoPorts.Parity.Space,
            _ => IoPorts.Parity.None
        };

    private static IoPorts.StopBits ToStopBits(
        SerialStopBits stopBits
    ) =>
        stopBits switch {
            SerialStopBits.OnePointFive =>
                IoPorts.StopBits.OnePointFive,
            SerialStopBits.Two =>
                IoPorts.StopBits.Two,
            _ => IoPorts.StopBits.One
        };

    private static IoPorts.Handshake ToHandshake(
        SerialHandshake handshake
    ) =>
        handshake switch {
            SerialHandshake.XOnXOff =>
                IoPorts.Handshake.XOnXOff,
            SerialHandshake.RequestToSend =>
                IoPorts.Handshake.RequestToSend,
            SerialHandshake.RequestToSendXOnXOff =>
                IoPorts.Handshake.RequestToSendXOnXOff,
            _ => IoPorts.Handshake.None
        };
}
#endif
