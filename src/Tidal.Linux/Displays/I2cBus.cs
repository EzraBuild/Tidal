namespace Tidal.Linux.Displays;

/// <summary>
/// An I2C bus to a monitor: the DDC wires of an HDMI, DVI or VGA cable, or a DisplayPort AUX
/// channel. Linux exposes them as /dev/i2c-N (see <c>DevI2cBus</c>); tests use a fake monitor.
/// </summary>
public abstract class I2cBus : IDisposable
{
    /// <summary>Writes bytes to the device at a 7-bit address. Throws <see cref="IOException"/> when it fails.</summary>
    public abstract void Write(byte address, ReadOnlySpan<byte> data);

    /// <summary>Fills the buffer from the device at a 7-bit address. Throws <see cref="IOException"/> when it fails.</summary>
    public abstract void Read(byte address, Span<byte> buffer);

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
    }
}
