#requires -Version 5.1
<#
.SYNOPSIS
    Build & install Math2Tex into a stable per-user location.

.DESCRIPTION
    1. Stops any running Math2Tex process.
    2. Runs `dotnet publish` (Release, self-contained, single-file, win-x64).
    3. Copies the publish output to %LocalAppData%\Math2Tex\.
    4. Creates a desktop shortcut.

    No administrator required. Uninstall via uninstall.ps1.
    User settings (backend-settings.json, history.json) live in %AppData%\Math2Tex\
    and are unaffected by reinstalls.
#>

$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Definition
Set-Location $here

$installDir = Join-Path $env:LOCALAPPDATA "Math2Tex"
$publishDir = Join-Path $here "publish"

# Resolve dotnet
$dotnet = "C:\Program Files\dotnet\dotnet.exe"
if (-not (Test-Path $dotnet)) {
    $cmd = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($cmd) { $dotnet = $cmd.Source } else {
        Write-Error "dotnet SDK not found. Install .NET 8 SDK from https://dot.net"
        exit 1
    }
}

# 1. Stop running instance
$running = Get-Process Math2Tex -ErrorAction SilentlyContinue
if ($running) {
    foreach ($p in $running) {
        Write-Host "  Stopping running Math2Tex (PID $($p.Id))..."
        try { $p | Stop-Process -Force } catch { Write-Warning "  could not stop PID $($p.Id): $_" }
    }
    Start-Sleep -Milliseconds 500
}

# 2. Publish
Write-Host ""
Write-Host "[1/3] Publishing (Release, self-contained, single-file)..." -ForegroundColor Cyan
if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
& $dotnet publish -c Release -r win-x64 --self-contained `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $publishDir | Out-Host
if ($LASTEXITCODE -ne 0) {
    Write-Error "publish failed (exit $LASTEXITCODE)"
    exit 1
}

# 3. Mirror to install dir
Write-Host ""
Write-Host "[2/3] Installing to $installDir ..." -ForegroundColor Cyan
if (Test-Path $installDir) { Remove-Item -Recurse -Force $installDir }
New-Item -ItemType Directory -Path $installDir -Force | Out-Null
Copy-Item -Path (Join-Path $publishDir "*") -Destination $installDir -Recurse -Force

$installedExe = Join-Path $installDir "Math2Tex.exe"
if (-not (Test-Path $installedExe)) {
    Write-Error "Math2Tex.exe not found in install dir after copy"
    exit 1
}

# 4. Desktop shortcut
Write-Host ""
Write-Host "[3/3] Creating desktop shortcut..." -ForegroundColor Cyan
$desktopShortcut = Join-Path ([Environment]::GetFolderPath("Desktop")) "Math2Tex.lnk"
$ws = New-Object -ComObject WScript.Shell
$lnk = $ws.CreateShortcut($desktopShortcut)
$lnk.TargetPath = $installedExe
$lnk.WorkingDirectory = $installDir
$lnk.IconLocation = "$installedExe,0"
$lnk.Description = "Math2Tex - clipboard math to LaTeX"
$lnk.Save()

Write-Host ""
Write-Host "Done." -ForegroundColor Green
Write-Host "  Install dir: $installDir"
Write-Host "  Shortcut   : $desktopShortcut"
Write-Host ""
Write-Host "Launch:    Math2Tex.exe will start from a stable path."
Write-Host "Autostart: tray icon -> right click -> tick `"开机自启动`"."
