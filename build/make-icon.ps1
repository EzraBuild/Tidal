# Renders Tidal's icon: the ring-and-tide mark (same geometry as src/Tidal.App/Tray/Logo.cs) in white
# on an ocean-blue rounded tile. Writes the app's .ico and the website's PNGs.
#
#   powershell -ExecutionPolicy Bypass -File build\make-icon.ps1

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase
$root = Split-Path -Parent $PSScriptRoot

function Render-Icon([int]$size) {
    $visual = New-Object System.Windows.Media.DrawingVisual
    $dc = $visual.RenderOpen()
    $dc.PushTransform((New-Object System.Windows.Media.ScaleTransform ($size / 24.0), ($size / 24.0)))

    # Tile: ocean gradient, iOS-style rounded square.
    $tile = New-Object System.Windows.Media.LinearGradientBrush(
        [System.Windows.Media.Color]::FromRgb(0x3A, 0xA6, 0xF0), [System.Windows.Media.Color]::FromRgb(0x0E, 0x4C, 0x86), 90)
    $dc.DrawRoundedRectangle($tile, $null, (New-Object System.Windows.Rect 0, 0, 24, 24), 5.4, 5.4)

    # The mark, scaled to sit inside the tile (Logo.cs draws it on a 24x24 grid).
    $dc.PushTransform((New-Object System.Windows.Media.TranslateTransform 3.6, 3.6))
    $dc.PushTransform((New-Object System.Windows.Media.ScaleTransform 0.7, 0.7))
    $white = [System.Windows.Media.Brushes]::White
    $pen = New-Object System.Windows.Media.Pen $white, 1.6
    $dc.DrawGeometry($null, $pen, (New-Object System.Windows.Media.EllipseGeometry (New-Object System.Windows.Point 12, 12), 9.25, 9.25))
    $water = New-Object System.Windows.Media.CombinedGeometry ([System.Windows.Media.GeometryCombineMode]::Intersect),
        (New-Object System.Windows.Media.EllipseGeometry (New-Object System.Windows.Point 12, 12), 6.75, 6.75),
        ([System.Windows.Media.Geometry]::Parse('M 4,12.5 Q 8,8.5 12,12.5 T 20,12.5 V 21 H 4 Z'))
    $dc.DrawGeometry($white, $null, $water)
    $dc.Close()

    $bitmap = New-Object System.Windows.Media.Imaging.RenderTargetBitmap $size, $size, 96, 96, ([System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = New-Object System.IO.MemoryStream
    $encoder.Save($stream)
    return ,$stream.ToArray()
}

# .ico with PNG-compressed images (supported since Windows Vista).
$sizes = 16, 20, 24, 32, 40, 48, 64, 256
$images = foreach ($s in $sizes) { ,(Render-Icon $s) }
$ico = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter $ico
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $writer.Write([byte]$dim); $writer.Write([byte]$dim); $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32)
    $writer.Write([uint32]$images[$i].Length); $writer.Write([uint32]$offset)
    $offset += $images[$i].Length
}
foreach ($image in $images) { $writer.Write($image) }
$writer.Flush()

$assets = Join-Path $root 'src\Tidal.App\Assets'
New-Item -ItemType Directory -Force $assets | Out-Null
[IO.File]::WriteAllBytes((Join-Path $assets 'Tidal.ico'), $ico.ToArray())

$site = Join-Path $root 'website'
New-Item -ItemType Directory -Force $site | Out-Null
[IO.File]::WriteAllBytes((Join-Path $site 'icon-512.png'), (Render-Icon 512))
[IO.File]::WriteAllBytes((Join-Path $site 'favicon.png'), (Render-Icon 64))
"Wrote $assets\Tidal.ico and website icons"
