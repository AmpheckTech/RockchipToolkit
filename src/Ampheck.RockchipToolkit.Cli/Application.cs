using System.CommandLine;
using System.Globalization;
using Ampheck.RockchipToolkit.Cli.Commands;
using Ampheck.RockchipToolkit.Cli.Terminal;

namespace Ampheck.RockchipToolkit.Cli;

internal static class Application {
    public static async Task<int> Main(string[] args) {
        using var console = new ConsoleRenderer();
        try {
            var tree = new CliCommandTree(console);
            if (args.Length == 0) {
                WriteMotd(console);
                return await tree.Root.Parse(["--help"])
                    .InvokeAsync(CreateInvocation())
                    .ConfigureAwait(false);
            }

            if (args is ["--version"]) {
                WriteMotd(console);
                PrintVersion();
                return 0;
            }

            var parsed = tree.Root.Parse(args);
            var verbose = parsed.GetValue(tree.Verbose);
            try {
                return await parsed.InvokeAsync(CreateInvocation())
                    .ConfigureAwait(false);
            } catch (Exception error) {
                Console.Error.WriteLine(
                    verbose
                        ? error.ToString()
                        : $"error: {error.Message}");
                return 1;
            }
        } catch (Exception error) {
            Console.Error.WriteLine($"error: {error.Message}");
            return 1;
        }
    }

    private static InvocationConfiguration CreateInvocation() => new() {
        Output = Console.Out,
        EnableDefaultExceptionHandler = false
    };

    private static void PrintVersion() {
        var version = typeof(Application).Assembly
            .GetName().Version?.ToString(3) ?? "unknown";
        Console.Out.WriteLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"arkt {version}"));
    }

    private const string MotdText = """
         _    __  __ ____  _   _ _____ ____ _  __
        / \  |  \/  |  _ \| | | | ____/ ___| |/ /
       / _ \ | |\/| | |_) | |_| |  _|| |   | ' /
      / ___ \| |  | |  __/|  _  | |__| |___| . \
     /_/   \_\_|  |_|_|   |_| |_|_____\____|_|\_\
                     ROCKCHIP TOOLKIT
    """;

    private static void WriteMotd(ConsoleRenderer console) {
        if (console.IsInteractive) {
            Console.Out.WriteLine(MotdText);
            Console.Out.WriteLine();
        }
    }
}
