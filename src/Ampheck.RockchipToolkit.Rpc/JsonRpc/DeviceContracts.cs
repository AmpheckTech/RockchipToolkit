namespace Ampheck.RockchipToolkit.Rpc.JsonRpc;

public sealed record DeviceListParams {
    public required DeviceTarget Target { get; init; }
}

public sealed record DeviceDescriptor {
    public required ushort VendorId { get; init; }
    public required ushort ProductId { get; init; }
    public required ushort UsbVersion { get; init; }
    public required string Mode { get; init; }
    public string? SerialNumber { get; init; }
    public string? PlatformId { get; init; }
    public string? Description { get; init; }
}

public sealed record DeviceListResult {
    public required DeviceDescriptor[] Devices { get; init; }
}

public sealed record DeviceLoaderParams {
    public required DeviceTarget Target { get; init; }
    public string? BlobId { get; init; }
    public string? Url { get; init; }
}

public sealed record DeviceInfoParams {
    public required DeviceTarget Target { get; init; }
    public string? LoaderBlobId { get; init; }
    public string? LoaderUrl { get; init; }
}

public sealed record DeviceInfoResult {
    public required string ChipIdentifier { get; init; }
    public required string PlatformName { get; init; }
    public required string Manufacturer { get; init; }
    public required byte ManufacturerCode { get; init; }
    public required long CapacityBytes { get; init; }
    public required uint BlockSizeBytes { get; init; }
    public required uint PageSizeBytes { get; init; }
    public required uint BlockCount { get; init; }
    public required byte EccBits { get; init; }
    public required byte AccessTime { get; init; }
    public required byte ChipSelectMask { get; init; }
}

public sealed record DeviceChipInfoParams {
    public required DeviceTarget Target { get; init; }
    public string? LoaderBlobId { get; init; }
    public string? LoaderUrl { get; init; }
}

public sealed record DeviceChipInfoResult {
    public required string Identifier { get; init; }
    public required string PlatformName { get; init; }
}

public sealed record DeviceChipIdParams {
    public required DeviceTarget Target { get; init; }
    public string? LoaderBlobId { get; init; }
    public string? LoaderUrl { get; init; }
}

public sealed record DeviceResetParams {
    public required DeviceTarget Target { get; init; }
    public ToolkitResetMode? Mode { get; init; }
}

public sealed record DeviceCapabilityParams {
    public required DeviceTarget Target { get; init; }
    public string? LoaderBlobId { get; init; }
    public string? LoaderUrl { get; init; }
}

public sealed record DeviceCapabilityResult {
    public required ulong Flags { get; init; }
}

public sealed record DeviceChipIdResult {
    public required string ChipId { get; init; }
}
