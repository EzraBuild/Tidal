# Tidal

A Windows tray app for controlling external monitors: hardware brightness and contrast over DDC/CI,
the built-in laptop screen through WMI, and (planned) adaptive brightness, hotkeys, input switching
and software dimming. Inspired by Lunar for macOS; written from scratch.

## Status

| # | Feature | State |
|---|---------|-------|
| 1 | Detect monitors; read/write brightness and contrast (DDC/CI, WMI) | Done (test UI) |
| 2 | Tray popup with per-monitor sliders and "link all" | Planned |
| 3 | Global hotkeys with per-monitor min/max | Planned |
| 4 | Adaptive brightness: sunrise/sunset schedule, sync from built-in screen / light sensor | Planned |
| 5 | Input source (VCP 0x60) and volume (VCP 0x62) | Planned |
| 6 | Software dimming below 0% / for monitors without DDC/CI | Planned |
| 7 | Per-app presets | Planned |
| 8 | JSON settings, start with Windows | Planned |

## Layout

```
src/Tidal.Core         Hardware layer, no UI
  Native/              P/Invoke: user32 (monitor enumeration, CCD) and dxva2 (DDC/CI)
  Displays/            Display model, detection, DDC/CI + WMI backends, write throttling
src/Tidal.App          WPF app (WPF-UI for Windows 11 styling: Mica, rounded corners)
tools/Tidal.Diag       Console diagnostics: what each display supports
tests/Tidal.Core.Tests Unit tests for the throttling queue, capabilities parser and interop structs
```

### How detection works

`DisplayTopology` joins three views of the same hardware:

- **GDI monitors** (`EnumDisplayMonitors`) give bounds, the primary flag, and DDC/CI handles via
  `GetPhysicalMonitorsFromHMONITOR`.
- **CCD display paths** (`QueryDisplayConfig`) give real model names, a stable device path (used as
  the display's settings ID) and the connection type, which tells built-in panels apart.
- **WMI** (`root\WMI` `WmiMonitorBrightness`) provides brightness for built-in panels, matched to a
  display by PnP instance path.

Each display becomes a `DdcDisplay`, `WmiDisplay` or `UncontrolledDisplay`. Displays without
hardware control expose a `Note` explaining why (DDC/CI off in the monitor menu, dock or adapter in
the way, wireless connection and so on).

### Throttling

All I/O for a display goes through its own `DeviceCommandQueue`, a dedicated thread that:

- coalesces writes per VCP code, so dragging a slider sends only the latest value once the monitor
  is ready,
- waits at least 50 ms between DDC/CI commands, and
- retries reads and writes that fail; I2C errors happen now and then.

## Build and run

Requires the .NET 8 SDK on Windows 10/11.

```powershell
dotnet build
dotnet test
dotnet run --project src/Tidal.App            # test UI
dotnet run --project tools/Tidal.Diag          # list displays and what Tidal can control
dotnet run --project tools/Tidal.Diag -- --caps         # include DDC/CI capabilities (slow)
dotnet run --project tools/Tidal.Diag -- --test-write   # change brightness briefly, verify, restore
dotnet run --project tools/Tidal.Diag -- --set 2 70     # set display 2 to 70%
```
