using System.CommandLine;
using Ampheck.RockchipToolkit.Rpc;

namespace Ampheck.RockchipToolkit.Cli.Commands;

internal sealed partial class RockUsbCommands {
    private Option<string?> SectorOption(bool required) {
        return new Option<string?>(
            "--sector",
            "-s") {
            Required = required,
            Description = "Starting sector, decimal or 0x-prefixed hex."
        };
    }

    private Option<string?> CountOption(bool required) {
        return new Option<string?>(
            "--count",
            "-n") {
            Required = required,
            Description = "Number of 512-byte sectors."
        };
    }

    private Option<bool> NoResetOption() {
        return new Option<bool>("--no-reset") {
            Description = "Do not reset after flashing."
        };
    }

    private ToolkitResetMode ParseReset(string value) {
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

    private ToolkitStorage ParseStorage(string value) {
        return value.ToLowerInvariant() switch {
            "emmc" => ToolkitStorage.Emmc,
            "sd-card" => ToolkitStorage.SdCard,
            "spi-nor" => ToolkitStorage.SpiNor,
            _ => throw new ArgumentException(
                $"Invalid storage '{value}'.")
        };
    }
}
