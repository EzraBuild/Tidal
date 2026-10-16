using System.Runtime.InteropServices;
using Tidal.Core;
using Tidal.Core.Displays;
using Tidal.Windows.Native;

namespace Tidal.Windows.Displays;

/// <summary>
/// Builds the list of displays by joining three Windows views of the same hardware: GDI monitors
/// (bounds, DDC/CI handles), CCD display paths (real names, device paths, connection type) and
/// WMI brightness instances (built-in screens).
/// </summary>
public sealed class WindowsDisplayDetector : IDisplayDetector
{
    private sealed record Target(string GdiDeviceName, string FriendlyName, string DevicePath, uint OutputTechnology);

    private sealed record GdiMonitor(IntPtr Handle, string GdiDeviceName, ScreenRect Bounds, bool IsPrimary);

    private sealed record Physical(SafePhysicalMonitorHandle Handle, string Description);

    private sealed class Candidate(DisplayInfo info, Physical? physical)
    {
        public DisplayInfo Info { get; set; } = info;
        public Physical? Physical { get; } = physical;
        public string? WmiInstance { get; set; }
    }

    public IReadOnlyList<Display> Detect()
    {
        var targets = QueryTargets();
        var candidates = new List<Candidate>();

        foreach (var monitor in EnumerateGdiMonitors())
        {
            var physical = GetPhysicalMonitors(monitor.Handle);
            var monitorTargets = targets
                .Where(t => string.Equals(t.GdiDeviceName, monitor.GdiDeviceName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Normally one of each. In clone mode a GDI monitor has several; pair them up by index.
            int count = Math.Max(1, Math.Max(physical.Count, monitorTargets.Count));
            for (int i = 0; i < count; i++)
            {
                var target = i < monitorTargets.Count ? monitorTargets[i] : null;
                var phys = i < physical.Count ? physical[i] : null;
                candidates.Add(new Candidate(BuildInfo(monitor, target, phys?.Description), phys));
            }
        }

        AssignWmiInstances(candidates, WmiBrightness.FindInstances());
        MakeNamesUnique(candidates);

        return candidates
            .OrderBy(c => c.Info.Bounds.Left)
            .ThenBy(c => c.Info.Bounds.Top)
            .Select(CreateDisplay)
            .ToList();
    }

    private static Display CreateDisplay(Candidate candidate)
    {
        if (candidate.WmiInstance is not null)
        {
            candidate.Physical?.Handle.Dispose(); // built-in screens don't need the DDC handle
            return new WmiDisplay(candidate.Info, candidate.WmiInstance);
        }
        if (candidate.Physical is not null)
            return new DdcDisplay(candidate.Info, new Dxva2Channel(candidate.Physical.Handle), DescribeNoDdc(candidate.Info));

        return new UncontrolledDisplay(candidate.Info, "Windows didn't provide a hardware control channel for this display.");
    }

    private static string DescribeNoDdc(DisplayInfo info) => info.Connection switch
    {
        "Wireless" or "Virtual" or "USB display" =>
            $"{info.Connection} connections don't carry DDC/CI, so Tidal can't change this display's hardware settings.",
        _ when info.IsInternal =>
            "Windows doesn't expose brightness control for this built-in screen.",
        _ =>
            "This monitor didn't respond to DDC/CI. Turn on DDC/CI in the monitor's on-screen menu, " +
            "or connect it directly instead of through a dock, KVM switch or adapter.",
    };

    private static void AssignWmiInstances(List<Candidate> candidates, List<string> wmiInstances)
    {
        // WMI instance names are the PnP instance path plus a suffix, e.g. "DISPLAY\AUOB39E\4&11a01538&0&UID8388688_0".
        foreach (var candidate in candidates)
        {
            if (candidate.Info.InstancePath is not { } path) continue;
            var match = wmiInstances.FirstOrDefault(w => w.StartsWith(path + "_", StringComparison.OrdinalIgnoreCase)
                                                         || string.Equals(w, path, StringComparison.OrdinalIgnoreCase));
            if (match is null) continue;
            candidate.WmiInstance = match;
            wmiInstances.Remove(match);
        }

        // Fallback when paths didn't line up: one leftover built-in screen and one leftover WMI instance.
        var unmatchedInternal = candidates.Where(c => c.Info.IsInternal && c.WmiInstance is null).ToList();
        if (unmatchedInternal.Count == 1 && wmiInstances.Count == 1)
            unmatchedInternal[0].WmiInstance = wmiInstances[0];
    }

    private static void MakeNamesUnique(List<Candidate> candidates)
    {
        foreach (var group in candidates.GroupBy(c => c.Info.Name, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
        {
            int n = 1;
            foreach (var candidate in group.OrderBy(c => c.Info.Bounds.Left))
                candidate.Info = candidate.Info with { Name = $"{candidate.Info.Name} ({n++})" };
        }
    }

    private static DisplayInfo BuildInfo(GdiMonitor monitor, Target? target, string? physicalDescription)
    {
        bool isInternal = target is not null && IsInternalTechnology(target.OutputTechnology);
        string? devicePath = string.IsNullOrEmpty(target?.DevicePath) ? null : target.DevicePath;

        string name = target?.FriendlyName is { Length: > 0 } friendly ? friendly
            : isInternal ? "Built-in Display"
            : physicalDescription is { Length: > 0 } description && !description.StartsWith("Generic", StringComparison.OrdinalIgnoreCase) ? description
            : $"Display {monitor.GdiDeviceName.TrimStart('\\', '.').Replace("DISPLAY", "", StringComparison.OrdinalIgnoreCase)}";

        return new DisplayInfo
        {
            Id = devicePath ?? monitor.GdiDeviceName,
            Name = name,
            DeviceName = monitor.GdiDeviceName,
            Bounds = monitor.Bounds,
            IsPrimary = monitor.IsPrimary,
            IsInternal = isInternal,
            Connection = target is null ? "Unknown" : DescribeConnection(target.OutputTechnology),
            HardwareId = DevicePathPart(devicePath, 1),
            InstancePath = ToInstancePath(devicePath),
        };
    }

    // ---- CCD: names, device paths, connection types ----

    private static List<Target> QueryTargets()
    {
        DISPLAYCONFIG_PATH_INFO[] paths;
        int error;
        do
        {
            error = User32.GetDisplayConfigBufferSizes(User32.QDC_ONLY_ACTIVE_PATHS, out uint pathCount, out uint modeCount);
            if (error != User32.ERROR_SUCCESS) break;
            paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
            var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
            error = User32.QueryDisplayConfig(User32.QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
            if (error == User32.ERROR_SUCCESS) return ReadTargets(paths.AsSpan(0, (int)pathCount));
        }
        while (error == User32.ERROR_INSUFFICIENT_BUFFER); // topology changed between the two calls

        Log.Warning($"QueryDisplayConfig failed with error {error}; display names will be generic.");
        return [];
    }

    private static List<Target> ReadTargets(ReadOnlySpan<DISPLAYCONFIG_PATH_INFO> paths)
    {
        var targets = new List<Target>();
        foreach (var path in paths)
        {
            var source = new DISPLAYCONFIG_SOURCE_DEVICE_NAME
            {
                header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                {
                    type = User32.DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME,
                    size = (uint)Marshal.SizeOf<DISPLAYCONFIG_SOURCE_DEVICE_NAME>(),
                    adapterId = path.sourceInfo.adapterId,
                    id = path.sourceInfo.id,
                },
            };
            if (User32.DisplayConfigGetDeviceInfo(ref source) != User32.ERROR_SUCCESS) continue;

            var target = new DISPLAYCONFIG_TARGET_DEVICE_NAME
            {
                header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                {
                    type = User32.DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME,
                    size = (uint)Marshal.SizeOf<DISPLAYCONFIG_TARGET_DEVICE_NAME>(),
                    adapterId = path.targetInfo.adapterId,
                    id = path.targetInfo.id,
                },
            };
            if (User32.DisplayConfigGetDeviceInfo(ref target) != User32.ERROR_SUCCESS) continue;

            targets.Add(new Target(
                source.viewGdiDeviceName,
                target.monitorFriendlyDeviceName ?? "",
                target.monitorDevicePath ?? "",
                target.outputTechnology));
        }
        return targets;
    }

    // DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY values.
    private static bool IsInternalTechnology(uint technology) => technology is 6 or 11 or 13 or 0x80000000;

    private static string DescribeConnection(uint technology) => technology switch
    {
        0 => "VGA",
        4 => "DVI",
        5 => "HDMI",
        10 => "DisplayPort",
        18 => "USB-C",
        15 => "Wireless",
        16 => "USB display",
        17 => "Virtual",
        _ when IsInternalTechnology(technology) => "Built-in",
        _ => "Other",
    };

    // Device path: \\?\DISPLAY#DELA1C8#5&35c26193&0&UID4352#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}
    private static string? DevicePathPart(string? devicePath, int index)
    {
        var parts = devicePath?.Split('#');
        return parts is not null && parts.Length > index ? parts[index] : null;
    }

    private static string? ToInstancePath(string? devicePath)
    {
        var parts = devicePath?.Split('#');
        if (parts is null || parts.Length < 3) return null;
        return $"{parts[0].Replace(@"\\?\", "")}\\{parts[1]}\\{parts[2]}";
    }

    // ---- GDI monitors and their DDC/CI handles ----

    private static List<GdiMonitor> EnumerateGdiMonitors()
    {
        var monitors = new List<GdiMonitor>();
        User32.MonitorEnumProc callback = (IntPtr hMonitor, IntPtr _, ref RECT _, IntPtr _) =>
        {
            var info = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
            if (User32.GetMonitorInfo(hMonitor, ref info))
            {
                var r = info.rcMonitor;
                monitors.Add(new GdiMonitor(
                    hMonitor,
                    info.szDevice,
                    new ScreenRect(r.Left, r.Top, r.Right, r.Bottom),
                    (info.dwFlags & User32.MONITORINFOF_PRIMARY) != 0));
            }
            return true;
        };
        User32.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
        GC.KeepAlive(callback);
        return monitors;
    }

    private static List<Physical> GetPhysicalMonitors(IntPtr hMonitor)
    {
        if (!Dxva2.GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out uint count) || count == 0)
            return [];

        var array = new PHYSICAL_MONITOR[count];
        if (!Dxva2.GetPhysicalMonitorsFromHMONITOR(hMonitor, count, array))
        {
            Log.Warning($"GetPhysicalMonitorsFromHMONITOR failed (error 0x{Marshal.GetLastPInvokeError():X8})");
            return [];
        }
        return array
            .Select(p => new Physical(new SafePhysicalMonitorHandle(p.hPhysicalMonitor), p.szPhysicalMonitorDescription ?? ""))
            .ToList();
    }
}
