using System.Net.Sockets;
using System.Text;
using Ampheck.Adb.Connection;

namespace Ampheck.Adb.Server;

public static class AdbServerClient {
    private static readonly AdbServerOptions DefaultOptions = new();

    public static async ValueTask<bool> IsAvailableAsync(
        CancellationToken cancellationToken = default,
        AdbServerOptions? options = null) {
        var serverOptions = options ?? DefaultOptions;
        using var timeout =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
        timeout.CancelAfter(serverOptions.ConnectTimeout);
        try {
            using var connection = await ConnectAsync(
                    timeout.Token,
                    serverOptions)
                .ConfigureAwait(false);
            await SendRequestAsync(
                    connection.Stream,
                    "host:version",
                    timeout.Token)
                .ConfigureAwait(false);
            await ReadStatusAsync(
                    connection.Stream,
                    timeout.Token)
                .ConfigureAwait(false);
            await ReadLengthPrefixedPayloadAsync(
                    connection.Stream,
                    timeout.Token)
                .ConfigureAwait(false);
            return true;
        } catch (OperationCanceledException) when (
            cancellationToken.IsCancellationRequested) {
            throw;
        } catch (Exception error) when (
            error is IOException or
            SocketException or
            OperationCanceledException) {
            return false;
        }
    }

    public static async Task<IReadOnlyList<AdbServerDevice>>
        ListDevicesAsync(
            CancellationToken cancellationToken = default,
            AdbServerOptions? options = null) {
        using var connection = await ConnectAsync(
                cancellationToken,
                options ?? DefaultOptions)
            .ConfigureAwait(false);
        await SendRequestAsync(
                connection.Stream,
                "host:devices-l",
                cancellationToken)
            .ConfigureAwait(false);
        await ReadStatusAsync(
                connection.Stream,
                cancellationToken)
            .ConfigureAwait(false);
        var payload = await ReadLengthPrefixedPayloadAsync(
                connection.Stream,
                cancellationToken)
            .ConfigureAwait(false);
        return ParseDevices(payload);
    }

    public static async ValueTask<IAdbShellStream> OpenShellAsync(
        string serialNumber,
        string? command,
        CancellationToken cancellationToken = default,
        AdbServerOptions? options = null) {
        if (command is not null) {
            var commandStream = await OpenServiceAsync(
                    serialNumber,
                    $"shell:{command}",
                    cancellationToken,
                    options ?? DefaultOptions)
                .ConfigureAwait(false);
            return new AdbServerShell(commandStream);
        }

        try {
            var terminalStream = await OpenServiceAsync(
                    serialNumber,
                    "shell,v2,TERM=xterm-256color,pty:",
                    cancellationToken,
                    options ?? DefaultOptions)
                .ConfigureAwait(false);
            return new AdbShellProtocolSession(terminalStream);
        } catch (IOException) {
            var legacyStream = await OpenServiceAsync(
                    serialNumber,
                    "shell:",
                    cancellationToken,
                    options ?? DefaultOptions)
                .ConfigureAwait(false);
            return new AdbServerShell(legacyStream);
        }
    }

    public static Task<AdbServerServiceStream> OpenSyncAsync(
        string serialNumber,
        CancellationToken cancellationToken = default,
        AdbServerOptions? options = null) {
        return OpenServiceAsync(
            serialNumber,
            "sync:",
            cancellationToken,
            options ?? DefaultOptions);
    }

    public static async Task RebootBootloaderAsync(
        string serialNumber,
        CancellationToken cancellationToken = default,
        AdbServerOptions? options = null) {
        await RebootAsync(
                serialNumber,
                "bootloader",
                cancellationToken,
                options)
            .ConfigureAwait(false);
    }

    public static async Task RebootAsync(
        string serialNumber,
        string mode,
        CancellationToken cancellationToken = default,
        AdbServerOptions? options = null) {
        using var connection = await ConnectAsync(
                cancellationToken,
                options ?? DefaultOptions)
            .ConfigureAwait(false);
        await SelectTransportAsync(
                connection.Stream,
                serialNumber,
                cancellationToken)
            .ConfigureAwait(false);
        await SendRequestAsync(
                connection.Stream,
                $"reboot:{mode}",
                cancellationToken)
            .ConfigureAwait(false);
        await ReadStatusAsync(
                connection.Stream,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public static async Task<string> ExecuteHostAsync(
        string service,
        CancellationToken cancellationToken = default,
        AdbServerOptions? options = null) {
        using var connection = await ConnectAsync(
                cancellationToken,
                options ?? DefaultOptions)
            .ConfigureAwait(false);
        await SendRequestAsync(
                connection.Stream,
                service,
                cancellationToken)
            .ConfigureAwait(false);
        await ReadStatusAsync(
                connection.Stream,
                cancellationToken)
            .ConfigureAwait(false);
        return await ReadLengthPrefixedPayloadAsync(
                connection.Stream,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public static async Task<string> ExecuteForwardAsync(
        string service,
        CancellationToken cancellationToken = default,
        AdbServerOptions? options = null) {
        using var connection = await ConnectAsync(
                cancellationToken,
                options ?? DefaultOptions)
            .ConfigureAwait(false);
        await SendRequestAsync(
                connection.Stream,
                service,
                cancellationToken)
            .ConfigureAwait(false);
        await ReadStatusAsync(
                connection.Stream,
                cancellationToken)
            .ConfigureAwait(false);
        await ReadStatusAsync(
                connection.Stream,
                cancellationToken)
            .ConfigureAwait(false);
        return await ReadLengthPrefixedPayloadAsync(
                connection.Stream,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<AdbServerConnection> ConnectAsync(
        CancellationToken cancellationToken,
        AdbServerOptions options) {
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
        CancellationToken cancellationToken) {
        await SendRequestAsync(
                stream,
                $"host:transport:{serialNumber}",
                cancellationToken)
            .ConfigureAwait(false);
        await ReadStatusAsync(stream, cancellationToken)
            .ConfigureAwait(false);
    }

    public static async Task KillAsync(
        CancellationToken cancellationToken = default,
        AdbServerOptions? options = null) {
        using var connection = await ConnectAsync(
                cancellationToken,
                options ?? DefaultOptions)
            .ConfigureAwait(false);
        await SendRequestAsync(
                connection.Stream,
                "host:kill",
                cancellationToken)
            .ConfigureAwait(false);
        await ReadStatusAsync(
                connection.Stream,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public static async Task<AdbServerServiceStream>
        OpenServiceAsync(
            string serialNumber,
            string service,
            CancellationToken cancellationToken,
            AdbServerOptions options) {
        var connection = await ConnectAsync(cancellationToken, options)
            .ConfigureAwait(false);
        try {
            await SelectTransportAsync(
                    connection.Stream,
                    serialNumber,
                    cancellationToken)
                .ConfigureAwait(false);
            await SendRequestAsync(
                    connection.Stream,
                    service,
                    cancellationToken)
                .ConfigureAwait(false);
            await ReadStatusAsync(
                    connection.Stream,
                    cancellationToken)
                .ConfigureAwait(false);
            return new AdbServerServiceStream(connection);
        } catch {
            connection.Dispose();
            throw;
        }
    }

    private static async Task SendRequestAsync(
        NetworkStream stream,
        string service,
        CancellationToken cancellationToken) {
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
        CancellationToken cancellationToken) {
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
        CancellationToken cancellationToken) {
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
        CancellationToken cancellationToken) {
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

    private static IReadOnlyList<AdbServerDevice> ParseDevices(
        string payload) {
        var devices = new List<AdbServerDevice>();
        foreach (var rawLine in payload.Split('\n')) {
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line)) {
                continue;
            }

            var fields = line.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 2) {
                continue;
            }

            string? product = null;
            string? model = null;
            string? device = null;
            foreach (var field in fields.Skip(2)) {
                var separator = field.IndexOf(':');
                if (separator <= 0) {
                    continue;
                }

                var key = field[..separator];
                var value = field[(separator + 1)..];
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

            devices.Add(new AdbServerDevice(
                fields[0],
                fields[1],
                product,
                model,
                device));
        }

        return devices.AsReadOnly();
    }
}
