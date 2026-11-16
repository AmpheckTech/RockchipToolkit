using System.CommandLine;
using System.Reflection;

namespace Ampheck.RockchipToolkit.Arkt.Commands.Framework;

internal sealed class CommandParameter(
    MemberInfo? member,
    Type valueType,
    IReadOnlyList<string> names,
    CommandParamAttribute attribute,
    object? defaultValue,
    bool isArgument,
    int position
) {
    public MemberInfo? Member { get; } = member;
    public Type ValueType { get; } = valueType;
    public IReadOnlyList<string> Names { get; } = names;
    public CommandParamAttribute Attribute { get; } = attribute;
    public object? DefaultValue { get; } = defaultValue;
    public bool IsArgument { get; } = isArgument;
    public int Position { get; set; } = position;
    public Argument? Argument { get; set; }
    public Option? Option { get; set; }
}
