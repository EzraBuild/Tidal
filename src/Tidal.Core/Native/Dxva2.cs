using System.Runtime.InteropServices;

namespace Tidal.Core.Native;

// DDC/CI through the Monitor Configuration API (dxva2.dll).

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
internal struct PHYSICAL_MONITOR
{
    public IntPtr hPhysicalMonitor;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string szPhysicalMonitorDescription;
}

internal static class Dxva2
{
    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, out uint pdwNumberOfPhysicalMonitors);

    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, uint dwPhysicalMonitorArraySize, [Out] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DestroyPhysicalMonitor(IntPtr hMonitor);

    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetVCPFeatureAndVCPFeatureReply(
        SafePhysicalMonitorHandle hMonitor, byte bVCPCode, out uint pvct, out uint pdwCurrentValue, out uint pdwMaximumValue);

    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetVCPFeature(SafePhysicalMonitorHandle hMonitor, byte bVCPCode, uint dwNewValue);

    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCapabilitiesStringLength(SafePhysicalMonitorHandle hMonitor, out uint pdwCapabilitiesStringLengthInCharacters);

    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CapabilitiesRequestAndCapabilitiesReply(
        SafePhysicalMonitorHandle hMonitor, [Out] byte[] pszASCIICapabilitiesString, uint dwCapabilitiesStringLengthInCharacters);
}

/// <summary>
/// Owns a physical monitor handle and destroys it when released. Passing this (rather than an
/// IntPtr) to P/Invoke keeps the handle alive until any in-flight DDC call returns.
/// </summary>
internal sealed class SafePhysicalMonitorHandle : SafeHandle
{
    // Physical monitor handles are small indices and 0 is a valid one, so only -1 counts as invalid.
    public SafePhysicalMonitorHandle(IntPtr handle) : base(invalidHandleValue: new IntPtr(-1), ownsHandle: true)
        => SetHandle(handle);

    public override bool IsInvalid => handle == new IntPtr(-1);

    protected override bool ReleaseHandle() => Dxva2.DestroyPhysicalMonitor(handle);
}
