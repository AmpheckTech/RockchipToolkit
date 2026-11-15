using Ampheck.RockchipToolkit.Cli.Commands;
using Ampheck.RockchipToolkit.Cli.Presentation;
using Ampheck.RockchipToolkit.Rpc.JsonRpc;

namespace Ampheck.RockchipToolkit.Cli.Execution.Rpc;

internal static partial class ToolkitClientCommandRunner {
    private static async Task<int> RunDevicesAsync(
        ToolkitRpcClient client,
        DeviceTarget target,
        Action<TaskEventNotification> report) {
        var result = await client.RequestAsync<
            DeviceListParams,
            DeviceListResult>(
                JsonRpcMethods.DeviceList,
                new DeviceListParams { Target = target },
                report)
            .ConfigureAwait(false);
        if (result.Devices.Length > 0) {
            Console.WriteLine(
                RpcOutputFormatter.FormatDevices(result));
        }

        return 0;
    }

    private static async Task<int> RunInfoAsync(
        ToolkitRpcClient client,
        ToolkitPayloadPlan payload,
        CommandOptions options,
        DeviceTarget target,
        Action<TaskEventNotification> report) {
        var loader = await PrepareLoaderAsync(payload, options)
            .ConfigureAwait(false);
        var result = await client.RequestAsync<
            DeviceInfoParams,
            DeviceInfoResult>(
                JsonRpcMethods.DeviceInfo,
                new DeviceInfoParams {
                    Target = target,
                    LoaderBlobId = loader.BlobId,
                    LoaderUrl = loader.Url,
                    LoaderServerPath = loader.ServerPath
                },
                report)
            .ConfigureAwait(false);
        Console.WriteLine(
            RpcOutputFormatter.FormatInfo(result));
        return 0;
    }

    private static async Task<int> RunChipInfoAsync(
        ToolkitRpcClient client,
        ToolkitPayloadPlan payload,
        CommandOptions options,
        DeviceTarget target,
        Action<TaskEventNotification> report) {
        var loader = await PrepareLoaderAsync(payload, options)
            .ConfigureAwait(false);
        var result = await client.RequestAsync<
            DeviceChipInfoParams,
            DeviceChipInfoResult>(
                JsonRpcMethods.DeviceChipInfo,
                new DeviceChipInfoParams {
                    Target = target,
                    LoaderBlobId = loader.BlobId,
                    LoaderUrl = loader.Url,
                    LoaderServerPath = loader.ServerPath
                },
                report)
            .ConfigureAwait(false);
        Console.WriteLine(
            RpcOutputFormatter.FormatChipInfo(result));
        return 0;
    }

    private static async Task<int> RunChipIdAsync(
        ToolkitRpcClient client,
        ToolkitPayloadPlan payload,
        CommandOptions options,
        DeviceTarget target,
        Action<TaskEventNotification> report) {
        var loader = await PrepareLoaderAsync(payload, options)
            .ConfigureAwait(false);
        var result = await client.RequestAsync<
            DeviceChipIdParams,
            DeviceChipIdResult>(
                JsonRpcMethods.DeviceChipId,
                new DeviceChipIdParams {
                    Target = target,
                    LoaderBlobId = loader.BlobId,
                    LoaderUrl = loader.Url,
                    LoaderServerPath = loader.ServerPath
                },
                report)
            .ConfigureAwait(false);
        Console.WriteLine(result.ChipId);
        return 0;
    }

    private static async Task<int> RunCapabilityAsync(
        ToolkitRpcClient client,
        ToolkitPayloadPlan payload,
        CommandOptions options,
        DeviceTarget target,
        Action<TaskEventNotification> report) {
        var loader = await PrepareLoaderAsync(payload, options)
            .ConfigureAwait(false);
        var result = await client.RequestAsync<
            DeviceCapabilityParams,
            DeviceCapabilityResult>(
                JsonRpcMethods.DeviceCapability,
                new DeviceCapabilityParams {
                    Target = target,
                    LoaderBlobId = loader.BlobId,
                    LoaderUrl = loader.Url,
                    LoaderServerPath = loader.ServerPath
                },
                report)
            .ConfigureAwait(false);
        Console.WriteLine(
            RpcOutputFormatter.FormatCapabilities(result));
        return 0;
    }

    private static async Task<int> RunLoaderAsync(
        ToolkitRpcClient client,
        ToolkitPayloadPlan payload,
        CommandOptions options,
        DeviceTarget target,
        Action<TaskEventNotification> report) {
        var input = await payload.PrepareFileAsync(
                options.File!,
                options.FetchLocation,
                CancellationToken.None)
            .ConfigureAwait(false);
        await client.RequestAsync<
            DeviceLoaderParams,
            EmptyResult>(
                JsonRpcMethods.DeviceLoader,
                new DeviceLoaderParams {
                    Target = target,
                    BlobId = input.BlobId,
                    Url = input.Url,
                    ServerPath = input.ServerPath
                },
                report)
            .ConfigureAwait(false);
        return 0;
    }
}
