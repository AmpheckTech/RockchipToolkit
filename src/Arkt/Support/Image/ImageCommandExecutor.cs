using Ampheck.RockchipToolkit.Arkt.Terminal;
using Ampheck.RockchipToolkit.Images.Common;
using Ampheck.RockchipToolkit.Images.Update;
using Ampheck.RockchipToolkit.Runtime.Terminal;

namespace Ampheck.RockchipToolkit.Arkt.Support.Image;

internal static class ImageCommandExecutor {
    public static async Task<int> RunAsync(
        string input,
        string output,
        bool force,
        string? chip,
        bool unpacking,
        ConsoleRenderer console
    ) {
        using var stop = new CancellationTokenSource();
        ConsoleCancelEventHandler handler = (_, eventArgs) => {
            eventArgs.Cancel = true;
            stop.Cancel();
        };
        Console.CancelKeyPress += handler;
        using var progress = new ImageProgressDisplay(
            console,
            unpacking ? "unpack" : "pack");
        console.StartActivity(unpacking
            ? $"Unpacking {input}"
            : $"Packing {input}");
        try {
            if (unpacking) {
                await RockchipImageExtractor.UnpackAsync(
                        input,
                        output,
                        force,
                        progress,
                        stop.Token)
                    .ConfigureAwait(false);
                progress.Complete();
                console.CompleteActivity(
                    $"Unpacked {input} to {output}");
            } else {
                await RockchipImagePacker.PackAsync(
                        input,
                        output,
                        force,
                        chip,
                        progress,
                        stop.Token)
                    .ConfigureAwait(false);
                progress.Complete();
                console.CompleteActivity(
                    $"Packed {input} to {output}");
            }

            return 0;
        } catch (OperationCanceledException)
            when (stop.IsCancellationRequested) {
            console.CompleteActivity();
            Console.Error.WriteLine("Operation canceled.");
            return 130;
        } finally {
            console.CompleteActivity();
            Console.CancelKeyPress -= handler;
        }
    }

}
