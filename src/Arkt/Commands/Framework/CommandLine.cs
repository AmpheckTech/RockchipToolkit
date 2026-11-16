using System.CommandLine;
using System.Globalization;
using System.Reflection;

namespace Ampheck.RockchipToolkit.Arkt.Commands.Framework;

internal sealed class CommandLine(TextWriter output) {
    private static readonly CommandCatalog Catalog =
        CommandCatalog.Discover();

    public CommandInvocation? Parse(string[] args) {
        return WithEnglishCli(() => {
            var invocation = CreateInvocation();
            CommandInvocation? result = null;
            var root = CreateRoot(
                invocation,
                (command, binder, parsed) => result = new CommandInvocation(
                    command,
                    binder,
                    parsed));
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

            parsed.Invoke(invocation);
            return result;
        });
    }

    public void PrintHelp(string[] args) {
        WithEnglishCli(() => {
            var invocation = CreateInvocation();
            var root = CreateRoot(invocation, (_, _, _) => { });
            root.Parse(args).Invoke(invocation);
            return 0;
        });
    }

    public void PrintVersion() {
        var assembly = typeof(CommandLine).Assembly;
        var version = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ??
            assembly.GetName().Version?.ToString(3) ??
            "unknown";
        var separator = version.IndexOf('+');
        if (separator >= 0 && separator < version.Length - 1) {
            var revision = version[(separator + 1)..];
            if (revision.Length > 12) {
                revision = revision[..12];
            }

            version = $"{version[..separator]}-{revision}";
        }

        output.WriteLine($"arkt {version}");
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
            var invocation = CreateInvocation();
            var root = CreateRoot(invocation, (_, _, _) => { });
            root.Parse(args).Invoke(invocation);
            return 0;
        });
        return true;
    }

    private RootCommand CreateRoot(
        InvocationConfiguration invocation,
        Action<
            CommandDescriptor,
            CommandParameterBinder,
            ParseResult> onInvocation
    ) {
        var root = new RootCommand("Rockchip device command-line toolkit.");
        var builder = new CommandTreeBuilder(
            Catalog,
            onInvocation);
        foreach (var descriptor in Catalog.Roots) {
            root.Add(builder.Build(descriptor, invocation));
        }

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

    private InvocationConfiguration CreateInvocation() =>
        new() {
            Output = output,
            EnableDefaultExceptionHandler = false
        };
}
