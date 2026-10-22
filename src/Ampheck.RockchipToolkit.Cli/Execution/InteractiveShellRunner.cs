using Ampheck.Adb.Connection;
using Ampheck.RockchipToolkit.Cli.Presentation;

namespace Ampheck.RockchipToolkit.Cli.Execution;

internal static class InteractiveShellRunner {
    public static async Task RunAsync(
        IAdbShellStream shell,
        ConsoleRenderer console) {
        console.WriteStatus(
            "Interactive ADB shell. Use exit or Ctrl+D to leave.");
        var interactive = ConsoleInput.IsInteractiveConsole;
        if (interactive) {
            await TryResizeAsync(shell).ConfigureAwait(false);
        }

        var receiveTask = shell.ReadAsync(
            text => {
                Console.Out.Write(text);
                Console.Out.Flush();
            },
            readTimeout: Timeout.InfiniteTimeSpan);
        if (!interactive) {
            var stdinTask = PumpStandardInputAsync(shell);
            await stdinTask.ConfigureAwait(false);
            await receiveTask.ConfigureAwait(false);
            return;
        }

        var originalControlC = Console.TreatControlCAsInput;
        try {
            Console.TreatControlCAsInput = true;
            _ = PumpResizeAsync(shell, receiveTask);
            var inputTask = PumpConsoleKeysAsync(
                shell,
                receiveTask);
            await WaitForInputOrRemoteAsync(
                    receiveTask,
                    inputTask)
                .ConfigureAwait(false);
        } finally {
            Console.TreatControlCAsInput = originalControlC;
        }
    }

    private static async Task PumpResizeAsync(
        IAdbShellStream shell,
        Task receiveTask) {
        var (lastColumns, lastRows) = GetTerminalSize();
        while (!receiveTask.IsCompleted) {
            await Task.Delay(250).ConfigureAwait(false);
            var (columns, rows) = GetTerminalSize();
            if (columns == lastColumns && rows == lastRows) {
                continue;
            }

            try {
                await shell.ResizeAsync(columns, rows)
                    .ConfigureAwait(false);
                lastColumns = columns;
                lastRows = rows;
            } catch (Exception error) when (
                error is IOException or ObjectDisposedException) {
                return;
            }
        }
    }

    private static async ValueTask TryResizeAsync(
        IAdbShellStream shell) {
        var (columns, rows) = GetTerminalSize();
        try {
            await shell.ResizeAsync(columns, rows)
                .ConfigureAwait(false);
        } catch (Exception error) when (
            error is IOException or ObjectDisposedException) {
        }
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

    private static async Task WaitForInputOrRemoteAsync(
        Task receiveTask,
        Task inputTask) {
        var completed = await Task.WhenAny(
                receiveTask,
                inputTask)
            .ConfigureAwait(false);
        if (completed == inputTask) {
            await inputTask.ConfigureAwait(false);
        }

        try {
            await receiveTask.ConfigureAwait(false);
        } catch (IOException) when (
            inputTask.IsCompleted) {
        }
    }

    private static Task PumpConsoleKeysAsync(
        IAdbShellStream shell,
        Task receiveTask) {
        return Task.Factory.StartNew(
                async () => {
                    while (!receiveTask.IsCompleted) {
                        ConsoleKeyInfo key;
                        try {
                            key = Console.ReadKey(intercept: true);
                        } catch (InvalidOperationException) {
                            return;
                        }

                        if (receiveTask.IsCompleted) {
                            return;
                        }

                        var text = AdbInputTranslator.Translate(key);
                        if (text.Length > 0) {
                            await shell.WriteAsync(text)
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
        IAdbShellStream shell) {
        return Task.Run(async () => {
            await using var input = Console.OpenStandardInput();
            var buffer = new byte[4096];
            while (true) {
                var read = input.Read(buffer, 0, buffer.Length);
                if (read == 0) {
                    break;
                }

                await shell.WriteAsync(
                        buffer.AsMemory(0, read))
                    .ConfigureAwait(false);
            }

            await shell.WriteAsync("\x04").ConfigureAwait(false);
        });
    }
}
