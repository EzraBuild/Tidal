using System.Buffers.Binary;
using System.Text;

namespace Tidal.Linux.Displays;

/// <summary>What Tidal reads from a monitor's EDID (the VESA E-EDID base block): its maker, model and name.</summary>
public sealed record Edid
{
    /// <summary>Three-letter PNP manufacturer ID, e.g. <c>DEL</c>.</summary>
    public required string ManufacturerId { get; init; }

    public required ushort ProductCode { get; init; }

    /// <summary>The numeric serial number; 0 when the maker left it out.</summary>
    public uint SerialNumber { get; init; }

    /// <summary>The monitor name descriptor, e.g. <c>DELL P2422HE</c>.</summary>
    public string? Name { get; init; }

    /// <summary>The serial number descriptor, when there is one.</summary>
    public string? SerialText { get; init; }

    /// <summary>Manufacturer and product code as Windows writes them, e.g. <c>DELA1C8</c>.</summary>
    public string HardwareId => $"{ManufacturerId}{ProductCode:X4}";

    /// <summary>Something that tells two monitors of the same model apart, or null if the EDID has nothing.</summary>
    public string? Serial => SerialText ?? (SerialNumber != 0 ? SerialNumber.ToString("X8") : null);

    private static ReadOnlySpan<byte> Header => [0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00];

    /// <summary>Reads the base block. Returns null if this isn't an EDID.</summary>
    public static Edid? Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < 128 || !data[..8].SequenceEqual(Header)) return null;

        string? name = null, serial = null;
        for (int offset = 54; offset <= 108; offset += 18)
        {
            var descriptor = data.Slice(offset, 18);
            if (descriptor[0] != 0 || descriptor[1] != 0) continue; // a detailed timing, not a text descriptor
            if (descriptor[3] == 0xFC) name ??= Text(descriptor[5..]);
            else if (descriptor[3] == 0xFF) serial ??= Text(descriptor[5..]);
        }

        int maker = data[8] << 8 | data[9]; // three 5-bit letters, 1 = A
        return new Edid
        {
            ManufacturerId = $"{Letter(maker >> 10)}{Letter(maker >> 5)}{Letter(maker)}",
            ProductCode = BinaryPrimitives.ReadUInt16LittleEndian(data[10..]),
            SerialNumber = BinaryPrimitives.ReadUInt32LittleEndian(data[12..]),
            Name = name,
            SerialText = serial,
        };
    }

    private static char Letter(int bits) => (char)('A' - 1 + (bits & 0x1F));

    // Up to 13 ASCII characters, ended by a line feed and padded with spaces.
    private static string? Text(ReadOnlySpan<byte> bytes)
    {
        int end = bytes.IndexOf((byte)0x0A);
        string text = Encoding.ASCII.GetString(end >= 0 ? bytes[..end] : bytes).Trim();
        return text.Length > 0 ? text : null;
    }
}
