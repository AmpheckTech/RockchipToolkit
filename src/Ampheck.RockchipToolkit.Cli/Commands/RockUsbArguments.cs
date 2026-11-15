using Ampheck.RockchipToolkit.Rpc;

namespace Ampheck.RockchipToolkit.Cli.Commands;

internal sealed record DeviceTargetArguments(
    TransportKind Transport = TransportKind.Auto,
    ushort? VendorId = null,
    ushort? ProductId = null);

internal sealed record LoaderArguments(
    string? Loader,
    InputFetchLocation FetchLocation);

internal sealed record DeviceQueryArguments(
    DeviceTargetArguments Target,
    LoaderArguments Loader);

internal sealed record DeviceLoaderArguments(
    DeviceTargetArguments Target,
    string Location,
    InputFetchLocation FetchLocation);

internal sealed record FirmwareFlashArguments(
    DeviceTargetArguments Target,
    string Source,
    InputFetchLocation FetchLocation,
    string? Loader,
    bool ResetAfterFlash);

internal sealed record StorageReadArguments(
    DeviceTargetArguments Target,
    LoaderArguments Loader,
    string Output,
    uint Sector,
    ushort Count);

internal sealed record StorageWriteArguments(
    DeviceTargetArguments Target,
    LoaderArguments Loader,
    string Input,
    InputFetchLocation FetchLocation,
    string? Backup,
    uint Sector,
    long? Length,
    long? SourceOffset,
    bool EraseBeforeWrite);

internal sealed record StorageEraseArguments(
    DeviceTargetArguments Target,
    LoaderArguments Loader,
    uint Sector,
    ushort Count);

internal sealed record StorageSelectArguments(
    DeviceTargetArguments Target,
    LoaderArguments Loader,
    ToolkitStorage Storage);

internal sealed record DeviceResetArguments(
    DeviceTargetArguments Target,
    ToolkitResetMode Mode);
