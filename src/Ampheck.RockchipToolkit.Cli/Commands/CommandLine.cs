using System.CommandLine;
using System.Globalization;
using Ampheck.RockchipToolkit.Cli.Infrastructure;

namespace Ampheck.RockchipToolkit.Cli.Commands;

internal static class CommandLine {
    public static ParsedCommand Parse(string[] args) {
        return WithEnglishCli(() => {
            var state = new CommandParseState();
            var root = CreateRoot(state);
            var parsed = root.Parse(args);
            if (parsed.Errors.Count > 0) {
                if (CommandSuggestion.Find(root, args) is { } suggestion) {
                    throw new ArgumentException(
                        $"Unrecognized command or argument " +
                        $"'{suggestion.Token}'. Did you mean " +
                        $"'{suggestion.Suggestion}'?");
                }

                var message = string.Join(
                    Environment.NewLine,
                    parsed.Errors.Select(error =>
                        error.Message));
                throw new ArgumentException(
                    message);
            }

            parsed.Invoke(Invocation);
            if (state.Command is null) {
                throw new ArgumentException(
                    "A command is required. Use --help to list commands.");
            }

            var separator = Array.IndexOf(args, "--");
            if (state.Command == CommandNames.AdbShell &&
                state.Options.AdbShellCommand is null &&
                separator >= 0) {
                state.Options.AdbShellArguments =
                    args[(separator + 1)..];
            }

            return new ParsedCommand(
                state.Command,
                state.Options);
        });
    }

    public static void PrintHelp(string[] args) {
        WithEnglishCli(() => {
            var state = new CommandParseState();
            var root = CreateRoot(state);
            root.Parse(args).Invoke(Invocation);
            return 0;
        });
    }

    public static void PrintVersion() {
        Console.Out.WriteLine(ProductVersion.Format(
            "arkt",
            typeof(CommandLine).Assembly));
        Console.Out.Flush();
    }

    public static bool TryPrintCompletion(string[] args) {
        if (args.Length == 0 ||
            !args[0].StartsWith(
                "[suggest:",
                StringComparison.Ordinal)) {
            return false;
        }

        WithEnglishCli(() => {
            var state = new CommandParseState();
            var root = CreateRoot(state);
            root.Parse(args).Invoke(Invocation);
            return 0;
        });
        return true;
    }

    private static RootCommand CreateRoot(
        CommandParseState state) {
        var root = new RootCommand(
            "Flash and inspect Rockchip devices over RockUSB and ADB.");
        ServerCommands.AddTo(root, state);
        RockUsbCommands.AddTo(root, state);
        root.Add(AdbCommands.Create(state));
        root.Add(SerialCommands.Create(state));
        root.Add(CompletionCommands.Create(state));
        return root;
    }

    private static T WithEnglishCli<T>(Func<T> action) {
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try {
            CultureInfo.CurrentUICulture =
                CultureInfo.GetCultureInfo("en-US");
            return action();
        } finally {
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    private static InvocationConfiguration Invocation { get; } =
        new() {
            EnableDefaultExceptionHandler = false
        };
}
