using System.CommandLine;
using Ampheck.RockchipToolkit.Cli.Commands.Parsers;
using Ampheck.RockchipToolkit.Protocol.RockUsb;

namespace Ampheck.RockchipToolkit.Cli.Commands;

internal sealed partial class CliCommandTree {
    private Command BuildDevices() {
        var options = new RockUsbCommandOptions();
        var command = new Command(
            "devices",
            "List connected RockUSB devices.");
        options.AddTo(command);
        command.SetAction((parsed, token) => RunDevicesAsync(
            options.ReadTarget(parsed),
            token));
        return command;
    }

    private Command BuildInfo() {
        var options = new RockUsbCommandOptions(includeLoader: true);
        var command = new Command(
            "info",
            "Read chip and flash information.");
        options.AddTo(command);
        command.SetAction((parsed, token) => RunInfoAsync(
            options.ReadTarget(parsed),
            parsed.GetValue(options.Loader!),
            token));
        return command;
    }

    private Command BuildCapability() {
        var options = new RockUsbCommandOptions(includeLoader: true);
        var command = new Command(
            "capability",
            "Read device capabilities.");
        options.AddTo(command);
        command.SetAction((parsed, token) => RunCapabilityAsync(
            options.ReadTarget(parsed),
            parsed.GetValue(options.Loader!),
            token));
        return command;
    }

    private Command BuildChip() {
        var group = new Command("chip", "Read chip-level information.");
        var idOptions = new RockUsbCommandOptions(includeLoader: true);
        var id = new Command("id", "Read the flash ID.");
        idOptions.AddTo(id);
        id.SetAction((parsed, token) => RunChipIdAsync(
            idOptions.ReadTarget(parsed),
            parsed.GetValue(idOptions.Loader!),
            token));
        group.Add(id);
        var infoOptions = new RockUsbCommandOptions(includeLoader: true);
        var info = new Command("info", "Read chip information.");
        infoOptions.AddTo(info);
        info.SetAction((parsed, token) => RunChipInfoAsync(
            infoOptions.ReadTarget(parsed),
            parsed.GetValue(infoOptions.Loader!),
            token));
        group.Add(info);
        return group;
    }

    private Command BuildLoader() {
        var loader = CreateArgument<string>(
            "loader",
            "RAM loader file, URL, or UNC path.");
        var options = new RockUsbCommandOptions();
        var command = new Command(
            "loader",
            "Download a RAM loader while the device is in MaskROM mode.") {
            loader
        };
        options.AddTo(command);
        command.SetAction((parsed, token) => RunLoaderAsync(
            options.ReadTarget(parsed),
            parsed.GetValue(loader)!,
            token));
        return command;
    }

    private Command BuildReset() {
        var mode = new Argument<string>("mode") {
            Description = "disconnect, normal, power-off, reset-mask-rom, " +
                "or reset-mass-storage.",
            Arity = ArgumentArity.ZeroOrOne
        };
        var options = new RockUsbCommandOptions();
        var command = new Command(
            "reset",
            "Reset or disconnect the device.") {
            mode
        };
        options.AddTo(command);
        command.SetAction((parsed, token) => {
            var value = parsed.GetValue(mode);
            var resetMode = string.IsNullOrEmpty(value)
                ? ResetMode.Normal
                : ValueParser.ParseResetMode("mode", value);
            return RunResetAsync(
                options.ReadTarget(parsed),
                resetMode,
                token);
        });
        return command;
    }

    private Command BuildErase() {
        var sector = NumberOption<uint>(
            "--sector",
            "-s",
            "Starting sector.",
            required: true,
            ValueParser.ParseUInt32);
        var count = NumberOption<ushort>(
            "--count",
            "-n",
            "Number of sectors.",
            required: true,
            ValueParser.ParseUInt16);
        var options = new RockUsbCommandOptions(includeLoader: true);
        var command = new Command("erase", "Erase a sector range.") {
            sector,
            count
        };
        options.AddTo(command);
        command.SetAction((parsed, token) => RunEraseAsync(
            options.ReadTarget(parsed),
            parsed.GetValue(options.Loader!),
            parsed.GetValue(sector),
            parsed.GetValue(count),
            token));
        return command;
    }

    private Command BuildFlash() {
        var source = CreateArgument<string>(
            "source",
            "Firmware image or directory.");
        var watch = new Option<bool>("--watch", "-w") {
            Description = "Flash each new MaskROM device as it appears."
        };
        var bootloader = new Option<string?>("--bootloader") {
            Description = "Persist a bootloader at sector 0x40."
        };
        var idb = new Option<string?>("--idb") {
            Description = "Build and persist an IDB from a MiniLoader."
        };
        var noReset = new Option<bool>("--no-reset") {
            Description = "Do not reset after flashing."
        };
        var options = new RockUsbCommandOptions(includeLoader: true);
        var command = new Command(
            "flash",
            "Flash an update image or firmware directory.") {
            source,
            watch,
            bootloader,
            idb,
            noReset
        };
        options.AddTo(command);
        command.SetAction((parsed, token) => {
            var sourceValue = parsed.GetValue(source)!;
            var loaderValue = parsed.GetValue(options.Loader!);
            var bootloaderValue = parsed.GetValue(bootloader);
            var idbValue = parsed.GetValue(idb);
            var target = options.ReadTarget(parsed);
            var resetAfterFlash = !parsed.GetValue(noReset);
            return parsed.GetValue(watch)
                ? RunFlashWatchAsync(
                    sourceValue,
                    loaderValue,
                    bootloaderValue,
                    idbValue,
                    target,
                    resetAfterFlash,
                    parsed.GetValue(Verbose),
                    token)
                : RunFlashAsync(
                    sourceValue,
                    loaderValue,
                    bootloaderValue,
                    idbValue,
                    target,
                    resetAfterFlash,
                    token);
        });
        return command;
    }

    private Command BuildRead() {
        var output = CreateArgument<string>(
            "output",
            "Local output path.");
        var sector = NumberOption<uint>(
            "--sector",
            "-s",
            "Starting sector.",
            required: false,
            ValueParser.ParseUInt32);
        var count = NumberOption<ushort>(
            "--count",
            "-n",
            "Number of sectors.",
            required: false,
            ValueParser.ParseUInt16);
        count.DefaultValueFactory = _ => 1;
        var options = new RockUsbCommandOptions(includeLoader: true);
        var command = new Command(
            "read",
            "Read sectors from the device.") {
            output,
            sector,
            count
        };
        options.AddTo(command);
        command.SetAction((parsed, token) => RunReadAsync(
            options.ReadTarget(parsed),
            parsed.GetValue(options.Loader!),
            parsed.GetValue(output)!,
            parsed.GetValue(sector),
            parsed.GetValue(count),
            token));
        return command;
    }

    private Command BuildStorage() {
        var storage = new Argument<string>("type") {
            Description = "emmc, sd-card, or spi-nor."
        };
        var options = new RockUsbCommandOptions(includeLoader: true);
        var command = new Command(
            "storage",
            "Change the active storage device.") {
            storage
        };
        options.AddTo(command);
        command.SetAction((parsed, token) => RunStorageAsync(
            options.ReadTarget(parsed),
            parsed.GetValue(options.Loader!),
            ValueParser.ParseStorageType(
                "type",
                parsed.GetValue(storage)!),
            token));
        return command;
    }

    private Command BuildWrite() {
        var input = CreateArgument<string>(
            "input",
            "Local path, URL, or UNC path.");
        var sector = NumberOption<uint>(
            "--sector",
            "-s",
            "Starting sector.",
            required: true,
            ValueParser.ParseUInt32);
        var backup = new Option<string?>("--backup", "-b") {
            Description = "Back up the target range before writing."
        };
        var noErase = new Option<bool>("--no-erase") {
            Description = "Skip the default erase before writing."
        };
        var length = OptionalLongOption(
            "--length",
            "Number of bytes to write.");
        var sourceOffset = OptionalLongOption(
            "--source-offset",
            "Byte offset into the input file.");
        var options = new RockUsbCommandOptions(includeLoader: true);
        var command = new Command(
            "write",
            "Write a sector-aligned file to the device.") {
            input,
            sector,
            backup,
            noErase,
            length,
            sourceOffset
        };
        options.AddTo(command);
        command.SetAction((parsed, token) => RunWriteAsync(
            options.ReadTarget(parsed),
            parsed.GetValue(options.Loader!),
            parsed.GetValue(input)!,
            parsed.GetValue(backup),
            parsed.GetValue(sector),
            parsed.GetValue(length),
            parsed.GetValue(sourceOffset),
            !parsed.GetValue(noErase),
            token));
        return command;
    }
}
