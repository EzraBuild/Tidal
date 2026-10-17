using System.Runtime.Versioning;
using Tidal.Linux.Native;

namespace Tidal.Linux.Tests;

/// <summary>The real file system and C library, on Linux.</summary>
[SupportedOSPlatform("linux")]
public sealed class LinuxSystemTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("tidal-sysfs-").FullName;

    [LinuxFact]
    public void Sysfs_reads_text_bytes_entries_and_links()
    {
        Directory.CreateDirectory($"{_root}/devices/i2c-5");
        File.WriteAllText($"{_root}/status", "connected\n");
        File.CreateSymbolicLink($"{_root}/ddc", "devices/i2c-5");
        var sysfs = new Sysfs();

        Assert.Equal("connected", sysfs.ReadText($"{_root}/status"));
        Assert.Equal("connected\n"u8.ToArray(), sysfs.ReadBytes($"{_root}/status"));
        Assert.Null(sysfs.ReadText($"{_root}/missing"));
        Assert.Equal(["ddc", "devices", "status"], sysfs.List(_root).Order());
        Assert.Equal("i2c-5", sysfs.LinkName($"{_root}/ddc"));
        Assert.Null(sysfs.LinkName($"{_root}/status"));
        Assert.Equal($"{_root}/devices/i2c-5", sysfs.RealPath($"{_root}/ddc"));
    }

    [LinuxFact]
    public void Sysfs_reads_files_that_report_no_size()
    {
        Assert.Contains("Name:", new Sysfs().ReadText("/proc/self/status"));
    }

    [LinuxFact]
    public void Sysfs_writes_files_and_says_when_it_may_not()
    {
        string path = $"{_root}/brightness";
        File.WriteAllText(path, "100\n");
        var sysfs = new Sysfs();

        Assert.True(sysfs.TryWrite(path, "42"));
        Assert.StartsWith("42", File.ReadAllText(path));

        if (Environment.IsPrivilegedProcess) return; // root may write anything
        File.SetUnixFileMode(path, UnixFileMode.UserRead);
        Assert.False(sysfs.TryWrite(path, "43"));
    }

    [LinuxFact]
    public void A_missing_bus_is_file_not_found()
    {
        Assert.Throws<FileNotFoundException>(() => DevI2cBus.Open(250));
    }

    [LinuxFact]
    public void I2c_calls_report_the_system_error()
    {
        // /dev/null isn't an I2C adapter, so selecting an address fails with ENOTTY.
        using var bus = DevI2cBus.Open("/dev/null");

        var error = Assert.Throws<IOException>(() => bus.Write(0x37, [0x51, 0x80, 0x3F]));
        Assert.Contains("select address 0x37", error.Message);
        Assert.Contains("errno 25", error.Message);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
