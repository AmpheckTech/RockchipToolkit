using System.CommandLine;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Ampheck.RockchipToolkit.Arkt.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Ampheck.RockchipToolkit.Arkt.Commands.Framework;

internal sealed partial class CommandParameterBinder {
    public async ValueTask<int> InvokeAsync(
        object command,
        ParseResult parsed,
        IServiceProvider services,
        CancellationToken cancellationToken
    ) {
        var arguments = new object?[_bindings.Count];
        foreach (var binding in _bindings) {
            arguments[binding.Index] = BindHandlerParameter(
                binding,
                parsed,
                services,
                cancellationToken);
        }

        object? result;
        try {
            result = _handler.Invoke(
                _handler.IsStatic ? null : command,
                arguments);
        } catch (TargetInvocationException error) when (
            error.InnerException is not null) {
            ExceptionDispatchInfo.Capture(error.InnerException).Throw();
            throw;
        }

        return await ((ValueTask<int>)result!).ConfigureAwait(false);
    }

    private static object? BindHandlerParameter(
        HandlerBinding binding,
        ParseResult parsed,
        IServiceProvider services,
        CancellationToken cancellationToken
    ) {
        return binding.Injection switch {
            Injection.InvocationContext =>
                services.GetRequiredService<CommandInvocationContext>(),
            Injection.CancellationToken => cancellationToken,
            Injection.ParseResult => parsed,
            _ when binding.GroupType is { } type =>
                BindCommandModel(type, binding.Members, parsed),
            _ => ReadDirectValue(binding.Members[0], parsed)
        };
    }

    private static object? ReadDirectValue(
        CommandParameter parameter,
        ParseResult parsed
    ) {
        object symbol = parameter.Argument is not null
            ? parameter.Argument
            : parameter.Option ??
                throw new InvalidOperationException(
                    "A command parameter has no CLI symbol.");
        return ReadValue(symbol, parameter, parsed);
    }

    private static object BindCommandModel(
        Type type,
        IReadOnlyList<CommandParameter> members,
        ParseResult parsed
    ) {
        var instance = CreateParameterInstance(type);
        foreach (var member in members) {
            object symbol = member.Argument is not null
                ? member.Argument
                : member.Option ??
                    throw new InvalidOperationException(
                        "A command parameter has no CLI symbol.");
            var value = ReadValue(
                symbol,
                member,
                parsed);
            SetMemberValue(instance, member.Member!, value);
        }

        return instance;
    }

    private static object? ReadValue(
        object symbol,
        CommandParameter parameter,
        ParseResult parsed
    ) {
        if (parsed.GetResult((Symbol)symbol) is null) {
            return parameter.DefaultValue;
        }

        var method = typeof(ParseResult)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Single(candidate =>
                candidate.Name == nameof(ParseResult.GetValue) &&
                candidate.IsGenericMethod &&
                candidate.GetParameters() is [var value] &&
                value.ParameterType.IsGenericType &&
                value.ParameterType.GetGenericTypeDefinition() ==
                    (parameter.IsArgument
                        ? typeof(Argument<>)
                        : typeof(Option<>)))
            .MakeGenericMethod(parameter.ValueType);
        return method.Invoke(parsed, [symbol]);
    }

    private static void SetMemberValue(
        object instance,
        MemberInfo member,
        object? value
    ) {
        switch (member) {
            case PropertyInfo property when property.CanWrite:
                if (property.SetMethod?.IsPublic != true) {
                    throw new InvalidOperationException(
                        $"Command parameter property '{property.Name}' " +
                        "must have a public setter.");
                }

                property.SetValue(instance, value);
                break;
            case FieldInfo field when !field.IsInitOnly:
                field.SetValue(instance, value);
                break;
            default:
                throw new InvalidOperationException(
                    $"Command parameter member '{member.Name}' is not writable.");
        }
    }
}
