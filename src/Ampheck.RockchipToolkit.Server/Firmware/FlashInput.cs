using Ampheck.RockchipToolkit.Images;
using Ampheck.RockchipToolkit.Server.IO;

namespace Ampheck.RockchipToolkit.Server.Firmware;

internal sealed class FlashInput : IAsyncDisposable {
    private readonly InputFile? _file;
    private readonly string? _temporaryDirectory;

    private FlashInput(
        FlashSourceKind sourceKind,
        string location,
        InputFile? file = null,
        string? temporaryDirectory = null) {
        SourceKind = sourceKind;
        Location = location;
        _file = file;
        _temporaryDirectory = temporaryDirectory;
    }

    public FlashSourceKind SourceKind { get; }
    public string Location { get; }

    public static async ValueTask<FlashInput> OpenAsync(
        string location,
        FirmwareFileContext context,
        CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(location);
        if (Directory.Exists(location)) {
            return new FlashInput(
                FlashSourceKind.FirmwareDirectory,
                Path.GetFullPath(location));
        }

        if (File.Exists(location)) {
            return IsArchivePath(location)
                ? await OpenArchiveAsync(
                        location,
                        context,
                        cancellationToken)
                    .ConfigureAwait(false)
                : await OpenUpdateImageAsync(
                        location,
                        context,
                        cancellationToken)
                    .ConfigureAwait(false);
        }

        if (!WebUri.TryGetHttp(location, out var uri)) {
            throw new FileNotFoundException(
                $"Firmware source '{location}' was not found.",
                location);
        }

        if (uri.AbsolutePath.EndsWith(
                "/",
                StringComparison.Ordinal)) {
            return new FlashInput(
                FlashSourceKind.FirmwareDirectory,
                location);
        }

        if (IsArchivePath(uri.AbsolutePath)) {
            return await OpenArchiveAsync(
                    location,
                    context,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (!IsUpdateImagePath(uri) &&
            await HasFirmwareParameterAsync(
                    location,
                    context,
                    cancellationToken)
                .ConfigureAwait(false)) {
            return new FlashInput(
                FlashSourceKind.FirmwareDirectory,
                location);
        }

        return await OpenUpdateImageAsync(
                location,
                context,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public ValueTask DisposeAsync() {
        var fileDispose = _file?.DisposeAsync() ??
            ValueTask.CompletedTask;
        return fileDispose.IsCompletedSuccessfully
            ? DisposeDirectory()
            : DisposeCoreAsync(fileDispose);
    }

    private static bool IsUpdateImagePath(Uri uri) {
        return uri.AbsolutePath.EndsWith(
            ".img",
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsArchivePath(string path) {
        return path.EndsWith(
            ".zip",
            StringComparison.OrdinalIgnoreCase);
    }

    private static async ValueTask<FlashInput> OpenArchiveAsync(
        string location,
        FirmwareFileContext context,
        CancellationToken cancellationToken) {
        var file = await InputFile.OpenAsync(
                location,
                context,
                cancellationToken)
            .ConfigureAwait(false);
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"ampheck-firmware-{Guid.NewGuid():N}");
        try {
            Directory.CreateDirectory(directory);
            await FirmwareArchive.ExtractAsync(
                file.Path,
                directory,
                context.Limits,
                alreadyExpandedBytes: 0,
                cancellationToken)
                .ConfigureAwait(false);
            var firmwareDirectory =
                FirmwareArchive.FindRoot(directory);
            return new FlashInput(
                FlashSourceKind.FirmwareDirectory,
                firmwareDirectory,
                file,
                directory);
        } catch {
            await file.DisposeAsync().ConfigureAwait(false);
            TryDeleteDirectory(directory);
            throw;
        }
    }

    private static async ValueTask<FlashInput> OpenUpdateImageAsync(
        string location,
        FirmwareFileContext context,
        CancellationToken cancellationToken) {
        var file = await InputFile.OpenAsync(
                location,
                context,
                cancellationToken)
            .ConfigureAwait(false);
        try {
            using var update = RockchipUpdateImage.Open(file.Path);
            return new FlashInput(
                FlashSourceKind.UpdateImage,
                file.Path,
                file);
        } catch (InvalidDataException error) {
            await file.DisposeAsync().ConfigureAwait(false);
            throw new ArgumentException(
                $"'{location}' is neither a firmware directory nor a " +
                "Rockchip update image.",
                nameof(location),
                error);
        }
    }

    private static async Task<bool> HasFirmwareParameterAsync(
        string location,
        FirmwareFileContext context,
        CancellationToken cancellationToken) {
        var source = FirmwareSource.Create(location);
        InputFile? parameterFile = null;
        try {
            parameterFile = await source.TryOpenAsync(
                    "parameter.txt",
                    context,
                    cancellationToken)
                .ConfigureAwait(false);
            if (parameterFile is null) {
                return false;
            }

            var parameter = await File.ReadAllTextAsync(
                    parameterFile.Path,
                    cancellationToken)
                .ConfigureAwait(false);
            RockchipParameterParser.Parse(parameter);
            return true;
        } catch (Exception error) when (
            error is ArgumentException or InvalidDataException) {
            return false;
        } finally {
            if (parameterFile is not null) {
                await parameterFile.DisposeAsync()
                    .ConfigureAwait(false);
            }
        }
    }

    private ValueTask DisposeDirectory() {
        TryDeleteDirectory(_temporaryDirectory);
        return ValueTask.CompletedTask;
    }

    private async ValueTask DisposeCoreAsync(
        ValueTask fileDispose) {
        await fileDispose.ConfigureAwait(false);
        TryDeleteDirectory(_temporaryDirectory);
    }

    private static void TryDeleteDirectory(string? path) {
        if (path is null) {
            return;
        }

        try {
            if (Directory.Exists(path)) {
                Directory.Delete(path, recursive: true);
            }
        } catch (Exception error) when (
            error is IOException or UnauthorizedAccessException) {
        }
    }
}
