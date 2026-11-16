using Ampheck.RockchipToolkit.Core.Limits;
using Ampheck.RockchipToolkit.Core.Ports.Files;
using Ampheck.RockchipToolkit.Features.Devices.Services;
using Ampheck.RockchipToolkit.Features.Firmware.Services;
using Microsoft.Extensions.Logging;

namespace Ampheck.RockchipToolkit.Features.Storage.Services;

internal sealed partial class StorageService(
    DeviceAccess access,
    ToolkitTransferLimits limits,
    IRemoteFileClient remoteFiles,
    ILoggerFactory loggerFactory
) {
    private static void ValidateWrite(
        long inputLength,
        long sourceOffset,
        long length
    ) {
        if (sourceOffset < 0 ||
            length <= 0 ||
            sourceOffset > inputLength ||
            length > inputLength - sourceOffset) {
            throw new InvalidDataException(
                "The write range is outside the input file.");
        }
    }
}
