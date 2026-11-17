using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Ampheck.RockchipToolkit.Cli.Terminal;

internal static class InteractiveTerminal {
    // Opening the standard-output stream duplicates the native descriptor on
    // Unix. Serial data can arrive in thousands of chunks, so keep one stream
    // alive for the process instead of leaking descriptors per write.
    private static readonly Stream StandardOutput =
        Console.OpenStandardOutput();

    public static async Task RunAsync(
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> write,
        Task completion,
        ConsoleRenderer console,
        string status = "Interactive serial terminal. Use Ctrl+] to leave.",
        CancellationToken cancellationToken = default
    ) {
        console.WriteStatus(
            status);
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
            var completed = await Task.WhenAny(
                    completion,
                    inputTask)
                .ConfigureAwait(false);
            await terminalStop.CancelAsync()
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
        CancellationToken cancellationToken = default
    ) {
        return StandardOutput.WriteAsync(
            data,
            cancellationToken);
    }

    private static async Task PumpConsoleKeysAsync(
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> write,
        Task completion,
        CancellationToken cancellationToken
    ) {
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

            var bytes = TerminalInputTranslator.Translate(key);
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
        CancellationToken cancellationToken
    ) {
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
        int count
    ) {
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
        nuint count
    );

    private static Task PumpStandardInputAsync(
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> write,
        CancellationToken cancellationToken
    ) {
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
        }, cancellationToken);
    }
}
