using Ampheck.RockchipToolkit;
using Ampheck.RockchipToolkit.Devices;
using Ampheck.RockchipToolkit.Devices.Internal;
using Ampheck.RockchipToolkit.Protocol.RockUsb;

namespace Ampheck.RockchipToolkit.Storage;

public sealed partial class StorageAccess {
    public async ValueTask<string> ReadAsync(
        DeviceTarget target,
        string? loaderPath,
        string outputPath,
        uint sector,
        ushort count,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken
    ) {
        if (count == 0) {
            throw new InvalidDataException(
                "The read count must be greater than zero.");
        }

        await using var lease = await _access.OpenAsync(
                target,
                loaderPath,
                progress,
                cancellationToken)
            .ConfigureAwait(false);
        var totalBytes = checked(
            (long)count *
            RockUsbProtocol.SectorSize);
        var item = Path.GetFileName(outputPath);
        progress?.Report(new OperationProgress(
            ToolkitOperationStage.Reading,
            item,
            Completed: 0,
            Total: totalBytes));
        var data = await lease.Device.ReadAsync(
                sector,
                count,
                cancellationToken)
            .ConfigureAwait(false);
        var path = outputPath;
        if (Directory.Exists(path)) {
            path = Path.Combine(
                path,
                $"read-{sector:X8}.bin");
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllBytesAsync(path, data, cancellationToken)
            .ConfigureAwait(false);
        progress?.Report(new OperationProgress(
            ToolkitOperationStage.Reading,
            item,
            Completed: totalBytes,
            Total: totalBytes));
        return Path.GetFullPath(path);
    }
}
