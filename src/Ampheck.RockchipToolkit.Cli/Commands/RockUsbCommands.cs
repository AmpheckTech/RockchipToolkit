using System.CommandLine;
using Ampheck.RockchipToolkit.Cli.Execution.Rpc;
using Ampheck.RockchipToolkit.Rpc;

namespace Ampheck.RockchipToolkit.Cli.Commands;

internal static partial class RockUsbCommands {
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
        var options = new CommandOptions();
        CommandOptionsBinder.AddRockUsbOptions(
            command,
            state,
            options,
            CommandNames.Devices,
            (context, _) => ExecuteRpc(
                CommandNames.Devices,
                options,
                context));
        return command;
    }

    private static Command CreateInfoCommand(
        CommandParseState state,
        string name,
        string canonicalCommand,
        string description) {
        var command = new Command(name, description);
        var options = new CommandOptions();
        CommandOptionsBinder.AddRockUsbOptions(
            command,
            state,
            options,
            canonicalCommand,
            (context, _) => ExecuteRpc(
                canonicalCommand,
                options,
                context),
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
        var options = new CommandOptions();
        CommandOptionsBinder.AddRockUsbOptions(
            id,
            state,
            options,
            CommandNames.ChipId,
            (context, _) => ExecuteRpc(
                CommandNames.ChipId,
                options,
                context),
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
        var options = new CommandOptions();
        command.Add(location);
        CommandOptionsBinder.AddRockUsbOptions(
            command,
            state,
            options,
            CommandNames.Loader,
            (context, _) => ExecuteRpc(
                CommandNames.Loader,
                options,
                context),
            allowFetch: true,
            bind: parsed => options.File =
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
        var options = new CommandOptions();
        command.Add(source);
        command.Add(noReset);
        CommandOptionsBinder.AddRockUsbOptions(
            command,
            state,
            options,
            CommandNames.Flash,
            (context, _) => ExecuteRpc(
                CommandNames.Flash,
                options,
                context),
            allowLoader: true,
            allowFetch: true,
            bind: parsed => {
                options.Source =
                    parsed.GetValue(source);
                options.ResetAfterFlash =
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
        var options = new CommandOptions();
        command.Add(output);
        command.Add(sector);
        command.Add(count);
        CommandOptionsBinder.AddRockUsbOptions(
            command,
            state,
            options,
            CommandNames.Read,
            (context, _) => ExecuteRpc(
                CommandNames.Read,
                options,
                context),
            allowLoader: true,
            bind: parsed => {
                options.File =
                    parsed.GetValue(output);
                options.Sector =
                    ValueParser.ParseOptionalUInt32(
                        "--sector",
                        parsed.GetValue(sector));
                options.Count =
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
        var options = new CommandOptions();
        command.Add(input);
        command.Add(sector);
        command.Add(backup);
        command.Add(noErase);
        command.Add(length);
        command.Add(sourceOffset);
        CommandOptionsBinder.AddRockUsbOptions(
            command,
            state,
            options,
            CommandNames.Write,
            (context, _) => ExecuteRpc(
                CommandNames.Write,
                options,
                context),
            allowLoader: true,
            allowFetch: true,
            bind: parsed => {
                options.File =
                    parsed.GetValue(input);
                options.Sector =
                    ValueParser.ParseOptionalUInt32(
                        "--sector",
                        parsed.GetValue(sector));
                options.Backup = parsed.GetValue(backup);
                options.EraseBeforeWrite =
                    !parsed.GetValue(noErase);
                options.WriteLength =
                    ValueParser.ParseOptionalInt64(
                        "--length",
                        parsed.GetValue(length));
                options.WriteSourceOffset =
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
        var options = new CommandOptions();
        command.Add(sector);
        command.Add(count);
        CommandOptionsBinder.AddRockUsbOptions(
            command,
            state,
            options,
            CommandNames.Erase,
            (context, _) => ExecuteRpc(
                CommandNames.Erase,
                options,
                context),
            allowLoader: true,
            bind: parsed => {
                options.Sector =
                    ValueParser.ParseOptionalUInt32(
                        "--sector",
                        parsed.GetValue(sector));
                options.Count =
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
        var options = new CommandOptions();
        command.Add(storage);
        CommandOptionsBinder.AddRockUsbOptions(
            command,
            state,
            options,
            CommandNames.Storage,
            (context, _) => ExecuteRpc(
                CommandNames.Storage,
                options,
                context),
            allowLoader: true,
            bind: parsed => {
                options.Storage = ParseStorage(
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
        var options = new CommandOptions();
        command.Add(modeArgument);
        CommandOptionsBinder.AddRockUsbOptions(
            command,
            state,
            options,
            CommandNames.Reset,
            (context, _) => ExecuteRpc(
                CommandNames.Reset,
                options,
                context),
            allowLoader: true,
            bind: parsed => {
                var value = parsed.GetValue(modeArgument);
                options.Reset = value is null
                    ? ToolkitResetMode.Normal
                    : ParseReset(value);
            });
        return command;
    }

}
