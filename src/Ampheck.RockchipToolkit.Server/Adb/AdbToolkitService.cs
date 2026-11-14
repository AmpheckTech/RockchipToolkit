using System.Collections.Concurrent;
using Ampheck.Adb;
using Ampheck.Adb.Server;
using Ampheck.RockchipToolkit.Rpc.JsonRpc;
using Ampheck.RockchipToolkit.Server.Blobs;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Server.Adb;

[JsonRpcService(JsonRpcServiceLifetime.Singleton)]
internal sealed class AdbToolkitService {

    private static readonly TimeSpan SessionLifetime =
        TimeSpan.FromMinutes(5);
    private readonly AdbServerProcess _server;
    private readonly AdbClient _client;
    private readonly BlobStore _blobs;
    private readonly ConcurrentDictionary<string, PendingShell>
        _pendingShells = new(StringComparer.Ordinal);

    public AdbToolkitService(
        AdbServerProcess server,
        BlobStore blobs,
        ToolkitHostOptions options,
        ILoggerFactory loggerFactory) {
        _server = server;
        _blobs = blobs;
        _client = new AdbClient(
            new AdbClientOptions {
                Server = new AdbServerOptions {
                    Host = "127.0.0.1",
                    Port = options.AdbPort
                }
            },
            loggerFactory);
    }

    [JsonRpcMethod(JsonRpcMethods.AdbServerEnsure)]
    public async ValueTask<EmptyResult> EnsureServerAsync(
        EmptyParams parameters,
        JsonRpcCallContext context) {
        await _server.EnsureRunningAsync(
                context.CancellationToken)
            .ConfigureAwait(false);
        return new EmptyResult();
    }

    [JsonRpcMethod(JsonRpcMethods.AdbDevices)]
    public async ValueTask<AdbDevicesResult> DevicesAsync(
        AdbDevicesParams parameters,
        JsonRpcCallContext context) {
        await _server.EnsureRunningAsync(
                context.CancellationToken)
            .ConfigureAwait(false);
        var devices = await _client.GetDevicesAsync(
                context.CancellationToken)
            .ConfigureAwait(false);
        return new AdbDevicesResult {
            Devices = devices
                .Where(device =>
                    parameters.SerialNumber is null ||
                    device.SerialNumber.Equals(
                        parameters.SerialNumber,
                        StringComparison.OrdinalIgnoreCase))
                .Select(ToDescriptor)
                .ToArray()
        };
    }

    [JsonRpcMethod(JsonRpcMethods.AdbShell, longRunning: true)]
    public async ValueTask<TextResult> ShellAsync(
        AdbShellParams parameters,
        JsonRpcCallContext context) {
        if (string.IsNullOrWhiteSpace(parameters.Command)) {
            throw new InvalidDataException(
                "The ADB shell command is empty.");
        }

        await _server.EnsureRunningAsync(
                context.CancellationToken)
            .ConfigureAwait(false);
        var device = await _client.GetOnlineDeviceAsync(
                parameters.SerialNumber,
                context.CancellationToken)
            .ConfigureAwait(false);
        return new TextResult {
            Text = await device.ExecuteShellAsync(
                    parameters.Command,
                    context.CancellationToken)
                .ConfigureAwait(false)
        };
    }

    [JsonRpcMethod(JsonRpcMethods.AdbShellSession)]
    public async ValueTask<StreamSessionResult> OpenShellSessionAsync(
        AdbShellSessionParams parameters,
        JsonRpcCallContext context) {
        await _server.EnsureRunningAsync(
                context.CancellationToken)
            .ConfigureAwait(false);
        _ = await _client.GetOnlineDeviceAsync(
                parameters.SerialNumber,
                context.CancellationToken)
            .ConfigureAwait(false);
        RemoveExpiredShells();
        var id = Guid.NewGuid().ToString("N");
        _pendingShells[id] = new PendingShell(
            parameters.SerialNumber,
            DateTimeOffset.UtcNow);
        return new StreamSessionResult {
            SessionId = id
        };
    }

    [JsonRpcMethod(JsonRpcMethods.AdbPush, longRunning: true)]
    public async ValueTask<EmptyResult> PushAsync(
        AdbPushParams parameters,
        JsonRpcCallContext context) {
        await _server.EnsureRunningAsync(
                context.CancellationToken)
            .ConfigureAwait(false);
        var blob = _blobs.Get(parameters.BlobId);
        var destination = parameters.Destination.EndsWith(
            "/",
            StringComparison.Ordinal)
                ? parameters.Destination + blob.Reference.FileName
                : parameters.Destination;
        var device = await _client.GetOnlineDeviceAsync(
                parameters.SerialNumber,
                context.CancellationToken)
            .ConfigureAwait(false);
        await device.PushAsync(
                blob.Path,
                destination,
                cancellationToken: context.CancellationToken)
            .ConfigureAwait(false);
        return new EmptyResult();
    }

    [JsonRpcMethod(JsonRpcMethods.AdbPull, longRunning: true)]
    public async ValueTask<BlobReference> PullAsync(
        AdbPullParams parameters,
        JsonRpcCallContext context) {
        if (string.IsNullOrWhiteSpace(parameters.Source)) {
            throw new InvalidDataException(
                "The ADB remote source path is empty.");
        }

        await _server.EnsureRunningAsync(
                context.CancellationToken)
            .ConfigureAwait(false);
        var device = await _client.GetOnlineDeviceAsync(
                parameters.SerialNumber,
                context.CancellationToken)
            .ConfigureAwait(false);
        var name = Path.GetFileName(parameters.Source);
        if (string.IsNullOrWhiteSpace(name)) {
            name = "adb-pull.bin";
        }

        var temporary = Path.Combine(
            Path.GetTempPath(),
            $"ampheck-adb-pull-{Guid.NewGuid():N}");
        try {
            await device.PullAsync(
                    parameters.Source,
                    temporary,
                    cancellationToken: context.CancellationToken)
                .ConfigureAwait(false);
            return await _blobs.CreateAsync(
                    name,
                    async (destination, cancellationToken) => {
                        await using var source = new FileStream(
                            temporary,
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.Read,
                            128 * 1024,
                            FileOptions.Asynchronous |
                            FileOptions.SequentialScan);
                        await source.CopyToAsync(
                                destination,
                                cancellationToken)
                            .ConfigureAwait(false);
                    },
                    context.CancellationToken)
                .ConfigureAwait(false);
        } finally {
            try {
                File.Delete(temporary);
            } catch (Exception error) when (
                error is IOException or UnauthorizedAccessException) {
            }
        }
    }

    [JsonRpcMethod(JsonRpcMethods.AdbReboot)]
    public async ValueTask<EmptyResult> RebootAsync(
        AdbRebootParams parameters,
        JsonRpcCallContext context) {
        await _server.EnsureRunningAsync(
                context.CancellationToken)
            .ConfigureAwait(false);
        var device = await _client.GetOnlineDeviceAsync(
                parameters.SerialNumber,
                context.CancellationToken)
            .ConfigureAwait(false);
        await device.RebootAsync(
                parameters.Mode switch {
                    ToolkitAdbRebootMode.Recovery =>
                        AdbRebootMode.Recovery,
                    ToolkitAdbRebootMode.Bootloader =>
                        AdbRebootMode.Bootloader,
                    _ => AdbRebootMode.System
                },
                context.CancellationToken)
            .ConfigureAwait(false);
        return new EmptyResult();
    }

    public async ValueTask<AdbShell> TakeShellAsync(
        string sessionId,
        CancellationToken cancellationToken) {
        if (!_pendingShells.TryRemove(
                sessionId,
                out var pending) ||
            DateTimeOffset.UtcNow - pending.CreatedAt >
                SessionLifetime) {
            throw new ToolkitRpcException(
                JsonRpcErrorCodes.NotFound,
                $"ADB shell session '{sessionId}' was not found.");
        }

        await _server.EnsureRunningAsync(cancellationToken)
            .ConfigureAwait(false);
        var device = await _client.GetOnlineDeviceAsync(
                pending.SerialNumber,
                cancellationToken)
            .ConfigureAwait(false);
        return await device.OpenShellAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static AdbDeviceDescriptor ToDescriptor(
        AdbDevice device) => new() {
            SerialNumber = device.SerialNumber,
            State = FormatState(device.State),
            Product = device.Product,
            Model = device.Model,
            DeviceName = device.DeviceName
        };

    private static string FormatState(AdbDeviceState state) =>
        state switch {
            AdbDeviceState.Device => "device",
            AdbDeviceState.Offline => "offline",
            AdbDeviceState.Unauthorized => "unauthorized",
            AdbDeviceState.Recovery => "recovery",
            AdbDeviceState.Sideload => "sideload",
            AdbDeviceState.Bootloader => "bootloader",
            _ => "unknown"
        };

    private void RemoveExpiredShells() {
        var now = DateTimeOffset.UtcNow;
        foreach (var pair in _pendingShells) {
            if (now - pair.Value.CreatedAt > SessionLifetime) {
                _pendingShells.TryRemove(pair.Key, out _);
            }
        }
    }

    private sealed record PendingShell(
        string? SerialNumber,
        DateTimeOffset CreatedAt);
}
