using Ampheck.RockchipToolkit.Arkt.Commands.Framework;
using Ampheck.RockchipToolkit.Arkt.Support.Image;
using Ampheck.RockchipToolkit.Arkt.Terminal;
using Ampheck.RockchipToolkit.Runtime.Terminal;

namespace Ampheck.RockchipToolkit.Arkt.Commands.Image;

[CommandGroup(
    ["image"],
    Description = "Unpack or pack Rockchip RKFW/RKAF images.")]
internal sealed class ImageCommands(ConsoleRenderer console) {
    [CommandHandler(
        ["image", "pack"],
        Description = "Build an RKFW or RKAF image.")]
    private ValueTask<int> ExecutePackAsync(
        [CommandParam(
            ["input"],
            Position = 0,
            Description = "Directory produced by image unpack.")]
        string input,
        [CommandParam(
            ["output"],
            Position = 1,
            Description = "Output RKFW or RKAF image.")]
        string output,
        [CommandParam(
            ["--chip"],
            Description = "RKFW chip name, for example RK3399.")]
        string? chip,
        [CommandParam(
            ["--force"],
            Description = "Replace an existing output.")]
        bool force,
        [CommandParam(
            ["--verbose", "-v"],
            Description = "Enable informational and debug logs.")]
        bool verbose,
        CancellationToken cancellationToken
    ) {
        return new ValueTask<int>(
            ImageCommandExecutor.RunAsync(
            input,
            output,
            force,
            chip,
            unpacking: false,
                console));
    }

    [CommandHandler(
        ["image", "unpack"],
        Description =
            "Extract an RKFW or RKAF image into a firmware directory.")]
    private ValueTask<int> ExecuteUnpackAsync(
        [CommandParam(
            ["input"],
            Position = 0,
            Description = "RKFW or RKAF image to extract.")]
        string input,
        [CommandParam(
            ["output"],
            Position = 1,
            Description = "Directory that receives the extracted files.")]
        string output,
        [CommandParam(
            ["--force"],
            Description = "Replace an existing output.")]
        bool force,
        [CommandParam(
            ["--verbose", "-v"],
            Description = "Enable informational and debug logs.")]
        bool verbose,
        CancellationToken cancellationToken
    ) {
        return new ValueTask<int>(
            ImageCommandExecutor.RunAsync(
            input,
            output,
            force,
            chip: null,
            unpacking: true,
                console));
    }
}
