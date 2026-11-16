using Ampheck.RockchipToolkit.Adb.Model;

namespace Ampheck.RockchipToolkit.Adb.Services;

internal sealed partial class AdbToolkitService {
    public async ValueTask PushAsync(
        AdbPushParams parameters,
        CancellationToken cancellationToken
    ) {
        if (!File.Exists(parameters.Source)) {
            throw new FileNotFoundException(
                $"ADB push input '{parameters.Source}' was not found.",
                parameters.Source);
        }

        await _server.EnsureRunningAsync(cancellationToken)
            .ConfigureAwait(false);
        var fileName = Path.GetFileName(parameters.Source);
        var destination = parameters.Destination.EndsWith(
            "/",
            StringComparison.Ordinal)
                ? parameters.Destination + fileName
                : parameters.Destination;
        var device = await _client.GetOnlineDeviceAsync(
                parameters.SerialNumber,
                cancellationToken)
            .ConfigureAwait(false);
        await device.PushAsync(
                parameters.Source,
                destination,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask PullAsync(
        AdbPullParams parameters,
        CancellationToken cancellationToken
    ) {
        if (string.IsNullOrWhiteSpace(parameters.Source)) {
            throw new InvalidDataException(
                "The ADB remote source path is empty.");
        }

        await _server.EnsureRunningAsync(cancellationToken)
            .ConfigureAwait(false);
        var device = await _client.GetOnlineDeviceAsync(
                parameters.SerialNumber,
                cancellationToken)
            .ConfigureAwait(false);
        var destination = Path.GetFullPath(parameters.Destination);
        if (Directory.Exists(destination) ||
            Path.EndsInDirectorySeparator(destination)) {
            Directory.CreateDirectory(destination);
            var name = Path.GetFileName(parameters.Source);
            destination = Path.Combine(
                destination,
                string.IsNullOrWhiteSpace(name)
                    ? "adb-pull.bin"
                    : name);
        }

        var directory = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(directory)) {
            Directory.CreateDirectory(directory);
        }

        await device.PullAsync(
                parameters.Source,
                destination,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }
}
