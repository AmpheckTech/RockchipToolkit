using System.CommandLine;
using System.Globalization;
using Ampheck.RockchipToolkit.Cli.Infrastructure;

namespace Ampheck.RockchipToolkit.Cli.Commands;

internal static class CommandLine {
    public static ParsedCommand? Parse(
        string[] args,
        TextWriter output) {
        return WithEnglishCli(() => {
            var state = new CommandParseState();
            var root = CreateRoot(state, output);
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

            parsed.Invoke(CreateInvocation(output));
            if (state.HelpDisplayed) {
                return null;
            }

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

    public static void PrintHelp(
        string[] args,
        TextWriter output) {
        WithEnglishCli(() => {
            var state = new CommandParseState();
            var root = CreateRoot(state, output);
            root.Parse(args).Invoke(CreateInvocation(output));
            return 0;
        });
    }

    public static void PrintVersion(TextWriter output) {
        output.WriteLine(ProductVersion.Format(
            "arkt",
            typeof(CommandLine).Assembly));
        output.Flush();
    }

    public static bool TryPrintCompletion(
        string[] args,
        TextWriter output) {
        if (args.Length == 0 ||
            !args[0].StartsWith(
                "[suggest:",
                StringComparison.Ordinal)) {
            return false;
        }

        WithEnglishCli(() => {
            var state = new CommandParseState();
            var root = CreateRoot(state, output);
            root.Parse(args).Invoke(CreateInvocation(output));
            return 0;
        });
        return true;
    }

    private static RootCommand CreateRoot(
        CommandParseState state,
        TextWriter output) {
        var root = new RootCommand(
            "Flash and inspect Rockchip devices over RockUSB and ADB.");
        ServerCommands.AddTo(root, state);
        RockUsbCommands.AddTo(root, state);
        root.Add(AdbCommands.Create(state));
        root.Add(SerialCommands.Create(state));
        root.Add(ImageCommands.Create(state));
        root.Add(CompletionCommands.Create(state));
        ConfigureBareCommandHelp(root, state, output);
        return root;
    }

    private static void ConfigureBareCommandHelp(
        Command command,
        CommandParseState state,
        TextWriter output) {
        foreach (var subcommand in command.Subcommands) {
            ConfigureBareCommandHelp(subcommand, state, output);
        }

        if (command.Subcommands.Count == 0 ||
            command.Action is not null) {
            return;
        }

        // System.CommandLine rejects command groups without an action, so bare
        // groups need this help action. Explicit defaults such as "adb reboot"
        // are preserved by the guard above.
        command.SetAction(_ => {
            command.Parse(["--help"]).Invoke(CreateInvocation(output));
            state.HelpDisplayed = true;
            return 0;
        });
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

    private static InvocationConfiguration CreateInvocation(
        TextWriter output) =>
        new() {
            Output = output,
            EnableDefaultExceptionHandler = false
        };
}
