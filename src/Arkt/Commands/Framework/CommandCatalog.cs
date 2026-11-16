using System.Reflection;

namespace Ampheck.RockchipToolkit.Arkt.Commands.Framework;

internal sealed class CommandCatalog {
    private readonly Dictionary<
        string,
        IReadOnlyList<CommandDescriptor>> _children;

    private CommandCatalog(
        IReadOnlyList<CommandDescriptor> commands,
        Dictionary<string, IReadOnlyList<CommandDescriptor>> children
    ) {
        _children = children;
        Roots = commands
            .Where(command => command.Path.Count == 1)
            .OrderBy(command => command.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<CommandDescriptor> Roots { get; }

    public static CommandCatalog Discover() {
        var commands = DiscoverDescriptors()
            .OrderBy(
                command => command.CommandName,
                StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var byPath = new Dictionary<
            string,
            CommandDescriptor>(StringComparer.OrdinalIgnoreCase);
        foreach (var command in commands) {
            if (!byPath.TryAdd(command.CommandName, command)) {
                throw new InvalidOperationException(
                    $"CLI command path '{command.CommandName}' is used " +
                    "more than once.");
            }
        }

        var children = commands
            .Where(command => command.Path.Count > 1)
            .GroupBy(
                command => ParentName(command.Path),
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<CommandDescriptor>)group
                    .OrderBy(
                        command => command.Name,
                        StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                StringComparer.OrdinalIgnoreCase);
        foreach (var pair in children) {
            if (!byPath.ContainsKey(pair.Key)) {
                throw new InvalidOperationException(
                    $"CLI command parent '{pair.Key}' was not found.");
            }
        }

        foreach (var command in commands) {
            if (!command.HasAction &&
                !children.ContainsKey(command.CommandName)) {
                throw new InvalidOperationException(
                    $"CLI command '{command.CommandName}' has no action " +
                    "and no subcommands.");
            }
        }

        return new CommandCatalog(
            byPath.Values.ToArray(),
            children);
    }

    public IReadOnlyList<CommandDescriptor> Children(
        CommandDescriptor command
    ) =>
        _children.TryGetValue(
            command.CommandName,
            out var children)
            ? children
            : [];

    private static IReadOnlyList<CommandDescriptor>
        DiscoverDescriptors() {
        var descriptors = new List<CommandDescriptor>();
        var groupDescriptors = new Dictionary<
            string,
            CommandDescriptor>(StringComparer.OrdinalIgnoreCase);
        foreach (var type in typeof(CommandCatalog).Assembly.GetTypes()) {
            var group = type.GetCustomAttribute<CommandGroupAttribute>(
                inherit: false);
            if (group is not null) {
                var descriptor = CreateGroupDescriptor(type, group);
                if (groupDescriptors.TryGetValue(
                        descriptor.CommandName,
                        out var existing) &&
                    !SameGroupMetadata(existing, descriptor)) {
                    throw new InvalidOperationException(
                        $"CLI command group '{descriptor.CommandName}' has " +
                        "conflicting metadata.");
                }

                groupDescriptors.TryAdd(
                    descriptor.CommandName,
                    descriptor);
            }

            foreach (var method in type.GetMethods(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly)) {
                var handler = method.GetCustomAttribute<
                    CommandHandlerAttribute>(inherit: false);
                if (handler is null) {
                    continue;
                }

                var descriptor = CreateHandlerDescriptor(
                    type,
                    method,
                    handler);
                if (group is not null &&
                    !HasPathPrefix(descriptor.Path, group.Path)) {
                    throw new InvalidOperationException(
                        $"CLI handler '{descriptor.CommandName}' is outside " +
                        $"declared command group '{string.Join(' ', group.Path)}'.");
                }

                descriptors.Add(descriptor);
            }
        }

        descriptors.AddRange(groupDescriptors.Values);
        return descriptors;
    }

    private static CommandDescriptor CreateGroupDescriptor(
        Type type,
        CommandGroupAttribute attribute
    ) {
        ValidatePath(type.FullName!, attribute.Path);
        return new CommandDescriptor(
            type,
            attribute.Path,
            attribute.Description,
            Aliases: [],
            TreatUnmatchedTokensAsErrors: true,
            Handler: null);
    }

    private static CommandDescriptor CreateHandlerDescriptor(
        Type type,
        MethodInfo method,
        CommandHandlerAttribute attribute
    ) {
        ValidatePath(type.FullName!, attribute.Path);
        return new CommandDescriptor(
            type,
            attribute.Path,
            attribute.Description,
            attribute.Aliases ?? [],
            attribute.TreatUnmatchedTokensAsErrors,
            method);
    }

    private static void ValidatePath(
        string owner,
        IReadOnlyList<string> path
    ) {
        if (path.Count == 0) {
            throw new InvalidOperationException(
                $"CLI command '{owner}' has an empty path.");
        }

        foreach (var segment in path) {
            if (string.IsNullOrWhiteSpace(segment) ||
                segment.Any(char.IsWhiteSpace)) {
                throw new InvalidOperationException(
                    $"CLI command '{owner}' contains an invalid " +
                    $"path segment '{segment}'.");
            }
        }
    }

    private static bool HasPathPrefix(
        IReadOnlyList<string> path,
        IReadOnlyList<string> prefix
    ) =>
        path.Count >= prefix.Count &&
        path.Take(prefix.Count).SequenceEqual(
            prefix,
            StringComparer.OrdinalIgnoreCase);

    private static bool SameGroupMetadata(
        CommandDescriptor left,
        CommandDescriptor right
    ) =>
        left.Description == right.Description &&
        left.Aliases.SequenceEqual(right.Aliases);

    private static string ParentName(
        IReadOnlyList<string> path
    ) =>
        string.Join(' ', path.Take(path.Count - 1));
}
