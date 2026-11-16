using Ampheck.RockchipToolkit.Adb.Client.Connection;

namespace Ampheck.RockchipToolkit.Adb.Server;

public static partial class AdbServerClient {
    public static async ValueTask<IAdbShellStream> OpenShellAsync(
        string serialNumber,
        string? command,
        CancellationToken cancellationToken = default,
        AdbServerOptions? options = null
    ) {
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
        AdbServerOptions? options = null
    ) {
        return OpenServiceAsync(
            serialNumber,
            "sync:",
            cancellationToken,
            options ?? DefaultOptions);
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
}
