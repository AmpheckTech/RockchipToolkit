using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Ampheck.Serial;

internal sealed unsafe class LinuxSerialPortConnection(
    SerialPortOptions options) : ISerialPortConnection {
    private const int OpenReadWrite = 0x0002;
    private const int OpenNoControllingTerminal = 0x0100;
    private const int OpenNonBlocking = 0x0800;
    private const int OpenCloseOnExec = 0x80000;
    private const nuint GetTermios = 0x802C542A;
    private const nuint SetTermios = 0x402C542B;
    private const uint ControlBaud = 0x100F;
    private const uint ControlBaudOther = 0x1000;
    private const uint ControlSize = 0x30;
    private const uint ControlStopBits = 0x40;
    private const uint ControlRead = 0x80;
    private const uint ControlParityEnable = 0x100;
    private const uint ControlParityOdd = 0x200;
    private const uint ControlLocal = 0x800;
    private const uint ControlMarkSpaceParity = 0x40000000;
    private const uint ControlRtsCts = 0x80000000;
    private const uint OutputPostProcess = 0x1;
    private const uint InputBreak = 0x1;
    private const uint InputBreakInterrupt = 0x2;
    private const uint InputParityError = 0x8;
    private const uint InputStrip = 0x20;
    private const uint InputNewLine = 0x40;
    private const uint InputIgnoreCarriageReturn = 0x80;
    private const uint InputCarriageReturnToNewLine = 0x100;
    private const uint InputXOn = 0x0400;
    private const uint InputXOff = 0x1000;
    private const uint LocalInterrupt = 0x1;
    private const uint LocalCanonical = 0x2;
    private const uint LocalEcho = 0x8;
    private const uint LocalEchoNewLine = 0x40;
    private const uint LocalExtended = 0x8000;
    private const int ControlCharacterTimeout = 5;
    private const int ControlCharacterMinimum = 6;
    private const int PollInput = 0x0001;
    private const int PollOutput = 0x0004;
    private const int PollError = 0x0008;
    private const int PollHangUp = 0x0010;
    private const int PollInvalid = 0x0020;
    private const int NoData = 0;
    private const int Interrupted = 4;
    private const int TryAgain = 11;
    private const int NotATerminal = 25;
    private const nuint SetBits = 0x5416;
    private const nuint ClearBits = 0x5417;
    private const int ModemDtr = 0x002;
    private const int ModemRts = 0x004;
    private readonly LinuxPollDescriptor[] _poll = [new()];
    private int _fileDescriptor = -1;

    public void Open() {
        if (Volatile.Read(ref _fileDescriptor) >= 0) {
            return;
        }

        var descriptor = OpenNative(
            options.PortName,
            OpenReadWrite |
            OpenNoControllingTerminal |
            OpenNonBlocking |
            OpenCloseOnExec);
        if (descriptor < 0) {
            throw Error("open");
        }

        try {
            Configure(descriptor);
            Volatile.Write(ref _fileDescriptor, descriptor);
        } catch {
            CloseNative(descriptor);
            throw;
        }
    }

    public int Read(
        byte[] buffer,
        int offset,
        int count) {
        var descriptor = Volatile.Read(
            ref _fileDescriptor);
        if (descriptor < 0) {
            throw new IOException("The serial port is not open.");
        }

        _poll[0] = new LinuxPollDescriptor {
            FileDescriptor = descriptor,
            Events = PollInput
        };
        var ready = Poll(_poll, 1, 100);
        if (ready < 0) {
            if (Marshal.GetLastPInvokeError() == Interrupted) {
                return 0;
            }

            throw Error("poll");
        }

        if (ready == 0) {
            return 0;
        }

        var events = _poll[0].ReturnedEvents;
        if ((events & (PollError | PollHangUp | PollInvalid)) != 0) {
            throw new IOException(
                $"Serial port '{options.PortName}' disconnected.");
        }

        fixed (byte* pointer = buffer) {
            var read = ReadNative(
                descriptor,
                pointer + offset,
                checked((nuint)count));
            if (read >= 0) {
                return checked((int)read);
            }
        }

        var error = Marshal.GetLastPInvokeError();
        if (error is Interrupted or TryAgain) {
            return 0;
        }

        throw Error("read");
    }

    public void Write(ReadOnlyMemory<byte> data) {
        var descriptor = Volatile.Read(
            ref _fileDescriptor);
        if (descriptor < 0) {
            throw new IOException("The serial port is not open.");
        }

        using var pinned = data.Pin();
        var offset = 0;
        while (offset < data.Length) {
            var written = WriteNative(
                descriptor,
                (byte*)pinned.Pointer + offset,
                checked((nuint)(data.Length - offset)));
            if (written > 0) {
                offset = checked(offset + (int)written);
                continue;
            }

            var error = Marshal.GetLastPInvokeError();
            if (error == Interrupted) {
                continue;
            }

            if (error is TryAgain or NoData) {
                _poll[0] = new LinuxPollDescriptor {
                    FileDescriptor = descriptor,
                    Events = PollOutput
                };
                if (Poll(_poll, 1, 1000) <= 0) {
                    throw Error("poll");
                }

                continue;
            }

            throw Error("write");
        }
    }

    public void Dispose() {
        var descriptor = Interlocked.Exchange(
            ref _fileDescriptor,
            -1);
        if (descriptor >= 0) {
            CloseNative(descriptor);
        }
    }

    private void Configure(int descriptor) {
        var attributes = new LinuxTermios2();
        if (Ioctl(
                descriptor,
                GetTermios,
                ref attributes) != 0) {
            throw Error("TCGETS2");
        }

        MakeRaw(ref attributes);
        attributes.ControlFlags |=
            ControlLocal |
            ControlRead;
        attributes.ControlFlags =
            (attributes.ControlFlags & ~ControlSize) |
            options.DataBits switch {
                5 => 0u,
                6 => 0x10u,
                7 => 0x20u,
                8 => 0x30u,
                _ => throw new ArgumentException(
                    "Data bits must be between 5 and 8.",
                    nameof(options))
            };
        attributes.ControlFlags &=
            ~(ControlStopBits |
                ControlParityEnable |
                ControlParityOdd |
                ControlMarkSpaceParity |
                ControlRtsCts);
        if (options.StopBits != SerialStopBits.One) {
            attributes.ControlFlags |= ControlStopBits;
        }

        switch (options.Parity) {
            case SerialParity.Odd:
                attributes.ControlFlags |=
                    ControlParityEnable |
                    ControlParityOdd;
                break;
            case SerialParity.Even:
                attributes.ControlFlags |=
                    ControlParityEnable;
                break;
            case SerialParity.Mark:
                attributes.ControlFlags |=
                    ControlParityEnable |
                    ControlParityOdd |
                    ControlMarkSpaceParity;
                break;
            case SerialParity.Space:
                attributes.ControlFlags |=
                    ControlParityEnable |
                    ControlMarkSpaceParity;
                break;
        }

        attributes.InputFlags &= ~(InputXOn | InputXOff);
        if (options.Handshake is
            SerialHandshake.XOnXOff or
            SerialHandshake.RequestToSendXOnXOff) {
            attributes.InputFlags |=
                InputXOn |
                InputXOff;
        }

        if (options.Handshake is
            SerialHandshake.RequestToSend or
            SerialHandshake.RequestToSendXOnXOff) {
            attributes.ControlFlags |= ControlRtsCts;
        }

        var speed = checked((uint)options.BaudRate);
        attributes.ControlFlags =
            (attributes.ControlFlags & ~ControlBaud) |
            ControlBaudOther;
        attributes.InputSpeed = speed;
        attributes.OutputSpeed = speed;
        if (Ioctl(
                descriptor,
                SetTermios,
                ref attributes) != 0) {
            throw Error("TCSETS2");
        }

        SetModemLine(
            descriptor,
            ModemDtr,
            options.DtrEnable);
        SetModemLine(
            descriptor,
            ModemRts,
            options.RtsEnable);
    }

    private static void MakeRaw(ref LinuxTermios2 attributes) {
        attributes.InputFlags &=
            ~(InputBreak |
                InputBreakInterrupt |
                InputParityError |
                InputStrip |
                InputNewLine |
                InputIgnoreCarriageReturn |
                InputCarriageReturnToNewLine |
                InputXOn);
        attributes.OutputFlags &= ~OutputPostProcess;
        attributes.LocalFlags &=
            ~(LocalEcho |
                LocalEchoNewLine |
                LocalCanonical |
                LocalInterrupt |
                LocalExtended);
        attributes.ControlFlags &= ~(ControlSize | ControlParityEnable);
        attributes.ControlCharacters[ControlCharacterTimeout] = 0;
        attributes.ControlCharacters[ControlCharacterMinimum] = 1;
    }

    private void SetModemLine(
        int descriptor,
        int line,
        bool enabled) {
        var value = line;
        if (Ioctl(
                descriptor,
                enabled ? SetBits : ClearBits,
                ref value) == 0 ||
            Marshal.GetLastPInvokeError() == NotATerminal) {
            return;
        }

        throw Error("ioctl");
    }

    private IOException Error(string operation) {
        var code = Marshal.GetLastPInvokeError();
        var message = new Win32Exception(code).Message;
        return new IOException(
            $"Serial port '{options.PortName}' {operation} failed: " +
            message);
    }

    [DllImport(
        "libc",
        EntryPoint = "open",
        SetLastError = true)]
    private static extern int OpenNative(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        int flags);

    [DllImport(
        "libc",
        EntryPoint = "close",
        SetLastError = true)]
    private static extern int CloseNative(int descriptor);

    [DllImport(
        "libc",
        EntryPoint = "read",
        SetLastError = true)]
    private static extern nint ReadNative(
        int descriptor,
        byte* buffer,
        nuint count);

    [DllImport(
        "libc",
        EntryPoint = "write",
        SetLastError = true)]
    private static extern nint WriteNative(
        int descriptor,
        byte* buffer,
        nuint count);

    [DllImport(
        "libc",
        EntryPoint = "poll",
        SetLastError = true)]
    private static extern int Poll(
        [In, Out] LinuxPollDescriptor[] descriptors,
        nuint count,
        int timeout);

    [DllImport(
        "libc",
        EntryPoint = "ioctl",
        SetLastError = true)]
    private static extern int Ioctl(
        int descriptor,
        nuint request,
        ref int value);

    [DllImport(
        "libc",
        EntryPoint = "ioctl",
        SetLastError = true)]
    private static extern int Ioctl(
        int descriptor,
        nuint request,
        ref LinuxTermios2 attributes);
}
