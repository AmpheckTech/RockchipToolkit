using Ampheck.RockchipToolkit.Cli.Commands;
using Ampheck.RockchipToolkit.Rpc;
using Ampheck.RockchipToolkit.Rpc.JsonRpc;

namespace Ampheck.RockchipToolkit.Cli.Execution.Rpc;

internal static class ToolkitRpcPayloads {
    public static DeviceTarget CreateTarget(
        DeviceTargetArguments options) => new() {
            Transport = options.Transport switch {
                TransportKind.RockUsb => ToolkitTransport.RockUsb,
                TransportKind.LibUsb => ToolkitTransport.LibUsb,
                _ => ToolkitTransport.Auto
            },
            VendorId = options.VendorId,
            ProductId = options.ProductId
        };

    public static async ValueTask<PayloadReference>
        PrepareLoaderAsync(
            CliRpcSession session,
            LoaderArguments loader,
            CancellationToken cancellationToken) {
        return string.IsNullOrWhiteSpace(loader.Loader)
            ? new PayloadReference(
                BlobId: null,
                Url: null,
                ServerPath: null)
            : await session.Payload.PrepareFileAsync(
                    loader.Loader,
                    loader.FetchLocation,
                    cancellationToken)
                .ConfigureAwait(false);
    }
}
