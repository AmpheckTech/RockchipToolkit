using System.CommandLine;
using System.Globalization;
using Ampheck.RockchipToolkit.Cli.Infrastructure;

namespace Ampheck.RockchipToolkit.Cli.Commands;

internal sealed class CommandLine(TextWriter output) {
    public CliInvocation? Parse(string[] args) {
        return WithEnglishCli(() => {
            var state = new CommandParseState();
            var root = CreateRoot(state);
            state.Arguments = args;
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

            parsed.Invoke(CreateInvocation());
            if (state.HelpDisplayed) {
                return null;
            }

            if (state.Invocation is null) {
                throw new ArgumentException(
                    "A command is required. Use --help to list commands.");
            }

            return state.Invocation;
        });
    }

    public void PrintHelp(string[] args) {
        WithEnglishCli(() => {
            var state = new CommandParseState();
            var root = CreateRoot(state);
            root.Parse(args).Invoke(CreateInvocation());
            return 0;
        });
    }

    public void PrintVersion() {
        output.WriteLine(ProductVersion.Format(
            "arkt",
            typeof(CommandLine).Assembly));
        output.Flush();
    }

    public bool TryPrintCompletion(string[] args) {
        if (args.Length == 0 ||
            !args[0].StartsWith(
                "[suggest:",
                StringComparison.Ordinal)) {
            return false;
        }

        WithEnglishCli(() => {
            var state = new CommandParseState();
            var root = CreateRoot(state);
            root.Parse(args).Invoke(CreateInvocation());
            return 0;
        });
        return true;
    }

    private RootCommand CreateRoot(CommandParseState state) {
        var root = new RootCommand(
            "Flash and inspect Rockchip devices over RockUSB and ADB.");
        new ServerCommands(state).Register(root);
        new RockUsbCommands(state).Register(root);
        new AdbCommands(state).Register(root);
        new SerialCommands(state).Register(root);
        new ImageCommands(state).Register(root);
        new CompletionCommands(state).Register(root);
        ConfigureBareCommandHelp(root, state);
        return root;
    }

    private void ConfigureBareCommandHelp(
        Command command,
        CommandParseState state) {
        foreach (var subcommand in command.Subcommands) {
            ConfigureBareCommandHelp(subcommand, state);
        }

        if (command.Subcommands.Count == 0 ||
            command.Action is not null) {
            return;
        }

        // System.CommandLine rejects command groups without an action, so bare
        // groups need this help action. Explicit defaults such as "adb reboot"
        // are preserved by the guard above.
        command.SetAction(_ => {
            command.Parse(["--help"]).Invoke(CreateInvocation());
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

    private InvocationConfiguration CreateInvocation() =>
        new() {
            Output = output,
            EnableDefaultExceptionHandler = false
        };
}
