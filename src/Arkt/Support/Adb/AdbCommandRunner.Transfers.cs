using Ampheck.RockchipToolkit.Adb.Model;
using Ampheck.RockchipToolkit.Arkt.Terminal;
using Ampheck.RockchipToolkit.Runtime.Terminal;

namespace Ampheck.RockchipToolkit.Arkt.Support.Adb;

internal sealed partial class AdbCommandRunner {
    public async Task<int> PushAsync(
        string? serialNumber,
        string source,
        string destination,
        CancellationToken cancellationToken = default
    ) {
        if (!File.Exists(source)) {
            throw new FileNotFoundException(
                $"Local file '{source}' was not found.",
                source);
        }

        using var progress = console.IsInteractive
            ? new ProgressBar(
                console,
                Path.GetFileName(source),
                new FileInfo(source).Length,
                trackRate: false)
            : null;
        progress?.Report(0);
        await adb.PushAsync(
                new AdbPushParams {
                    SerialNumber = serialNumber,
                    Source = Path.GetFullPath(source),
                    Destination = destination
                },
                cancellationToken)
            .ConfigureAwait(false);
        progress?.Report(new FileInfo(source).Length);
        Console.WriteLine($"pushed {source} -> {destination}");
        return 0;
    }

    public async Task<int> PullAsync(
        string? serialNumber,
        string source,
        string destination,
        CancellationToken cancellationToken = default
    ) {
        await adb.PullAsync(
                new AdbPullParams {
                    SerialNumber = serialNumber,
                    Source = source,
                    Destination = Path.GetFullPath(destination)
                },
                cancellationToken)
            .ConfigureAwait(false);
        Console.WriteLine($"pulled {source} -> {destination}");
        return 0;
    }
}
