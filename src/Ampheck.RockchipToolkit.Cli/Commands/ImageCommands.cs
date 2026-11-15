using System.CommandLine;
using Ampheck.RockchipToolkit.Cli.Execution;

namespace Ampheck.RockchipToolkit.Cli.Commands;

internal sealed class ImageCommands(CommandParseState state) {
    public void Register(RootCommand root) {
        var image = new Command(
            CommandNames.Image,
            "Unpack or pack Rockchip RKFW/RKAF images.");
        image.Add(CreateUnpackCommand());
        image.Add(CreatePackCommand());
        root.Add(image);
    }

    private Command CreateUnpackCommand() {
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
            CommandNames.ImageUnpack,
            input,
            output);
        return command;
    }

    private Command CreatePackCommand() {
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
            CommandNames.ImagePack,
            input,
            output,
            chip);
        return command;
    }

    private void AddCommonOptions(
        Command command,
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
            var options = new ImageCommandArguments(
                parsed.GetValue(input) ??
                    throw new ArgumentException(
                        "An input path is required."),
                parsed.GetValue(output) ??
                    throw new ArgumentException(
                        "An output path is required."),
                parsed.GetValue(force),
                chip is null ? null : parsed.GetValue(chip),
                canonicalCommand == CommandNames.ImageUnpack);
            state.Invocation = new CliInvocation(
                canonicalCommand,
                RequiresServer: false,
                (context, _) => ImageCommandRunner.RunAsync(
                    options,
                    context.Console));
            return 0;
        });
    }
}
