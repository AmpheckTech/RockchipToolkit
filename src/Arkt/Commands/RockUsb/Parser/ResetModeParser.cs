using Ampheck.RockchipToolkit.Arkt.Commands.Framework.Parser;
using Ampheck.RockchipToolkit.HardwareTransport.Protocol;

namespace Ampheck.RockchipToolkit.Arkt.Commands.RockUsb.Parser;

internal sealed class ResetModeParser :
    ICommandValueParser<ResetMode> {
    public ResetMode Parse(string name, string value) =>
        value.ToLowerInvariant() switch {
            "reset-mass-storage" => ResetMode.ResetMassStorage,
            "power-off" => ResetMode.PowerOff,
            "reset-mask-rom" => ResetMode.ResetMaskRom,
            "disconnect" => ResetMode.Disconnect,
            _ => ResetMode.Normal
        };
}
