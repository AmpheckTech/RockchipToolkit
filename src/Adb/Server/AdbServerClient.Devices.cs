using System.Net.Sockets;

namespace Ampheck.RockchipToolkit.Adb.Server;

public static partial class AdbServerClient {
    public static async ValueTask<bool> IsAvailableAsync(
        CancellationToken cancellationToken = default,
        AdbServerOptions? options = null
    ) {
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

    private static IReadOnlyList<AdbServerDevice> ParseDevices(
        string payload
    ) {
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
