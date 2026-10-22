using System.ComponentModel;
using System.Runtime.InteropServices;
using Ampheck.RockchipToolkit.RockUsb.Configuration;
using Ampheck.RockchipToolkit.RockUsb.Interop;
using Microsoft.Win32.SafeHandles;

namespace Ampheck.RockchipToolkit.RockUsb.Transport;

internal static class RockUsbBootIo {
    private const int BlockSize = 4096;

    public static void DownloadEntry(
        string path,
        RockUsbOptions options,
        ushort requestCode,
        ReadOnlySpan<byte> data) {
        var ioctl = requestCode switch {
            0x0471 => RockUsbNative.DownloadBoot471Ioctl,
            0x0472 => RockUsbNative.DownloadBoot472Ioctl,
            _ => throw new ArgumentOutOfRangeException(
                nameof(requestCode),
                "Rockchip BOOT entries use request 0x0471 or 0x0472.")
        };

        using var handle = RockUsbPipeIo.OpenFile(path, options);
        if (handle.IsInvalid) {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Unable to open the rockusb.sys device for BOOT download.");
        }

        var packet = BuildPacket(data, out var sendPendingPacket);
        SendPacket(handle, ioctl, packet, sendPendingPacket);
    }

    private static byte[] BuildPacket(
        ReadOnlySpan<byte> data,
        out bool sendPendingPacket) {
        var payloadLength = data.Length;
        sendPendingPacket =
            payloadLength % BlockSize == BlockSize - 2;
        if (payloadLength % BlockSize == BlockSize - 1) {
            payloadLength++;
        }

        var packet = new byte[checked(payloadLength + 2)];
        data.CopyTo(packet);
        var crc = Crc16Ccitt.Compute(packet.AsSpan(0, payloadLength));
        packet[payloadLength] = (byte)(crc >> 8);
        packet[payloadLength + 1] = (byte)crc;
        return packet;
    }

    private static void SendPacket(
        SafeFileHandle handle,
        uint ioctl,
        byte[] packet,
        bool sendPendingPacket) {
        for (var offset = 0; offset < packet.Length; offset += BlockSize) {
            var length = Math.Min(BlockSize, packet.Length - offset);
            var chunk = packet.AsSpan(offset, length).ToArray();
            if (!RockUsbNative.DeviceIoControl(
                    handle,
                    ioctl,
                    chunk,
                    checked((uint)chunk.Length),
                    null,
                    0,
                    out _,
                    IntPtr.Zero)) {
                var error = Marshal.GetLastWin32Error();
                throw new Win32Exception(
                    error,
                    $"rockusb.sys BOOT IOCTL 0x{ioctl:X8} failed at " +
                    $"{offset}/{packet.Length} (Win32 {error}).");
            }

            Thread.Sleep(50);
        }

        if (sendPendingPacket &&
            !RockUsbNative.DeviceIoControl(
                handle,
                ioctl,
                new byte[1],
                1,
                null,
                0,
                out _,
                IntPtr.Zero)) {
            var error = Marshal.GetLastWin32Error();
            throw new Win32Exception(
                error,
                $"rockusb.sys BOOT pending packet failed (Win32 {error}).");
        }
    }

}
