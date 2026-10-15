namespace Tidal.Core.Displays;

/// <summary>MCCS Virtual Control Panel feature codes used by Tidal.</summary>
public static class VcpCode
{
    public const byte Brightness = 0x10;
    public const byte Contrast = 0x12;
    public const byte InputSource = 0x60;
    public const byte AudioVolume = 0x62;
    public const byte AudioMute = 0x8D;
    public const byte PowerMode = 0xD6;
}

/// <summary>A VCP feature's current and maximum value, as reported by the monitor.</summary>
public readonly record struct VcpReply(uint Current, uint Maximum);
