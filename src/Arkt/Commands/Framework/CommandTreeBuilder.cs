using System.CommandLine;

namespace Ampheck.RockchipToolkit.Arkt.Commands.Framework;

internal sealed class CommandTreeBuilder(
    CommandCatalog catalog,
    Action<
        CommandDescriptor,
        CommandParameterBinder,
        ParseResult> onInvocation
) {
    public Command Build(
        CommandDescriptor descriptor,
        InvocationConfiguration invocation
    ) {
        var binder = descriptor.Handler is null
            ? null
            : CommandParameterBinder.Create(descriptor.Handler);
        var command = new Command(
            descriptor.Name,
            descriptor.Description) {
            TreatUnmatchedTokensAsErrors =
                descriptor.TreatUnmatchedTokensAsErrors
        };

        foreach (var alias in descriptor.Aliases) {
            command.Aliases.Add(alias);
        }

        foreach (var argument in binder?.Arguments ?? []) {
            command.Add(argument);
        }

        foreach (var option in binder?.Options ?? []) {
            command.Add(option);
        }

        var subcommands = catalog.Children(descriptor)
            .Select(child => Build(child, invocation))
            .ToArray();
        ValidateSubcommands(subcommands);
        foreach (var subcommand in subcommands) {
            command.Add(subcommand);
        }

        if (binder is not null) {
            command.SetAction(parsed => {
                onInvocation(descriptor, binder, parsed);
                return 0;
            });
        } else {
            command.SetAction(_ => {
                command.Parse(["--help"]).Invoke(invocation);
                return 0;
            });
        }

        return command;
    }

    private static void ValidateSubcommands(
        IReadOnlyList<Command> commands
    ) {
        var names = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var command in commands) {
            if (!names.Add(command.Name)) {
                throw new InvalidOperationException(
                    $"CLI command name '{command.Name}' is used more " +
                    "than once.");
            }

            foreach (var alias in command.Aliases) {
                if (!names.Add(alias)) {
                    throw new InvalidOperationException(
                        $"CLI command alias '{alias}' is used more than " +
                        "once.");
                }
            }
        }
    }
}
