using Ampheck.RockchipToolkit.Cli.Commands;
using Ampheck.RockchipToolkit.Cli.Execution.Rpc;
using Ampheck.RockchipToolkit.Cli.Presentation;
using Ampheck.RockchipToolkit.Rpc;
using Ampheck.RockchipToolkit.Rpc.JsonRpc;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Cli.Execution;

internal sealed partial class AdbCommandRunner {
    public async Task<int> PushAsync(
        AdbTransferArguments options,
        CancellationToken cancellationToken = default) {
        if (!File.Exists(options.Source)) {
            throw new FileNotFoundException(
                $"Local file '{options.Source}' was not found.",
                options.Source);
        }

        if (Directory.Exists(options.Source)) {
            throw new NotSupportedException(
                "Directory push is not supported.");
        }

        await using var session = await CreateSessionAsync(
                cancellationToken)
            .ConfigureAwait(false);
        var length = new FileInfo(options.Source).Length;
        using var progress = context.Console.IsInteractive &&
            length > 0
                ? new ProgressBar(
                    context.Console,
                    Path.GetFileName(options.Source),
                    length)
                : null;
        BlobReference? blob = null;
        if (!session.Client.UseServerPaths) {
            context.Logger.LogInformation(
                "Uploading {LocalPath} for ADB push",
                options.Source);
            blob = await session.Client.UploadBlobAsync(
                    options.Source,
                    progress,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        try {
            await session.Client.PushAdbFileAsync(
                    new AdbPushParams {
                        SerialNumber = options.SerialNumber,
                        BlobId = blob?.BlobId,
                        ServerPath = session.Client.UseServerPaths
                            ? Path.GetFullPath(options.Source)
                            : null,
                        Destination = options.Destination
                    },
                    session.Report,
                    cancellationToken)
                .ConfigureAwait(false);
        } finally {
            if (blob is not null) {
                await session.Client.DeleteBlobAsync(blob.BlobId)
                    .ConfigureAwait(false);
            }
        }

        Console.WriteLine(
            $"pushed {options.Source} -> {options.Destination}");
        return 0;
    }

    public async Task<int> PullAsync(
        AdbTransferArguments options,
        CancellationToken cancellationToken = default) {
        await using var session = await CreateSessionAsync(
                cancellationToken)
            .ConfigureAwait(false);
        var destination = options.Destination;
        var file = await session.Client.PullAdbFileAsync(
                new AdbPullParams {
                    SerialNumber = options.SerialNumber,
                    Source = options.Source,
                    DestinationServerPath =
                        session.Client.UseServerPaths
                            ? Path.GetFullPath(destination)
                            : null
                },
                session.Report,
                cancellationToken)
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
                context.Logger.LogInformation(
                    "Downloading ADB pull result to {LocalPath}",
                    destination);
                await BlobOutput.WriteAsync(
                        session.Client,
                        file,
                        destination,
                        context.Console,
                        context.Logger,
                        cancellationToken)
                    .ConfigureAwait(false);
            } else {
                destination = file.Path ??
                    throw new InvalidDataException(
                        "The server returned no pull destination.");
            }
        } finally {
            if (file.BlobId is { } blobId) {
                await session.Client.DeleteBlobAsync(blobId)
                    .ConfigureAwait(false);
            }
        }

        Console.WriteLine(
            $"pulled {options.Source} -> {destination}");
        return 0;
    }
}
