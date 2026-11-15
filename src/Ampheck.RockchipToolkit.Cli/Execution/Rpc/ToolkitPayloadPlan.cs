using System.IO.Compression;
using Ampheck.RockchipToolkit.Cli.Commands;
using Ampheck.RockchipToolkit.Cli.Infrastructure;
using Ampheck.RockchipToolkit.Cli.Presentation;
using Ampheck.RockchipToolkit.Images;
using Ampheck.RockchipToolkit.Rpc.JsonRpc;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Cli.Execution.Rpc;

internal sealed class ToolkitPayloadPlan : IAsyncDisposable {
    private readonly ToolkitRpcClient _client;
    private readonly ClientInputFetcher _fetcher;
    private readonly ConsoleRenderer _console;
    private readonly List<string> _temporaryFiles = [];
    private readonly List<string> _blobIds = [];

    public ToolkitPayloadPlan(
        ToolkitRpcClient client,
        ILogger logger,
        ConsoleRenderer console) {
        _client = client;
        _console = console;
        _fetcher = new ClientInputFetcher(logger, console);
    }

    public async ValueTask<PayloadReference> PrepareFirmwareAsync(
        string location,
        InputFetchLocation fetchLocation,
        CancellationToken cancellationToken) {
        if (IsHttpLocation(location)) {
            if (fetchLocation != InputFetchLocation.Client) {
                return new PayloadReference(
                    BlobId: null,
                    Url: location,
                    ServerPath: null);
            }

            var downloaded = await FetchAsync(
                    location,
                    cancellationToken)
                .ConfigureAwait(false);
            if (_client.UseServerPaths) {
                return new PayloadReference(
                    BlobId: null,
                    Url: null,
                    ServerPath: Path.GetFullPath(downloaded));
            }

            return await UploadAsync(
                    downloaded,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (_client.UseServerPaths) {
            if (!Directory.Exists(location) &&
                !File.Exists(location)) {
                throw new FileNotFoundException(
                    $"Firmware source '{location}' was not found.",
                    location);
            }

            return new PayloadReference(
                BlobId: null,
                Url: null,
                ServerPath: Path.GetFullPath(location));
        }

        if (Directory.Exists(location)) {
            var zip = await CreateFirmwareZipAsync(
                    location,
                    cancellationToken)
                .ConfigureAwait(false);
            return await UploadAsync(zip, cancellationToken)
                .ConfigureAwait(false);
        }

        if (!File.Exists(location)) {
            throw new FileNotFoundException(
                $"Firmware source '{location}' was not found.",
                location);
        }

        return await UploadAsync(location, cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask<PayloadReference> PrepareFileAsync(
        string location,
        InputFetchLocation fetchLocation,
        CancellationToken cancellationToken) {
        if (IsHttpLocation(location)) {
            if (fetchLocation != InputFetchLocation.Client) {
                return new PayloadReference(
                    BlobId: null,
                    Url: location,
                    ServerPath: null);
            }

            var downloaded = await FetchAsync(
                    location,
                    cancellationToken)
                .ConfigureAwait(false);
            if (_client.UseServerPaths) {
                return new PayloadReference(
                    BlobId: null,
                    Url: null,
                    ServerPath: Path.GetFullPath(downloaded));
            }

            return await UploadAsync(
                    downloaded,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (_client.UseServerPaths) {
            if (!File.Exists(location)) {
                throw new FileNotFoundException(
                    $"Input file '{location}' was not found.",
                    location);
            }

            return new PayloadReference(
                BlobId: null,
                Url: null,
                ServerPath: Path.GetFullPath(location));
        }

        if (!File.Exists(location)) {
            throw new FileNotFoundException(
                $"Input file '{location}' was not found.",
                location);
        }

        return await UploadAsync(location, cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync() {
        foreach (var blobId in _blobIds) {
            await _client.DeleteBlobAsync(blobId)
                .ConfigureAwait(false);
        }

        foreach (var path in _temporaryFiles) {
            TryDelete(path);
        }
    }

    private async ValueTask<PayloadReference> UploadAsync(
        string path,
        CancellationToken cancellationToken) {
        var length = new FileInfo(path).Length;
        using var progress = _console.IsInteractive &&
            length > 0
                ? new ProgressBar(
                    _console,
                    Path.GetFileName(path),
                    length)
                : null;
        var blob = await _client.UploadBlobAsync(
                path,
                progress,
                cancellationToken)
            .ConfigureAwait(false);
        _blobIds.Add(blob.BlobId);
        return new PayloadReference(
            blob.BlobId,
            Url: null,
            ServerPath: null);
    }

    private async ValueTask<string> FetchAsync(
        string location,
        CancellationToken cancellationToken) {
        var path = await _fetcher.DownloadAsync(
                location,
                cancellationToken)
            .ConfigureAwait(false);
        _temporaryFiles.Add(path);
        return path;
    }

    private async ValueTask<string> CreateFirmwareZipAsync(
        string directory,
        CancellationToken cancellationToken) {
        var root = Path.GetFullPath(directory);
        var parameterPath = Path.Combine(root, "parameter.txt");
        if (!File.Exists(parameterPath)) {
            throw new InvalidDataException(
                $"Firmware directory '{directory}' contains no parameter.txt.");
        }

        var parameter = await File.ReadAllTextAsync(
                parameterPath,
                cancellationToken)
            .ConfigureAwait(false);
        var partitions = RockchipParameterParser.Parse(parameter);
        var files = new List<string> { parameterPath };
        AddIfExists(files, Path.Combine(root, "package-file"));
        foreach (var partition in partitions) {
            AddIfExists(
                files,
                Path.Combine(root, $"{partition.Name}.img"));
        }

        var zipPath = Path.Combine(
            Path.GetTempPath(),
            $"arkt-firmware-{Guid.NewGuid():N}.zip");
        _temporaryFiles.Add(zipPath);
        using var archive = ZipFile.Open(
            zipPath,
            ZipArchiveMode.Create);
        foreach (var path in files) {
            var entry = archive.CreateEntry(
                Path.GetFileName(path),
                CompressionLevel.Fastest);
            await using var source = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                128 * 1024,
                FileOptions.Asynchronous |
                FileOptions.SequentialScan);
            await using var destination = entry.Open();
            await source.CopyToAsync(
                    destination,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return zipPath;
    }

    private static void AddIfExists(
        ICollection<string> files,
        string path) {
        if (File.Exists(path)) {
            files.Add(path);
        }
    }

    private static bool IsHttpLocation(string location) =>
        Uri.TryCreate(
            location,
            UriKind.Absolute,
            out var uri) &&
        uri.Scheme is "http" or "https";

    private static void TryDelete(string path) {
        try {
            File.Delete(path);
        } catch (Exception error) when (
            error is IOException or UnauthorizedAccessException) {
        }
    }
}

internal sealed record PayloadReference(
    string? BlobId,
    string? Url,
    string? ServerPath);
