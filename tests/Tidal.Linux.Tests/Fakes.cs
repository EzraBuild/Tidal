using System.Text;
using Tidal.Linux.Displays;

namespace Tidal.Linux.Tests;

/// <summary>A made-up sysfs: files, symbolic links, and which files this user may write.</summary>
internal sealed class FakeSysfs : ISysfs
{
    private readonly Dictionary<string, byte[]> _files = [];
    private readonly Dictionary<string, string> _links = []; // path → absolute target

    /// <summary>Files writes are refused for, as for a user without permission.</summary>
    public HashSet<string> ReadOnly { get; } = [];

    public FakeSysfs File(string path, string text) => File(path, Encoding.ASCII.GetBytes(text + "\n"));

    public FakeSysfs File(string path, byte[] bytes)
    {
        _files[path] = bytes;
        return this;
    }

    public FakeSysfs Link(string path, string target)
    {
        _links[path] = target;
        return this;
    }

    public string? Text(string path) => _files.TryGetValue(path, out var bytes) ? Encoding.ASCII.GetString(bytes).TrimEnd('\n') : null;

    public IReadOnlyList<string> List(string directory)
    {
        string prefix = directory.TrimEnd('/') + "/";
        return _files.Keys.Concat(_links.Keys)
            .Where(path => path.StartsWith(prefix, StringComparison.Ordinal))
            .Select(path => path[prefix.Length..].Split('/')[0])
            .Distinct()
            .ToList();
    }

    public string? ReadText(string path) => Text(path);

    public byte[]? ReadBytes(string path) => _files.TryGetValue(path, out var bytes) ? bytes : null;

    public string? LinkName(string path) => _links.TryGetValue(path, out var target) ? target.Split('/')[^1] : null;

    public string RealPath(string path) => _links.TryGetValue(path, out var target) ? target : path;

    public bool TryWrite(string path, string text)
    {
        if (ReadOnly.Contains(path)) return false;
        File(path, text);
        return true;
    }
}

internal sealed class FakeLogind : ILogind
{
    public List<(string Subsystem, string Device, uint Value)> Calls { get; } = [];

    public void SetBrightness(string subsystem, string device, uint value) => Calls.Add((subsystem, device, value));
}

/// <summary>
/// A monitor at the end of an I2C bus: an EDID at 0x50 and a DDC/CI interface at 0x37 that
/// answers Get VCP and capabilities requests the way the standard describes.
/// </summary>
internal sealed class FakeMonitor : I2cBus
{
    private byte[]? _reply;

    public byte[]? Edid { get; init; }

    public Dictionary<byte, (ushort Current, ushort Maximum)> Vcp { get; } = [];

    public string Capabilities { get; set; } = "";

    /// <summary>Everything written to the DDC/CI address, one message per write.</summary>
    public List<byte[]> Received { get; } = [];

    /// <summary>Changes the next reply before it's read, e.g. to break its checksum.</summary>
    public Func<byte[], byte[]>? Corrupt { get; set; }

    public bool Disposed { get; private set; }

    public override void Write(byte address, ReadOnlySpan<byte> data)
    {
        if (address == 0x50)
        {
            if (Edid is null) throw new IOException("no EDID");
            return;
        }
        if (address != I2cDdcChannel.Address) throw new IOException("nothing at that address");

        var message = data.ToArray();
        Received.Add(message);
        Assert.Equal(0x51, message[0]);
        Assert.Equal(0x80 | (message.Length - 3), message[1]);
        Assert.Equal(Xor(0x6E, message[..^1]), message[^1]);

        _reply = message[2] switch
        {
            0x01 => GetVcpReply(message[3]),
            0xF3 => CapabilitiesReply(message[3] << 8 | message[4]),
            _ => null, // Set VCP: no reply
        };
        if (message[2] == 0x03) Vcp[message[3]] = ((ushort)(message[4] << 8 | message[5]), Vcp.GetValueOrDefault(message[3]).Maximum);
    }

    public override void Read(byte address, Span<byte> buffer)
    {
        if (address == 0x50)
        {
            (Edid ?? throw new IOException("no EDID")).AsSpan(0, buffer.Length).CopyTo(buffer);
            return;
        }
        var reply = _reply ?? throw new IOException("no reply pending");
        if (Corrupt is { } corrupt)
        {
            reply = corrupt(reply);
            Corrupt = null;
        }
        buffer.Fill(0xFF); // what an idle bus reads as
        reply.AsSpan(0, Math.Min(reply.Length, buffer.Length)).CopyTo(buffer);
        _reply = null;
    }

    private byte[] GetVcpReply(byte code)
    {
        if (!Vcp.TryGetValue(code, out var value))
            return Frame([0x02, 0x01, code, 0x00, 0, 0, 0, 0]); // unsupported
        return Frame([0x02, 0x00, code, 0x00, (byte)(value.Maximum >> 8), (byte)value.Maximum, (byte)(value.Current >> 8), (byte)value.Current]);
    }

    private byte[] CapabilitiesReply(int offset)
    {
        var bytes = Encoding.ASCII.GetBytes(Capabilities);
        var fragment = bytes.Skip(offset).Take(32).ToArray();
        return Frame([0xE3, (byte)(offset >> 8), (byte)offset, .. fragment]);
    }

    /// <summary>A reply as the monitor sends it: source address, length, payload, checksum.</summary>
    public static byte[] Frame(byte[] payload)
    {
        byte[] reply = [0x6E, (byte)(0x80 | payload.Length), .. payload, 0];
        reply[^1] = Xor(0x50, reply[..^1]);
        return reply;
    }

    private static byte Xor(byte seed, byte[] bytes) => bytes.Aggregate(seed, (sum, b) => (byte)(sum ^ b));

    protected override void Dispose(bool disposing) => Disposed = true;
}

/// <summary>Builds EDID base blocks.</summary>
internal static class TestEdid
{
    public static byte[] Create(string maker, ushort product, uint serial = 0, string? name = null, string? serialText = null)
    {
        var edid = new byte[128];
        byte[] header = [0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00];
        header.CopyTo(edid, 0);
        int id = (maker[0] - 'A' + 1) << 10 | (maker[1] - 'A' + 1) << 5 | (maker[2] - 'A' + 1);
        edid[8] = (byte)(id >> 8);
        edid[9] = (byte)id;
        BitConverter.GetBytes(product).CopyTo(edid, 10);
        BitConverter.GetBytes(serial).CopyTo(edid, 12);

        // First descriptor: a detailed timing (any non-zero pixel clock).
        edid[54] = 0x01;
        edid[55] = 0x1D;
        int offset = 72;
        if (name is not null) Descriptor(edid, ref offset, 0xFC, name);
        if (serialText is not null) Descriptor(edid, ref offset, 0xFF, serialText);

        edid[127] = (byte)(256 - edid[..127].Sum(b => b) % 256);
        return edid;
    }

    private static void Descriptor(byte[] edid, ref int offset, byte tag, string text)
    {
        edid[offset + 3] = tag;
        var bytes = Encoding.ASCII.GetBytes(text.Length < 13 ? text + "\n" : text);
        for (int i = 0; i < 13; i++) edid[offset + 5 + i] = i < bytes.Length ? bytes[i] : (byte)' ';
        offset += 18;
    }
}

/// <summary>A test that only means something on Linux; skipped elsewhere.</summary>
public sealed class LinuxFactAttribute : FactAttribute
{
    public LinuxFactAttribute()
    {
        if (!OperatingSystem.IsLinux()) Skip = "Linux only";
    }
}
