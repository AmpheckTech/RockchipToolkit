using System.CommandLine;

namespace Ampheck.RockchipToolkit.Cli.Commands;

internal static class CommandSuggestion {
    public static (string Token, string Suggestion)? Find(
        RootCommand root,
        IReadOnlyList<string> args) {
        var current = (Command)root;
        var commands = new List<Command> { current };
        for (var index = 0; index < args.Count; index++) {
            var token = args[index];
            if (token == "--") {
                return null;
            }

            if (token.StartsWith('-')) {
                var options = commands
                    .SelectMany(command => command.Options)
                    .SelectMany(GetNames)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (options.Any(option => option.Equals(
                        token,
                        StringComparison.Ordinal))) {
                    continue;
                }

                return FindNearest(token, options) is { } option
                    ? (token, option)
                    : null;
            }

            var child = current.Subcommands.FirstOrDefault(command =>
                GetNames(command).Any(name => name.Equals(
                    token,
                    StringComparison.Ordinal)));
            if (child is not null) {
                current = child;
                commands.Add(child);
                continue;
            }

            if (current.Subcommands.Count > 0) {
                var candidates = current.Subcommands
                    .SelectMany(GetNames)
                    .ToArray();
                if (FindNearest(token, candidates) is { } command) {
                    return (token, command);
                }

                var rootCommands = root.Subcommands
                    .Where(command => !commands.Contains(command))
                    .SelectMany(GetNames)
                    .ToArray();
                return FindNearest(token, rootCommands) is { } rootCommand
                        ? (token, $"arkt {rootCommand}")
                        : null;
            }
        }

        return null;
    }

    private static string? FindNearest(
        string token,
        IReadOnlyList<string> candidates) {
        var maximumDistance = Math.Clamp(
            token.Length / 3,
            1,
            3);
        return candidates
            .Select(candidate => (
                Candidate: candidate,
                Distance: Distance(
                    token.ToLowerInvariant(),
                    candidate.ToLowerInvariant())))
            .Where(value =>
                value.Distance <= maximumDistance)
            .OrderBy(value => value.Distance)
            .ThenBy(value => value.Candidate.Length)
            .ThenBy(value => value.Candidate)
            .Select(value => value.Candidate)
            .FirstOrDefault();
    }

    private static IEnumerable<string> GetNames(Command command) =>
        new[] { command.Name }
            .Concat(command.Aliases);

    private static IEnumerable<string> GetNames(Option option) =>
        new[] { option.Name }
            .Concat(option.Aliases);

    private static int Distance(
        string left,
        string right) {
        if (left.Length == right.Length &&
            IsAdjacentTransposition(left, right)) {
            return 1;
        }

        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];
        for (var index = 0; index <= right.Length; index++) {
            previous[index] = index;
        }

        for (var leftIndex = 1;
            leftIndex <= left.Length;
            leftIndex++) {
            current[0] = leftIndex;
            for (var rightIndex = 1;
                rightIndex <= right.Length;
                rightIndex++) {
                var substitution =
                    left[leftIndex - 1] == right[rightIndex - 1]
                        ? 0
                        : 1;
                current[rightIndex] = Math.Min(
                    Math.Min(
                        current[rightIndex - 1] + 1,
                        previous[rightIndex] + 1),
                    previous[rightIndex - 1] + substitution);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }

    private static bool IsAdjacentTransposition(
        string left,
        string right) {
        for (var index = 0;
            index < left.Length - 1;
            index++) {
            if (left[index] == right[index]) {
                continue;
            }

            return left[index] == right[index + 1] &&
                left[index + 1] == right[index] &&
                left[(index + 2)..] == right[(index + 2)..];
        }

        return false;
    }
}
