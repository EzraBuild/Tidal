using System.Runtime.InteropServices;
using Tidal.Core.Native;

namespace Tidal.Core.Tests;

/// <summary>Interop structs must match the Windows SDK sizes (x64) exactly or calls corrupt memory.</summary>
public class NativeStructTests
{
    [Fact]
    public void Struct_sizes_match_the_Windows_headers()
    {
        Assert.True(Environment.Is64BitProcess, "sizes below are for x64");
        Assert.Equal(104, Marshal.SizeOf<MONITORINFOEX>());
        Assert.Equal(72, Marshal.SizeOf<DISPLAYCONFIG_PATH_INFO>());
        Assert.Equal(64, Marshal.SizeOf<DISPLAYCONFIG_MODE_INFO>());
        Assert.Equal(84, Marshal.SizeOf<DISPLAYCONFIG_SOURCE_DEVICE_NAME>());
        Assert.Equal(420, Marshal.SizeOf<DISPLAYCONFIG_TARGET_DEVICE_NAME>());
        Assert.Equal(264, Marshal.SizeOf<PHYSICAL_MONITOR>());
    }
}
