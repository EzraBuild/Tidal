using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Tidal.Linux.Displays;

namespace Tidal.Linux.Native;

/// <summary>An I2C bus through Linux's i2c-dev driver (/dev/i2c-N). Used from one thread at a time.</summary>
internal sealed unsafe class DevI2cBus : I2cBus
{
    private readonly SafeFileHandle _handle;
    private int _address = -1;

    private DevI2cBus(SafeFileHandle handle) => _handle = handle;

    /// <summary>
    /// Opens /dev/i2c-N. Throws <see cref="FileNotFoundException"/> when the i2c-dev module isn't
    /// loaded and <see cref="UnauthorizedAccessException"/> when this user may not use the bus.
    /// </summary>
    public static DevI2cBus Open(int number) => Open($"/dev/i2c-{number}");

    internal static DevI2cBus Open(string path)
        => new(File.OpenHandle(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite));

    public override void Write(byte address, ReadOnlySpan<byte> data)
    {
        Select(address);
        nint written;
        fixed (byte* bytes = data) written = Libc.write(_handle, bytes, (nuint)data.Length);
        if (written < 0) throw LastError("write");
        if (written != data.Length) throw new IOException($"write: sent {written} of {data.Length} bytes");
    }

    public override void Read(byte address, Span<byte> buffer)
    {
        Select(address);
        nint read;
        fixed (byte* bytes = buffer) read = Libc.read(_handle, bytes, (nuint)buffer.Length);
        if (read < 0) throw LastError("read");
        if (read != buffer.Length) throw new IOException($"read: got {read} of {buffer.Length} bytes");
    }

    private void Select(byte address)
    {
        if (_address == address) return;
        if (Libc.ioctl(_handle, Libc.I2C_SLAVE, address) < 0) throw LastError($"select address 0x{address:X2}");
        _address = address;
    }

    private static IOException LastError(string operation)
    {
        int errno = Marshal.GetLastPInvokeError();
        return new IOException($"{operation}: {Marshal.GetPInvokeErrorMessage(errno)} (errno {errno})");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _handle.Dispose();
    }
}
