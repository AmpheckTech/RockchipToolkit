using Ampheck.RockchipToolkit.Arkt.Commands.Framework;
using Ampheck.RockchipToolkit.Arkt.Commands.Framework.Parser;
using Ampheck.RockchipToolkit.Arkt.Commands.RockUsb.Parser;
using Ampheck.RockchipToolkit.Arkt.Support.Storage;
using Ampheck.RockchipToolkit.HardwareTransport.Protocol;

namespace Ampheck.RockchipToolkit.Arkt.Commands.RockUsb;

internal sealed class RockUsbStorageCommands(StorageCommandRunner runner) {
    [CommandHandler(
        ["erase"],
        Description = "Erase a sector range."
    )]
    private ValueTask<int> ExecuteEraseAsync(
        [CommandParam(
            ["--sector", "-s"],
            Required = true,
            Description = "Starting sector, decimal or 0x-prefixed hex.",
            Parser = typeof(HexUInt32Parser))]
        uint sector,
        [CommandParam(
            ["--count", "-n"],
            Required = true,
            Description = "Number of 512-byte sectors.",
            Parser = typeof(HexUShortParser))]
        ushort count,
        [CommandParam] RockUsbLoaderParameters parameters,
        CancellationToken cancellationToken
    ) =>
        new(runner.RunEraseAsync(
            RockUsbTargetFactory.Create(parameters),
            parameters.Loader,
            sector,
            count,
            cancellationToken));

    [CommandHandler(
        ["flash"],
        Description = "Flash an update image or a firmware directory."
    )]
    private ValueTask<int> ExecuteFlashAsync(
        [CommandParam(
            ["source"],
            Position = 0,
            Description =
                "update image, firmware directory, or HTTP(S) URL.")]
        string source,
        [CommandParam(
            ["--no-reset"],
            Description = "Do not reset after flashing.")]
        bool noReset,
        [CommandParam] RockUsbLoaderParameters parameters,
        CancellationToken cancellationToken
    ) =>
        new(runner.RunFlashAsync(
            source,
            parameters.Loader,
            RockUsbTargetFactory.Create(parameters),
            !noReset,
            cancellationToken));

    [CommandHandler(
        ["read"],
        Description = "Read sectors from the device."
    )]
    private ValueTask<int> ExecuteReadAsync(
        [CommandParam(
            ["output"],
            Position = 0,
            Description = "Local output path.")]
        string output,
        [CommandParam(
            ["--sector", "-s"],
            Description = "Starting sector, decimal or 0x-prefixed hex.",
            Parser = typeof(HexUInt32Parser))]
        uint sector,
        [CommandParam(
            ["--count", "-n"],
            Description = "Number of 512-byte sectors.",
            Parser = typeof(HexUInt16Parser))]
        ushort? count,
        [CommandParam] RockUsbLoaderParameters parameters,
        CancellationToken cancellationToken
    ) =>
        new(runner.RunReadAsync(
            RockUsbTargetFactory.Create(parameters),
            parameters.Loader,
            output,
            sector,
            count ?? 1,
            cancellationToken));

    [CommandHandler(
        ["storage"],
        Description = "Change the active storage device."
    )]
    private ValueTask<int> ExecuteSelectAsync(
        [CommandParam(
            ["type"],
            Position = 0,
            Description = "emmc, sd-card, or spi-nor.",
            Parser = typeof(StorageTypeParser),
            Choices = ["emmc", "sd-card", "spi-nor"])]
        StorageType storage,
        [CommandParam] RockUsbLoaderParameters parameters,
        CancellationToken cancellationToken
    ) =>
        new(runner.RunStorageAsync(
            RockUsbTargetFactory.Create(parameters),
            parameters.Loader,
            storage,
            cancellationToken));

    [CommandHandler(
        ["write"],
        Description = "Write a sector-aligned file to the device."
    )]
    private ValueTask<int> ExecuteWriteAsync(
        [CommandParam(
            ["input"],
            Position = 0,
            Description = "Local path, URL, or UNC path.")]
        string input,
        [CommandParam(
            ["--sector", "-s"],
            Required = true,
            Description = "Starting sector, decimal or 0x-prefixed hex.",
            Parser = typeof(HexUInt32Parser))]
        uint sector,
        [CommandParam(
            ["--backup", "-b"],
            Description = "Back up the target range before writing.")]
        string? backup,
        [CommandParam(
            ["--no-erase"],
            Description = "Skip the default erase before writing.")]
        bool noErase,
        [CommandParam(
            ["--length"],
            Description =
                "Number of bytes to write; defaults to the remaining file.",
            Parser = typeof(HexInt64Parser))]
        long? length,
        [CommandParam(
            ["--source-offset"],
            Description = "Byte offset into the input file.",
            Parser = typeof(HexInt64Parser))]
        long? sourceOffset,
        [CommandParam] RockUsbLoaderParameters parameters,
        CancellationToken cancellationToken
    ) =>
        new(runner.RunWriteAsync(
            RockUsbTargetFactory.Create(parameters),
            parameters.Loader,
            input,
            backup,
            sector,
            length,
            sourceOffset,
            !noErase,
            cancellationToken));
}
