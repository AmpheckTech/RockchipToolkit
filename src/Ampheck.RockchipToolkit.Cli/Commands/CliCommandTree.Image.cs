using System.CommandLine;
using Ampheck.RockchipToolkit.Cli.Terminal;
using Ampheck.RockchipToolkit.Images.Common;
using Ampheck.RockchipToolkit.Images.Update;

namespace Ampheck.RockchipToolkit.Cli.Commands;

internal sealed partial class CliCommandTree {
    private Command BuildImage() {
        var group = new Command(
            "image",
            "Unpack or pack Rockchip RKFW/RKAF images.") {
            BuildImagePack(),
            BuildImageUnpack()
        };
        return group;
    }

    private Command BuildImagePack() {
        var input = CreateArgument<string>(
            "input",
            "Directory produced by image unpack.");
        var output = CreateArgument<string>(
            "output",
            "Output RKFW or RKAF image.");
        var chip = new Option<string?>("--chip") {
            Description = "RKFW chip name, for example RK3399."
        };
        var force = new Option<bool>("--force") {
            Description = "Replace an existing output."
        };
        var command = new Command(
            "pack",
            "Build an RKFW or RKAF image.") {
            input,
            output,
            chip,
            force
        };
        command.SetAction((parsed, _) => RunImageAsync(
            parsed.GetValue(input)!,
            parsed.GetValue(output)!,
            parsed.GetValue(force),
            parsed.GetValue(chip),
            unpacking: false));
        return command;
    }

    private Command BuildImageUnpack() {
        var input = CreateArgument<string>(
            "input",
            "RKFW or RKAF image to extract.");
        var output = CreateArgument<string>(
            "output",
            "Directory that receives extracted files.");
        var force = new Option<bool>("--force") {
            Description = "Replace an existing output."
        };
        var command = new Command(
            "unpack",
            "Extract an RKFW or RKAF image into a firmware directory.") {
            input,
            output,
            force
        };
        command.SetAction((parsed, _) => RunImageAsync(
            parsed.GetValue(input)!,
            parsed.GetValue(output)!,
            parsed.GetValue(force),
            chip: null,
            unpacking: true));
        return command;
    }

    private async Task<int> RunImageAsync(
        string input,
        string output,
        bool force,
        string? chip,
        bool unpacking
    ) {
        using var stop = new CancellationTokenSource();
        ConsoleCancelEventHandler handler = (_, eventArgs) => {
            eventArgs.Cancel = true;
            stop.Cancel();
        };
        Console.CancelKeyPress += handler;
        using var progress = new ImageProgressDisplay(
            _console,
            unpacking ? "unpack" : "pack");
        _console.StartActivity(unpacking
            ? $"Unpacking {input}"
            : $"Packing {input}");
        try {
            if (unpacking) {
                await RockchipImageExtractor.UnpackAsync(
                        input,
                        output,
                        force,
                        progress,
                        stop.Token)
                    .ConfigureAwait(false);
                progress.Complete();
                _console.CompleteActivity(
                    $"Unpacked {input} to {output}");
            } else {
                await RockchipImagePacker.PackAsync(
                        input,
                        output,
                        force,
                        chip,
                        progress,
                        stop.Token)
                    .ConfigureAwait(false);
                progress.Complete();
                _console.CompleteActivity(
                    $"Packed {input} to {output}");
            }

            return 0;
        } catch (OperationCanceledException)
            when (stop.IsCancellationRequested) {
            _console.CompleteActivity();
            Console.Error.WriteLine("Operation canceled.");
            return 130;
        } finally {
            _console.CompleteActivity();
            Console.CancelKeyPress -= handler;
        }
    }
}
