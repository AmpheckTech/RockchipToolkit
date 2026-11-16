namespace Ampheck.RockchipToolkit.Adb.Server;

public static partial class AdbServerClient {
    public static async Task RebootAsync(
        string serialNumber,
        string mode,
        CancellationToken cancellationToken = default,
        AdbServerOptions? options = null
    ) {
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
}
