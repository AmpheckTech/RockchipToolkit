using System.Runtime.InteropServices;

namespace Ampheck.RockchipToolkit.HardwareTransport.Backends.Serial;

[StructLayout(LayoutKind.Sequential)]
internal struct LinuxPollDescriptor {
    public int FileDescriptor;
    public short Events;
    public short ReturnedEvents;
}
