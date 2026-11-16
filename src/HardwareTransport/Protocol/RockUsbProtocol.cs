using Ampheck.RockchipToolkit.HardwareTransport.Ports.Usb;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ampheck.RockchipToolkit.HardwareTransport.Protocol;

/// <summary>
/// Implements Rockchip's UFI-style bulk protocol without owning a USB backend.
/// </summary>
/// <remarks>
/// Command envelopes follow USB Mass Storage Bulk-Only Transport: a 31-byte
/// Command Block Wrapper (CBW), optional data payload, then a 13-byte Command
/// Status Wrapper (CSW). Rockchip opcodes occupy the CBW command bytes. The
/// CBW and CSW signatures, tag, transfer length, and residue are little-endian;
/// the RockUSB address and transfer count are big-endian.
/// </remarks>
public sealed partial class RockUsbProtocol : IDisposable {
    public const int SectorSize = 512;
    public const ushort MaxSectorsPerTransfer = ushort.MaxValue;

    private const int CbwLength = 31; // Fixed Bulk-Only Transport CBW size.
    private const int CswLength = 13; // Fixed Bulk-Only Transport CSW size.
    private const int MaxFlashInfoLength = 512; // Maximum flash-info response.
    private const uint CbwSignature = 0x43425355; // "USBC" in wire order.
    private const uint CswSignature = 0x53425355; // "USBS" in wire order.
    private readonly IUsbTransport _transport;
    private readonly TimeSpan _timeout;
    private readonly ILogger<RockUsbProtocol> _logger;
    private readonly SemaphoreSlim _commandGate = new(1, 1);
    private uint _tag = 0xA5A50000; // Arbitrary nonzero CBW/CSW correlation tag.
    private bool _disposed;

    public RockUsbProtocol(
        IUsbTransport transport,
        TimeSpan? timeout = null,
        ILogger<RockUsbProtocol>? logger = null
    ) {
        _transport = transport ??
            throw new ArgumentNullException(nameof(transport));
        _timeout = timeout ?? TimeSpan.FromSeconds(5);
        _logger = logger ?? NullLogger<RockUsbProtocol>.Instance;
        if (_timeout <= TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                "The command timeout must be positive.");
        }
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        _logger.LogTrace("Disposing RockUSB protocol");
        _commandGate.Dispose();
    }
}
