using Ampheck.RockchipToolkit.Core.Limits;
using Ampheck.RockchipToolkit.Core.Ports.Files;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Features.Firmware.Model;

public sealed record InputFileContext(
    ILogger Logger,
    Func<string, ValueTask> Report,
    Func<long, long, ValueTask>? Progress,
    ToolkitTransferLimits Limits,
    IRemoteFileClient RemoteFiles
);
