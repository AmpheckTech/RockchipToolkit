using System.CommandLine;
using System.CommandLine.Parsing;
using System.Reflection;
using Ampheck.RockchipToolkit.Arkt.Commands.Framework.Parser;

namespace Ampheck.RockchipToolkit.Arkt.Commands.Framework;

internal sealed partial class CommandParameterBinder {
    private void CreateSymbols() {
        var arguments = _parameters
            .Where(parameter => parameter.IsArgument)
            .OrderBy(parameter => parameter.Position)
            .Select(CreateArgument)
            .ToArray();
        var options = _parameters
            .Where(parameter => !parameter.IsArgument)
            .OrderBy(
                parameter => parameter.Names[0],
                StringComparer.OrdinalIgnoreCase)
            .Select(CreateOption)
            .ToArray();
        Arguments = arguments;
        Options = options;
    }

    private static Argument CreateArgument(CommandParameter parameter) {
        var argumentType = typeof(Argument<>)
            .MakeGenericType(parameter.ValueType);
        var argument = (Argument)Activator.CreateInstance(
            argumentType,
            parameter.Names[0])!;
        argument.Description = parameter.Attribute.Description;
        argument.Arity = new ArgumentArity(
            parameter.Attribute.MinimumArity,
            Math.Min(
                parameter.Attribute.MaximumArity,
                ArgumentArity.ZeroOrMore.MaximumNumberOfValues));
        ConfigureValueSymbol(argument, parameter);
        parameter.Argument = argument;
        return argument;
    }

    private static Option CreateOption(CommandParameter parameter) {
        var optionType = typeof(Option<>)
            .MakeGenericType(parameter.ValueType);
        var aliases = parameter.Names.Skip(1).ToArray();
        var option = (Option)Activator.CreateInstance(
            optionType,
            parameter.Names[0],
            aliases)!;
        option.Description = parameter.Attribute.Description;
        option.Required = parameter.Attribute.Required;
        ConfigureValueSymbol(option, parameter);
        parameter.Option = option;
        return option;
    }

    private static void ConfigureValueSymbol(
        object symbol,
        CommandParameter parameter
    ) {
        if (parameter.Attribute.Choices is { Length: > 0 } choices) {
            if (parameter.IsArgument) {
                typeof(ArgumentValidation)
                    .GetMethod(
                        "AcceptOnlyFromAmong",
                        BindingFlags.Static |
                        BindingFlags.Public)!
                    .MakeGenericMethod(parameter.ValueType)
                    .Invoke(null, [symbol, choices]);
            } else {
                symbol.GetType()
                    .GetMethod(
                        "AcceptOnlyFromAmong",
                        BindingFlags.Instance |
                        BindingFlags.Public)!
                    .Invoke(symbol, [choices]);
            }
        }

        if (parameter.Attribute.Parser is null) {
            return;
        }

        var parser = Activator.CreateInstance(
            parameter.Attribute.Parser) ??
            throw new InvalidOperationException(
                $"Unable to create parser '{parameter.Attribute.Parser}'.");
        var customParser = CreateCustomParser(
            parameter.ValueType,
            parser,
            parameter.Names[0]);
        symbol.GetType()
            .GetProperty("CustomParser")!
            .SetValue(symbol, customParser);
    }

    private static Delegate CreateCustomParser(
        Type valueType,
        object parser,
        string name
    ) {
        var method = typeof(CommandParameterBinder)
            .GetMethod(
                nameof(CreateArgumentParser),
                BindingFlags.Static |
                BindingFlags.NonPublic)!
            .MakeGenericMethod(valueType);
        return (Delegate)method.Invoke(null, [parser, name])!;
    }

    private static Func<ArgumentResult, T> CreateArgumentParser<T>(
        object parser,
        string name
    ) {
        var typed = (ICommandValueParser<T>)parser;
        return result => typed.Parse(name, GetToken(result.Tokens));
    }

    private static string GetToken(
        IReadOnlyList<Token> tokens
    ) {
        if (tokens.Count != 1) {
            throw new ArgumentException(
                "The command parameter expects exactly one value.");
        }

        return tokens[0].Value;
    }
}
