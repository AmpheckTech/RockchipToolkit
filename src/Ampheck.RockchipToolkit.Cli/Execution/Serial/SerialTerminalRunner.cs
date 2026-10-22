using System.ComponentModel;
using System.Runtime.InteropServices;
using Ampheck.RockchipToolkit.Cli.Presentation;

namespace Ampheck.RockchipToolkit.Cli.Execution.Serial;

internal static class SerialTerminalRunner {
    public static async Task RunAsync(
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> write,
        Task completion,
        ConsoleRenderer console,
        CancellationToken cancellationToken = default) {
        console.WriteStatus(
            "Interactive serial terminal. Use Ctrl+] to leave.");
        var interactive = ConsoleInput.IsInteractiveConsole;
        if (!interactive) {
            await PumpStandardInputAsync(
                    write,
                    cancellationToken)
                .ConfigureAwait(false);
            try {
                await completion.WaitAsync(
                        TimeSpan.FromMilliseconds(1500),
                        cancellationToken)
                    .ConfigureAwait(false);
            } catch (TimeoutException) {
            }

            return;
        }

        var originalControlC = Console.TreatControlCAsInput;
        try {
            Console.TreatControlCAsInput = true;
            using var terminalMode =
                OperatingSystem.IsWindows()
                    ? null
                    : await UnixTerminalMode.EnterAsync(
                            cancellationToken)
                        .ConfigureAwait(false);
            var (columns, rows) = GetTerminalSize();
            await ResizeRemoteAsync(
                    write,
                    columns,
                    rows,
                    cancellationToken)
                .ConfigureAwait(false);
            using var terminalStop =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);
            var inputTask = OperatingSystem.IsWindows()
                ? PumpConsoleKeysAsync(
                    write,
                    completion,
                    terminalStop.Token)
                : PumpRawInputAsync(
                    write,
                    completion,
                    terminalStop.Token);
            var resizeTask = PumpResizeAsync(
                write,
                completion,
                terminalStop.Token);
            var completed = await Task.WhenAny(
                    completion,
                    inputTask)
                .ConfigureAwait(false);
            await terminalStop.CancelAsync()
                .ConfigureAwait(false);
            await IgnoreCancellationAsync(resizeTask)
                .ConfigureAwait(false);
            if (completed == inputTask) {
                await inputTask.ConfigureAwait(false);
                return;
            }

            await completion.ConfigureAwait(false);
        } finally {
            Console.TreatControlCAsInput = originalControlC;
        }
    }

    public static ValueTask WriteOutputAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default) {
        return Console.OpenStandardOutput().WriteAsync(
            data,
            cancellationToken);
    }

    private static async Task PumpConsoleKeysAsync(
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> write,
        Task completion,
        CancellationToken cancellationToken) {
        while (!completion.IsCompleted &&
            !cancellationToken.IsCancellationRequested) {
            ConsoleKeyInfo key;
            try {
                key = Console.ReadKey(intercept: true);
            } catch (InvalidOperationException) {
                return;
            }

            if ((key.Modifiers & ConsoleModifiers.Control) != 0 &&
                (key.Key == ConsoleKey.Oem6 ||
                    key.KeyChar is ']' or '\u001d') ||
                key.KeyChar == '\u001d') {
                return;
            }

            var bytes = AdbInputTranslator.Translate(key);
            if (bytes.Length > 0) {
                await write(
                        System.Text.Encoding.UTF8.GetBytes(bytes),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private static Task PumpRawInputAsync(
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> write,
        Task completion,
        CancellationToken cancellationToken) {
        return Task.Factory.StartNew(
                async () => {
                    var buffer = new byte[4096];
                    while (!completion.IsCompleted &&
                        !cancellationToken.IsCancellationRequested) {
                        var read = ReadUnixStandardInput(
                            buffer,
                            buffer.Length);
                        if (read < 0) {
                            var code =
                                Marshal.GetLastPInvokeError();
                            if (code == 4) {
                                continue;
                            }

                            throw new IOException(
                                new Win32Exception(code).Message);
                        }

                        if (read == 0) {
                            return;
                        }

                        var exit = Array.IndexOf(
                            buffer,
                            (byte)0x1D,
                            0,
                            read);
                        var count = exit < 0 ? read : exit;
                        if (count > 0) {
                            await write(
                                    buffer.AsMemory(0, count),
                                    cancellationToken)
                                .ConfigureAwait(false);
                        }

                        if (exit >= 0) {
                            return;
                        }
                    }
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default)
            .Unwrap();
    }

    private static int ReadUnixStandardInput(
        byte[] buffer,
        int count) {
        var read = ReadNative(
            0,
            buffer,
            checked((nuint)count));
        return read > int.MaxValue
            ? int.MaxValue
            : (int)read;
    }

    [DllImport(
        "libc",
        EntryPoint = "read",
        SetLastError = true)]
    private static extern nint ReadNative(
        int descriptor,
        byte[] buffer,
        nuint count);

    private static async Task PumpResizeAsync(
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> write,
        Task completion,
        CancellationToken cancellationToken) {
        var (lastColumns, lastRows) = GetTerminalSize();
        while (!completion.IsCompleted &&
            !cancellationToken.IsCancellationRequested) {
            await Task.Delay(
                    TimeSpan.FromMilliseconds(250),
                    cancellationToken)
                .ConfigureAwait(false);
            var (columns, rows) = GetTerminalSize();
            if (columns == lastColumns &&
                rows == lastRows) {
                continue;
            }

            await ResizeRemoteAsync(
                    write,
                    columns,
                    rows,
                    cancellationToken)
                .ConfigureAwait(false);
            lastColumns = columns;
            lastRows = rows;
        }
    }

    private static Task PumpStandardInputAsync(
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> write,
        CancellationToken cancellationToken) {
        return Task.Run(async () => {
            await using var input = Console.OpenStandardInput();
            var buffer = new byte[4096];
            while (!cancellationToken.IsCancellationRequested) {
                var read = await input.ReadAsync(
                        buffer,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0) {
                    return;
                }

                await write(
                        buffer.AsMemory(0, read),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        });
    }

    private static ValueTask ResizeRemoteAsync(
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> write,
        int columns,
        int rows,
        CancellationToken cancellationToken) {
        var command = System.Text.Encoding.ASCII.GetBytes(
            $"stty rows {rows} cols {columns}\r");
        return write(command, cancellationToken);
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

    private static async Task IgnoreCancellationAsync(
        Task task) {
        try {
            await task.ConfigureAwait(false);
        } catch (OperationCanceledException) {
        }
    }
}
