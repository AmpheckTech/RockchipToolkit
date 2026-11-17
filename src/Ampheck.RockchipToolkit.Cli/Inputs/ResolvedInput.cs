namespace Ampheck.RockchipToolkit.Cli.Inputs;

internal sealed class ResolvedInput(
    string path,
    bool temporary
) : IAsyncDisposable {
    public string Path { get; } = path;

    public ValueTask DisposeAsync() {
        if (temporary) {
            TryDelete(Path);
        }

        return ValueTask.CompletedTask;
    }

    private static void TryDelete(string path) {
        try {
            File.Delete(path);
        } catch (Exception error) when (
            error is IOException or UnauthorizedAccessException) {
        }
    }
}
