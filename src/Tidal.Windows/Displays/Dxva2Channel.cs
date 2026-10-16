using System.Runtime.InteropServices;
using System.Text;
using Tidal.Core.Displays;
using Tidal.Windows.Native;

namespace Tidal.Windows.Displays;

/// <summary>DDC/CI through the Windows Monitor Configuration API (dxva2), which does the framing and timing.</summary>
internal sealed class Dxva2Channel(SafePhysicalMonitorHandle handle) : IDdcChannel
{
    public VcpReply GetVcp(byte code)
        => Dxva2.GetVCPFeatureAndVCPFeatureReply(handle, code, out _, out uint current, out uint maximum)
            ? new VcpReply(current, maximum)
            : throw LastError();

    public void SetVcp(byte code, uint value)
    {
        if (!Dxva2.SetVCPFeature(handle, code, value)) throw LastError();
    }

    public string GetCapabilities()
    {
        if (!Dxva2.GetCapabilitiesStringLength(handle, out uint length)) throw LastError();
        if (length == 0) throw new IOException("empty capabilities string");

        var buffer = new byte[length];
        if (!Dxva2.CapabilitiesRequestAndCapabilitiesReply(handle, buffer, length)) throw LastError();
        return Encoding.ASCII.GetString(buffer).TrimEnd('\0');
    }

    private static IOException LastError() => new($"error 0x{Marshal.GetLastPInvokeError():X8}");

    public void Dispose() => handle.Dispose();
}
