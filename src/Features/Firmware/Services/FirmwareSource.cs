using Ampheck.RockchipToolkit.Features.Firmware.Model;

namespace Ampheck.RockchipToolkit.Features.Firmware.Services;

internal sealed class FirmwareSource {
    private readonly string _location;
    private readonly Uri? _baseUri;

    private FirmwareSource(
        string location,
        Uri? baseUri
    ) {
        _location = location;
        _baseUri = baseUri;
    }

    public static FirmwareSource Create(string location) {
        ArgumentException.ThrowIfNullOrWhiteSpace(location);
        if (WebUri.TryGetHttp(location, out var uri)) {
            return new FirmwareSource(
                location,
                WebUri.AsDirectory(uri));
        }

        if (!Directory.Exists(location)) {
            throw new DirectoryNotFoundException(
                $"Firmware directory '{location}' was not found.");
        }

        return new FirmwareSource(location, null);
    }

    public string GetLocation(string fileName) {
        return _baseUri is null
            ? Path.Combine(_location, fileName)
            : new Uri(_baseUri, fileName).AbsoluteUri;
    }

    public async ValueTask<InputFile?> TryOpenAsync(
        string fileName,
        InputFileContext context,
        CancellationToken cancellationToken = default
    ) {
        try {
            return await InputFile.OpenAsync(
                    GetLocation(fileName),
                    context,
                    cancellationToken)
                .ConfigureAwait(false);
        } catch (FileNotFoundException) {
            return null;
        } catch (HttpRequestException error) when (
            error.StatusCode == System.Net.HttpStatusCode.NotFound) {
            return null;
        }
    }
}
