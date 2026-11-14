using Microsoft.Extensions.Logging;

namespace Ampheck.Adb;

public sealed partial class AdbClient {
    internal async Task InstallAsync(
        AdbDevice device,
        string apkPath,
        AdbInstallOptions? installOptions,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(device);
        installOptions ??= new AdbInstallOptions();
        if (installOptions.UserId is < 0) {
            throw new ArgumentOutOfRangeException(
                nameof(installOptions),
                "The Android user ID cannot be negative.");
        }

        var remotePath =
            $"/data/local/tmp/ampheck-{Guid.NewGuid():N}.apk";
        try {
            await PushAsync(
                    device,
                    apkPath,
                    remotePath,
                    DefaultPushMode,
                    progress,
                    cancellationToken)
                .ConfigureAwait(false);
            var arguments = new List<string> {
                "pm",
                "install"
            };
            if (installOptions.ReplaceExisting) {
                arguments.Add("-r");
            }

            if (installOptions.AllowDowngrade) {
                arguments.Add("-d");
            }

            if (installOptions.GrantPermissions) {
                arguments.Add("-g");
            }

            if (installOptions.UserId is { } userId) {
                arguments.Add("--user");
                arguments.Add(userId.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
            }

            arguments.Add($"\"{remotePath}\"");
            var output = await ExecuteShellCommandAsync(
                    device,
                    string.Join(" ", arguments),
                    cancellationToken)
                .ConfigureAwait(false);
            if (!output.Contains(
                    "Success",
                    StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(output)
                        ? "APK installation failed."
                        : output.Trim());
            }
        } finally {
            try {
                await ExecuteShellCommandAsync(
                        device,
                        $"rm -f \"{remotePath}\"",
                        CancellationToken.None)
                    .ConfigureAwait(false);
            } catch (Exception error) when (
                error is IOException or InvalidOperationException) {
                _logger.LogWarning(
                    error,
                    "Unable to remove temporary APK {RemotePath}",
                    remotePath);
            }
        }
    }
}
