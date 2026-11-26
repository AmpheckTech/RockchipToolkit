using Ampheck.RockchipToolkit;
using Ampheck.RockchipToolkit.Cli.Inputs;
using Ampheck.RockchipToolkit.Cli.Terminal;
using Ampheck.RockchipToolkit.Devices;
using Ampheck.RockchipToolkit.Firmware;
using Ampheck.RockchipToolkit.Hardware;
using Ampheck.RockchipToolkit.Protocol.RockUsb;
using Ampheck.RockchipToolkit.Storage;

namespace Ampheck.RockchipToolkit.Cli.Commands;

internal sealed partial class CliCommandTree {
    private async Task<int> RunEraseAsync(
        DeviceTarget target,
        string? loaderLocation,
        uint sector,
        ushort count,
        CancellationToken cancellationToken
    ) {
        using var progress = new OperationProgressDisplay(_console);
        await using var loader = await _inputs.ResolveOptionalAsync(
                loaderLocation,
                progress,
                cancellationToken)
            .ConfigureAwait(false);
        var storage = CreateStorageAccess();
        await storage.EraseAsync(
                target,
                loader?.Path,
                sector,
                count,
                progress,
                cancellationToken)
            .ConfigureAwait(false);
        progress.Complete();
        Console.WriteLine("Erase complete");
        return 0;
    }

    private async Task<int> RunFlashAsync(
        string source,
        string? loaderLocation,
        string? bootloaderLocation,
        string? idbLocation,
        DeviceTarget target,
        bool resetAfterFlash,
        CancellationToken cancellationToken
    ) {
        await FlashOnceAsync(
                source,
                loaderLocation,
                bootloaderLocation,
                idbLocation,
                target,
                resetAfterFlash,
                cancellationToken)
            .ConfigureAwait(false);
        return 0;
    }

    private async Task FlashOnceAsync(
        string source,
        string? loaderLocation,
        string? bootloaderLocation,
        string? idbLocation,
        DeviceTarget target,
        bool resetAfterFlash,
        CancellationToken cancellationToken
    ) {
        using var progress = new OperationProgressDisplay(_console);
        await using var resolvedSource = await _inputs.ResolveAsync(
                source,
                progress,
                cancellationToken)
            .ConfigureAwait(false);
        await using var resolvedLoader = await _inputs.ResolveOptionalAsync(
                loaderLocation,
                progress,
                cancellationToken)
            .ConfigureAwait(false);
        await using var resolvedBootloader = await _inputs.ResolveOptionalAsync(
                bootloaderLocation,
                progress,
                cancellationToken)
            .ConfigureAwait(false);
        await using var resolvedIdb = await _inputs.ResolveOptionalAsync(
                idbLocation,
                progress,
                cancellationToken)
            .ConfigureAwait(false);
        var firmware = CreateFirmwareFlasher();
        await firmware.FlashAsync(
                target,
                resolvedSource.Path,
                new FirmwareFlashOptions {
                    LoaderPath = resolvedLoader?.Path,
                    BootloaderPath = resolvedBootloader?.Path,
                    IdbPath = resolvedIdb?.Path,
                    ResetAfterFlash = resetAfterFlash
                },
                progress,
                cancellationToken)
            .ConfigureAwait(false);
        progress.Complete();
        Console.WriteLine("Flash complete");
    }

    private async Task<int> RunReadAsync(
        DeviceTarget target,
        string? loaderLocation,
        string output,
        uint sector,
        ushort count,
        CancellationToken cancellationToken
    ) {
        using var progress = new OperationProgressDisplay(_console);
        await using var loader = await _inputs.ResolveOptionalAsync(
                loaderLocation,
                progress,
                cancellationToken)
            .ConfigureAwait(false);
        var storage = CreateStorageAccess();
        var path = await storage.ReadAsync(
                target,
                loader?.Path,
                output,
                sector,
                count,
                progress,
                cancellationToken)
            .ConfigureAwait(false);
        progress.Complete();
        Console.WriteLine(
            $"Read {new FileInfo(path).Length:N0} bytes to {path}");
        return 0;
    }

    private async Task<int> RunWriteAsync(
        DeviceTarget target,
        string? loaderLocation,
        string inputLocation,
        string? backup,
        uint sector,
        long? length,
        long? sourceOffset,
        bool eraseBeforeWrite,
        CancellationToken cancellationToken
    ) {
        using var progress = new OperationProgressDisplay(_console);
        await using var input = await _inputs.ResolveAsync(
                inputLocation,
                progress,
                cancellationToken)
            .ConfigureAwait(false);
        await using var loader = await _inputs.ResolveOptionalAsync(
                loaderLocation,
                progress,
                cancellationToken)
            .ConfigureAwait(false);
        var storage = CreateStorageAccess();
        var bytesWritten = await storage.WriteAsync(
                target,
                loader?.Path,
                input.Path,
                sector,
                new StorageWriteOptions {
                    Length = length,
                    SourceOffset = sourceOffset,
                    EraseBeforeWrite = eraseBeforeWrite,
                    BackupPath = backup
                },
                progress,
                cancellationToken)
            .ConfigureAwait(false);
        progress.Complete();
        Console.WriteLine(
            $"Write complete: {bytesWritten:N0} bytes");
        return 0;
    }

    private async Task<int> RunStorageAsync(
        DeviceTarget target,
        string? loaderLocation,
        StorageType selectedStorage,
        CancellationToken cancellationToken
    ) {
        using var progress = new OperationProgressDisplay(_console);
        await using var loader = await _inputs.ResolveOptionalAsync(
                loaderLocation,
                progress,
                cancellationToken)
            .ConfigureAwait(false);
        var storage = CreateStorageAccess();
        await storage.SelectAsync(
                target,
                loader?.Path,
                selectedStorage,
                cancellationToken)
            .ConfigureAwait(false);
        Console.WriteLine($"Storage changed to {selectedStorage}");
        return 0;
    }

    private static StorageAccess CreateStorageAccess() =>
        new(new UsbDeviceProviderFactory());

    private static FirmwareFlasher CreateFirmwareFlasher() =>
        new(new UsbDeviceProviderFactory());
}
