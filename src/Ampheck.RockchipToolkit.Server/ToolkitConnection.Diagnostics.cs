using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Server;

/// <summary>Provides operation diagnostics and client metadata normalization.</summary>
internal sealed partial class ToolkitConnection : IAsyncDisposable {
    private void LogOperationFailure(
        Exception error,
        ToolkitChannel channel,
        string message) {
        if (error is IOException or
            InvalidOperationException or
            InvalidDataException or
            ArgumentException or
            TimeoutException or
            UnauthorizedAccessException or
            NotSupportedException or
            HttpRequestException) {
            _logger.LogWarning(
                "{Message}: client={ClientName} channel={ChannelId} " +
                "operation={Operation} target={Target} error={ErrorMessage}",
                message,
                _clientName,
                channel.ChannelId,
                ToolkitOperationNames.GetName(
                    channel.Request.Operation),
                DescribeTarget(channel.Request),
                error.Message);
            return;
        }

        _logger.LogError(
            error,
            "{Message}: client={ClientName} channel={ChannelId} " +
            "operation={Operation} target={Target}",
            message,
            _clientName,
            channel.ChannelId,
            ToolkitOperationNames.GetName(
                channel.Request.Operation),
            DescribeTarget(channel.Request));
    }

    private static string DescribeTarget(
        ToolkitRequest request) {
        if (!string.IsNullOrWhiteSpace(request.SerialPort)) {
            return request.SerialPort;
        }

        if (!string.IsNullOrWhiteSpace(request.Source)) {
            return DescribeLocation(request.Source);
        }

        if (!string.IsNullOrWhiteSpace(request.File)) {
            return DescribeLocation(request.File);
        }

        if (request.VendorId.HasValue ||
            request.ProductId.HasValue) {
            return $"{request.VendorId ?? 0:X4}:" +
                $"{request.ProductId ?? 0:X4}";
        }

        return "-";
    }

    private static string DescribeLocation(string location) =>
        Uri.TryCreate(location, UriKind.Absolute, out var uri) &&
        uri.Scheme is "http" or "https"
            ? uri.GetLeftPart(UriPartial.Path)
            : location;

    private static string NormalizeClientName(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return "unknown";
        }

        var characters = value
            .Take(80)
            .Select(character =>
                char.IsControl(character) ? ' ' : character)
            .ToArray();
        var normalized = new string(characters).Trim();
        return normalized.Length == 0 ? "unknown" : normalized;
    }
}
