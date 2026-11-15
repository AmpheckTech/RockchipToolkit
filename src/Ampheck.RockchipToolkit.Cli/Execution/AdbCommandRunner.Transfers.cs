using Ampheck.RockchipToolkit.Cli.Commands;
using Ampheck.RockchipToolkit.Cli.Execution.Rpc;
using Ampheck.RockchipToolkit.Cli.Presentation;
using Ampheck.RockchipToolkit.Rpc.JsonRpc;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Cli.Execution;

internal static partial class AdbCommandRunner {
    private static async Task<int> PushAsync(
        ToolkitRpcClient client,
        CommandOptions options,
        ConsoleRenderer console,
        ILogger logger,
        Action<TaskEventNotification> report) {
        var source = RequireTransferPath(
            options.AdbSource,
            "local source");
        var destination = RequireTransferPath(
            options.AdbDestination,
            "remote destination");
        if (!File.Exists(source)) {
            throw new FileNotFoundException(
                $"Local file '{source}' was not found.",
                source);
        }

        if (Directory.Exists(source)) {
            throw new NotSupportedException(
                "Directory push is not supported.");
        }

        var length = new FileInfo(source).Length;
        using var progress = console.IsInteractive &&
            length > 0
                ? new ProgressBar(
                    console,
                    Path.GetFileName(source),
                    length)
                : null;
        BlobReference? blob = null;
        if (!client.UseServerPaths) {
            logger.LogInformation(
                "Uploading {LocalPath} for ADB push",
                source);
            blob = await client.UploadBlobAsync(
                    source,
                    progress)
                .ConfigureAwait(false);
        }

        try {
            await client.PushAdbFileAsync(
                    new AdbPushParams {
                        SerialNumber = options.AdbSerial,
                        BlobId = blob?.BlobId,
                        ServerPath = client.UseServerPaths
                            ? Path.GetFullPath(source)
                            : null,
                        Destination = destination
                    },
                    report)
                .ConfigureAwait(false);
        } finally {
            if (blob is not null) {
                await client.DeleteBlobAsync(blob.BlobId)
                    .ConfigureAwait(false);
            }
        }

        Console.WriteLine($"pushed {source} -> {destination}");
        return 0;
    }

    private static async Task<int> PullAsync(
        ToolkitRpcClient client,
        CommandOptions options,
        ConsoleRenderer console,
        ILogger logger,
        Action<TaskEventNotification> report) {
        var source = RequireTransferPath(
            options.AdbSource,
            "remote source");
        var destination = RequireTransferPath(
            options.AdbDestination,
            "local destination");
        var file = await client.PullAdbFileAsync(
                new AdbPullParams {
                    SerialNumber = options.AdbSerial,
                    Source = source,
                    DestinationServerPath = client.UseServerPaths
                        ? Path.GetFullPath(destination)
                        : null
                },
                report)
            .ConfigureAwait(false);
        try {
            if (file.BlobId is not null &&
                (Directory.Exists(destination) ||
                    Path.EndsInDirectorySeparator(destination))) {
                Directory.CreateDirectory(destination);
                destination = Path.Combine(
                    destination,
                    file.FileName);
            }

            if (file.BlobId is not null) {
                logger.LogInformation(
                    "Downloading ADB pull result to {LocalPath}",
                    destination);
                await BlobOutput.WriteAsync(
                        client,
                        file,
                        destination,
                        console,
                        logger)
                    .ConfigureAwait(false);
            } else {
                destination = file.Path ??
                    throw new InvalidDataException(
                        "The server returned no pull destination.");
            }
        } finally {
            if (file.BlobId is { } blobId) {
                await client.DeleteBlobAsync(blobId)
                    .ConfigureAwait(false);
            }
        }

        Console.WriteLine($"pulled {source} -> {destination}");
        return 0;
    }
}
