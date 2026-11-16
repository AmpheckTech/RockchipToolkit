namespace Ampheck.RockchipToolkit.Images.Rkfw;

/// <summary>Entry type in a Rockchip boot image.</summary>
public enum BootEntryType : byte {
    Code471 = 1,
    Code472 = 2,
    Loader = 4
}
