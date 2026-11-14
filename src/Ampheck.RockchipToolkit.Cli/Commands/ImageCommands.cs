using System.CommandLine;

namespace Ampheck.RockchipToolkit.Cli.Commands;

internal static class ImageCommands {
    public static Command Create(CommandParseState state) {
        var image = new Command(
            CommandNames.Image,
            "Unpack or pack Rockchip RKFW/RKAF images.");
        image.Add(CreateUnpackCommand(state));
        image.Add(CreatePackCommand(state));
        return image;
    }

    private static Command CreateUnpackCommand(
        CommandParseState state) {
        var command = new Command(
            "unpack",
            "Extract an RKFW or RKAF image into a firmware directory.");
        var input = new Argument<string>("input") {
            Description = "RKFW or RKAF image to extract."
        };
        var output = new Argument<string>("output") {
            Description = "Directory that receives the extracted files."
        };
        command.Add(input);
        command.Add(output);
        AddCommonOptions(
            command,
            state,
            CommandNames.ImageUnpack,
            input,
            output);
        return command;
    }

    private static Command CreatePackCommand(
        CommandParseState state) {
        var command = new Command(
            "pack",
            "Build an RKFW or RKAF image.");
        var input = new Argument<string>("input") {
            Description = "Directory produced by image unpack."
        };
        var output = new Argument<string>("output") {
            Description = "Output RKFW or RKAF image."
        };
        var chip = new Option<string?>("--chip") {
            Description = "RKFW chip name, for example RK3399."
        };
        command.Add(input);
        command.Add(output);
        command.Add(chip);
        AddCommonOptions(
            command,
            state,
            CommandNames.ImagePack,
            input,
            output,
            chip);
        return command;
    }

    private static void AddCommonOptions(
        Command command,
        CommandParseState state,
        string canonicalCommand,
        Argument<string> input,
        Argument<string> output,
        Option<string?>? chip = null) {
        var force = new Option<bool>("--force") {
            Description = "Replace an existing output."
        };
        var verbose = new Option<bool>("--verbose", "-v") {
            Description = "Enable informational and debug logs."
        };
        command.Add(force);
        command.Add(verbose);
        command.SetAction(parsed => {
            state.Command = canonicalCommand;
            state.Options.ImageInput = parsed.GetValue(input) ??
                throw new ArgumentException("An input path is required.");
            state.Options.ImageOutput = parsed.GetValue(output) ??
                throw new ArgumentException("An output path is required.");
            state.Options.ImageForce = parsed.GetValue(force);
            state.Options.ImageChip = chip is null
                ? null
                : parsed.GetValue(chip);
            return 0;
        });
    }
}
