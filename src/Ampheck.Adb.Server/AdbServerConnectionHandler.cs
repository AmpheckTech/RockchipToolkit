using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Ampheck.Adb.Server;

internal sealed partial class AdbServerConnectionHandler {
    private const string Features =
        "cmd,shell_v2,stat_v2,ls_v2,fixed_push_mkdir,apex,abb,abb_exec";
    private readonly TcpClient _client;
    private readonly AdbServerDeviceRegistry _devices;
    private readonly AdbServerForwardManager _forwards;
    private readonly ILogger _logger;
    private readonly Action _stopServer;
    private readonly TimeSpan _trackDevicesInterval;
    private string? _transportSerial;

    public AdbServerConnectionHandler(
        TcpClient client,
        AdbServerDeviceRegistry devices,
        AdbServerForwardManager forwards,
        AdbServerDaemonOptions options,
        ILogger logger,
        Action stopServer) {
        _client = client;
        _devices = devices;
        _forwards = forwards;
        _logger = logger;
        _stopServer = stopServer;
        _trackDevicesInterval = options.TrackDevicesInterval;
    }

    public async Task RunAsync(CancellationToken cancellationToken) {
        try {
            await using var stream = _client.GetStream();
            while (!cancellationToken.IsCancellationRequested) {
                var request = await AdbServerProtocol.ReadRequestAsync(
                        stream,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (request is null) {
                    return;
                }

                if (_transportSerial is not null) {
                    if (request.StartsWith(
                        "host:",
                        StringComparison.Ordinal)) {
                        if (!await HandleHostRequestAsync(
                                    stream,
                                    request,
                                    cancellationToken)
                                .ConfigureAwait(false)) {
                            return;
                        }

                        continue;
                    }

                    await RunDeviceServiceAsync(
                            stream,
                            request,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return;
                }

                if (!await HandleHostRequestAsync(
                            stream,
                            request,
                            cancellationToken)
                        .ConfigureAwait(false)) {
                    return;
                }
            }
        } catch (Exception error) when (
            error is IOException or
            SocketException or
            OperationCanceledException or
            InvalidOperationException) {
            _logger.LogDebug(
                error,
                "ADB server client disconnected");
        } finally {
            _client.Dispose();
        }
    }

    private async ValueTask<bool> HandleHostRequestAsync(
        Stream stream,
        string request,
        CancellationToken cancellationToken) {
        if (request == "host:version") {
            await WritePayloadAsync(
                    stream,
                    "0029",
                    cancellationToken)
                .ConfigureAwait(false);
            return false;
        }

        if (request == "host:features") {
            await WritePayloadAsync(
                    stream,
                    Features,
                    cancellationToken)
                .ConfigureAwait(false);
            return false;
        }

        if (request is "host:devices" or "host:devices-l") {
            await _devices.RefreshAsync(cancellationToken)
                .ConfigureAwait(false);
            await WriteDevicesAsync(
                    stream,
                    detailed: request.EndsWith(
                        "-l",
                        StringComparison.Ordinal),
                    cancellationToken)
                .ConfigureAwait(false);
            return false;
        }

        if (request == "host:track-devices") {
            await TrackDevicesAsync(
                    stream,
                    cancellationToken)
                .ConfigureAwait(false);
            return false;
        }

        if (request == "host:kill") {
            await AdbServerProtocol.WriteStatusAsync(
                    stream,
                    "OKAY",
                    cancellationToken)
                .ConfigureAwait(false);
            _stopServer();
            return false;
        }

        if (request == "host:list-forward") {
            var payload = string.Join(
                '\n',
                _forwards.List().Select(forward =>
                    $"{forward.Serial} " +
                    $"{forward.Local} {forward.Remote}"));
            if (payload.Length > 0) {
                payload += '\n';
            }

            await WritePayloadAsync(
                    stream,
                    payload,
                    cancellationToken)
                .ConfigureAwait(false);
            return false;
        }

        if (request == "host:killforward-all") {
            await _forwards.RemoveAllAsync()
                .ConfigureAwait(false);
            await WriteForwardResultAsync(
                    stream,
                    string.Empty,
                    cancellationToken)
                .ConfigureAwait(false);
            return false;
        }

        if (request.StartsWith(
            "host:killforward:",
            StringComparison.Ordinal)) {
            await _forwards.RemoveAsync(
                    serialNumber: null,
                    request["host:killforward:".Length..],
                    cancellationToken)
                .ConfigureAwait(false);
            await WriteForwardResultAsync(
                    stream,
                    string.Empty,
                    cancellationToken)
                .ConfigureAwait(false);
            return false;
        }

        if (request.StartsWith(
            "host:forward:",
            StringComparison.Ordinal)) {
            await AddForwardAsync(
                    stream,
                    serialNumber: null,
                    request["host:forward:".Length..],
                    cancellationToken)
                .ConfigureAwait(false);
            return false;
        }

        if (request == "host:tport:any" ||
            request == "host:tport:usb" ||
            request == "host:tport:local") {
            var device = await _devices.RequireAsync(
                    serialNumber: null,
                    cancellationToken)
                .ConfigureAwait(false);
            _transportSerial = device.SerialNumber;
            await AdbServerProtocol.WriteTransportIdAsync(
                    stream,
                    device.TransportId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (request.StartsWith(
            "host:tport:serial:",
            StringComparison.Ordinal)) {
            var serialNumber =
                request["host:tport:serial:".Length..];
            var device = await _devices.RequireAsync(
                    serialNumber,
                    cancellationToken)
                .ConfigureAwait(false);
            _transportSerial = device.SerialNumber;
            await AdbServerProtocol.WriteTransportIdAsync(
                    stream,
                    device.TransportId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (request.StartsWith(
            "host:transport-id:",
            StringComparison.Ordinal)) {
            if (!ulong.TryParse(
                request["host:transport-id:".Length..],
                out var transportId)) {
                throw new InvalidOperationException(
                    "The ADB transport ID is invalid.");
            }

            var device = _devices.RequireByTransportId(
                transportId);
            _transportSerial = device.SerialNumber;
            await AdbServerProtocol.WriteStatusAsync(
                    stream,
                    "OKAY",
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (request == "host:transport-any") {
            var device = await _devices.RequireAsync(
                    serialNumber: null,
                    cancellationToken)
                .ConfigureAwait(false);
            _transportSerial = device.SerialNumber;
            await AdbServerProtocol.WriteStatusAsync(
                    stream,
                    "OKAY",
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (request.StartsWith(
            "host:transport:",
            StringComparison.Ordinal)) {
            var serialNumber = request["host:transport:".Length..];
            var device = await _devices.RequireAsync(
                    serialNumber,
                    cancellationToken)
                .ConfigureAwait(false);
            _transportSerial = device.SerialNumber;
            await AdbServerProtocol.WriteStatusAsync(
                    stream,
                    "OKAY",
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (request is "host:get-state" or "host:get-serialno") {
            var device = await _devices.RequireAsync(
                    serialNumber: null,
                    cancellationToken)
                .ConfigureAwait(false);
            await WritePayloadAsync(
                    stream,
                    request.EndsWith(
                        "get-state",
                        StringComparison.Ordinal)
                        ? device.State
                        : device.SerialNumber,
                    cancellationToken)
                .ConfigureAwait(false);
            return false;
        }

        if (request.StartsWith(
            "host-serial:",
            StringComparison.Ordinal)) {
            return await HandleSerialHostRequestAsync(
                    stream,
                    request,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (request.StartsWith(
            "host:connect:",
            StringComparison.Ordinal)) {
            var (host, port) = ParseNetworkAddress(
                request["host:connect:".Length..]);
            var result = await _devices.ConnectNetworkAsync(
                    host,
                    port,
                    cancellationToken)
                .ConfigureAwait(false);
            await WritePayloadAsync(
                    stream,
                    result,
                    cancellationToken)
                .ConfigureAwait(false);
            return false;
        }

        if (request.StartsWith(
            "host:disconnect:",
            StringComparison.Ordinal)) {
            var address = request["host:disconnect:".Length..];
            if (string.IsNullOrWhiteSpace(address)) {
                var allResult =
                    await _devices.DisconnectAllNetworkAsync()
                        .ConfigureAwait(false);
                await WritePayloadAsync(
                        stream,
                        allResult,
                        cancellationToken)
                    .ConfigureAwait(false);
                return false;
            }

            var (host, port) = ParseNetworkAddress(address);
            var result = await _devices.DisconnectNetworkAsync(
                    host,
                    port,
                    cancellationToken)
                .ConfigureAwait(false);
            await WritePayloadAsync(
                    stream,
                    result,
                    cancellationToken)
                .ConfigureAwait(false);
            return false;
        }

        await AdbServerProtocol.WriteFailureAsync(
                stream,
                $"Unsupported ADB server request '{request}'.",
                cancellationToken)
            .ConfigureAwait(false);
        return false;
    }

    private async ValueTask<bool> HandleSerialHostRequestAsync(
        Stream stream,
        string request,
        CancellationToken cancellationToken) {
        var body = request["host-serial:".Length..];
        if (TrySplitCommand(
            body,
            "forward",
            out var forwardSerial,
            out var forwardSpec)) {
            await AddForwardAsync(
                    stream,
                    forwardSerial,
                    forwardSpec,
                    cancellationToken)
                .ConfigureAwait(false);
            return false;
        }

        if (TrySplitCommand(
            body,
            "killforward",
            out var removeSerial,
            out var removeSpec)) {
            await _forwards.RemoveAsync(
                    removeSerial,
                    removeSpec,
                    cancellationToken)
                .ConfigureAwait(false);
            await WriteForwardResultAsync(
                    stream,
                    string.Empty,
                    cancellationToken)
                .ConfigureAwait(false);
            return false;
        }

        if (TrySplitCommand(
            body,
            "killforward-all",
            out _,
            out _)) {
            await _forwards.RemoveAllAsync()
                .ConfigureAwait(false);
            await WriteForwardResultAsync(
                    stream,
                    string.Empty,
                    cancellationToken)
                .ConfigureAwait(false);
            return false;
        }

        var match = SerialHostRequestPattern().Match(body);
        if (!match.Success) {
            await AdbServerProtocol.WriteFailureAsync(
                    stream,
                    $"Unsupported ADB serial request '{request}'.",
                    cancellationToken)
                .ConfigureAwait(false);
            return false;
        }

        var serialNumber = match.Groups["serial"].Value;
        var command = match.Groups["command"].Value;
        var device = await _devices.RequireAsync(
                serialNumber,
                cancellationToken)
            .ConfigureAwait(false);
        switch (command) {
            case "get-state":
                await WritePayloadAsync(
                        stream,
                        device.State,
                        cancellationToken)
                    .ConfigureAwait(false);
                return false;
            case "get-serialno":
                await WritePayloadAsync(
                        stream,
                        device.SerialNumber,
                        cancellationToken)
                    .ConfigureAwait(false);
                return false;
            case "get-devpath":
                await WritePayloadAsync(
                        stream,
                        device.PlatformId ?? device.SerialNumber,
                        cancellationToken)
                    .ConfigureAwait(false);
                return false;
            default:
                await AdbServerProtocol.WriteFailureAsync(
                        stream,
                        $"Unsupported serial request '{command}'.",
                        cancellationToken)
                    .ConfigureAwait(false);
                return false;
        }
    }

    private async ValueTask RunDeviceServiceAsync(
        Stream stream,
        string service,
        CancellationToken cancellationToken) {
        var device = await _devices.RequireAsync(
                _transportSerial,
                cancellationToken)
            .ConfigureAwait(false);
        AdbServerDeviceLease? lease = null;
        try {
            lease = await device.OpenStreamAsync(
                    service,
                    cancellationToken)
                .ConfigureAwait(false);
            await AdbServerProtocol.WriteStatusAsync(
                    stream,
                    "OKAY",
                    cancellationToken)
                .ConfigureAwait(false);
            await AdbServerStreamBridge.RunAsync(
                    stream,
                    lease.Stream,
                    cancellationToken)
                .ConfigureAwait(false);
        } catch (Exception error) when (
            error is IOException or
            InvalidOperationException or
            UnauthorizedAccessException or
            System.ComponentModel.Win32Exception) {
            _logger.LogDebug(
                error,
                "ADB service {Service} failed for {SerialNumber}",
                service,
                device.SerialNumber);
            if (lease is null) {
                await AdbServerProtocol.WriteFailureAsync(
                        stream,
                        error.Message,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        } finally {
            if (lease is not null) {
                await lease.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private async ValueTask AddForwardAsync(
        Stream stream,
        string? serialNumber,
        string spec,
        CancellationToken cancellationToken) {
        if (spec.StartsWith(
            "norebind:",
            StringComparison.Ordinal)) {
            spec = spec["norebind:".Length..];
        }

        var parts = spec.Split(
            ';',
            StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2) {
            throw new InvalidDataException(
                "The ADB forward specification is invalid.");
        }

        var device = await _devices.RequireAsync(
                serialNumber ?? _transportSerial,
                cancellationToken)
            .ConfigureAwait(false);
        var assigned = await _forwards.AddAsync(
                device.SerialNumber,
                parts[0],
                parts[1],
                cancellationToken)
            .ConfigureAwait(false);
        await WriteForwardResultAsync(
                stream,
                assigned,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask TrackDevicesAsync(
        Stream stream,
        CancellationToken cancellationToken) {
        await AdbServerProtocol.WriteStatusAsync(
                stream,
                "OKAY",
                cancellationToken)
            .ConfigureAwait(false);
        string? previous = null;
        while (!cancellationToken.IsCancellationRequested) {
            await _devices.RefreshAsync(cancellationToken)
                .ConfigureAwait(false);
            var snapshot = FormatDevices(detailed: true);
            if (!string.Equals(
                snapshot,
                previous,
                StringComparison.Ordinal)) {
                await AdbServerProtocol.WritePayloadAsync(
                        stream,
                        snapshot,
                        cancellationToken)
                    .ConfigureAwait(false);
                previous = snapshot;
            }

            await Task.Delay(
                    _trackDevicesInterval,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async ValueTask WriteDevicesAsync(
        Stream stream,
        bool detailed,
        CancellationToken cancellationToken) {
        await AdbServerProtocol.WriteStatusAsync(
                stream,
                "OKAY",
                cancellationToken)
            .ConfigureAwait(false);
        await AdbServerProtocol.WritePayloadAsync(
                stream,
                FormatDevices(detailed),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask WritePayloadAsync(
        Stream stream,
        string payload,
        CancellationToken cancellationToken) {
        await AdbServerProtocol.WriteStatusAsync(
                stream,
                "OKAY",
                cancellationToken)
            .ConfigureAwait(false);
        await AdbServerProtocol.WritePayloadAsync(
                stream,
                payload,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask WriteForwardResultAsync(
        Stream stream,
        string payload,
        CancellationToken cancellationToken) {
        await AdbServerProtocol.WriteStatusAsync(
                stream,
                "OKAY",
                cancellationToken)
            .ConfigureAwait(false);
        await AdbServerProtocol.WriteStatusAsync(
                stream,
                "OKAY",
                cancellationToken)
            .ConfigureAwait(false);
        await AdbServerProtocol.WritePayloadAsync(
                stream,
                payload,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private string FormatDevices(bool detailed) {
        var output = new StringBuilder();
        foreach (var device in _devices.List()) {
            output.Append(device.SerialNumber)
                .Append('\t')
                .Append(device.State);
            if (detailed) {
                if (!string.IsNullOrWhiteSpace(device.Product)) {
                    output.Append(" product:")
                        .Append(Sanitize(device.Product));
                }

                if (!string.IsNullOrWhiteSpace(device.Model)) {
                    output.Append(" model:")
                        .Append(Sanitize(device.Model));
                }

                if (!string.IsNullOrWhiteSpace(device.DeviceName)) {
                    output.Append(" device:")
                        .Append(Sanitize(device.DeviceName));
                }

                if (!string.IsNullOrWhiteSpace(device.PlatformId)) {
                    output.Append(" usb:")
                        .Append(Sanitize(device.PlatformId));
                }

                output.Append(" transport_id:")
                    .Append(device.TransportId);
            }

            output.Append('\n');
        }

        return output.ToString();
    }

    private static string Sanitize(string value) {
        return value
            .Replace(' ', '_')
            .Replace('\r', '_')
            .Replace('\n', '_');
    }

    private static (string Host, int Port) ParseNetworkAddress(
        string value) {
        var separator = value.LastIndexOf(':');
        if (separator <= 0 ||
            !int.TryParse(
                value[(separator + 1)..],
                out var port) ||
            port is <= 0 or > ushort.MaxValue) {
            throw new InvalidOperationException(
                $"Invalid network ADB address '{value}'.");
        }

        return (value[..separator], port);
    }

    private static bool TrySplitCommand(
        string value,
        string command,
        out string serialNumber,
        out string arguments) {
        var marker = $":{command}:";
        var index = value.IndexOf(
            marker,
            StringComparison.Ordinal);
        if (index <= 0) {
            serialNumber = string.Empty;
            arguments = string.Empty;
            return false;
        }

        serialNumber = value[..index];
        arguments = value[(index + marker.Length)..];
        return true;
    }

    [GeneratedRegex(
        "^(?<serial>.+):(?<command>get-state|get-serialno|get-devpath)$")]
    private static partial Regex SerialHostRequestPattern();
}
