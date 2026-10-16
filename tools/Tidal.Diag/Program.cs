// Tidal diagnostics: lists displays and what Tidal can control on each.
//
//   Tidal.Diag                detect and describe every display
//   Tidal.Diag --caps         also fetch DDC/CI capabilities strings (slow)
//   Tidal.Diag --test-write   briefly change each display's brightness, read it back, then restore it
//   Tidal.Diag --set N P      set display N's brightness to P percent
//   Tidal.Diag --set-input N C   switch display N to input code C (hex; 0F DisplayPort 1, 11 HDMI 1, ...)
//   Tidal.Diag --watch-builtin S   print the built-in screen's brightness as it changes, for S seconds
//   Tidal.Diag --verbose      include debug log lines

using System.Diagnostics;
using System.Runtime.InteropServices;
using Tidal.Core;
using Tidal.Core.Adaptive;
using Tidal.Core.Displays;
using Tidal.Windows.Adaptive;
using Tidal.Windows.Displays;

SetProcessDpiAwarenessContext(-4); // per-monitor aware, so bounds are in physical pixels

bool showCapabilities = args.Contains("--caps");
bool testWrite = args.Contains("--test-write");
bool verbose = args.Contains("--verbose");

Log.Written += (level, message) =>
{
    if (verbose || level >= LogLevel.Warning) Console.WriteLine($"    [{level}] {message}");
};

using var manager = new DisplayManager(new WindowsDisplayDetector());
var stopwatch = Stopwatch.StartNew();
var displays = await manager.RefreshAsync();
Console.WriteLine($"Found {displays.Count} display(s) in {stopwatch.ElapsedMilliseconds} ms");

for (int i = 0; i < displays.Count; i++)
{
    var display = displays[i];
    var info = display.Info;
    Console.WriteLine();
    Console.WriteLine($"[{i + 1}] {display.Name}{(info.IsPrimary ? "  (primary)" : "")}");
    Console.WriteLine($"    Connection:  {info.Connection}{(info.IsInternal ? ", built-in" : "")}");
    Console.WriteLine($"    Bounds:      {info.Bounds}");
    Console.WriteLine($"    Id:          {display.Id}");
    Console.WriteLine($"    Control:     {display.Method}");
    Console.WriteLine($"    Brightness:  {Percent(display.SupportsBrightness, display.Brightness)}");
    Console.WriteLine($"    Contrast:    {Percent(display.SupportsContrast, display.Contrast)}");
    if (display.Note is { } note) Console.WriteLine($"    Note:        {note}");

    if (showCapabilities && display is DdcDisplay { Method: ControlMethod.DdcCi } ddc)
        await PrintCapabilities(ddc);
}

int watchIndex = Array.IndexOf(args, "--watch-builtin");
if (watchIndex >= 0)
{
    int seconds = watchIndex + 1 < args.Length && int.TryParse(args[watchIndex + 1], out int s) ? s : 30;
    if (!WmiBuiltInDisplaySource.IsAvailable)
    {
        Console.WriteLine("No built-in screen with WMI brightness.");
        return 1;
    }
    Console.WriteLine();
    Console.WriteLine($"Watching the built-in screen's brightness for {seconds} s...");
    var watch = Stopwatch.StartNew();
    using var source = new WmiBuiltInDisplaySource();
    source.LevelChanged += (_, _) => Console.WriteLine($"  {watch.Elapsed.TotalSeconds,6:0.00} s  {source.Percent}%");
    source.Start();
    await Task.Delay(TimeSpan.FromSeconds(seconds));
    return 0;
}

int inputIndex = Array.IndexOf(args, "--set-input");
if (inputIndex >= 0)
{
    if (inputIndex + 2 >= args.Length
        || !int.TryParse(args[inputIndex + 1], out int number) || number < 1 || number > displays.Count
        || !byte.TryParse(args[inputIndex + 2], System.Globalization.NumberStyles.HexNumber, null, out byte code))
    {
        Console.WriteLine($"Usage: --set-input <display 1-{displays.Count}> <input code in hex, e.g. 11 for HDMI 1>");
        return 1;
    }
    var display = displays[number - 1];
    // Inputs arrive with the capabilities, a moment after detection.
    for (int i = 0; i < 50 && display.InputSources.Count == 0; i++) await Task.Delay(100);
    var target = new InputSource(code);
    if (!display.InputSources.Contains(target))
    {
        Console.WriteLine($"{display.Name} doesn't list {target}. Inputs: {string.Join(", ", display.InputSources)}");
        return 1;
    }
    Console.WriteLine();
    Console.WriteLine($"{display.Name}: was on {display.CurrentInput?.ToString() ?? "unknown"}, switching to {target}");
    display.SetInput(target);
    await display.RefreshAsync(); // queued behind the write, so it has been sent
}

int setIndex = Array.IndexOf(args, "--set");
if (setIndex >= 0)
{
    if (setIndex + 2 >= args.Length
        || !int.TryParse(args[setIndex + 1], out int number) || number < 1 || number > displays.Count
        || !int.TryParse(args[setIndex + 2], out int percent))
    {
        Console.WriteLine($"Usage: --set <display 1-{displays.Count}> <brightness 0-100>");
        return 1;
    }
    var display = displays[number - 1];
    if (!display.SupportsBrightness)
    {
        Console.WriteLine($"{display.Name} has no hardware brightness control.");
        return 1;
    }
    display.SetBrightness(percent);
    bool answered = await display.RefreshAsync(); // queued behind the write
    Console.WriteLine();
    Console.WriteLine($"{display.Name}: brightness set to {percent}%, reads back {(answered ? display.Brightness + "%" : "nothing")}");
}

if (testWrite)
{
    Console.WriteLine();
    Console.WriteLine("Write test: simulates a slider drag, reads the value back from the hardware, then restores it.");
    foreach (var display in displays.Where(d => d.SupportsBrightness))
        await TestBrightnessWrite(display);
}

return 0;

static async Task PrintCapabilities(DdcDisplay display)
{
    var stopwatch = Stopwatch.StartNew();
    var capabilities = await display.GetCapabilitiesAsync();
    if (capabilities is null)
    {
        Console.WriteLine("    Capabilities: no reply");
        return;
    }
    Console.WriteLine($"    Capabilities ({stopwatch.ElapsedMilliseconds} ms): model {capabilities.Model ?? "?"}, MCCS {capabilities.MccsVersion ?? "?"}, {capabilities.VcpCodes.Count} VCP codes");
    Console.WriteLine($"      Input source (0x60): {Describe(capabilities, VcpCode.InputSource)}");
    if (capabilities.Supports(VcpCode.InputSource) && await display.ReadVcpAsync(VcpCode.InputSource) is { } input)
        Console.WriteLine($"      Current input:       {new InputSource((byte)(input.Current & 0xFF))} (0x{input.Current & 0xFF:X2})");
    Console.WriteLine($"      Volume (0x62):       {Describe(capabilities, VcpCode.AudioVolume)}");
    Console.WriteLine($"      Raw: {capabilities.Raw}");
}

static string Describe(MonitorCapabilities capabilities, byte code)
{
    if (!capabilities.Supports(code)) return "not listed";
    var values = capabilities.AllowedValues(code);
    return values.Count == 0 ? "supported" : "values " + string.Join(" ", values.Select(v => v.ToString("X2")));
}

static async Task TestBrightnessWrite(Display display)
{
    int original = display.Brightness!.Value;
    int target = original <= 50 ? original + 10 : original - 10;
    Console.Write($"  {display.Name}: {original}% -> {target}% ... ");

    // 25 writes 10 ms apart, faster than any monitor accepts them; the queue should coalesce these.
    for (int step = 1; step <= 25; step++)
    {
        display.SetBrightness(original + (target - original) * step / 25);
        await Task.Delay(10);
    }
    await Task.Delay(500);
    bool answered = await display.RefreshAsync();
    int? readBack = answered ? display.Brightness : null;
    Console.Write(readBack == target ? $"read back {readBack}% OK" : $"read back {readBack?.ToString() ?? "nothing"} MISMATCH");

    display.SetBrightness(original);
    await Task.Delay(500);
    answered = await display.RefreshAsync();
    Console.WriteLine($"; restored to {(answered ? display.Brightness + "%" : "? (no reply)")}");
}

static string Percent(bool supported, int? value) => supported ? $"{value}%" : "not available";

[DllImport("user32.dll")]
static extern bool SetProcessDpiAwarenessContext(nint value);
