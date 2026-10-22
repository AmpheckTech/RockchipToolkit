using System.Runtime.InteropServices;

namespace Ampheck.Serial;

[StructLayout(LayoutKind.Sequential)]
internal struct LinuxPollDescriptor {
    public int FileDescriptor;
    public short Events;
    public short ReturnedEvents;
}
