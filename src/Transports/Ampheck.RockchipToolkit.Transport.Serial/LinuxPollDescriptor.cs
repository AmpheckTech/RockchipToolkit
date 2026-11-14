using System.Runtime.InteropServices;

namespace Ampheck.RockchipToolkit.Transport.Serial;

[StructLayout(LayoutKind.Sequential)]
internal struct LinuxPollDescriptor {
    public int FileDescriptor;
    public short Events;
    public short ReturnedEvents;
}
