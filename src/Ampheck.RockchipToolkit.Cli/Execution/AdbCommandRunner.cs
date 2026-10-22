using Ampheck.Adb;
using Ampheck.RockchipToolkit.Cli.Commands;
using Ampheck.RockchipToolkit.Cli.Infrastructure;
using Ampheck.RockchipToolkit.Cli.Presentation;
using Ampheck.RockchipToolkit.Rpc;
using Ampheck.RockchipToolkit.Server.Devices;
using Ampheck.RockchipToolkit.Transport;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Cli.Execution;

internal static class AdbCommandRunner {
    public static async Task<int> ExecuteAsync(
        string command,
        CommandOptions options,
        ILoggerFactory loggerFactory,
        ILogger logger,
        ConsoleRenderer console) {
        try {
            var environment = AdbEnvironment.Resolve();
            var client = new AdbClient(
                environment.Client,
                loggerFactory: loggerFactory);
            return await (command switch {
                CommandNames.AdbDevices => ListDevicesAsync(
                    client,
                    options),
                CommandNames.AdbShell => RunShellAsync(
                    client,
                    options,
                    logger,
                    console),
                CommandNames.AdbPush => PushAsync(
                    client,
                    options,
                    logger,
                    console),
                CommandNames.AdbPull => PullAsync(
                    client,
                    options,
                    logger,
                    console),
                CommandNames.AdbRebootSystem => RebootAsync(
                    client,
                    options,
                    AdbRebootMode.System,
                    console),
                CommandNames.AdbRebootRecovery => RebootAsync(
                    client,
                    options,
                    AdbRebootMode.Recovery,
                    console),
                CommandNames.AdbRebootLoader => EnterLoaderAsync(
                    client,
                    options,
                    loggerFactory,
                    console,
                    environment.Toolkit),
                _ => throw new ArgumentException(
                    $"Unknown ADB command '{command}'.")
            }).ConfigureAwait(false);
        } catch (Exception error) when (
            UsbAccessError.IsPermissionFailure(error)) {
            throw UsbAccessError.Wrap(error);
        }
    }

    private static async Task<int> ListDevicesAsync(
        AdbClient client,
        CommandOptions options) {
        var devices = await client.GetDevicesAsync()
            .ConfigureAwait(false);
        EnsureUsbFilterSupported(devices, options);
        foreach (var device in FilterDevices(devices, options)) {
            var identity = FormatIdentity(device);
            Console.WriteLine(
                $"{device.SerialNumber}\t" +
                $"{FormatState(device.State)}\t{identity}");
        }

        Console.Out.Flush();
        return 0;
    }

    private static async Task<int> PushAsync(
        AdbClient client,
        CommandOptions options,
        ILogger logger,
        ConsoleRenderer console) {
        var source = RequireTransferPath(
            options.AdbSource,
            "local source");
        var destination = RequireTransferPath(
            options.AdbDestination,
            "remote destination");
        if (!File.Exists(source)) {
            throw new FileNotFoundException(
                $"Local file '{source}' was not found.",
                source);
        }

        if (Directory.Exists(source)) {
            throw new NotSupportedException(
                "Directory push is not supported.");
        }

        var remotePath = destination.EndsWith('/')
            ? destination + Path.GetFileName(source)
            : destination;
        var device = await SelectDeviceAsync(
                client,
                options)
            .ConfigureAwait(false);
        logger.LogInformation(
            "Pushing {LocalPath} to {SerialNumber}:{RemotePath}",
            source,
            device.SerialNumber,
            remotePath);
        using var progress = CreateProgress(
            console,
            Path.GetFileName(source),
            new FileInfo(source).Length);
        await device.PushAsync(
                source,
                remotePath,
                progress: progress)
            .ConfigureAwait(false);
        Console.WriteLine($"pushed {source} -> {remotePath}");
        Console.Out.Flush();
        return 0;
    }

    private static async Task<int> PullAsync(
        AdbClient client,
        CommandOptions options,
        ILogger logger,
        ConsoleRenderer console) {
        var source = RequireTransferPath(
            options.AdbSource,
            "remote source");
        var destination = RequireTransferPath(
            options.AdbDestination,
            "local destination");
        var device = await SelectDeviceAsync(
                client,
                options)
            .ConfigureAwait(false);
        var remoteInfo = await device.StatFileAsync(source)
            .ConfigureAwait(false);
        if (remoteInfo.IsDirectory) {
            throw new NotSupportedException(
                "Directory pull is not supported.");
        }

        var destinationIsDirectory =
            Directory.Exists(destination) ||
            Path.EndsInDirectorySeparator(destination);
        if (destinationIsDirectory) {
            Directory.CreateDirectory(destination);
            destination = Path.Combine(
                destination,
                remoteInfo.Name);
        }

        logger.LogInformation(
            "Pulling {SerialNumber}:{RemotePath} to {LocalPath}",
            device.SerialNumber,
            source,
            destination);
        using var progress = CreateProgress(
            console,
            remoteInfo.Name,
            remoteInfo.Size);
        await device.PullAsync(
                source,
                destination,
                progress)
            .ConfigureAwait(false);
        Console.WriteLine($"pulled {source} -> {destination}");
        Console.Out.Flush();
        return 0;
    }

    private static async Task<int> RunShellAsync(
        AdbClient client,
        CommandOptions options,
        ILogger logger,
        ConsoleRenderer console) {
        var device = await SelectDeviceAsync(
                client,
                options)
            .ConfigureAwait(false);
        logger.LogInformation(
            "Using ADB device {SerialNumber} via {Backend}",
            device.SerialNumber,
            device.Backend);
        if (options.AdbShellCommand is null &&
            options.AdbShellArguments.Length == 0) {
            await using var shell = await device.OpenShellAsync()
                .ConfigureAwait(false);
            await InteractiveShellRunner.RunAsync(shell, console)
                .ConfigureAwait(false);
            return 0;
        }

        var command = options.AdbShellCommand ??
            AdbShellCommandBuilder.FromArguments(
                options.AdbShellArguments);
        var output = await device.ExecuteShellAsync(
                command)
            .ConfigureAwait(false);
        Console.Out.Write(output);
        if (output.Length > 0 &&
            !output.EndsWith('\n')) {
            Console.WriteLine();
        }

        Console.Out.Flush();
        return 0;
    }

    private static async Task<int> RebootAsync(
        AdbClient client,
        CommandOptions options,
        AdbRebootMode mode,
        ConsoleRenderer console) {
        var device = await SelectDeviceAsync(
                client,
                options)
            .ConfigureAwait(false);
        var name = mode == AdbRebootMode.Recovery
            ? "recovery"
            : "normal";
        console.WriteStatus($"Requesting {name} reboot");
        await device.RebootAsync(mode)
            .ConfigureAwait(false);
        console.WriteStatus(
            $"{name} reboot requested",
            complete: true);
        return 0;
    }

    private static async Task<int> EnterLoaderAsync(
        AdbClient client,
        CommandOptions options,
        ILoggerFactory loggerFactory,
        ConsoleRenderer console,
        ToolkitEndpoint? toolkitEndpoint) {
        var device = await SelectDeviceAsync(
                client,
                options)
            .ConfigureAwait(false);
        console.WriteStatus("Requesting Loader mode");
        await device.RebootAsync(AdbRebootMode.Bootloader)
            .ConfigureAwait(false);
        console.WriteStatus("Waiting for Rockchip Loader");
        if (toolkitEndpoint is not null) {
            return await WaitForRemoteLoaderAsync(
                    toolkitEndpoint,
                    options.VendorId,
                    options.ProductId,
                    options,
                    console)
                .ConfigureAwait(false);
        }

        return await DeviceConnector.WaitForLoaderAsync(
                options.Transport switch {
                    TransportKind.RockUsb =>
                        ToolkitTransport.RockUsb,
                    TransportKind.LibUsb =>
                        ToolkitTransport.LibUsb,
                    _ => ToolkitTransport.Auto
                },
                options.VendorId,
                options.ProductId,
                message => {
                    console.WriteStatus(message, complete: true);
                    return ValueTask.CompletedTask;
                },
                loggerFactory,
                CancellationToken.None)
            .ConfigureAwait(false);
    }

    private static async Task<int> WaitForRemoteLoaderAsync(
        ToolkitEndpoint endpoint,
        ushort? vendorId,
        ushort? productId,
        CommandOptions options,
        ConsoleRenderer console) {
        var token = Environment.GetEnvironmentVariable(
            "ARKT_SERVER_TOKEN");
        await using var client = new ToolkitClient(
            endpoint,
            token);
        for (var attempt = 0; attempt < 40; attempt++) {
            var found = 0;
            var request = new ToolkitRequest {
                Operation = ToolkitOperation.Devices,
                Transport = options.Transport switch {
                    TransportKind.RockUsb =>
                        ToolkitTransport.RockUsb,
                    TransportKind.LibUsb =>
                        ToolkitTransport.LibUsb,
                    _ => ToolkitTransport.Auto
                },
                VendorId = vendorId,
                ProductId = productId
            };
            await client.ExecuteAsync(
                    request,
                    value => {
                        if (value.Message.Contains(
                                "Loader",
                                StringComparison.OrdinalIgnoreCase) ||
                            value.Message.Contains(
                                "MaskRom",
                                StringComparison.OrdinalIgnoreCase)) {
                            Volatile.Write(ref found, 1);
                        }
                    })
                .ConfigureAwait(false);
            if (Volatile.Read(ref found) != 0) {
                console.WriteStatus(
                    "Device entered Loader mode",
                    complete: true);
                await Task.Delay(
                        TimeSpan.FromMilliseconds(500))
                    .ConfigureAwait(false);
                return 0;
            }

            await Task.Delay(
                    TimeSpan.FromMilliseconds(500))
                .ConfigureAwait(false);
        }

        throw new TimeoutException(
            "The device did not enter Loader mode within 20 seconds.");
    }

    private static async Task<AdbDevice> SelectDeviceAsync(
        AdbClient client,
        CommandOptions options) {
        var devices = await client.GetDevicesAsync()
            .ConfigureAwait(false);
        EnsureUsbFilterSupported(devices, options);
        var matching = FilterDevices(devices, options).ToArray();
        if (matching.Length == 0) {
            throw new InvalidOperationException(
                "No matching ADB device was found.");
        }

        if (matching.Length > 1) {
            throw new InvalidOperationException(
                "Multiple ADB devices were found. Use --serial to select one.");
        }

        return matching[0];
    }

    private static IEnumerable<AdbDevice> FilterDevices(
        IEnumerable<AdbDevice> devices,
        CommandOptions options) {
        return devices.Where(device =>
            (!options.VendorId.HasValue ||
                device.VendorId == options.VendorId) &&
            (!options.ProductId.HasValue ||
                device.ProductId == options.ProductId) &&
            (options.AdbSerial is null ||
                device.SerialNumber.Equals(
                    options.AdbSerial,
                    StringComparison.OrdinalIgnoreCase)));
    }

    private static void EnsureUsbFilterSupported(
        IEnumerable<AdbDevice> devices,
        CommandOptions options) {
        if ((options.VendorId.HasValue ||
                options.ProductId.HasValue) &&
            devices.Any(device =>
                device.Backend == AdbDeviceBackend.AdbServer)) {
            throw new InvalidOperationException(
                "VID/PID filtering is unavailable through the ADB server. " +
                "Use --serial or stop the ADB server.");
        }
    }

    private static string FormatDetails(AdbDevice device) {
        var details = new[] {
            FormatField("product", device.Product),
            FormatField("model", device.Model),
            FormatField("device", device.DeviceName)
        }.Where(value => value is not null);
        var text = string.Join(" ", details);
        return text.Length == 0 ? "adb server" : text;
    }

    private static string FormatIdentity(AdbDevice device) {
        if (!device.VendorId.HasValue ||
            !device.ProductId.HasValue) {
            return FormatDetails(device);
        }

        var vendorId = device.VendorId.Value;
        var productId = device.ProductId.Value;
        var known = RockchipUsbCatalog.Resolve(
            vendorId,
            productId);
        return known is null
            ? $"{vendorId:X4}:{productId:X4}"
            : $"{vendorId:X4}:{productId:X4} " +
              $"{known.Family} {known.Description}";
    }

    private static string? FormatField(
        string name,
        string? value) {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : $"{name}:{value}";
    }

    private static string FormatState(AdbDeviceState state) {
        return state switch {
            AdbDeviceState.Device => "device",
            AdbDeviceState.Offline => "offline",
            AdbDeviceState.Unauthorized => "unauthorized",
            AdbDeviceState.Recovery => "recovery",
            AdbDeviceState.Sideload => "sideload",
            AdbDeviceState.Bootloader => "bootloader",
            _ => "unknown"
        };
    }

    private static ProgressBar? CreateProgress(
        ConsoleRenderer console,
        string label,
        long total) {
        return console.IsInteractive && total > 0
            ? new ProgressBar(console, label, total)
            : null;
    }

    private static string RequireTransferPath(
        string? value,
        string description) {
        if (string.IsNullOrWhiteSpace(value)) {
            throw new ArgumentException(
                $"A {description} is required.");
        }

        return value;
    }
}
