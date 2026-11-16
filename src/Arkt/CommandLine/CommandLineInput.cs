using Ampheck.RockchipToolkit.Arkt.Resources;
using Ampheck.RockchipToolkit.Arkt.Terminal;
using Ampheck.RockchipToolkit.Runtime.Terminal;

namespace Ampheck.RockchipToolkit.Arkt.CommandLine;

internal static class CommandLineInput {
    public static bool HasOption(
        IEnumerable<string> arguments,
        params string[] names
    ) {
        foreach (var argument in arguments) {
            if (argument == "--") {
                return false;
            }

            if (names.Contains(argument, StringComparer.OrdinalIgnoreCase)) {
                return true;
            }
        }

        return false;
    }

    public static bool IsHelpCommand(string argument) =>
        argument.Equals("help", StringComparison.OrdinalIgnoreCase) ||
        argument.Equals("h", StringComparison.OrdinalIgnoreCase);

    public static bool IsRootHelpOption(string argument) =>
        argument is "--help" or "-h" or "-?" or "/?";

    public static void WriteMotd(ConsoleRenderer console) {
        if (console.IsInteractive) {
            Motd.Write(Console.Out);
        }
    }
}
