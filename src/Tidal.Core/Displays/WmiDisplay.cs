using System.Management;
using System.Runtime.InteropServices;

namespace Tidal.Core.Displays;

/// <summary>A built-in laptop screen controlled through WMI (root\WMI WmiMonitorBrightness).</summary>
public sealed class WmiDisplay : Display
{
    // WMI calls are much faster than DDC/CI; the gap just keeps slider drags from piling up.
    private static readonly TimeSpan CommandGap = TimeSpan.FromMilliseconds(15);

    private readonly WmiBrightness _wmi;
    private readonly DeviceCommandQueue _queue;

    internal WmiDisplay(DisplayInfo info, string wmiInstanceName) : base(info)
    {
        _wmi = new WmiBrightness(wmiInstanceName);
        _queue = new DeviceCommandQueue(info.Name, CommandGap);
    }

    public override ControlMethod Method => SupportsBrightness ? ControlMethod.Wmi : ControlMethod.None;

    internal override async Task InitializeAsync(CancellationToken cancellationToken)
    {
        int? brightness = await _queue.RunAsync(_wmi.TryRead).WaitAsync(cancellationToken);
        SupportsBrightness = brightness.HasValue;
        Note = SupportsBrightness ? null : "Windows didn't report a brightness level for this screen.";
        Log.Info($"{Name}: WMI brightness={brightness?.ToString() ?? "unavailable"}");
        OnValuesRead(brightness, null);
    }

    public override async Task<bool> RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (!SupportsBrightness)
        {
            await InitializeAsync(cancellationToken);
            return SupportsBrightness;
        }
        int? brightness = await _queue.RunAsync(_wmi.TryRead).WaitAsync(cancellationToken);
        OnValuesRead(brightness, null);
        return brightness.HasValue;
    }

    private protected override void WriteBrightness(int percent) => _queue.Post(VcpCode.Brightness, () => _wmi.Write(percent));

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _queue.Dispose();
            _wmi.Dispose();
        }
    }
}

/// <summary>Thin wrapper over the WMI brightness classes. Used only from one queue thread.</summary>
internal sealed class WmiBrightness(string instanceName) : IDisposable
{
    private const string Scope = @"root\WMI";
    private ManagementObject? _methods;

    /// <summary>Instance names of active panels with WMI brightness. Empty on most desktops.</summary>
    public static List<string> FindInstances()
    {
        var names = new List<string>();
        try
        {
            using var searcher = new ManagementObjectSearcher(Scope, "SELECT InstanceName, Active FROM WmiMonitorBrightness");
            using var results = searcher.Get();
            foreach (var item in results)
            {
                using (item)
                {
                    if (item["Active"] is true && item["InstanceName"] is string name) names.Add(name);
                }
            }
        }
        catch (Exception ex) when (ex is ManagementException or COMException or UnauthorizedAccessException)
        {
            // Machines without an ACPI-controlled panel answer "Not supported".
            Log.Debug($"WMI brightness unavailable: {ex.Message}");
        }
        return names;
    }

    public int? TryRead()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(Scope, "SELECT InstanceName, CurrentBrightness FROM WmiMonitorBrightness");
            using var results = searcher.Get();
            foreach (var item in results)
            {
                using (item)
                {
                    if (IsThisInstance(item)) return Convert.ToInt32(item["CurrentBrightness"]);
                }
            }
        }
        catch (Exception ex) when (ex is ManagementException or COMException)
        {
            Log.Warning($"WMI brightness read failed: {ex.Message}");
        }
        return null;
    }

    public void Write(int percent)
    {
        try
        {
            InvokeSetBrightness(percent);
        }
        catch (Exception ex) when (ex is ManagementException or COMException)
        {
            // The cached WMI object can go stale (e.g. after sleep); fetch it again and retry once.
            Log.Debug($"WMI brightness write failed, retrying: {ex.Message}");
            _methods?.Dispose();
            _methods = null;
            InvokeSetBrightness(percent);
        }
    }

    private void InvokeSetBrightness(int percent)
    {
        _methods ??= FindMethodsObject() ?? throw new InvalidOperationException($"WMI brightness instance '{instanceName}' is gone.");
        using var args = _methods.GetMethodParameters("WmiSetBrightness");
        args["Timeout"] = 0u;
        args["Brightness"] = (byte)Math.Clamp(percent, 0, 100);
        _methods.InvokeMethod("WmiSetBrightness", args, null)?.Dispose();
    }

    private ManagementObject? FindMethodsObject()
    {
        using var searcher = new ManagementObjectSearcher(Scope, "SELECT * FROM WmiMonitorBrightnessMethods");
        using var results = searcher.Get();
        ManagementObject? match = null;
        foreach (var item in results)
        {
            if (match is null && IsThisInstance(item)) match = (ManagementObject)item;
            else item.Dispose();
        }
        return match;
    }

    private bool IsThisInstance(ManagementBaseObject item)
        => string.Equals(item["InstanceName"] as string, instanceName, StringComparison.OrdinalIgnoreCase);

    public void Dispose() => _methods?.Dispose();
}
