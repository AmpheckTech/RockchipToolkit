using System.CommandLine;
using Ampheck.RockchipToolkit.Rpc;

namespace Ampheck.RockchipToolkit.Cli.Commands;

internal static class RockUsbCommands {
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

    public static void AddTo(
        RootCommand root,
        CommandParseState state) {
        root.Add(CreateDevicesCommand(state));
        root.Add(CreateInfoCommand(
            state,
            CommandNames.Info,
            CommandNames.Info,
            "Read chip and flash information."));
        root.Add(CreateChipCommand(state));
        root.Add(CreateInfoCommand(
            state,
            CommandNames.Capability,
            CommandNames.Capability,
            "Read device capabilities."));
        root.Add(CreateLoaderCommand(state));
        root.Add(CreateFlashCommand(state));
        root.Add(CreateReadCommand(state));
        root.Add(CreateWriteCommand(state));
        root.Add(CreateEraseCommand(state));
        root.Add(CreateStorageCommand(state));
        root.Add(CreateResetCommand(state));
    }

    private static Command CreateDevicesCommand(
        CommandParseState state) {
        var command = new Command(
            CommandNames.Devices,
            "List connected RockUSB devices.");
        CommandOptionsBinder.AddRockUsbOptions(
            command,
            state,
            CommandNames.Devices);
        return command;
    }

    private static Command CreateInfoCommand(
        CommandParseState state,
        string name,
        string canonicalCommand,
        string description) {
        var command = new Command(name, description);
        CommandOptionsBinder.AddRockUsbOptions(
            command,
            state,
            canonicalCommand,
            allowLoader: true);
        return command;
    }

    private static Command CreateChipCommand(
        CommandParseState state) {
        var command = new Command(
            "chip",
            "Read chip-level information.");
        command.Add(CreateInfoCommand(
            state,
            "info",
            CommandNames.ChipInfo,
            "Read chip information."));

        var id = new Command(
            "id",
            "Read the flash ID.");
        CommandOptionsBinder.AddRockUsbOptions(
            id,
            state,
            CommandNames.ChipId,
            allowLoader: true);
        command.Add(id);
        return command;
    }

    private static Command CreateLoaderCommand(
        CommandParseState state) {
        var command = new Command(
            CommandNames.Loader,
            "Download a loader while the device is in MaskROM mode.");
        var location = new Argument<string>(
            "loader") {
            Description = "Loader file, URL, or UNC path."
        };
        command.Add(location);
        CommandOptionsBinder.AddRockUsbOptions(
            command,
            state,
            CommandNames.Loader,
            allowFetch: true,
            bind: parsed => state.Options.File =
                parsed.GetValue(location));
        return command;
    }

    private static Command CreateFlashCommand(
        CommandParseState state) {
        var command = new Command(
            CommandNames.Flash,
            "Flash an update image or a firmware directory.");
        var source = new Argument<string>(
            "source") {
            Description =
                "update image, firmware directory, or HTTP(S) URL."
        };
        var noReset = NoResetOption();
        command.Add(source);
        command.Add(noReset);
        CommandOptionsBinder.AddRockUsbOptions(
            command,
            state,
            CommandNames.Flash,
            allowLoader: true,
            allowFetch: true,
            bind: parsed => {
                state.Options.Source =
                    parsed.GetValue(source);
                state.Options.ResetAfterFlash =
                    !parsed.GetValue(noReset);
            });
        return command;
    }

    private static Command CreateReadCommand(
        CommandParseState state) {
        var command = new Command(
            CommandNames.Read,
            "Read sectors from the device.");
        var output = new Argument<string>(
            "output") {
            Description = "Local path or HTTP(S) PUT target."
        };
        var sector = SectorOption(required: false);
        var count = CountOption(required: false);
        command.Add(output);
        command.Add(sector);
        command.Add(count);
        CommandOptionsBinder.AddRockUsbOptions(
            command,
            state,
            CommandNames.Read,
            allowLoader: true,
            bind: parsed => {
                state.Options.File =
                    parsed.GetValue(output);
                state.Options.Sector =
                    ValueParser.ParseOptionalUInt32(
                        "--sector",
                        parsed.GetValue(sector));
                state.Options.Count =
                    ValueParser.ParseOptionalUInt16(
                        "--count",
                        parsed.GetValue(count));
            });
        return command;
    }

    private static Command CreateWriteCommand(
        CommandParseState state) {
        var command = new Command(
            CommandNames.Write,
            "Write a sector-aligned file to the device.");
        var input = new Argument<string>(
            "input") {
            Description = "Local path, URL, or UNC path."
        };
        var sector = SectorOption(required: true);
        var backup = new Option<string?>(
            "--backup",
            "-b") {
            Description = "Back up the target range before writing."
        };
        var noErase = new Option<bool>("--no-erase") {
            Description = "Skip the default erase before writing."
        };
        var length = new Option<string?>("--length") {
            Description = "Number of bytes to write; defaults to the remaining file."
        };
        var sourceOffset = new Option<string?>("--source-offset") {
            Description = "Byte offset into the input file."
        };
        command.Add(input);
        command.Add(sector);
        command.Add(backup);
        command.Add(noErase);
        command.Add(length);
        command.Add(sourceOffset);
        CommandOptionsBinder.AddRockUsbOptions(
            command,
            state,
            CommandNames.Write,
            allowLoader: true,
            allowFetch: true,
            bind: parsed => {
                state.Options.File =
                    parsed.GetValue(input);
                state.Options.Sector =
                    ValueParser.ParseOptionalUInt32(
                        "--sector",
                        parsed.GetValue(sector));
                state.Options.Backup = parsed.GetValue(backup);
                state.Options.EraseBeforeWrite =
                    !parsed.GetValue(noErase);
                state.Options.WriteLength =
                    ValueParser.ParseOptionalInt64(
                        "--length",
                        parsed.GetValue(length));
                state.Options.WriteSourceOffset =
                    ValueParser.ParseOptionalInt64(
                        "--source-offset",
                        parsed.GetValue(sourceOffset));
            });
        return command;
    }

    private static Command CreateEraseCommand(
        CommandParseState state) {
        var command = new Command(
            CommandNames.Erase,
            "Erase a sector range.");
        var sector = SectorOption(required: true);
        var count = CountOption(required: true);
        command.Add(sector);
        command.Add(count);
        CommandOptionsBinder.AddRockUsbOptions(
            command,
            state,
            CommandNames.Erase,
            allowLoader: true,
            bind: parsed => {
                state.Options.Sector =
                    ValueParser.ParseOptionalUInt32(
                        "--sector",
                        parsed.GetValue(sector));
                state.Options.Count =
                    ValueParser.ParseOptionalUInt16(
                        "--count",
                        parsed.GetValue(count));
            });
        return command;
    }

    private static Command CreateStorageCommand(
        CommandParseState state) {
        var command = new Command(
            CommandNames.Storage,
            "Change the active storage device.");
        var storage = new Argument<string>(
            "type") {
            Description = string.Join(", ", StorageTypes)
        };
        storage.AcceptOnlyFromAmong(StorageTypes);
        command.Add(storage);
        CommandOptionsBinder.AddRockUsbOptions(
            command,
            state,
            CommandNames.Storage,
            allowLoader: true,
            bind: parsed => {
                state.Options.Storage = ParseStorage(
                    parsed.GetValue(storage) ??
                    throw new ArgumentException(
                        "A storage type is required."));
            });
        return command;
    }

    private static Command CreateResetCommand(
        CommandParseState state) {
        var command = new Command(
            CommandNames.Reset,
            "Reset or disconnect the device.");
        var modeArgument = new Argument<string?>(
            "mode") {
            Arity = ArgumentArity.ZeroOrOne,
            Description = "Reset mode."
        };
        modeArgument.AcceptOnlyFromAmong(ResetModes);
        command.Add(modeArgument);
        CommandOptionsBinder.AddRockUsbOptions(
            command,
            state,
            CommandNames.Reset,
            allowLoader: true,
            bind: parsed => {
                var value = parsed.GetValue(modeArgument);
                state.Options.Reset = value is null
                    ? ToolkitResetMode.Normal
                    : ParseReset(value);
            });
        return command;
    }

    private static Option<string?> SectorOption(bool required) {
        return new Option<string?>(
            "--sector",
            "-s") {
            Required = required,
            Description = "Starting sector, decimal or 0x-prefixed hex."
        };
    }

    private static Option<string?> CountOption(bool required) {
        return new Option<string?>(
            "--count",
            "-n") {
            Required = required,
            Description = "Number of 512-byte sectors."
        };
    }

    private static Option<bool> NoResetOption() {
        return new Option<bool>("--no-reset") {
            Description = "Do not reset after flashing."
        };
    }

    private static ToolkitResetMode ParseReset(string value) {
        return value.ToLowerInvariant() switch {
            "normal" => ToolkitResetMode.Normal,
            "reset-mass-storage" => ToolkitResetMode.ResetMassStorage,
            "power-off" => ToolkitResetMode.PowerOff,
            "reset-mask-rom" => ToolkitResetMode.ResetMaskRom,
            "disconnect" => ToolkitResetMode.Disconnect,
            _ => throw new ArgumentException(
                $"Invalid reset mode '{value}'.")
        };
    }

    private static ToolkitStorage ParseStorage(string value) {
        return value.ToLowerInvariant() switch {
            "emmc" => ToolkitStorage.Emmc,
            "sd-card" => ToolkitStorage.SdCard,
            "spi-nor" => ToolkitStorage.SpiNor,
            _ => throw new ArgumentException(
                $"Invalid storage '{value}'.")
        };
    }
}
