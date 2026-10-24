namespace Ampheck.RockchipToolkit.Cli.Presentation;

internal sealed class DaemonLogFile : IDisposable {
    private readonly StreamWriter? _writer;

    private DaemonLogFile(StreamWriter? writer) {
        _writer = writer;
    }

    public static DaemonLogFile Open(
        string? path) {
        if (string.IsNullOrWhiteSpace(path)) {
            return new DaemonLogFile(writer: null);
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory)) {
            Directory.CreateDirectory(directory);
        }

        var stream = new FileStream(
            path,
            FileMode.Append,
            FileAccess.Write,
            FileShare.ReadWrite);
        var writer = new StreamWriter(stream) {
            AutoFlush = true
        };
        Console.SetOut(writer);
        Console.SetError(writer);
        return new DaemonLogFile(writer);
    }

    public void Dispose() {
        _writer?.Dispose();
    }
}
