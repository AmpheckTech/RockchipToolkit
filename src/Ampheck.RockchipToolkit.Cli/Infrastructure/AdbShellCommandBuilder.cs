using System.Text;

namespace Ampheck.RockchipToolkit.Cli.Infrastructure;

internal static class AdbShellCommandBuilder {
    private const string SafeCharacters =
        "@%+=:,./_-";

    public static string FromArguments(
        IReadOnlyList<string> arguments) {
        var command = new StringBuilder();
        foreach (var argument in arguments) {
            if (command.Length > 0) {
                command.Append(' ');
            }

            command.Append(Quote(argument));
        }

        return command.ToString();
    }

    private static string Quote(string argument) {
        if (argument.Length == 0) {
            return "''";
        }

        if (argument.All(character =>
                char.IsAsciiLetterOrDigit(character) ||
                SafeCharacters.Contains(character))) {
            return argument;
        }

        return $"'{argument.Replace(
            "'",
            "'\\''",
            StringComparison.Ordinal)}'";
    }
}
