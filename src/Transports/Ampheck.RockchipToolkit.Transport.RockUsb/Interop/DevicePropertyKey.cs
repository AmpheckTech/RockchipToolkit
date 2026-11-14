using System.Runtime.InteropServices;

namespace Ampheck.RockchipToolkit.Transport.RockUsb.Interop;

[StructLayout(LayoutKind.Sequential)]
internal readonly struct DevicePropertyKey {
    public DevicePropertyKey(
        Guid formatId,
        uint propertyId) {
        FormatId = formatId;
        PropertyId = propertyId;
    }

    public Guid FormatId { get; }
    public uint PropertyId { get; }
}
