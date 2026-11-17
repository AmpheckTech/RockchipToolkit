
namespace Ampheck.RockchipToolkit.Firmware.Internal;

internal sealed class InputFile {
    private InputFile(
        string path
    ) {
        Path = path;
    }

    public string Path { get; }

    public static ValueTask<InputFile> OpenAsync(
        string location,
        CancellationToken cancellationToken = default
    ) {
        ArgumentException.ThrowIfNullOrWhiteSpace(location);
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(location)) {
            throw new FileNotFoundException(
                $"Input file '{location}' was not found.",
                location);
        }

        return ValueTask.FromResult(new InputFile(location));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
