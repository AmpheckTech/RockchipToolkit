namespace Ampheck.Adb.Server;

internal static class AdbServerStreamBridge {
    public static async Task RunAsync(
        Stream client,
        Stream service,
        CancellationToken cancellationToken) {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        var upload = service.CopyToAsync(
            client,
            stop.Token);
        var download = client.CopyToAsync(
            service,
            stop.Token);
        await Task.WhenAny(upload, download).ConfigureAwait(false);
        await stop.CancelAsync().ConfigureAwait(false);
        try {
            await Task.WhenAll(upload, download).ConfigureAwait(false);
        } catch (Exception error) when (
            error is OperationCanceledException or
            IOException or
            ObjectDisposedException) {
        }
    }
}
