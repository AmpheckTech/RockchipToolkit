using Ampheck.RockchipToolkit.Arkt.Commands.Framework;
using Ampheck.RockchipToolkit.Arkt.Commands.RockUsb.Parser;
using Ampheck.RockchipToolkit.Arkt.Support.Devices;
using Ampheck.RockchipToolkit.HardwareTransport.Protocol;

namespace Ampheck.RockchipToolkit.Arkt.Commands.RockUsb;

internal sealed class RockUsbDeviceCommands(DeviceCommandRunner runner) {
    [CommandHandler(
        ["capability"],
        Description = "Read device capabilities."
    )]
    private ValueTask<int> ExecuteCapabilityAsync(
        [CommandParam] RockUsbLoaderParameters parameters,
        CancellationToken cancellationToken
    ) =>
        new(runner.RunCapabilityAsync(
            RockUsbTargetFactory.Create(parameters),
            parameters.Loader,
            cancellationToken));

    [CommandHandler(
        ["devices"],
        Description = "List connected RockUSB devices."
    )]
    private ValueTask<int> ExecuteDevicesAsync(
        [CommandParam] RockUsbParameters parameters,
        CancellationToken cancellationToken
    ) =>
        new(runner.RunDevicesAsync(
            RockUsbTargetFactory.Create(parameters),
            cancellationToken));

    [CommandHandler(
        ["info"],
        Description = "Read chip and flash information."
    )]
    private ValueTask<int> ExecuteInfoAsync(
        [CommandParam] RockUsbLoaderParameters parameters,
        CancellationToken cancellationToken
    ) =>
        new(runner.RunInfoAsync(
            RockUsbTargetFactory.Create(parameters),
            parameters.Loader,
            cancellationToken));

    [CommandHandler(
        ["loader"],
        Description =
            "Download a loader while the device is in MaskROM mode."
    )]
    private ValueTask<int> ExecuteLoaderAsync(
        [CommandParam(
            ["loader"],
            Position = 0,
            Description = "Loader file, URL, or UNC path.")]
        string location,
        [CommandParam] RockUsbParameters parameters,
        CancellationToken cancellationToken
    ) =>
        new(runner.RunLoaderAsync(
            RockUsbTargetFactory.Create(parameters),
            location,
            cancellationToken));

    [CommandHandler(
        ["reset"],
        Description = "Reset or disconnect the device."
    )]
    private ValueTask<int> ExecuteResetAsync(
        [CommandParam(
            ["mode"],
            Position = 0,
            MinimumArity = 0,
            Description = "Reset mode.",
            Parser = typeof(ResetModeParser),
            Choices = [
                "normal",
                "reset-mass-storage",
                "power-off",
                "reset-mask-rom",
                "disconnect"
            ])]
        ResetMode mode,
        [CommandParam] RockUsbParameters parameters,
        CancellationToken cancellationToken
    ) =>
        new(runner.RunResetAsync(
            RockUsbTargetFactory.Create(parameters),
            mode,
            cancellationToken));
}
