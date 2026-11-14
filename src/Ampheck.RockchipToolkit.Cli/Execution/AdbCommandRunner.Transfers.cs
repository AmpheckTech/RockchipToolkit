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
        logger.LogInformation(
            "Uploading {LocalPath} for ADB push",
            source);
        var blob = await client.UploadBlobAsync(
                source,
                progress)
            .ConfigureAwait(false);
        try {
            await client.RequestAsync<AdbPushParams, EmptyResult>(
                    JsonRpcMethods.AdbPush,
                    new AdbPushParams {
                        SerialNumber = options.AdbSerial,
                        BlobId = blob.BlobId,
                        Destination = destination
                    },
                    report)
                .ConfigureAwait(false);
        } finally {
            await client.DeleteBlobAsync(blob.BlobId)
                .ConfigureAwait(false);
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
        var blob = await client.RequestAsync<
            AdbPullParams,
            BlobReference>(
                JsonRpcMethods.AdbPull,
                new AdbPullParams {
                    SerialNumber = options.AdbSerial,
                    Source = source
                },
                report)
            .ConfigureAwait(false);
        try {
            if (Directory.Exists(destination) ||
                Path.EndsInDirectorySeparator(destination)) {
                Directory.CreateDirectory(destination);
                destination = Path.Combine(
                    destination,
                    blob.FileName);
            }

            logger.LogInformation(
                "Downloading ADB pull result to {LocalPath}",
                destination);
            await BlobOutput.WriteAsync(
                    client,
                    blob,
                    destination,
                    console,
                    logger)
                .ConfigureAwait(false);
        } finally {
            await client.DeleteBlobAsync(blob.BlobId)
                .ConfigureAwait(false);
        }

        Console.WriteLine($"pulled {source} -> {destination}");
        return 0;
    }
}
