using System.IO.Compression;
using Ampheck.RockchipToolkit.Cli.Commands;
using Ampheck.RockchipToolkit.Cli.Infrastructure;
using Ampheck.RockchipToolkit.Cli.Presentation;
using Ampheck.RockchipToolkit.Images;
using Ampheck.RockchipToolkit.Rpc;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Cli.Execution.Rpc;

internal sealed class ToolkitAttachmentPlan : IAsyncDisposable {
    private readonly Dictionary<ToolkitAttachmentRole, string> _paths = [];
    private readonly List<string> _temporaryFiles = [];
    private readonly ClientInputFetcher _fetcher;

    private ToolkitAttachmentPlan(
        ClientInputFetcher fetcher) {
        _fetcher = fetcher;
    }

    public ToolkitRequest Request { get; private set; } = null!;

    public static async ValueTask<ToolkitAttachmentPlan> CreateAsync(
        ToolkitRequest request,
        CommandOptions options,
        ILogger logger,
        ConsoleRenderer console,
        CancellationToken cancellationToken) {
        var plan = new ToolkitAttachmentPlan(
            new ClientInputFetcher(logger, console)) {
            Request = request
        };
        try {
            if (request.Operation == ToolkitOperation.Flash) {
                await plan.PrepareSourceAsync(
                        options,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            if (request.Operation == ToolkitOperation.Loader) {
                await plan.PrepareFileAsync(
                        ToolkitAttachmentRole.Loader,
                        options.File!,
                        options.FetchLocation,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            if (request.Operation == ToolkitOperation.Write) {
                await plan.PrepareFileAsync(
                        ToolkitAttachmentRole.Input,
                        options.File!,
                        options.FetchLocation,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            if (options.Loader is not null &&
                request.Operation != ToolkitOperation.Loader) {
                await plan.PrepareLoaderAsync(
                        options.Loader,
                        options.FetchLocation,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            return plan;
        } catch {
            await plan.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public ValueTask<Stream> OpenAsync(
        ToolkitAttachment attachment) {
        if (!_paths.TryGetValue(
                attachment.Role,
                out var path)) {
            throw new FileNotFoundException(
                $"No attachment was prepared for role " +
                $"{attachment.Role}.");
        }

        return ValueTask.FromResult<Stream>(
            new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                128 * 1024,
                FileOptions.Asynchronous |
                FileOptions.SequentialScan));
    }

    public ValueTask DisposeAsync() {
        foreach (var path in _temporaryFiles) {
            TryDelete(path);
        }

        _temporaryFiles.Clear();
        return ValueTask.CompletedTask;
    }

    private async ValueTask PrepareSourceAsync(
        CommandOptions options,
        CancellationToken cancellationToken) {
        var location = options.Source!;
        if (IsHttpLocation(location)) {
            if (options.FetchLocation !=
                InputFetchLocation.Client) {
                return;
            }

            var path = await FetchAsync(
                    location,
                    cancellationToken)
                .ConfigureAwait(false);
            Add(
                ToolkitAttachmentRole.Source,
                IsArchivePath(path)
                    ? ToolkitAttachmentKind.ZipDirectory
                    : ToolkitAttachmentKind.File,
                path);
            Request = Request with {
                SourceLocation = ToolkitInputKind.Attachment,
                Source = null
            };
            return;
        }

        await AddLocalSourceAsync(location, cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask PrepareFileAsync(
        ToolkitAttachmentRole role,
        string location,
        InputFetchLocation fetchLocation,
        CancellationToken cancellationToken) {
        if (IsHttpLocation(location) &&
            fetchLocation == InputFetchLocation.Client) {
            var path = await FetchAsync(
                    location,
                    cancellationToken)
                .ConfigureAwait(false);
            AddFile(role, path);
            SetFileLocation(
                role,
                ToolkitInputKind.Attachment);
            return;
        }

        if (IsHttpLocation(location)) {
            SetFileLocation(role, ToolkitInputKind.Url);
            return;
        }

        AddFile(role, location);
        SetFileLocation(role, ToolkitInputKind.Attachment);
    }

    private async ValueTask PrepareLoaderAsync(
        string location,
        InputFetchLocation fetchLocation,
        CancellationToken cancellationToken) {
        if (IsHttpLocation(location) &&
            fetchLocation == InputFetchLocation.Client) {
            var path = await FetchAsync(
                    location,
                    cancellationToken)
                .ConfigureAwait(false);
            AddFile(ToolkitAttachmentRole.Loader, path);
            Request = Request with {
                LoaderLocation = ToolkitInputKind.Attachment
            };
            return;
        }

        if (IsHttpLocation(location)) {
            Request = Request with {
                LoaderLocation = ToolkitInputKind.Url,
                Loader = location
            };
            return;
        }

        AddFile(ToolkitAttachmentRole.Loader, location);
        Request = Request with {
            LoaderLocation = ToolkitInputKind.Attachment
        };
    }

    private async ValueTask AddLocalSourceAsync(
        string location,
        CancellationToken cancellationToken) {
        if (Directory.Exists(location)) {
            var zip = Path.Combine(
                Path.GetTempPath(),
                $"ampheck-rpc-directory-{Guid.NewGuid():N}.zip");
            try {
                await CreateFirmwareZipAsync(
                        location,
                        zip,
                        cancellationToken)
                    .ConfigureAwait(false);
            } catch {
                TryDelete(zip);
                throw;
            }

            _temporaryFiles.Add(zip);
            Add(
                ToolkitAttachmentRole.Source,
                ToolkitAttachmentKind.ZipDirectory,
                zip);
        } else if (File.Exists(location)) {
            Add(
                ToolkitAttachmentRole.Source,
                IsArchivePath(location)
                    ? ToolkitAttachmentKind.ZipDirectory
                    : ToolkitAttachmentKind.File,
                location);
        } else {
            throw new FileNotFoundException(
                $"Firmware source '{location}' was not found.",
                location);
        }

        Request = Request with {
            SourceLocation = ToolkitInputKind.Attachment,
            Source = null
        };
    }

    private static async ValueTask CreateFirmwareZipAsync(
        string directory,
        string zipPath,
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

        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
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
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using var destination = entry.Open();
            await source.CopyToAsync(destination, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static void AddIfExists(
        List<string> files,
        string path) {
        if (File.Exists(path)) {
            files.Add(path);
        }
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

    private void SetFileLocation(
        ToolkitAttachmentRole role,
        ToolkitInputKind location) {
        Request = role == ToolkitAttachmentRole.Loader &&
            Request.Operation != ToolkitOperation.Loader
            ? Request with { LoaderLocation = location }
            : Request with { FileLocation = location };
    }

    private void AddFile(
        ToolkitAttachmentRole role,
        string location) {
        if (!File.Exists(location)) {
            throw new FileNotFoundException(
                $"Input file '{location}' was not found.",
                location);
        }

        Add(role, ToolkitAttachmentKind.File, location);
    }

    private void Add(
        ToolkitAttachmentRole role,
        ToolkitAttachmentKind kind,
        string path) {
        _paths.Add(role, path);
        Request = Request with {
            Attachments = [
                .. Request.Attachments,
                new ToolkitAttachment(
                    role,
                    kind,
                    Path.GetFileName(path),
                    GetFileLength(path))
            ]
        };
    }

    private static long GetFileLength(string path) {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        return stream.Length;
    }

    private static bool IsHttpLocation(string location) {
        return Uri.TryCreate(
                location,
                UriKind.Absolute,
                out var uri) &&
            uri.Scheme is "http" or "https";
    }

    private static bool IsArchivePath(string path) {
        return path.EndsWith(
            ".zip",
            StringComparison.OrdinalIgnoreCase);
    }

    private static void TryDelete(string path) {
        try {
            File.Delete(path);
        } catch (Exception error) when (
            error is IOException or UnauthorizedAccessException) {
        }
    }
}
