namespace Ampheck.RockchipToolkit.Protocol;

public sealed class RockUsbException : IOException {
    public RockUsbException(
        string message,
        RockUsbOperation operation,
        Exception? inner = null,
        RockUsbStatus? status = null,
        uint? residue = null)
        : base(message, inner) {
        Operation = operation;
        Status = status;
        Residue = residue;
    }

    public RockUsbOperation Operation { get; }
    public RockUsbStatus? Status { get; }
    public uint? Residue { get; }
}
