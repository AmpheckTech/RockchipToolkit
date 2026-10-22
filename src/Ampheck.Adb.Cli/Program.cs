namespace Ampheck.Adb.Cli;

internal static class Program {
    public static Task<int> Main(string[] args) {
        return AdbCliApplication.RunAsync(args);
    }
}
