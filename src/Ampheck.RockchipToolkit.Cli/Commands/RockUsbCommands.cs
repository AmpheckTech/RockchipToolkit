using System.CommandLine;
using Ampheck.RockchipToolkit.Cli.Execution.Rpc;
using Ampheck.RockchipToolkit.Rpc;

namespace Ampheck.RockchipToolkit.Cli.Commands;

internal sealed partial class RockUsbCommands(CommandParseState state) {
    private static readonly string[] StorageTypes = [
        "emmc",
        "sd-card",
        "spi-nor"
    ];
    private static readonly string[] ResetModes = [
        "normal",
        "reset-mass-storage",
        "power-off",
        "reset-mask-rom",
        "disconnect"
    ];

    public void Register(RootCommand root) {
        root.Add(CreateDevicesCommand());
        root.Add(CreateInfoCommand(
            CommandNames.Info,
            CommandNames.Info,
            "Read chip and flash information."));
        root.Add(CreateChipCommand());
        root.Add(CreateInfoCommand(
            CommandNames.Capability,
            CommandNames.Capability,
            "Read device capabilities."));
        root.Add(CreateLoaderCommand());
        root.Add(CreateFlashCommand());
        root.Add(CreateReadCommand());
        root.Add(CreateWriteCommand());
        root.Add(CreateEraseCommand());
        root.Add(CreateStorageCommand());
        root.Add(CreateResetCommand());
    }

    private Command CreateDevicesCommand() {
        var command = new Command(
            CommandNames.Devices,
            "List connected RockUSB devices.");
        var options = new RockUsbOptionSet(
            allowLoader: false,
            allowFetch: false);
        options.AddTo(command);
        command.SetAction(parsed => {
            var values = options.Parse(parsed);
            state.Invocation = new CliInvocation(
                CommandNames.Devices,
                RequiresServer: true,
                (context, cancellationToken) =>
                    new DeviceCommandRunner(context).RunDevicesAsync(
                        values.Target,
                        cancellationToken));
            return 0;
        });
        return command;
    }

    private Command CreateInfoCommand(
        string name,
        string canonicalCommand,
        string description) {
        var command = new Command(name, description);
        var options = new RockUsbOptionSet(
            allowLoader: true,
            allowFetch: false);
        options.AddTo(command);
        command.SetAction(parsed => {
            var values = options.Parse(parsed);
            var query = new DeviceQueryArguments(
                values.Target,
                new LoaderArguments(
                    values.Loader,
                    values.FetchLocation));
            state.Invocation = new CliInvocation(
                canonicalCommand,
                RequiresServer: true,
                (context, cancellationToken) => {
                    var runner = new DeviceCommandRunner(context);
                    return canonicalCommand switch {
                        CommandNames.Info =>
                            runner.RunInfoAsync(
                                query,
                                cancellationToken),
                        CommandNames.ChipInfo =>
                            runner.RunChipInfoAsync(
                                query,
                                cancellationToken),
                        CommandNames.ChipId =>
                            runner.RunChipIdAsync(
                                query,
                                cancellationToken),
                        _ => runner.RunCapabilityAsync(
                            query,
                            cancellationToken)
                    };
                });
            return 0;
        });
        return command;
    }

    private Command CreateChipCommand() {
        var command = new Command(
            "chip",
            "Read chip-level information.");
        command.Add(CreateInfoCommand(
            "info",
            CommandNames.ChipInfo,
            "Read chip information."));
        command.Add(CreateInfoCommand(
            "id",
            CommandNames.ChipId,
            "Read the flash ID."));
        return command;
    }

    private Command CreateLoaderCommand() {
        var command = new Command(
            CommandNames.Loader,
            "Download a loader while the device is in MaskROM mode.");
        var location = new Argument<string>(
            "loader") {
            Description = "Loader file, URL, or UNC path."
        };
        var options = new RockUsbOptionSet(
            allowLoader: false,
            allowFetch: true);
        command.Add(location);
        options.AddTo(command);
        command.SetAction(parsed => {
            var values = options.Parse(parsed);
            var loader = new DeviceLoaderArguments(
                values.Target,
                parsed.GetValue(location) ??
                    throw new ArgumentException(
                        "A loader path is required."),
                values.FetchLocation);
            state.Invocation = new CliInvocation(
                CommandNames.Loader,
                RequiresServer: true,
                (context, cancellationToken) =>
                    new DeviceCommandRunner(context).RunLoaderAsync(
                        loader,
                        cancellationToken));
            return 0;
        });
        return command;
    }

    private Command CreateFlashCommand() {
        var command = new Command(
            CommandNames.Flash,
            "Flash an update image or a firmware directory.");
        var source = new Argument<string>(
            "source") {
            Description =
                "update image, firmware directory, or HTTP(S) URL."
        };
        var noReset = NoResetOption();
        var options = new RockUsbOptionSet(
            allowLoader: true,
            allowFetch: true);
        command.Add(source);
        command.Add(noReset);
        options.AddTo(command);
        command.SetAction(parsed => {
            var values = options.Parse(parsed);
            var flash = new FirmwareFlashArguments(
                values.Target,
                parsed.GetValue(source) ??
                    throw new ArgumentException(
                        "A firmware source is required."),
                values.FetchLocation,
                values.Loader,
                !parsed.GetValue(noReset));
            state.Invocation = new CliInvocation(
                CommandNames.Flash,
                RequiresServer: true,
                (context, cancellationToken) =>
                    new StorageCommandRunner(context).RunFlashAsync(
                        flash,
                        cancellationToken));
            return 0;
        });
        return command;
    }

}
