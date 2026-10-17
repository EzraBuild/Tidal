using System.Text;
using Tidal.Core.Displays;

namespace Tidal.Linux.Displays;

/// <summary>
/// DDC/CI over an I2C bus, framed as the VESA DDC/CI standard describes. The monitor listens at
/// address 0x37. Every message carries a length byte and an XOR checksum, and the monitor needs a
/// moment before its reply can be read.
/// </summary>
public sealed class I2cDdcChannel(I2cBus bus) : IDdcChannel
{
    /// <summary>The 7-bit I2C address of a monitor's DDC/CI interface.</summary>
    public const byte Address = 0x37;

    // Addresses as they go over the wire (shifted left, with the read/write bit), which the checksums cover.
    private const byte DisplayWire = 0x6E;   // the monitor, as the host writes to it and as the source of replies
    private const byte HostWire = 0x51;      // the host, as the source of requests
    private const byte HostReplyWire = 0x50; // the host, as the destination of replies

    private const byte GetVcpRequest = 0x01;
    private const byte GetVcpReply = 0x02;
    private const byte SetVcpRequest = 0x03;
    private const byte CapabilitiesRequest = 0xF3;
    private const byte CapabilitiesReply = 0xE3;

    private const int GetVcpReplyLength = 11;        // address, length, 8 payload bytes, checksum
    private const int CapabilitiesFragmentLength = 32;
    private const int CapabilitiesReplyLength = 6 + CapabilitiesFragmentLength;
    private const int MaxCapabilitiesLength = 8192;  // real ones are a few hundred bytes
    private const int FragmentAttempts = 3;

    // How long the standard gives the monitor before its reply may be read.
    private static readonly TimeSpan GetVcpDelay = TimeSpan.FromMilliseconds(40);
    private static readonly TimeSpan CapabilitiesDelay = TimeSpan.FromMilliseconds(50);

    /// <summary>Waits between request and reply. Tests replace it so they don't sleep.</summary>
    internal Action<TimeSpan> Sleep { get; init; } = Thread.Sleep;

    public VcpReply GetVcp(byte code)
    {
        Send([GetVcpRequest, code]);
        Sleep(GetVcpDelay);

        Span<byte> buffer = stackalloc byte[GetVcpReplyLength];
        var reply = Receive(buffer);
        // Payload: opcode, result, code, type, maximum (2 bytes), current (2 bytes).
        if (reply.Length < 8 || reply[0] != GetVcpReply) throw new IOException($"unexpected reply to Get VCP 0x{code:X2} (opcode 0x{reply[0]:X2})");
        if (reply[2] != code) throw new IOException($"reply is for VCP 0x{reply[2]:X2}, not 0x{code:X2}");
        if (reply[1] == 0x01) throw new IOException($"the monitor doesn't support VCP 0x{code:X2}");
        if (reply[1] != 0x00) throw new IOException($"Get VCP 0x{code:X2} failed with result code 0x{reply[1]:X2}");

        return new VcpReply(Current: (uint)(reply[6] << 8 | reply[7]), Maximum: (uint)(reply[4] << 8 | reply[5]));
    }

    public void SetVcp(byte code, uint value)
    {
        if (value > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(value), "VCP values are 16 bits.");
        Send([SetVcpRequest, code, (byte)(value >> 8), (byte)value]);
    }

    public string GetCapabilities()
    {
        // The string comes in numbered fragments of up to 32 bytes; an empty fragment ends it.
        var text = new List<byte>();
        var buffer = new byte[CapabilitiesReplyLength];
        while (text.Count < MaxCapabilitiesLength)
        {
            var fragment = ReadCapabilitiesFragment(text.Count, buffer);
            if (fragment.Length == 0) break;
            text.AddRange(fragment);
            if (fragment.Contains((byte)0)) break; // some monitors end it with a NUL instead
        }

        int end = text.IndexOf(0);
        return Encoding.ASCII.GetString(text.ToArray(), 0, end >= 0 ? end : text.Count);
    }

    private ReadOnlySpan<byte> ReadCapabilitiesFragment(int offset, byte[] buffer)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                Send([CapabilitiesRequest, (byte)(offset >> 8), (byte)offset]);
                Sleep(CapabilitiesDelay);
                var reply = Receive(buffer);
                // Payload: opcode, offset (2 bytes), then the fragment.
                if (reply.Length < 3 || reply[0] != CapabilitiesReply) throw new IOException("unexpected reply to the capabilities request");
                int replyOffset = reply[1] << 8 | reply[2];
                if (replyOffset != offset) throw new IOException($"capabilities fragment for offset {replyOffset}, asked for {offset}");
                return reply[3..];
            }
            catch (IOException) when (attempt < FragmentAttempts)
            {
                Sleep(CapabilitiesDelay);
            }
        }
    }

    /// <summary>Sends a request: host address, length, payload, checksum.</summary>
    private void Send(ReadOnlySpan<byte> payload)
    {
        Span<byte> message = stackalloc byte[payload.Length + 3];
        message[0] = HostWire;
        message[1] = (byte)(0x80 | payload.Length);
        payload.CopyTo(message[2..]);
        message[^1] = Checksum(DisplayWire, message[..^1]);
        bus.Write(Address, message);
    }

    /// <summary>Reads a reply into the buffer, checks its framing, and returns its payload.</summary>
    private ReadOnlySpan<byte> Receive(Span<byte> buffer)
    {
        bus.Read(Address, buffer);

        if ((buffer[0] & 0xFE) != DisplayWire) throw new IOException($"reply from address 0x{buffer[0]:X2}, not the monitor");
        if ((buffer[1] & 0x80) == 0) throw new IOException($"malformed reply length byte 0x{buffer[1]:X2}");
        int length = buffer[1] & 0x7F;
        // A null message: the monitor is busy, or has nothing to say to this request.
        if (length == 0) throw new IOException("the monitor sent a null message");
        if (length + 3 > buffer.Length) throw new IOException($"reply too long ({length} bytes)");
        if (Checksum(HostReplyWire, buffer[..(length + 2)]) != buffer[length + 2]) throw new IOException("reply checksum mismatch");

        return buffer.Slice(2, length);
    }

    private static byte Checksum(byte seed, ReadOnlySpan<byte> bytes)
    {
        byte checksum = seed;
        foreach (byte b in bytes) checksum ^= b;
        return checksum;
    }

    public void Dispose() => bus.Dispose();
}
