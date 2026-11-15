using System.CommandLine;
using Ampheck.RockchipToolkit.Cli.Execution.Rpc;
using Ampheck.RockchipToolkit.Rpc;

namespace Ampheck.RockchipToolkit.Cli.Commands;

internal sealed partial class RockUsbCommands {
    private Command CreateReadCommand() {
        var command = new Command(
            CommandNames.Read,
            "Read sectors from the device.");
        var output = new Argument<string>(
            "output") {
            Description = "Local path or HTTP(S) PUT target."
        };
        var sector = SectorOption(required: false);
        var count = CountOption(required: false);
        var options = new RockUsbOptionSet(
            allowLoader: true,
            allowFetch: false);
        command.Add(output);
        command.Add(sector);
        command.Add(count);
        options.AddTo(command);
        command.SetAction(parsed => {
            var values = options.Parse(parsed);
            var read = new StorageReadArguments(
                values.Target,
                new LoaderArguments(
                    values.Loader,
                    values.FetchLocation),
                parsed.GetValue(output) ??
                    throw new ArgumentException(
                        "An output path is required."),
                ValueParser.ParseOptionalUInt32(
                    "--sector",
                    parsed.GetValue(sector)) ?? 0,
                ValueParser.ParseOptionalUInt16(
                    "--count",
                    parsed.GetValue(count)) ?? 1);
            state.Invocation = new CliInvocation(
                CommandNames.Read,
                RequiresServer: true,
                (context, cancellationToken) =>
                    new StorageCommandRunner(context).RunReadAsync(
                        read,
                        cancellationToken));
            return 0;
        });
        return command;
    }

    private Command CreateWriteCommand() {
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
            Description =
                "Number of bytes to write; defaults to the remaining file."
        };
        var sourceOffset = new Option<string?>("--source-offset") {
            Description = "Byte offset into the input file."
        };
        var options = new RockUsbOptionSet(
            allowLoader: true,
            allowFetch: true);
        command.Add(input);
        command.Add(sector);
        command.Add(backup);
        command.Add(noErase);
        command.Add(length);
        command.Add(sourceOffset);
        options.AddTo(command);
        command.SetAction(parsed => {
            var values = options.Parse(parsed);
            var write = new StorageWriteArguments(
                values.Target,
                new LoaderArguments(
                    values.Loader,
                    values.FetchLocation),
                parsed.GetValue(input) ??
                    throw new ArgumentException(
                        "An input path is required."),
                values.FetchLocation,
                parsed.GetValue(backup),
                ValueParser.ParseOptionalUInt32(
                    "--sector",
                    parsed.GetValue(sector)) ??
                    throw new ArgumentException(
                        "A starting sector is required."),
                ValueParser.ParseOptionalInt64(
                    "--length",
                    parsed.GetValue(length)),
                ValueParser.ParseOptionalInt64(
                    "--source-offset",
                    parsed.GetValue(sourceOffset)),
                !parsed.GetValue(noErase));
            state.Invocation = new CliInvocation(
                CommandNames.Write,
                RequiresServer: true,
                (context, cancellationToken) =>
                    new StorageCommandRunner(context).RunWriteAsync(
                        write,
                        cancellationToken));
            return 0;
        });
        return command;
    }

    private Command CreateEraseCommand() {
        var command = new Command(
            CommandNames.Erase,
            "Erase a sector range.");
        var sector = SectorOption(required: true);
        var count = CountOption(required: true);
        var options = new RockUsbOptionSet(
            allowLoader: true,
            allowFetch: false);
        command.Add(sector);
        command.Add(count);
        options.AddTo(command);
        command.SetAction(parsed => {
            var values = options.Parse(parsed);
            var erase = new StorageEraseArguments(
                values.Target,
                new LoaderArguments(
                    values.Loader,
                    values.FetchLocation),
                ValueParser.ParseOptionalUInt32(
                    "--sector",
                    parsed.GetValue(sector)) ??
                    throw new ArgumentException(
                        "A starting sector is required."),
                ValueParser.ParseOptionalUInt16(
                    "--count",
                    parsed.GetValue(count)) ??
                    throw new ArgumentException(
                        "A sector count is required."));
            state.Invocation = new CliInvocation(
                CommandNames.Erase,
                RequiresServer: true,
                (context, cancellationToken) =>
                    new StorageCommandRunner(context).RunEraseAsync(
                        erase,
                        cancellationToken));
            return 0;
        });
        return command;
    }

    private Command CreateStorageCommand() {
        var command = new Command(
            CommandNames.Storage,
            "Change the active storage device.");
        var storage = new Argument<string>(
            "type") {
            Description = string.Join(", ", StorageTypes)
        };
        storage.AcceptOnlyFromAmong(StorageTypes);
        var options = new RockUsbOptionSet(
            allowLoader: true,
            allowFetch: false);
        command.Add(storage);
        options.AddTo(command);
        command.SetAction(parsed => {
            var values = options.Parse(parsed);
            var select = new StorageSelectArguments(
                values.Target,
                new LoaderArguments(
                    values.Loader,
                    values.FetchLocation),
                ParseStorage(
                    parsed.GetValue(storage) ??
                    throw new ArgumentException(
                        "A storage type is required.")));
            state.Invocation = new CliInvocation(
                CommandNames.Storage,
                RequiresServer: true,
                (context, cancellationToken) =>
                    new StorageCommandRunner(context).RunStorageAsync(
                        select,
                        cancellationToken));
            return 0;
        });
        return command;
    }

    private Command CreateResetCommand() {
        var command = new Command(
            CommandNames.Reset,
            "Reset or disconnect the device.");
        var modeArgument = new Argument<string?>(
            "mode") {
            Arity = ArgumentArity.ZeroOrOne,
            Description = "Reset mode."
        };
        modeArgument.AcceptOnlyFromAmong(ResetModes);
        var options = new RockUsbOptionSet(
            allowLoader: false,
            allowFetch: false);
        command.Add(modeArgument);
        options.AddTo(command);
        command.SetAction(parsed => {
            var values = options.Parse(parsed);
            var value = parsed.GetValue(modeArgument);
            var reset = new DeviceResetArguments(
                values.Target,
                value is null
                    ? ToolkitResetMode.Normal
                    : ParseReset(value));
            state.Invocation = new CliInvocation(
                CommandNames.Reset,
                RequiresServer: true,
                (context, cancellationToken) =>
                    new DeviceCommandRunner(context).RunResetAsync(
                        reset,
                        cancellationToken));
            return 0;
        });
        return command;
    }
}
