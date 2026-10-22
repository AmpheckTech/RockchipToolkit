using Ampheck.RockchipToolkit.Transport;
using LibUsbDotNet.Info;
using LibUsbDotNet.LibUsb;

namespace Ampheck.RockchipToolkit.LibUsb.Transport;

internal static class LibUsbEndpointSelector {
    public static LibUsbEndpointSelection Select(
        IUsbDevice device,
        LibUsbOptions options) {
        if (options.BulkInEndpoint != 0 &&
            options.BulkOutEndpoint != 0) {
            return Validate(
                options.InterfaceNumber,
                options.BulkInEndpoint,
                options.BulkOutEndpoint);
        }

        var setting = FindRockUsbInterface(device, options.InterfaceNumber);
        if (setting is null) {
            throw new IOException(
                "No USB interface with both Bulk IN and OUT endpoints was found.");
        }

        var bulkIn = options.BulkInEndpoint;
        var bulkOut = options.BulkOutEndpoint;
        foreach (var endpoint in setting.Endpoints) {
            if ((endpoint.Attributes & 0x03) != 0x02) {
                continue;
            }

            if ((endpoint.EndpointAddress & 0x80) != 0 &&
                bulkIn == 0) {
                bulkIn = endpoint.EndpointAddress;
            } else if ((endpoint.EndpointAddress & 0x80) == 0 &&
                bulkOut == 0) {
                bulkOut = endpoint.EndpointAddress;
            }
        }

        return Validate(
            checked((byte)setting.Number),
            bulkIn,
            bulkOut);
    }

    private static UsbInterfaceInfo? FindRockUsbInterface(
        IUsbDevice device,
        byte requestedInterfaceNumber) {
        var configuration = device.Configs.FirstOrDefault();
        if (configuration is null) {
            return null;
        }

        return configuration.Interfaces.FirstOrDefault(candidate =>
            (requestedInterfaceNumber == 0 ||
                candidate.Number == requestedInterfaceNumber) &&
            (requestedInterfaceNumber != 0 ||
                RockchipUsbInterfaceProfile.Matches(
                    (byte)candidate.Class,
                    (byte)candidate.SubClass,
                    (byte)candidate.Protocol)) &&
            HasBulkEndpoints(candidate));
    }

    private static bool HasBulkEndpoints(UsbInterfaceInfo setting) {
        return setting.Endpoints.Any(endpoint =>
                (endpoint.Attributes & 0x03) == 0x02 &&
                (endpoint.EndpointAddress & 0x80) != 0) &&
            setting.Endpoints.Any(endpoint =>
                (endpoint.Attributes & 0x03) == 0x02 &&
                (endpoint.EndpointAddress & 0x80) == 0);
    }

    private static LibUsbEndpointSelection Validate(
        byte interfaceNumber,
        byte bulkIn,
        byte bulkOut) {
        if (bulkIn == 0 || (bulkIn & 0x80) == 0) {
            throw new IOException("The LibUSB bulk IN endpoint is invalid.");
        }

        if (bulkOut == 0 || (bulkOut & 0x80) != 0) {
            throw new IOException("The LibUSB bulk OUT endpoint is invalid.");
        }

        return new LibUsbEndpointSelection(
            interfaceNumber,
            bulkIn,
            bulkOut);
    }
}
