using System.Runtime.InteropServices;

namespace Ampheck.RockchipToolkit.Hardware.Serial;

[StructLayout(LayoutKind.Sequential)]
internal struct LinuxPollDescriptor {
    public int FileDescriptor;
    public short Events;
    public short ReturnedEvents;
}
