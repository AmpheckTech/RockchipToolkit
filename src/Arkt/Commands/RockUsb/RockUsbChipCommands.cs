using Ampheck.RockchipToolkit.Arkt.Commands.Framework;
using Ampheck.RockchipToolkit.Arkt.Support.Devices;

namespace Ampheck.RockchipToolkit.Arkt.Commands.RockUsb;

[CommandGroup(
    ["chip"],
    Description = "Read chip-level information.")]
internal sealed class RockUsbChipCommands(DeviceCommandRunner runner) {
    [CommandHandler(
        ["chip", "id"],
        Description = "Read the flash ID."
    )]
    private ValueTask<int> ExecuteIdAsync(
        [CommandParam] RockUsbLoaderParameters parameters,
        CancellationToken cancellationToken
    ) =>
        new(runner.RunChipIdAsync(
            RockUsbTargetFactory.Create(parameters),
            parameters.Loader,
            cancellationToken));

    [CommandHandler(
        ["chip", "info"],
        Description = "Read chip information."
    )]
    private ValueTask<int> ExecuteInfoAsync(
        [CommandParam] RockUsbLoaderParameters parameters,
        CancellationToken cancellationToken
    ) =>
        new(runner.RunChipInfoAsync(
            RockUsbTargetFactory.Create(parameters),
            parameters.Loader,
            cancellationToken));
}
