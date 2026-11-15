using Ampheck.RockchipToolkit.Cli.Commands;
using Ampheck.RockchipToolkit.Cli.Presentation;
using Ampheck.RockchipToolkit.Rpc.JsonRpc;

namespace Ampheck.RockchipToolkit.Cli.Execution.Rpc;

internal sealed class DeviceCommandRunner(CliCommandContext context) {
    public async Task<int> RunDevicesAsync(
        DeviceTargetArguments target,
        CancellationToken cancellationToken = default) {
        await using var session = context.OpenRpcSession();
        var result = await session.Client.ListDevicesAsync(
                new DeviceListParams {
                    Target = ToolkitRpcPayloads.CreateTarget(target)
                },
                session.Report,
                cancellationToken)
            .ConfigureAwait(false);
        if (result.Devices.Length > 0) {
            Console.WriteLine(
                RpcOutputFormatter.FormatDevices(result));
        }

        return 0;
    }

    public async Task<int> RunInfoAsync(
        DeviceQueryArguments options,
        CancellationToken cancellationToken = default) {
        await using var session = context.OpenRpcSession();
        var loader = await ToolkitRpcPayloads.PrepareLoaderAsync(
                session,
                options.Loader,
                cancellationToken)
            .ConfigureAwait(false);
        var result = await session.Client.GetDeviceInfoAsync(
                new DeviceInfoParams {
                    Target = ToolkitRpcPayloads.CreateTarget(
                        options.Target),
                    LoaderBlobId = loader.BlobId,
                    LoaderUrl = loader.Url,
                    LoaderServerPath = loader.ServerPath
                },
                session.Report,
                cancellationToken)
            .ConfigureAwait(false);
        Console.WriteLine(
            RpcOutputFormatter.FormatInfo(result));
        return 0;
    }

    public async Task<int> RunChipInfoAsync(
        DeviceQueryArguments options,
        CancellationToken cancellationToken = default) {
        await using var session = context.OpenRpcSession();
        var loader = await ToolkitRpcPayloads.PrepareLoaderAsync(
                session,
                options.Loader,
                cancellationToken)
            .ConfigureAwait(false);
        var result = await session.Client.GetChipInfoAsync(
                new DeviceChipInfoParams {
                    Target = ToolkitRpcPayloads.CreateTarget(
                        options.Target),
                    LoaderBlobId = loader.BlobId,
                    LoaderUrl = loader.Url,
                    LoaderServerPath = loader.ServerPath
                },
                session.Report,
                cancellationToken)
            .ConfigureAwait(false);
        Console.WriteLine(
            RpcOutputFormatter.FormatChipInfo(result));
        return 0;
    }

    public async Task<int> RunChipIdAsync(
        DeviceQueryArguments options,
        CancellationToken cancellationToken = default) {
        await using var session = context.OpenRpcSession();
        var loader = await ToolkitRpcPayloads.PrepareLoaderAsync(
                session,
                options.Loader,
                cancellationToken)
            .ConfigureAwait(false);
        var result = await session.Client.GetChipIdAsync(
                new DeviceChipIdParams {
                    Target = ToolkitRpcPayloads.CreateTarget(
                        options.Target),
                    LoaderBlobId = loader.BlobId,
                    LoaderUrl = loader.Url,
                    LoaderServerPath = loader.ServerPath
                },
                session.Report,
                cancellationToken)
            .ConfigureAwait(false);
        Console.WriteLine(result.ChipId);
        return 0;
    }

    public async Task<int> RunCapabilityAsync(
        DeviceQueryArguments options,
        CancellationToken cancellationToken = default) {
        await using var session = context.OpenRpcSession();
        var loader = await ToolkitRpcPayloads.PrepareLoaderAsync(
                session,
                options.Loader,
                cancellationToken)
            .ConfigureAwait(false);
        var result = await session.Client.GetCapabilitiesAsync(
                new DeviceCapabilityParams {
                    Target = ToolkitRpcPayloads.CreateTarget(
                        options.Target),
                    LoaderBlobId = loader.BlobId,
                    LoaderUrl = loader.Url,
                    LoaderServerPath = loader.ServerPath
                },
                session.Report,
                cancellationToken)
            .ConfigureAwait(false);
        Console.WriteLine(
            RpcOutputFormatter.FormatCapabilities(result));
        return 0;
    }

    public async Task<int> RunLoaderAsync(
        DeviceLoaderArguments options,
        CancellationToken cancellationToken = default) {
        await using var session = context.OpenRpcSession();
        var input = await session.Payload.PrepareFileAsync(
                options.Location,
                options.FetchLocation,
                cancellationToken)
            .ConfigureAwait(false);
        await session.Client.LoadLoaderAsync(
                new DeviceLoaderParams {
                    Target = ToolkitRpcPayloads.CreateTarget(
                        options.Target),
                    BlobId = input.BlobId,
                    Url = input.Url,
                    ServerPath = input.ServerPath
                },
                session.Report,
                cancellationToken)
            .ConfigureAwait(false);
        return 0;
    }

    public async Task<int> RunResetAsync(
        DeviceResetArguments options,
        CancellationToken cancellationToken = default) {
        await using var session = context.OpenRpcSession();
        await session.Client.ResetDeviceAsync(
                new DeviceResetParams {
                    Target = ToolkitRpcPayloads.CreateTarget(
                        options.Target),
                    Mode = options.Mode
                },
                cancellationToken)
            .ConfigureAwait(false);
        Console.WriteLine("Reset requested");
        return 0;
    }
}
