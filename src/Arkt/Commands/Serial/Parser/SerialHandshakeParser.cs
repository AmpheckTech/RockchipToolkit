using Ampheck.RockchipToolkit.Arkt.Commands.Framework.Parser;
using Ampheck.RockchipToolkit.HardwareTransport.Ports.Serial;

namespace Ampheck.RockchipToolkit.Arkt.Commands.Serial.Parser;

internal sealed class SerialHandshakeParser :
    ICommandValueParser<SerialHandshake> {
    public SerialHandshake Parse(string name, string value) =>
        value.ToLowerInvariant() switch {
            "xon-xoff" => SerialHandshake.XOnXOff,
            "rts-cts" => SerialHandshake.RequestToSend,
            "rts-cts-xon-xoff" =>
                SerialHandshake.RequestToSendXOnXOff,
            _ => SerialHandshake.None
        };
}
