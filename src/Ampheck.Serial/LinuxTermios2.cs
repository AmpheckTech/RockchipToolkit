using System.Runtime.InteropServices;

namespace Ampheck.Serial;

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct LinuxTermios2 {
    public uint InputFlags;
    public uint OutputFlags;
    public uint ControlFlags;
    public uint LocalFlags;
    public byte LineDiscipline;
    public fixed byte ControlCharacters[19];
    public uint InputSpeed;
    public uint OutputSpeed;
}
