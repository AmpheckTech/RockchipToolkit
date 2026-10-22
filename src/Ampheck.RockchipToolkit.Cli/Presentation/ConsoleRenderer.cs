using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace Ampheck.RockchipToolkit.Cli.Presentation;

internal sealed class ConsoleRenderer : IDisposable {
    private readonly object _sync = new();
    private readonly IAnsiConsole _console;
    private readonly bool _interactive;
    private int _progressLength;
    private CancellationTokenSource? _activityStop;
    private Task? _activityTask;

    public ConsoleRenderer() {
        _console = AnsiConsole.Create(new AnsiConsoleSettings {
            Ansi = AnsiSupport.Detect,
            ColorSystem = ColorSystemSupport.Detect,
            Interactive = InteractionSupport.Detect,
            Out = new AnsiConsoleOutput(Console.Error)
        });
        _interactive = _console.Profile.Capabilities.Interactive;
    }

    public bool IsInteractive => _interactive;
    public int Width => Math.Max(_console.Profile.Width, 40);

    public void WriteLog(
        DateTimeOffset timestamp,
        LogLevel level,
        string category,
        string message,
        Exception? exception) {
        var (label, color) = GetLevelStyle(level);
        var time = timestamp.ToString("HH:mm:ss");
        var shortCategory = GetShortCategory(category);
        WriteLine(
            $"[grey]{Markup.Escape(time)}[/] " +
            $"[{color}]{label}[/] " +
            $"[grey]{Markup.Escape(shortCategory)}[/] " +
            Markup.Escape(message));
        if (exception is not null) {
            WriteLine(
                $"[red]{Markup.Escape(exception.ToString())}[/]");
        }
    }

    public void WriteStatus(
        string message,
        bool complete = false) {
        var color = complete ? "green" : "cyan";
        var marker = complete ? "OK" : ">>";
        WriteLine(
            $"[{color}]{marker}[/] {Markup.Escape(message)}");
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
            if (_progressLength == 0) {
                return;
            }

            CompleteProgressLine(line);
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
            WriteLine($"[cyan]>>[/] {Markup.Escape(message)}");
            return;
        }

        var stop = new CancellationTokenSource();
        _activityStop = stop;
        _activityTask = Task.Run(async () => {
            var frames = new[] { "|", "/", "-", "\\" };
            var index = 0;
            while (!stop.IsCancellationRequested) {
                var frame = frames[index++ % frames.Length];
                SetProgress(
                    $"{frame} {message}");
                try {
                    await Task.Delay(120, stop.Token)
                        .ConfigureAwait(false);
                } catch (OperationCanceledException) {
                    break;
                }
            }
        });
    }

    public void CompleteActivity(
        string? message = null) {
        var stop = Interlocked.Exchange(
            ref _activityStop,
            null);
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
            WriteLine(
                $"[green]OK[/] {Markup.Escape(message)}");
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

    private void WriteLine(string markup) {
        lock (_sync) {
            ClearProgressLine();
            _console.MarkupLine(markup);
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

    private static string SanitizeProgressLine(string value) =>
        value.Replace('\r', ' ')
            .Replace('\n', ' ');

    private static (string Label, string Color) GetLevelStyle(
        LogLevel level) {
        return level switch {
            LogLevel.Trace => ("trce", "grey"),
            LogLevel.Debug => ("dbug", "mediumpurple2"),
            LogLevel.Information => ("info", "dodgerblue2"),
            LogLevel.Warning => ("warn", "orange1"),
            LogLevel.Error => ("fail", "red"),
            LogLevel.Critical => ("crit", "red"),
            _ => ("log", "grey")
        };
    }

    private static string GetShortCategory(string category) {
        var separator = category.LastIndexOf('.');
        return separator < 0
            ? category
            : category[(separator + 1)..];
    }
}
