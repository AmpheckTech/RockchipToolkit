using Ampheck.RockchipToolkit.Arkt.Terminal;
using Ampheck.RockchipToolkit.Core.Devices;
using Ampheck.RockchipToolkit.Features.Firmware.Model;
using Ampheck.RockchipToolkit.Features.Firmware.Services;
using Ampheck.RockchipToolkit.Features.Storage.Model;
using Ampheck.RockchipToolkit.Features.Storage.Services;
using Ampheck.RockchipToolkit.HardwareTransport.Protocol;
using Ampheck.RockchipToolkit.Runtime.Terminal;

namespace Ampheck.RockchipToolkit.Arkt.Support.Storage;

internal sealed class StorageCommandRunner(
    StorageService storage,
    FirmwareService firmware,
    ConsoleRenderer console
) {
    public async Task<int> RunFlashAsync(
        string source,
        string? loaderLocation,
        DeviceTarget target,
        bool resetAfterFlash,
        CancellationToken cancellationToken = default
    ) {
        using var progress = new OperationProgressDisplay(console);
        await firmware.FlashAsync(
                new FirmwareFlashParams {
                    Target = target,
                    FirmwareLocation = source,
                    LoaderLocation = loaderLocation,
                    ResetAfterFlash = resetAfterFlash
                },
                progress,
                cancellationToken)
            .ConfigureAwait(false);
        progress.Complete();
        Console.WriteLine("Flash complete");
        return 0;
    }

    public async Task<int> RunReadAsync(
        DeviceTarget target,
        string? loaderLocation,
        string output,
        uint sector,
        ushort count,
        CancellationToken cancellationToken = default
    ) {
        using var progress = new OperationProgressDisplay(console);
        var path = await storage.ReadAsync(
                new StorageReadParams {
                    Target = target,
                    LoaderLocation = loaderLocation,
                    OutputPath = output,
                    Sector = sector,
                    Count = count
                },
                progress,
                cancellationToken)
            .ConfigureAwait(false);
        progress.Complete();
        Console.WriteLine(
            $"Read {new FileInfo(path).Length:N0} bytes to {path}");
        return 0;
    }

    public async Task<int> RunWriteAsync(
        DeviceTarget target,
        string? loaderLocation,
        string inputLocation,
        string? backup,
        uint sector,
        long? length,
        long? sourceOffset,
        bool eraseBeforeWrite,
        CancellationToken cancellationToken = default
    ) {
        using var progress = new OperationProgressDisplay(console);
        var result = await storage.WriteAsync(
                new StorageWriteParams {
                    Target = target,
                    LoaderLocation = loaderLocation,
                    InputLocation = inputLocation,
                    Sector = sector,
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
            $"Write complete: {result.BytesWritten:N0} bytes");
        return 0;
    }

    public async Task<int> RunEraseAsync(
        DeviceTarget target,
        string? loaderLocation,
        uint sector,
        ushort count,
        CancellationToken cancellationToken = default
    ) {
        using var progress = new OperationProgressDisplay(console);
        await storage.EraseAsync(
                new StorageEraseParams {
                    Target = target,
                    LoaderLocation = loaderLocation,
                    Sector = sector,
                    Count = count
                },
                progress,
                cancellationToken)
            .ConfigureAwait(false);
        progress.Complete();
        Console.WriteLine("Erase complete");
        return 0;
    }

    public async Task<int> RunStorageAsync(
        DeviceTarget target,
        string? loaderLocation,
        StorageType selectedStorage,
        CancellationToken cancellationToken = default
    ) {
        await storage.SelectAsync(
                new StorageSelectParams {
                    Target = target,
                    LoaderLocation = loaderLocation,
                    Storage = selectedStorage
                },
                cancellationToken)
            .ConfigureAwait(false);
        Console.WriteLine($"Storage changed to {selectedStorage}");
        return 0;
    }
}
