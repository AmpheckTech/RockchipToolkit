using Ampheck.RockchipToolkit;

namespace Ampheck.RockchipToolkit.Cli.Inputs;

internal sealed class InputResolver(ToolkitTransferLimits limits) {
    private const int BufferSize = 128 * 1024;

    public async ValueTask<ResolvedInput?> ResolveOptionalAsync(
        string? location,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken
    ) {
        if (string.IsNullOrWhiteSpace(location)) {
            return null;
        }

        return await ResolveAsync(
                location,
                progress,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask<ResolvedInput> ResolveAsync(
        string location,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken
    ) {
        ArgumentException.ThrowIfNullOrWhiteSpace(location);
        if (!TryGetHttp(location, out var uri)) {
            return new ResolvedInput(
                Path.GetFullPath(location),
                temporary: false);
        }

        var path = Path.Combine(
            Path.GetTempPath(),
            $"ampheck-download-{Guid.NewGuid():N}.tmp");
        try {
            await using var remote = await RemoteFile.OpenAsync(
                    uri,
                    cancellationToken)
                .ConfigureAwait(false);
            if (remote.Length > limits.MaximumInputBytes) {
                throw new InvalidDataException(
                    $"Remote input exceeds the configured " +
                    $"{limits.MaximumInputBytes} byte limit.");
            }

            progress?.Report(new OperationProgress(
                ToolkitOperationStage.Downloading,
                Path.GetFileName(uri.AbsolutePath),
                Completed: 0,
                Total: remote.Length ?? 0));

            await using var destination = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var buffer = new byte[BufferSize];
            long total = 0;
            while (true) {
                var read = await remote.Content.ReadAsync(
                        buffer,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0) {
                    break;
                }

                if (read > limits.MaximumInputBytes - total) {
                    throw new InvalidDataException(
                        "Remote input exceeds the configured size limit.");
                }

                await destination.WriteAsync(
                        buffer.AsMemory(0, read),
                        cancellationToken)
                    .ConfigureAwait(false);
                total += read;
                if (remote.Length is > 0) {
                    progress?.Report(new OperationProgress(
                        ToolkitOperationStage.Downloading,
                        Path.GetFileName(uri.AbsolutePath),
                        total,
                        remote.Length.Value));
                }
            }

            await destination.FlushAsync(cancellationToken)
                .ConfigureAwait(false);
            return new ResolvedInput(path, temporary: true);
        } catch {
            TryDelete(path);
            throw;
        }
    }

    private static bool TryGetHttp(
        string location,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Uri? uri
    ) {
        if (Uri.TryCreate(location, UriKind.Absolute, out var candidate) &&
            candidate.Scheme is "http" or "https") {
            uri = candidate;
            return true;
        }

        if (location.StartsWith(
                "http://",
                StringComparison.OrdinalIgnoreCase) ||
            location.StartsWith(
                "https://",
                StringComparison.OrdinalIgnoreCase)) {
            throw new ArgumentException("Invalid HTTP or HTTPS URL.");
        }

        uri = null;
        return false;
    }

    private static void TryDelete(string path) {
        try {
            File.Delete(path);
        } catch (Exception error) when (
            error is IOException or UnauthorizedAccessException) {
        }
    }
}
