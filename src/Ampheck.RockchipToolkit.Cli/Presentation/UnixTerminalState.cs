using System.Runtime.InteropServices;

namespace Ampheck.RockchipToolkit.Cli.Presentation;

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct UnixTerminalState {
    public uint InputFlags;
    public uint OutputFlags;
    public uint ControlFlags;
    public uint LocalFlags;
    public byte LineDiscipline;
    public fixed byte ControlCharacters[32];
    public uint InputSpeed;
    public uint OutputSpeed;
}
