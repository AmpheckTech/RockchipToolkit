#if WINDOWS
using Ports = System.IO.Ports;

namespace Ampheck.Serial;

internal sealed class WindowsSerialPortConnection(
    SerialPortOptions options) : ISerialPortConnection {
    private Ports.SerialPort? _port;

    public void Open() {
        var port = new Ports.SerialPort(
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
        int count) =>
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

    private Ports.SerialPort RequirePort() =>
        _port ??
        throw new IOException(
            "The serial port is not open.");

    private static Ports.Parity ToParity(
        SerialParity parity) =>
        parity switch {
            SerialParity.Odd => Ports.Parity.Odd,
            SerialParity.Even => Ports.Parity.Even,
            SerialParity.Mark => Ports.Parity.Mark,
            SerialParity.Space => Ports.Parity.Space,
            _ => Ports.Parity.None
        };

    private static Ports.StopBits ToStopBits(
        SerialStopBits stopBits) =>
        stopBits switch {
            SerialStopBits.OnePointFive =>
                Ports.StopBits.OnePointFive,
            SerialStopBits.Two =>
                Ports.StopBits.Two,
            _ => Ports.StopBits.One
        };

    private static Ports.Handshake ToHandshake(
        SerialHandshake handshake) =>
        handshake switch {
            SerialHandshake.XOnXOff =>
                Ports.Handshake.XOnXOff,
            SerialHandshake.RequestToSend =>
                Ports.Handshake.RequestToSend,
            SerialHandshake.RequestToSendXOnXOff =>
                Ports.Handshake.RequestToSendXOnXOff,
            _ => Ports.Handshake.None
        };
}
#endif
