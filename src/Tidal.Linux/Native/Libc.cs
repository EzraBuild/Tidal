using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Tidal.Linux.Native;

// The C library calls for i2c-dev. A SafeFileHandle marshals as its file descriptor; the int
// parameter reads the low half of the pointer-sized register it arrives in.

internal static unsafe class Libc
{
    /// <summary>i2c-dev: sets the device address that later read() and write() calls talk to.</summary>
    public const nuint I2C_SLAVE = 0x0703;

    [DllImport("libc", SetLastError = true)]
    public static extern nint read(SafeFileHandle fd, byte* buffer, nuint count);

    [DllImport("libc", SetLastError = true)]
    public static extern nint write(SafeFileHandle fd, byte* buffer, nuint count);

    [DllImport("libc", SetLastError = true)]
    public static extern int ioctl(SafeFileHandle fd, nuint request, nint argument);
}
