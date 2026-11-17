using Ampheck.RockchipToolkit;
using Ampheck.RockchipToolkit.Images.Rkaf;

namespace Ampheck.RockchipToolkit.Firmware.Internal;

internal sealed class FlashInput {
    private readonly InputFile? _file;
    private readonly string? _temporaryDirectory;

    private FlashInput(
        FlashSourceKind sourceKind,
        string location,
        InputFile? file = null,
        string? temporaryDirectory = null
    ) {
        SourceKind = sourceKind;
        Location = location;
        _file = file;
        _temporaryDirectory = temporaryDirectory;
    }

    public FlashSourceKind SourceKind { get; }
    public string Location { get; }

    public static async ValueTask<FlashInput> OpenAsync(
        string location,
        ToolkitTransferLimits limits,
        CancellationToken cancellationToken = default
    ) {
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
                        limits,
                        cancellationToken)
                    .ConfigureAwait(false)
                : await OpenUpdateImageAsync(
                        location,
                        cancellationToken)
                    .ConfigureAwait(false);
        }

        throw new FileNotFoundException(
            $"Firmware source '{location}' was not found.",
            location);
    }

    public ValueTask DisposeAsync() {
        var fileDispose = _file?.DisposeAsync() ??
            ValueTask.CompletedTask;
        return fileDispose.IsCompletedSuccessfully
            ? DisposeDirectory()
            : DisposeCoreAsync(fileDispose);
    }

    private static bool IsArchivePath(string path) {
        return path.EndsWith(
            ".zip",
            StringComparison.OrdinalIgnoreCase);
    }

    private static async ValueTask<FlashInput> OpenArchiveAsync(
        string location,
        ToolkitTransferLimits limits,
        CancellationToken cancellationToken
    ) {
        var file = await InputFile.OpenAsync(
                location,
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
                limits,
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
        CancellationToken cancellationToken
    ) {
        var file = await InputFile.OpenAsync(
                location,
                cancellationToken)
            .ConfigureAwait(false);
        try {
            if (!FirmwareImageValidator.IsUpdateImage(file.Path)) {
                throw new InvalidDataException(
                    "The file is not a Rockchip update image.");
            }

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

    private ValueTask DisposeDirectory() {
        TryDeleteDirectory(_temporaryDirectory);
        return ValueTask.CompletedTask;
    }

    private async ValueTask DisposeCoreAsync(
        ValueTask fileDispose
    ) {
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
