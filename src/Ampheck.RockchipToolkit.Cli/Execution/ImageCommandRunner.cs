using Ampheck.RockchipToolkit.Cli.Commands;
using Ampheck.RockchipToolkit.Cli.Presentation;
using Ampheck.RockchipToolkit.Images;

namespace Ampheck.RockchipToolkit.Cli.Execution;

internal static class ImageCommandRunner {
    public static async Task<int> RunAsync(
        ImageCommandArguments options,
        ConsoleRenderer console) {
        using var stop = new CancellationTokenSource();
        ConsoleCancelEventHandler handler = (_, eventArgs) => {
            eventArgs.Cancel = true;
            stop.Cancel();
        };
        Console.CancelKeyPress += handler;
        var unpacking = options.Unpack;
        using var progress = new ImageProgressDisplay(
            console,
            unpacking ? "unpack" : "pack");
        console.StartActivity(unpacking
            ? $"Unpacking {options.Input}"
            : $"Packing {options.Input}");
        try {
            if (unpacking) {
                await RockchipImageExtractor.UnpackAsync(
                        options.Input,
                        options.Output,
                        options.Force,
                        progress,
                        stop.Token)
                    .ConfigureAwait(false);
                progress.Complete();
                console.CompleteActivity(
                    $"Unpacked {options.Input} to {options.Output}");
            } else {
                await RockchipImagePacker.PackAsync(
                        options.Input,
                        options.Output,
                        options.Force,
                        options.Chip,
                        progress,
                        stop.Token)
                    .ConfigureAwait(false);
                progress.Complete();
                console.CompleteActivity(
                    $"Packed {options.Input} to {options.Output}");
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

    private sealed class ImageProgressDisplay(
        ConsoleRenderer console,
        string label) : IProgress<RockchipImageProgress>, IDisposable {
        private ProgressBar? _progress;
        private long _total;

        public void Report(RockchipImageProgress value) {
            if (value.Total <= 0) {
                return;
            }

            if (_progress is null) {
                console.CompleteActivity();
                _progress = new ProgressBar(
                    console,
                    label,
                    value.Total);
                _total = value.Total;
            } else if (_total != value.Total &&
                value.Current >= _total) {
                _progress.Dispose();
                var phaseStart = _total;
                _total = value.Total;
                _progress = new ProgressBar(
                    console,
                    label,
                    value.Total - phaseStart);
                _progress.Report(value.Current - phaseStart);
                return;
            } else if (_total != value.Total) {
                _progress.UpdateTotal(value.Total);
                _total = value.Total;
            }

            _progress.Report(value.Current);
        }

        public void Complete() {
            _progress?.Dispose();
            _progress = null;
        }

        public void Dispose() {
            Complete();
        }
    }
}
