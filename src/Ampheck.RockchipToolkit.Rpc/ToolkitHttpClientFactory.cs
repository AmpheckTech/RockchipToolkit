using System.Net;

namespace Ampheck.RockchipToolkit.Rpc;

public static class ToolkitHttpClientFactory {
    private static readonly HttpClient Proxied = Create(useProxy: true);
    private static readonly HttpClient Direct = Create(useProxy: false);

    public static HttpClient For(Uri uri) {
        ArgumentNullException.ThrowIfNull(uri);
        return ShouldBypassProxy(uri) ? Direct : Proxied;
    }

    private static bool ShouldBypassProxy(Uri uri) {
        if (uri.IsLoopback) {
            return true;
        }

        if (IPAddress.TryParse(uri.Host, out var address)) {
            return IsPrivateAddress(address);
        }

        return uri.Host.Equals(
                "localhost",
                StringComparison.OrdinalIgnoreCase) ||
            uri.Host.EndsWith(
                ".local",
                StringComparison.OrdinalIgnoreCase) ||
            !uri.Host.Contains('.');
    }

    private static HttpClient Create(bool useProxy) {
        var handler = new SocketsHttpHandler {
            UseProxy = useProxy,
            AutomaticDecompression =
                DecompressionMethods.GZip |
                DecompressionMethods.Deflate |
                DecompressionMethods.Brotli
        };
        var client = new HttpClient(handler) {
            Timeout = TimeSpan.FromMinutes(30)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Ampheck.RockchipToolkit/0.1");
        return client;
    }

    private static bool IsPrivateAddress(IPAddress address) {
        if (address.IsIPv4MappedToIPv6) {
            address = address.MapToIPv4();
        }

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily ==
            System.Net.Sockets.AddressFamily.InterNetwork) {
            return bytes[0] == 10 ||
                bytes[0] == 127 ||
                bytes[0] == 169 && bytes[1] == 254 ||
                bytes[0] == 172 && bytes[1] is >= 16 and <= 31 ||
                bytes[0] == 192 && bytes[1] == 168 ||
                bytes[0] == 100 && bytes[1] is >= 64 and <= 127;
        }

        return address.IsIPv6LinkLocal ||
            address.IsIPv6SiteLocal ||
            (bytes[0] & 0xFE) == 0xFC;
    }
}
