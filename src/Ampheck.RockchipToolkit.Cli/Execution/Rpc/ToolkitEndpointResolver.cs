using System.Net;
using System.Net.Sockets;
using Ampheck.RockchipToolkit.Cli.Commands;
using Ampheck.RockchipToolkit.Rpc;

namespace Ampheck.RockchipToolkit.Cli.Execution.Rpc;

internal static class ToolkitEndpointResolver {
    private const string LocalEndpoint = "127.0.0.1:5070";

    public static async Task<ToolkitEndpoint?> ResolveAsync(
        CommandOptions options,
        CancellationToken cancellationToken) {
        var requested = options.ServerEndpoint ?? Environment.GetEnvironmentVariable("ARKT_SERVER");
        if (!string.IsNullOrWhiteSpace(requested)) {
            var endpoint = ToolkitEndpoint.Parse(requested);
            if (!await IsReachableAsync(endpoint, cancellationToken).ConfigureAwait(false)) {
                throw new InvalidOperationException($"Toolkit server {endpoint} is not reachable.");
            }

            return endpoint;
        }

        var local = ToolkitEndpoint.Parse(LocalEndpoint);
        return await IsReachableAsync(local, cancellationToken).ConfigureAwait(false)
            ? local
            : null;
    }

    private static async Task<bool> IsReachableAsync(ToolkitEndpoint endpoint, CancellationToken cancellationToken) {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMilliseconds(250));
        IPAddress[] addresses;
        try {
            addresses = IPAddress.TryParse(
                    endpoint.Host,
                    out var address)
                ? [address]
                : await Dns.GetHostAddressesAsync(
                        endpoint.Host,
                        timeout.Token)
                    .ConfigureAwait(false);
        } catch (Exception error) when (
            error is SocketException or
            OperationCanceledException) {
            return false;
        }

        foreach (var address in addresses) {
            using var client = new TcpClient(address.AddressFamily);
            try {
                await client.ConnectAsync(
                        address,
                        endpoint.Port,
                        timeout.Token)
                    .ConfigureAwait(false);
                return true;
            } catch (Exception error) when (
                error is SocketException or
                OperationCanceledException) {
            }
        }

        return false;
    }
}
