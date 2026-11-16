using Ampheck.RockchipToolkit.Arkt.Commands.Framework.Parser;
using Ampheck.RockchipToolkit.HardwareTransport.Protocol;

namespace Ampheck.RockchipToolkit.Arkt.Commands.RockUsb.Parser;

internal sealed class StorageTypeParser :
    ICommandValueParser<StorageType> {
    public StorageType Parse(string name, string value) =>
        value.ToLowerInvariant() switch {
            "sd-card" => StorageType.SdCard,
            "spi-nor" => StorageType.SpiNor,
            _ => StorageType.Emmc
        };
}
