namespace Tidal.Core.Displays;

/// <summary>A video input on a monitor, by its MCCS input source code (VCP 0x60).</summary>
public readonly record struct InputSource(byte Code)
{
    // MCCS 2.2 defines 0x01–0x12. Higher values are vendor-specific: Lenovo uses 0x13 and Dell 0x1B
    // for USB-C, so those are named too; anything else shows its code.
    private static readonly Dictionary<byte, string> Names = new()
    {
        [0x01] = "VGA 1", [0x02] = "VGA 2",
        [0x03] = "DVI 1", [0x04] = "DVI 2",
        [0x05] = "Composite 1", [0x06] = "Composite 2",
        [0x07] = "S-Video 1", [0x08] = "S-Video 2",
        [0x09] = "Tuner 1", [0x0A] = "Tuner 2", [0x0B] = "Tuner 3",
        [0x0C] = "Component 1", [0x0D] = "Component 2", [0x0E] = "Component 3",
        [0x0F] = "DisplayPort 1", [0x10] = "DisplayPort 2",
        [0x11] = "HDMI 1", [0x12] = "HDMI 2",
        [0x13] = "USB-C", [0x1B] = "USB-C",
    };

    public string Name => Names.TryGetValue(Code, out var name) ? name : $"Input {Code:X2}";

    public override string ToString() => Name;
}
