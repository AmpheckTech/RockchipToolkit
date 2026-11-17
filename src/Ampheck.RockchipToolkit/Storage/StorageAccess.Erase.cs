using Ampheck.RockchipToolkit;
using Ampheck.RockchipToolkit.Devices;
using Ampheck.RockchipToolkit.Devices.Internal;
using Ampheck.RockchipToolkit.Protocol.RockUsb;

namespace Ampheck.RockchipToolkit.Storage;

public sealed partial class StorageAccess {
    public async ValueTask EraseAsync(
        DeviceTarget target,
        string? loaderPath,
        uint sector,
        ushort count,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken
    ) {
        if (count == 0) {
            throw new InvalidDataException(
                "The erase count must be greater than zero.");
        }

        await using var lease = await _access.OpenAsync(
                target,
                loaderPath,
                progress,
                cancellationToken)
            .ConfigureAwait(false);
        progress?.Report(new OperationProgress(
            ToolkitOperationStage.Erasing,
            Item: null,
            Completed: 0,
            Total: checked(
                (long)count *
                RockUsbProtocol.SectorSize)));
        await lease.Device.EraseAsync(
                sector,
                count,
                (current, total) => ReportProgressAsync(
                    progress,
                    current,
                    total),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static ValueTask ReportProgressAsync(
        IProgress<OperationProgress>? progress,
        long current,
        long total
    ) {
        progress?.Report(new OperationProgress(
            ToolkitOperationStage.Erasing,
            Item: null,
            Completed: current,
            Total: total));
        return ValueTask.CompletedTask;
    }
}
