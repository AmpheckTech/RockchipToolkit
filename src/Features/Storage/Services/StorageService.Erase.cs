using Ampheck.RockchipToolkit.Core.Operations;
using Ampheck.RockchipToolkit.Features.Devices.Model;
using Ampheck.RockchipToolkit.Features.Storage.Model;

namespace Ampheck.RockchipToolkit.Features.Storage.Services;

internal sealed partial class StorageService {
    public async ValueTask EraseAsync(
        StorageEraseParams parameters,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken
    ) {
        if (parameters.Count == 0) {
            throw new InvalidDataException(
                "The erase count must be greater than zero.");
        }

        await using var lease = await access.OpenAsync(
                parameters.Target,
                parameters.LoaderLocation,
                progress,
                cancellationToken)
            .ConfigureAwait(false);
        progress.ReportStatus(
            $"Erasing {parameters.Count:N0} sectors at " +
            $"0x{parameters.Sector:X8}");
        await lease.Device.EraseAsync(
                parameters.Sector,
                parameters.Count,
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
        progress.ReportProgress("erase", current, total);
        return ValueTask.CompletedTask;
    }
}
