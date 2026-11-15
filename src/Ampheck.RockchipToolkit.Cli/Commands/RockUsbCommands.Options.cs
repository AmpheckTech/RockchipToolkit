using System.CommandLine;
using Ampheck.RockchipToolkit.Cli.Execution.Rpc;
using Ampheck.RockchipToolkit.Rpc;

namespace Ampheck.RockchipToolkit.Cli.Commands;

internal static partial class RockUsbCommands {
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

    private static Task<int> ExecuteRpc(
        string command,
        CommandOptions options,
        CliCommandContext context) {
        return ToolkitClientCommandRunner.RunAsync(
            command,
            options,
            context.Endpoint!,
            context.Logger,
            context.Console);
    }
}
