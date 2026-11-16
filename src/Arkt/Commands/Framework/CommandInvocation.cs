namespace Ampheck.RockchipToolkit.Arkt.Commands.Framework;

internal sealed record CommandInvocation(
    CommandDescriptor Command,
    CommandParameterBinder Binder,
    System.CommandLine.ParseResult ParseResult
);
