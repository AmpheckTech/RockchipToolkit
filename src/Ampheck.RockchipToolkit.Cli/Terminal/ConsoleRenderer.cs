namespace Ampheck.RockchipToolkit.Cli.Terminal;

internal sealed class ConsoleRenderer : IDisposable {
    private readonly object _sync = new();
    private readonly bool _interactive;
    private int _progressLength;
    private CancellationTokenSource? _activityStop;
    private Task? _activityTask;

    public ConsoleRenderer() {
        _interactive =
            ConsoleInput.IsInteractiveConsole &&
            !Console.IsErrorRedirected;
    }

    public bool IsInteractive => _interactive;

    public int Width => _interactive ? GetWidth() : 80;

    public void WriteStatus(
        string message,
        bool complete = false
    ) {
        WriteLine(
            $"{(complete ? "OK" : ">>")} {message}");
    }

    public void SetProgress(string line) {
        if (!_interactive) {
            return;
        }

        lock (_sync) {
            WriteProgress(line);
        }
    }

    public void CompleteProgress(string line) {
        if (!_interactive) {
            return;
        }

        lock (_sync) {
            if (_progressLength != 0) {
                CompleteProgressLine(line);
            }
        }
    }

    public void ClearProgress() {
        if (!_interactive) {
            return;
        }

        lock (_sync) {
            ClearProgressLine();
        }
    }

    public void StartActivity(string message) {
        CompleteActivity();
        if (!_interactive) {
            WriteLine($">> {message}");
            return;
        }

        var stop = new CancellationTokenSource();
        _activityStop = stop;
        _activityTask = Task.Run(async () => {
            var frames = new[] { "|", "/", "-", "\\" };
            var index = 0;
            while (!stop.IsCancellationRequested) {
                SetProgress(
                    $"{frames[index++ % frames.Length]} {message}");
                try {
                    await Task.Delay(120, stop.Token)
                        .ConfigureAwait(false);
                } catch (OperationCanceledException) {
                    break;
                }
            }
        });
    }

    public void CompleteActivity(string? message = null) {
        var stop = Interlocked.Exchange(ref _activityStop, null);
        if (stop is not null) {
            stop.Cancel();
            try {
                _activityTask?.GetAwaiter().GetResult();
            } catch (OperationCanceledException) {
            }

            stop.Dispose();
            _activityTask = null;
        }

        if (message is not null) {
            WriteLine($"OK {message}");
        } else if (_interactive) {
            ClearProgress();
        }
    }

    public void Dispose() {
        CompleteActivity();
        lock (_sync) {
            ClearProgressLine();
        }
    }

    private void WriteLine(string message) {
        lock (_sync) {
            ClearProgressLine();
            Console.Error.WriteLine(message);
        }
    }

    private void WriteProgress(string line) {
        line = SanitizeProgressLine(line);
        if (line.Length >= Width) {
            line = line[..(Width - 1)];
        }

        var padding = Math.Max(0, _progressLength - line.Length);
        Console.Error.Write('\r');
        Console.Error.Write(line);
        if (padding > 0) {
            Console.Error.Write(new string(' ', padding));
        }

        Console.Error.Flush();
        _progressLength = line.Length;
    }

    private void CompleteProgressLine(string line) {
        line = SanitizeProgressLine(line);
        if (line.Length >= Width) {
            line = line[..(Width - 1)];
        }

        var padding = Math.Max(0, _progressLength - line.Length);
        Console.Error.Write('\r');
        Console.Error.Write(line);
        if (padding > 0) {
            Console.Error.Write(new string(' ', padding));
        }

        Console.Error.WriteLine();
        Console.Error.Flush();
        _progressLength = 0;
    }

    private void ClearProgressLine() {
        if (_progressLength == 0) {
            return;
        }

        Console.Error.Write('\r');
        Console.Error.Write(new string(' ', _progressLength));
        Console.Error.Write('\r');
        Console.Error.Flush();
        _progressLength = 0;
    }

    private static int GetWidth() {
        try {
            return Math.Max(Console.WindowWidth, 40);
        } catch (IOException) {
            return 80;
        }
    }

    private static string SanitizeProgressLine(string value) =>
        value.Replace('\r', ' ')
            .Replace('\n', ' ');
}
