using Ampheck.RockchipToolkit.Adb;
using Ampheck.RockchipToolkit.Adb.Client;
using Ampheck.RockchipToolkit.Adb.Server;
using Ampheck.RockchipToolkit.Adb.Services;
using Ampheck.RockchipToolkit.Arkt.Commands.Framework;
using Ampheck.RockchipToolkit.Arkt.Support;
using Ampheck.RockchipToolkit.Arkt.Support.Adb;
using Ampheck.RockchipToolkit.Arkt.Support.Assets;
using Ampheck.RockchipToolkit.Arkt.Support.Devices;
using Ampheck.RockchipToolkit.Arkt.Support.Serial;
using Ampheck.RockchipToolkit.Arkt.Support.Storage;
using Ampheck.RockchipToolkit.Arkt.Terminal;
using Ampheck.RockchipToolkit.Core.Limits;
using Ampheck.RockchipToolkit.Core.Ports.Adb;
using Ampheck.RockchipToolkit.Core.Ports.Files;
using Ampheck.RockchipToolkit.Features.Assets.Providers;
using Ampheck.RockchipToolkit.Features.Assets.Services;
using Ampheck.RockchipToolkit.Features.Devices.Services;
using Ampheck.RockchipToolkit.Features.Firmware.Services;
using Ampheck.RockchipToolkit.Features.Storage.Services;
using Ampheck.RockchipToolkit.HardwareTransport.Backends;
using Ampheck.RockchipToolkit.HardwareTransport.Backends.Serial;
using Ampheck.RockchipToolkit.HardwareTransport.Ports.Serial;
using Ampheck.RockchipToolkit.HardwareTransport.Ports.Usb;
using Ampheck.RockchipToolkit.Runtime.Logging;
using Ampheck.RockchipToolkit.Runtime.Remote;
using Ampheck.RockchipToolkit.Runtime.Terminal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit;

internal static class ServiceComposition {
    public static ServiceProvider Create(
        bool verbose,
        string[] args,
        ConsoleRenderer console,
        ILoggerFactory loggerFactory
    ) {
        var services = new ServiceCollection();
        services.AddSingleton(console);
        services.AddSingleton(loggerFactory);
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        services.AddSingleton(new CommandInvocationContext(args));
        services.AddSingleton(ToolkitTransferLimits.Default);
        services.AddSingleton(AssetCachePaths.Create());
        services.AddSingleton<IAssetProvider, RockchipRkbinProvider>();
        services.AddSingleton<AssetProviderRegistry>();
        services.AddSingleton<AssetManifestParser>();
        services.AddSingleton<AssetCache>();
        services.AddSingleton<AssetReferenceStore>();
        services.AddSingleton<AssetManager>();
        services.AddSingleton<IRemoteFileClient, RemoteFileClient>();
        services.AddSingleton<RockchipBootLoader>();
        services.AddSingleton(
            new UsbDeviceProviderFactory());
        services.AddScoped<DeviceConnector>();
        services.AddScoped<DeviceAccess>();
        services.AddScoped<DeviceService>();
        services.AddScoped<StorageService>();
        services.AddScoped<FirmwareService>();
        services.AddSingleton(_ => new SerialHubManager(loggerFactory));
        services.AddSingleton(_ =>
            new AdbServerProcess(
                "127.0.0.1",
                AdbEnvironment.GetConfiguredPort(),
                loggerFactory));
        services.AddSingleton(_ =>
            new AdbClient(
                new AdbClientOptions {
                    Server = new AdbServerOptions {
                        Host = "127.0.0.1",
                        Port = AdbEnvironment.GetConfiguredPort()
                    }
                },
                loggerFactory));
        services.AddSingleton<AdbToolkitService>();
        services.AddScoped<AssetCommandRunner>();
        services.AddScoped<AdbCommandRunner>();
        services.AddScoped<DeviceCommandRunner>();
        services.AddScoped<StorageCommandRunner>();
        services.AddScoped<SerialCommandRunner>();
        return services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true });
    }
}
