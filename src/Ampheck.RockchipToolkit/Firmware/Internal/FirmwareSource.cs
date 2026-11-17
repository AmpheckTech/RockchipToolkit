
namespace Ampheck.RockchipToolkit.Firmware.Internal;

internal sealed class FirmwareSource {
    private readonly string _location;

    private FirmwareSource(string location) {
        _location = location;
    }

    public static FirmwareSource Create(string location) {
        ArgumentException.ThrowIfNullOrWhiteSpace(location);
        if (!Directory.Exists(location)) {
            throw new DirectoryNotFoundException(
                $"Firmware directory '{location}' was not found.");
        }

        return new FirmwareSource(location);
    }

    public string GetLocation(string fileName) =>
        Path.Combine(_location, fileName);

    public async ValueTask<InputFile?> TryOpenAsync(
        string fileName,
        CancellationToken cancellationToken = default
    ) {
        try {
            return await InputFile.OpenAsync(
                    GetLocation(fileName),
                    cancellationToken)
                .ConfigureAwait(false);
        } catch (FileNotFoundException) {
            return null;
        }
    }
}
