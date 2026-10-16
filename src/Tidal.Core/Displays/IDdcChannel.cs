namespace Tidal.Core.Displays;

/// <summary>
/// Sends DDC/CI commands to one monitor; each operating system gets there its own way. Used only
/// from the display's command queue, one command at a time. <see cref="DdcDisplay"/> paces the
/// commands and retries failures, so a channel makes one attempt and throws when it fails.
/// </summary>
public interface IDdcChannel : IDisposable
{
    /// <summary>Sends Get VCP Feature. Throws <see cref="IOException"/> when the monitor doesn't answer properly.</summary>
    VcpReply GetVcp(byte code);

    /// <summary>Sends Set VCP Feature. Throws <see cref="IOException"/> when the write fails.</summary>
    void SetVcp(byte code, uint value);

    /// <summary>Fetches the capabilities string. Throws <see cref="IOException"/> when the monitor doesn't answer properly.</summary>
    string GetCapabilities();
}
