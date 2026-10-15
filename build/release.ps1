# Builds a release: runs the tests, publishes Tidal self-contained for 64-bit Windows (no .NET
# install needed), and packs it with Velopack into artifacts\releases:
#   Tidal-win-Setup.exe     per-user installer, no admin rights needed
#   Tidal-win-Portable.zip  unzip-and-run copy
#   *.nupkg, releases.*.json  update feed files (upload these too, so installs can update later)
#
#   powershell -ExecutionPolicy Bypass -File build\release.ps1 [-Version 0.1.0]

param(
    # Defaults to <Version> in Directory.Build.props.
    [string]$Version
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    if (-not $Version) { $Version = ([xml](Get-Content Directory.Build.props)).Project.PropertyGroup.Version }
    Write-Host "Building Tidal $Version" -ForegroundColor Cyan

    dotnet test -c Release --nologo
    if ($LASTEXITCODE) { throw 'Tests failed.' }

    $publish = 'artifacts\publish'
    $releases = 'artifacts\releases'
    Remove-Item artifacts -Recurse -Force -ErrorAction SilentlyContinue
    dotnet publish src\Tidal.App -c Release -r win-x64 --self-contained -o $publish -p:Version=$Version --nologo
    if ($LASTEXITCODE) { throw 'Publish failed.' }

    dotnet tool restore
    dotnet vpk pack --packId Tidal --packVersion $Version --packTitle Tidal --packAuthors Tidal --runtime win-x64 `
        --packDir $publish --mainExe Tidal.exe --icon src\Tidal.App\Assets\Tidal.ico `
        --shortcuts StartMenuRoot --outputDir $releases
    if ($LASTEXITCODE) { throw 'Packing failed.' }

    Get-ChildItem $releases | Format-Table Name, @{ Label = 'Size'; Expression = { '{0:N1} MB' -f ($_.Length / 1MB) } } -AutoSize
}
finally {
    Pop-Location
}
