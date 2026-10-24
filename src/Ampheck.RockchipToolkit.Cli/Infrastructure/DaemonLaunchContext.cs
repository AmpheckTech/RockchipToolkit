namespace Ampheck.RockchipToolkit.Cli.Infrastructure;

internal sealed record DaemonLaunchContext(
    string[] Arguments,
    string? LogPath,
    string? StatePath) {
    private const string Marker = "--daemon";
    private const string Separator = "--";

    public static DaemonLaunchContext Parse(string[] args) {
        if (args.Length == 0 ||
            !args[0].Equals(Marker, StringComparison.Ordinal)) {
            return new DaemonLaunchContext(args, null, null);
        }

        var separator = Array.IndexOf(args, Separator, 1);
        if (separator < 0) {
            throw new ArgumentException(
                "The daemon launch context has no command separator.");
        }

        string? logPath = null;
        string? statePath = null;
        for (var index = 1; index < separator; index++) {
            switch (args[index]) {
                case "--log":
                    logPath = ReadValue(
                        args,
                        ref index,
                        separator,
                        "--log");
                    break;
                case "--state":
                    statePath = ReadValue(
                        args,
                        ref index,
                        separator,
                        "--state");
                    break;
                default:
                    throw new ArgumentException(
                        $"Unknown daemon launch argument '{args[index]}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(logPath) ||
            string.IsNullOrWhiteSpace(statePath)) {
            throw new ArgumentException(
                "The daemon launch context requires log and state paths.");
        }

        return new DaemonLaunchContext(
            args[(separator + 1)..],
            logPath,
            statePath);
    }

    private static string ReadValue(
        string[] args,
        ref int index,
        int end,
        string option) {
        if (++index >= end) {
            throw new ArgumentException(
                $"Daemon launch option {option} requires a value.");
        }

        return args[index];
    }
}
