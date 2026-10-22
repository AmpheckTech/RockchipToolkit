using Ampheck.Adb.Connection;

namespace Ampheck.Adb.Cli;

internal static class InteractiveTerminal {
    public static async Task RunAsync(
        IAdbShellStream shell,
        CancellationToken cancellationToken) {
        var interactive = ConsoleInput.IsInteractive;
        if (interactive) {
            await TryResizeAsync(shell, cancellationToken)
                .ConfigureAwait(false);
        }

        var receiveTask = shell.ReadAsync(
            text => {
                Console.Out.Write(text);
                Console.Out.Flush();
            },
            cancellationToken,
            Timeout.InfiniteTimeSpan);
        if (!interactive) {
            await PumpStandardInputAsync(
                    shell,
                    cancellationToken)
                .ConfigureAwait(false);
            await receiveTask.ConfigureAwait(false);
            return;
        }

        var originalControlC = Console.TreatControlCAsInput;
        try {
            Console.TreatControlCAsInput = true;
            _ = PumpResizeAsync(
                shell,
                receiveTask,
                cancellationToken);
            var inputTask = PumpConsoleKeysAsync(
                shell,
                receiveTask,
                cancellationToken);
            var completed = await Task.WhenAny(
                    receiveTask,
                    inputTask)
                .ConfigureAwait(false);
            if (completed == inputTask) {
                await inputTask.ConfigureAwait(false);
            }

            await receiveTask.ConfigureAwait(false);
        } finally {
            Console.TreatControlCAsInput = originalControlC;
        }
    }

    private static Task PumpConsoleKeysAsync(
        IAdbShellStream shell,
        Task receiveTask,
        CancellationToken cancellationToken) {
        return Task.Factory.StartNew(
                async () => {
                    while (!receiveTask.IsCompleted &&
                        !cancellationToken.IsCancellationRequested) {
                        ConsoleKeyInfo key;
                        try {
                            key = Console.ReadKey(intercept: true);
                        } catch (InvalidOperationException) {
                            return;
                        }

                        var text = AdbInputTranslator.Translate(key);
                        if (text.Length > 0) {
                            await shell.WriteAsync(
                                    text,
                                    cancellationToken)
                                .ConfigureAwait(false);
                        }
                    }
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default)
            .Unwrap();
    }

    private static Task PumpStandardInputAsync(
        IAdbShellStream shell,
        CancellationToken cancellationToken) {
        return Task.Run(async () => {
            await using var input = Console.OpenStandardInput();
            var buffer = new byte[4096];
            while (true) {
                var read = await input.ReadAsync(
                        buffer,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0) {
                    break;
                }

                await shell.WriteAsync(
                        buffer.AsMemory(0, read),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            await shell.WriteAsync(
                    "\x04",
                    cancellationToken)
                .ConfigureAwait(false);
        }, cancellationToken);
    }

    private static async Task PumpResizeAsync(
        IAdbShellStream shell,
        Task receiveTask,
        CancellationToken cancellationToken) {
        var (lastColumns, lastRows) = GetTerminalSize();
        while (!receiveTask.IsCompleted &&
            !cancellationToken.IsCancellationRequested) {
            await Task.Delay(250, cancellationToken)
                .ConfigureAwait(false);
            var (columns, rows) = GetTerminalSize();
            if (columns == lastColumns && rows == lastRows) {
                continue;
            }

            await shell.ResizeAsync(
                    columns,
                    rows,
                    cancellationToken)
                .ConfigureAwait(false);
            lastColumns = columns;
            lastRows = rows;
        }
    }

    private static async ValueTask TryResizeAsync(
        IAdbShellStream shell,
        CancellationToken cancellationToken) {
        var (columns, rows) = GetTerminalSize();
        await shell.ResizeAsync(
                columns,
                rows,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static (int Columns, int Rows) GetTerminalSize() {
        try {
            return (
                Math.Max(Console.WindowWidth, 1),
                Math.Max(Console.WindowHeight, 1));
        } catch (Exception error) when (
            error is IOException or InvalidOperationException) {
            return (80, 24);
        }
    }
}
