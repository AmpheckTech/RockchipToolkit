namespace Ampheck.RockchipToolkit.HardwareTransport.Protocol;

public sealed partial class RockUsbProtocol {
    public ValueTask<byte[]> ReadChipInfoAsync(
        CancellationToken cancellationToken = default
    ) {
        return ReadFixedAsync(
            RockUsbOperation.ReadChipInfo,
            16,
            cancellationToken);
    }

    public ValueTask<byte[]> ReadFlashIdAsync(
        CancellationToken cancellationToken = default
    ) {
        return ReadFixedAsync(
            RockUsbOperation.ReadFlashId,
            5,
            cancellationToken);
    }

    public ValueTask<byte[]> ReadFlashInfoAsync(
        CancellationToken cancellationToken = default
    ) {
        return ReadVariableAsync(
            RockUsbOperation.ReadFlashInfo,
            11,
            MaxFlashInfoLength,
            cancellationToken);
    }

    public ValueTask<byte[]> ReadCapabilityAsync(
        CancellationToken cancellationToken = default
    ) {
        return ReadFixedAsync(
            RockUsbOperation.ReadCapability,
            8,
            cancellationToken);
    }

    private async ValueTask<byte[]> ReadFixedAsync(
        RockUsbOperation operation,
        int length,
        CancellationToken cancellationToken
    ) {
        var data = new byte[length];
        await ExecuteAsync(
            operation,
            0,
            0,
            ReadWriteMode.Image,
            data,
            true,
            cancellationToken,
            data).ConfigureAwait(false);
        return data;
    }

    private async ValueTask<byte[]> ReadVariableAsync(
        RockUsbOperation operation,
        int declaredLength,
        int maximumLength,
        CancellationToken cancellationToken
    ) {
        var data = new byte[maximumLength];
        var actualLength = await ExecuteAsync(
            operation,
            0,
            0,
            ReadWriteMode.Image,
            data.AsMemory(0, declaredLength),
            true,
            cancellationToken,
            data,
            readMaximumLength: maximumLength).ConfigureAwait(false);

        if (actualLength < declaredLength) {
            throw new RockUsbException(
                $"The device returned only {actualLength} bytes for " +
                $"{operation}; at least {declaredLength} were required.",
                operation);
        }

        return data.AsSpan(0, actualLength).ToArray();
    }
}
