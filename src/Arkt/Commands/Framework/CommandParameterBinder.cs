using System.CommandLine;
using System.Reflection;

namespace Ampheck.RockchipToolkit.Arkt.Commands.Framework;

internal sealed partial class CommandParameterBinder {
    private readonly MethodInfo _handler;
    private readonly IReadOnlyList<HandlerBinding> _bindings;
    private readonly IReadOnlyList<CommandParameter> _parameters;

    private CommandParameterBinder(
        MethodInfo handler,
        IReadOnlyList<HandlerBinding> bindings,
        IReadOnlyList<CommandParameter> parameters
    ) {
        _handler = handler;
        _bindings = bindings;
        _parameters = parameters;
    }

    public IReadOnlyList<Argument> Arguments { get; private set; } = [];

    public IReadOnlyList<Option> Options { get; private set; } = [];

    public static CommandParameterBinder Create(MethodInfo handler) {
        if (handler.ReturnType != typeof(ValueTask<int>)) {
            throw new InvalidOperationException(
                $"Command handler '{handler.Name}' must return " +
                $"{nameof(ValueTask<int>)}.");
        }

        var bindings = new List<HandlerBinding>();
        var parameters = new List<CommandParameter>();
        foreach (var parameter in handler.GetParameters()) {
            var binding = CreateBinding(
                parameter,
                bindings.Count,
                parameters);
            bindings.Add(binding);
        }

        AssignArgumentPositions(parameters);
        ValidateParameterNames(parameters);
        var binder = new CommandParameterBinder(
            handler,
            bindings,
            parameters);
        binder.CreateSymbols();
        return binder;
    }

    private static HandlerBinding CreateBinding(
        ParameterInfo parameter,
        int index,
        List<CommandParameter> parameters
    ) {
        var injection = GetInjection(parameter.ParameterType);
        if (injection != Injection.None) {
            if (parameter.GetCustomAttribute<CommandParamAttribute>() is not null) {
                throw new InvalidOperationException(
                    $"Handler parameter '{parameter.Name}' is both injected " +
                    "and declared as a command parameter.");
            }

            return new HandlerBinding(
                index,
                injection,
                GroupType: null,
                Members: []);
        }

        var attribute = parameter.GetCustomAttribute<CommandParamAttribute>() ??
            throw new InvalidOperationException(
                $"Handler parameter '{parameter.Name}' must declare " +
                $"{nameof(CommandParamAttribute)}.");
        if (attribute.Names.Count == 0 &&
            HasCommandMembers(parameter.ParameterType)) {
            ValidateParameterModel(parameter.ParameterType);
            var instance = CreateParameterInstance(parameter.ParameterType);
            var members = GetCommandMembers(parameter.ParameterType)
                .Select(member => CreateParameter(
                    parameter,
                    member,
                    member.GetCustomAttribute<CommandParamAttribute>()!,
                    GetMemberValue(instance, member)))
                .ToArray();
            parameters.AddRange(members);
            return new HandlerBinding(
                index,
                Injection.None,
                parameter.ParameterType,
                members);
        }

        var defaultValue = parameter.HasDefaultValue
            ? parameter.DefaultValue
            : DefaultValue(parameter.ParameterType);
        var commandParameter = CreateParameter(
            parameter,
            member: null,
            attribute,
            defaultValue);
        parameters.Add(commandParameter);
        return new HandlerBinding(
            index,
            Injection.None,
            GroupType: null,
            [commandParameter]);
    }

    private static CommandParameter CreateParameter(
        ParameterInfo handlerParameter,
        MemberInfo? member,
        CommandParamAttribute attribute,
        object? defaultValue
    ) {
        var valueType = member switch {
            PropertyInfo property => property.PropertyType,
            FieldInfo field => field.FieldType,
            _ => handlerParameter.ParameterType
        };
        var sourceName = member?.Name ?? handlerParameter.Name ??
            throw new InvalidOperationException(
                "A command parameter has no source name.");
        var names = attribute.Names.Count > 0
            ? attribute.Names
            : [$"--{ToKebabCase(sourceName)}"];
        var isArgument = attribute.Position >= 0 ||
            !names[0].StartsWith('-');
        if (isArgument && names.Count != 1) {
            throw new InvalidOperationException(
                $"Command argument '{sourceName}' must have exactly one name.");
        }

        return new CommandParameter(
            member,
            valueType,
            names,
            attribute,
            defaultValue,
            isArgument,
            attribute.Position);
    }

    private static bool HasCommandMembers(Type type) =>
        GetCommandMembers(type).Any();

    private static IEnumerable<MemberInfo> GetCommandMembers(Type type) =>
        type.GetMembers(
                BindingFlags.Instance |
                BindingFlags.Public)
            .Where(member =>
                member is PropertyInfo or FieldInfo &&
                member.GetCustomAttribute<CommandParamAttribute>() is not null)
            .OrderBy(member => member.MetadataToken);

    private static void ValidateParameterModel(Type type) {
        foreach (var member in type.GetMembers(
            BindingFlags.Instance |
            BindingFlags.Public)) {
            if (member is not (PropertyInfo or FieldInfo) ||
                member.GetCustomAttribute<CommandParamAttribute>() is not null) {
                continue;
            }

            throw new InvalidOperationException(
                $"Command parameter member '{type.FullName}.{member.Name}' " +
                $"must declare {nameof(CommandParamAttribute)}.");
        }
    }

    private static object CreateParameterInstance(Type type) {
        try {
            return Activator.CreateInstance(type) ??
                throw new InvalidOperationException(
                    $"Unable to create command parameter model '{type}'.");
        } catch (MissingMethodException error) {
            throw new InvalidOperationException(
                $"Command parameter model '{type}' must have a public " +
                "parameterless constructor.",
                error);
        }
    }

    private static object? GetMemberValue(
        object instance,
        MemberInfo member
    ) =>
        member switch {
            PropertyInfo property => property.GetValue(instance),
            FieldInfo field => field.GetValue(instance),
            _ => throw new InvalidOperationException(
                $"Command parameter member '{member.Name}' is invalid.")
        };

    private static object? DefaultValue(Type type) =>
        type.IsValueType
            ? Activator.CreateInstance(type)
            : type.IsArray
                ? Array.CreateInstance(
                    type.GetElementType()!,
                    length: 0)
                : null;

    private static Injection GetInjection(Type type) {
        if (type == typeof(
            Support.CommandInvocationContext)) {
            return Injection.InvocationContext;
        }

        if (type == typeof(CancellationToken)) {
            return Injection.CancellationToken;
        }

        if (type == typeof(ParseResult)) {
            return Injection.ParseResult;
        }

        return Injection.None;
    }

    private static void AssignArgumentPositions(
        IReadOnlyList<CommandParameter> parameters
    ) {
        var arguments = parameters
            .Where(parameter => parameter.IsArgument)
            .ToArray();
        var positions = new HashSet<int>();
        var nextPosition = 0;
        foreach (var argument in arguments) {
            var position = argument.Position;
            if (position < 0) {
                while (!positions.Add(nextPosition)) {
                    nextPosition++;
                }

                argument.Position = nextPosition++;
            } else if (!positions.Add(position)) {
                throw new InvalidOperationException(
                    $"Command argument position {position} is used more " +
                    "than once.");
            }
        }
    }

    private static void ValidateParameterNames(
        IReadOnlyList<CommandParameter> parameters
    ) {
        var names = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in parameters) {
            foreach (var name in parameter.Names) {
                if (!names.Add(name)) {
                    throw new InvalidOperationException(
                        $"CLI parameter name '{name}' is used more than once.");
                }
            }
        }
    }

    private static string ToKebabCase(string value) {
        var result = new System.Text.StringBuilder(value.Length + 4);
        for (var index = 0; index < value.Length; index++) {
            var character = value[index];
            if (char.IsUpper(character) &&
                index > 0 &&
                result[^1] != '-') {
                result.Append('-');
            }

            result.Append(char.ToLowerInvariant(character));
        }

        return result.ToString();
    }

}
