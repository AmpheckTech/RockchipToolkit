using System.CommandLine;
using Ampheck.RockchipToolkit.Cli.Inputs;
using Ampheck.RockchipToolkit.Cli.Terminal;

namespace Ampheck.RockchipToolkit.Cli.Commands;

internal sealed partial class CliCommandTree {
    private readonly ConsoleRenderer _console;
    private readonly InputResolver _inputs = new(ToolkitTransferLimits.Default);

    public CliCommandTree(ConsoleRenderer console) {
        _console = console;
        Root = new RootCommand(
            "Rockchip device command-line toolkit.");
        Verbose = new Option<bool>("--verbose", "-v") {
            Description = "Show detailed exception information.",
            Recursive = true
        };
        Root.Add(Verbose);
        Root.Add(BuildAssets());
        Root.Add(BuildCapability());
        Root.Add(BuildChip());
        Root.Add(BuildCompletion());
        Root.Add(BuildDevices());
        Root.Add(BuildErase());
        Root.Add(BuildFlash());
        Root.Add(BuildImage());
        Root.Add(BuildInfo());
        Root.Add(BuildLoader());
        Root.Add(BuildRead());
        Root.Add(BuildReset());
        Root.Add(BuildSerial());
        Root.Add(BuildStorage());
        Root.Add(BuildVerify());
        Root.Add(BuildWrite());
    }

    public RootCommand Root { get; }
    public Option<bool> Verbose { get; }

    private static Argument<T> CreateArgument<T>(
        string name,
        string description
    ) => new(name) {
        Description = description
    };

    private static Option<T> RequiredOption<T>(
        string name,
        string description
    ) => new(name) {
        Description = description,
        Required = true
    };

    private static Option<T> NumberOption<T>(
        string name,
        string alias,
        string description,
        bool required,
        Func<string, string, T> parse
    ) {
        var option = new Option<T>(name, alias) {
            Description = description,
            Required = required
        };
        option.CustomParser = result => parse(
            name,
            result.Tokens.Single().Value);
        return option;
    }

    private static Option<long?> OptionalLongOption(
        string name,
        string description
    ) {
        var option = new Option<long?>(name) {
            Description = description
        };
        option.CustomParser = result => Parsers.ValueParser.ParseOptionalInt64(
            name,
            result.Tokens.Count == 0
                ? null
                : result.Tokens.Single().Value);
        return option;
    }
}
