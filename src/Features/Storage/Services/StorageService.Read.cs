using Ampheck.RockchipToolkit.Core.Operations;
using Ampheck.RockchipToolkit.Features.Devices.Model;
using Ampheck.RockchipToolkit.Features.Storage.Model;

namespace Ampheck.RockchipToolkit.Features.Storage.Services;

internal sealed partial class StorageService {
    public async ValueTask<string> ReadAsync(
        StorageReadParams parameters,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken
    ) {
        if (parameters.Count == 0) {
            throw new InvalidDataException(
                "The read count must be greater than zero.");
        }

        await using var lease = await access.OpenAsync(
                parameters.Target,
                parameters.LoaderLocation,
                progress,
                cancellationToken)
            .ConfigureAwait(false);
        progress.ReportStatus(
            $"Reading {parameters.Count:N0} sector(s) at " +
            $"0x{parameters.Sector:X8}");
        var data = await lease.Device.ReadAsync(
                parameters.Sector,
                parameters.Count,
                cancellationToken)
            .ConfigureAwait(false);
        var path = parameters.OutputPath;
        if (Directory.Exists(path)) {
            path = Path.Combine(
                path,
                $"read-{parameters.Sector:X8}.bin");
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllBytesAsync(path, data, cancellationToken)
            .ConfigureAwait(false);
        return Path.GetFullPath(path);
    }
}
