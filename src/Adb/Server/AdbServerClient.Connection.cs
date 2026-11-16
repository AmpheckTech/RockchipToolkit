using System.Net.Sockets;
using System.Text;

namespace Ampheck.RockchipToolkit.Adb.Server;

public static partial class AdbServerClient {
    private static readonly AdbServerOptions DefaultOptions = new();

    private static async Task<AdbServerConnection> ConnectAsync(
        CancellationToken cancellationToken,
        AdbServerOptions options
    ) {
        if (string.IsNullOrWhiteSpace(options.Host) ||
            options.Port is <= 0 or > ushort.MaxValue ||
            options.ConnectTimeout <= TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "The ADB server options are invalid.");
        }

        var client = new TcpClient();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        timeout.CancelAfter(options.ConnectTimeout);
        try {
            await client.ConnectAsync(
                    options.Host,
                    options.Port,
                    timeout.Token)
                .ConfigureAwait(false);
            client.NoDelay = true;
            return new AdbServerConnection(
                client,
                client.GetStream());
        } catch {
            client.Dispose();
            throw;
        }
    }

    private static async Task SelectTransportAsync(
        NetworkStream stream,
        string serialNumber,
        CancellationToken cancellationToken
    ) {
        await SendRequestAsync(
                stream,
                $"host:transport:{serialNumber}",
                cancellationToken)
            .ConfigureAwait(false);
        await ReadStatusAsync(stream, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task SendRequestAsync(
        NetworkStream stream,
        string service,
        CancellationToken cancellationToken
    ) {
        var payload = Encoding.UTF8.GetBytes(service);
        var prefix = Encoding.ASCII.GetBytes(
            payload.Length.ToString("x4"));
        await stream.WriteAsync(prefix, cancellationToken)
            .ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task ReadStatusAsync(
        NetworkStream stream,
        CancellationToken cancellationToken
    ) {
        var status = new byte[4];
        await ReadExactlyAsync(stream, status, cancellationToken)
            .ConfigureAwait(false);
        var text = Encoding.ASCII.GetString(status);
        if (text == "OKAY") {
            return;
        }

        if (text != "FAIL") {
            throw new IOException(
                $"Unexpected ADB server status '{text}'.");
        }

        var message = await ReadLengthPrefixedPayloadAsync(
                stream,
                cancellationToken)
            .ConfigureAwait(false);
        throw new IOException(
            string.IsNullOrWhiteSpace(message)
                ? "The ADB server rejected the request."
                : message);
    }

    private static async Task<string> ReadLengthPrefixedPayloadAsync(
        NetworkStream stream,
        CancellationToken cancellationToken
    ) {
        var lengthBytes = new byte[4];
        await ReadExactlyAsync(
                stream,
                lengthBytes,
                cancellationToken)
            .ConfigureAwait(false);
        var lengthText = Encoding.ASCII.GetString(lengthBytes);
        if (!int.TryParse(
                lengthText,
                System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture,
                out var length) ||
            length < 0) {
            throw new IOException(
                "The ADB server returned an invalid payload length.");
        }

        var payload = new byte[length];
        await ReadExactlyAsync(stream, payload, cancellationToken)
            .ConfigureAwait(false);
        return Encoding.UTF8.GetString(payload);
    }

    private static async Task ReadExactlyAsync(
        NetworkStream stream,
        Memory<byte> buffer,
        CancellationToken cancellationToken
    ) {
        var offset = 0;
        while (offset < buffer.Length) {
            var read = await stream.ReadAsync(
                    buffer[offset..],
                    cancellationToken)
                .ConfigureAwait(false);
            if (read == 0) {
                throw new EndOfStreamException(
                    "The ADB server closed the connection.");
            }

            offset += read;
        }
    }
}
